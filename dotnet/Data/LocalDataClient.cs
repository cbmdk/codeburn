using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBurnMenubar.Models;

namespace CodeBurnMenubar.Data;

public static class LocalDataClient
{
    private const long MaxFileSizeBytes = 128L * 1024 * 1024;

    // ── Public API ──────────────────────────────────────────────────────────

    public static async Task<MenubarPayload> FetchAsync(string period, string provider)
    {
        if (provider != "all" && provider != "claude")
            return BuildEmpty();

        var projectsDir = GetProjectsDir();
        if (!Directory.Exists(projectsDir))
            throw new Exception($".claude/projects not found at: {projectsDir}");

        var (periodStart, periodEnd) = GetPeriodRange(period);
        var historyStart = DateTime.Today.AddDays(-365);

        var apiCalls = new List<ApiCall>();
        var turns = new List<Turn>();
        var seenMsgIds = new HashSet<string>();

        await Task.Run(() =>
        {
            var files = Directory.GetFiles(projectsDir, "*.jsonl", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try { ParseFile(file, seenMsgIds, apiCalls, turns); }
                catch { /* skip corrupt / inaccessible files */ }
            }
        });

        var currentCalls = apiCalls.Where(c => c.Date >= periodStart && c.Date <= periodEnd).ToList();
        var currentTurns = turns.Where(t => t.Date >= periodStart && t.Date <= periodEnd).ToList();
        var historyCalls = apiCalls.Where(c => c.Date >= historyStart).ToList();

        return new MenubarPayload
        {
            Current = BuildCurrent(currentCalls, currentTurns),
            Optimize = new OptimizeBlock { FindingCount = 0 },
            History = BuildHistory(historyCalls),
        };
    }

    // ── File discovery ───────────────────────────────────────────────────────

    private static string GetProjectsDir() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "projects");

    private static (DateTime Start, DateTime End) GetPeriodRange(string period)
    {
        var now = DateTime.Now;
        var today = DateTime.Today;
        return period switch
        {
            "today" => (today, today.AddDays(1).AddTicks(-1)),
            "7d"    => (today.AddDays(-7), now),
            "30d"   => (today.AddDays(-30), now),
            "month" => (new DateTime(today.Year, today.Month, 1), now),
            _       => (today.AddDays(-365), now),
        };
    }

    // ── JSONL parsing ────────────────────────────────────────────────────────

    private static void ParseFile(
        string path,
        HashSet<string> seenMsgIds,
        List<ApiCall> apiCalls,
        List<Turn> turns)
    {
        if (new FileInfo(path).Length > MaxFileSizeBytes) return;

        // Per-file state: track best version of each assistant message
        var bestPerMsg = new Dictionary<string, RawAssistant>();

        // Ordered log for turn construction: user entries + assistant message IDs in sequence
        var log = new List<LogEntry>();

        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try { ProcessLine(line, bestPerMsg, log); }
            catch { /* skip malformed lines */ }
        }

        // Emit deduplicated API calls
        foreach (var (msgId, raw) in bestPerMsg)
        {
            if (!seenMsgIds.Add(msgId)) continue;

            apiCalls.Add(new ApiCall
            {
                SessionId        = raw.SessionId,
                Date             = raw.Timestamp.Date,
                Model            = raw.Model,
                InputTokens      = raw.InputTokens,
                OutputTokens     = raw.OutputTokens,
                CacheReadTokens  = raw.CacheReadTokens,
                CacheWriteTokens = raw.CacheCreationTokens,
                Cost             = Pricing.Calculate(raw.Model, raw.InputTokens, raw.OutputTokens,
                                       raw.CacheCreationTokens, raw.CacheReadTokens, raw.WebSearchRequests),
            });
        }

        // Build turns from the ordered log
        BuildTurns(log, bestPerMsg, seenMsgIds, turns);
    }

    private static void ProcessLine(
        string line,
        Dictionary<string, RawAssistant> bestPerMsg,
        List<LogEntry> log)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeEl)) return;
        var type = typeEl.GetString();

        var sessionId = root.TryGetProperty("sessionId", out var sidEl)
            ? sidEl.GetString() ?? "" : "";
        var timestamp = root.TryGetProperty("timestamp", out var tsEl)
            ? ParseTimestamp(tsEl.GetString()) : DateTime.UtcNow;

        if (type == "user")
        {
            var text = ExtractUserText(root);
            log.Add(new LogEntry(true, sessionId, timestamp, null, text));
            return;
        }

        if (type != "assistant") return;
        if (!root.TryGetProperty("message", out var msg)) return;
        if (!msg.TryGetProperty("role", out var roleEl) || roleEl.GetString() != "assistant") return;
        if (!msg.TryGetProperty("id", out var idEl)) return;

        var msgId = idEl.GetString();
        if (string.IsNullOrEmpty(msgId)) return;

        var model      = msg.TryGetProperty("model", out var mEl) ? mEl.GetString() ?? "" : "";
        var stopReason = msg.TryGetProperty("stop_reason", out var srEl) ? srEl.GetString() : null;

        int inputT = 0, outputT = 0, cacheCreate = 0, cacheRead = 0, webSearch = 0;
        if (msg.TryGetProperty("usage", out var usage))
        {
            inputT      = GetInt(usage, "input_tokens");
            outputT     = GetInt(usage, "output_tokens");
            cacheCreate = GetInt(usage, "cache_creation_input_tokens");
            cacheRead   = GetInt(usage, "cache_read_input_tokens");
            if (usage.TryGetProperty("server_tool_use", out var stu))
                webSearch = GetInt(stu, "web_search_requests");
        }

        var tools = msg.TryGetProperty("content", out var content)
            ? ExtractTools(content) : [];

        // Keep the best version: prefer stop_reason set, then highest output token count
        if (!bestPerMsg.TryGetValue(msgId, out var existing)
            || outputT > existing.OutputTokens
            || (stopReason != null && existing.StopReason == null))
        {
            bestPerMsg[msgId] = new RawAssistant(
                msgId, sessionId, timestamp, model,
                inputT, outputT, cacheCreate, cacheRead, webSearch,
                tools, stopReason);
        }

        log.Add(new LogEntry(false, sessionId, timestamp, msgId, ""));
    }

    private static void BuildTurns(
        List<LogEntry> log,
        Dictionary<string, RawAssistant> bestPerMsg,
        HashSet<string> seenMsgIds,
        List<Turn> turns)
    {
        string currentUserMsg = "";
        string currentSessionId = "";
        DateTime currentDate = DateTime.MinValue;
        var currentTools = new List<string>();
        double currentCost = 0;
        bool inTurn = false;

        void FlushTurn()
        {
            if (!inTurn) return;
            turns.Add(new Turn(currentUserMsg, currentSessionId, currentDate, currentTools.ToList(), currentCost));
            currentTools.Clear();
            currentCost = 0;
            inTurn = false;
        }

        foreach (var entry in log)
        {
            if (entry.IsUser)
            {
                FlushTurn();
                currentUserMsg = entry.UserText;
                currentSessionId = entry.SessionId;
                currentDate = entry.Timestamp.Date;
                inTurn = true;
            }
            else if (entry.MsgId is not null && bestPerMsg.TryGetValue(entry.MsgId, out var raw))
            {
                // Only accumulate if we haven't already counted this msg (it's in seenMsgIds)
                // We always add tools regardless since this is for classification
                currentTools.AddRange(raw.Tools);
                currentCost += Pricing.Calculate(raw.Model, raw.InputTokens, raw.OutputTokens,
                    raw.CacheCreationTokens, raw.CacheReadTokens, raw.WebSearchRequests);
                if (currentDate == DateTime.MinValue)
                    currentDate = raw.Timestamp.Date;
            }
        }

        FlushTurn();
    }

    // ── Payload construction ─────────────────────────────────────────────────

    private static CurrentBlock BuildCurrent(List<ApiCall> calls, List<Turn> turns)
    {
        var activities = BuildActivities(turns);
        return new CurrentBlock
        {
            Cost       = calls.Sum(c => c.Cost),
            Calls      = calls.Count,
            Sessions   = calls.Select(c => c.SessionId).Where(s => !string.IsNullOrEmpty(s)).Distinct().Count(),
            Activities = activities.Count > 0 ? activities : null,
        };
    }

    private static List<ActivityEntry> BuildActivities(List<Turn> turns)
    {
        if (turns.Count == 0) return [];
        return turns
            .GroupBy(t => ActivityClassifier.Classify(t.UserMessage, t.Tools))
            .Select(g => new ActivityEntry
            {
                Label = ActivityClassifier.Label(g.Key),
                Cost  = g.Sum(t => t.Cost),
                Turns = g.Count(),
            })
            .OrderByDescending(a => a.Cost)
            .Take(20)
            .ToList();
    }

    private static HistoryBlock BuildHistory(List<ApiCall> calls)
    {
        var daily = calls
            .GroupBy(c => c.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyHistoryEntry
            {
                Date         = g.Key.ToString("yyyy-MM-dd"),
                Cost         = g.Sum(c => c.Cost),
                Calls        = g.Count(),
                InputTokens  = g.Sum(c => c.InputTokens),
                OutputTokens = g.Sum(c => c.OutputTokens),
            })
            .ToList();

        return new HistoryBlock { Daily = daily };
    }

    private static MenubarPayload BuildEmpty() => new()
    {
        Current  = new CurrentBlock { Cost = 0, Calls = 0 },
        Optimize = new OptimizeBlock { FindingCount = 0 },
        History  = new HistoryBlock { Daily = [] },
    };

    // ── JSON helpers ─────────────────────────────────────────────────────────

    private static string ExtractUserText(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var msg)) return "";
        if (!msg.TryGetProperty("content", out var content)) return "";

        if (content.ValueKind == JsonValueKind.String)
            return content.GetString() ?? "";

        if (content.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var item in content.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var t) && t.GetString() == "text"
                    && item.TryGetProperty("text", out var txt))
                    sb.Append(txt.GetString());
            }
            return sb.ToString();
        }

        return "";
    }

    private static List<string> ExtractTools(JsonElement content)
    {
        var tools = new List<string>();
        if (content.ValueKind != JsonValueKind.Array) return tools;
        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var t) && t.GetString() == "tool_use"
                && block.TryGetProperty("name", out var name))
            {
                var n = name.GetString();
                if (n is not null) tools.Add(n);
            }
        }
        return tools;
    }

    private static int GetInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32() : 0;

    private static DateTime ParseTimestamp(string? ts)
    {
        if (ts is null) return DateTime.UtcNow;
        return DateTime.TryParse(ts, null, System.Globalization.DateTimeStyles.RoundtripKind,
            out var dt) ? dt.ToLocalTime() : DateTime.UtcNow;
    }

    // ── Private record types ─────────────────────────────────────────────────

    private sealed record ApiCall(
        string SessionId, DateTime Date, string Model,
        int InputTokens, int OutputTokens,
        int CacheReadTokens, int CacheWriteTokens,
        double Cost)
    {
        public ApiCall() : this("", DateTime.MinValue, "", 0, 0, 0, 0, 0) { }
    }

    private sealed record Turn(
        string UserMessage, string SessionId, DateTime Date,
        IReadOnlyList<string> Tools, double Cost);

    private sealed record RawAssistant(
        string MsgId, string SessionId, DateTime Timestamp, string Model,
        int InputTokens, int OutputTokens,
        int CacheCreationTokens, int CacheReadTokens, int WebSearchRequests,
        List<string> Tools, string? StopReason);

    private sealed record LogEntry(
        bool IsUser, string SessionId, DateTime Timestamp,
        string? MsgId, string UserText);
}
