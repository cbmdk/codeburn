using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CodeBurnMenubar.Data;

public static class Pricing
{
    // [inputPerToken, outputPerToken, cacheWritePerToken?, cacheReadPerToken?]
    // null → defaults: cacheWrite = input * 1.25,  cacheRead = input * 0.1
    private static readonly (string Key, double In, double Out, double? CW, double? CR)[] Table =
    [
        ("claude-opus-4-7",   5e-6,   2.5e-5,  6.25e-6, 5e-7),
        ("claude-opus-4-6",   5e-6,   2.5e-5,  6.25e-6, 5e-7),
        ("claude-opus-4-5",   5e-6,   2.5e-5,  6.25e-6, 5e-7),
        ("claude-opus-4-1",   1.5e-5, 7.5e-5,  1.875e-5, 1.5e-6),
        ("claude-opus-4",     1.5e-5, 7.5e-5,  1.875e-5, 1.5e-6),
        ("claude-sonnet-4-6", 3e-6,   1.5e-5,  3.75e-6,  3e-7),
        ("claude-sonnet-4-5", 3e-6,   1.5e-5,  3.75e-6,  3e-7),
        ("claude-sonnet-4",   3e-6,   1.5e-5,  3.75e-6,  3e-7),
        ("claude-haiku-4-5",  1e-6,   5e-6,    1.25e-6,  1e-7),
        ("claude-3-7-sonnet", 3e-6,   1.5e-5,  3.75e-6,  3e-7),
        ("claude-3-5-sonnet", 3e-6,   1.5e-5,  3.75e-6,  3e-7),
        ("claude-3-5-haiku",  1e-6,   5e-6,    1.25e-6,  1e-7),
        ("claude-3-haiku",    2.5e-7, 1.25e-6, null,     null),
        ("claude-3-opus",     1.5e-5, 7.5e-5,  null,     null),
        ("claude-3-sonnet",   3e-6,   1.5e-5,  null,     null),
    ];

    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["anthropic--claude-4.6-opus"]   = "claude-opus-4-6",
        ["anthropic--claude-4.6-sonnet"] = "claude-sonnet-4-6",
        ["anthropic--claude-4.5-opus"]   = "claude-opus-4-5",
        ["anthropic--claude-4.5-sonnet"] = "claude-sonnet-4-5",
        ["anthropic--claude-4.5-haiku"]  = "claude-haiku-4-5",
        ["cursor-auto"]                  = "claude-sonnet-4-5",
        ["cursor-agent-auto"]            = "claude-sonnet-4-5",
        ["claude-4.6-sonnet"]            = "claude-sonnet-4-6",
        ["claude-4.5-sonnet-thinking"]   = "claude-sonnet-4-5",
        ["claude-4-sonnet-thinking"]     = "claude-sonnet-4-5",
        ["claude-4-opus"]                = "claude-opus-4-5",
        ["claude-4.5-opus-high-thinking"]= "claude-opus-4-5",
    };

    // Models where fast-mode multiplier is 6x (when speed="fast")
    private static readonly HashSet<string> FastModels = ["claude-opus-4-7", "claude-opus-4-6"];

    private static readonly Regex DateSuffix = new(@"-\d{8}$", RegexOptions.Compiled);

    public static double Calculate(
        string model,
        int inputTokens,
        int outputTokens,
        int cacheCreationTokens,
        int cacheReadTokens,
        int webSearchRequests = 0,
        bool fast = false)
    {
        var canonical = Canonicalize(model);

        var entry = FindEntry(canonical);
        if (entry is null) return 0;

        var (_, inCost, outCost, cwRaw, crRaw) = entry.Value;
        double cw = cwRaw ?? inCost * 1.25;
        double cr = crRaw ?? inCost * 0.1;
        double multiplier = fast && FastModels.Contains(canonical) ? 6.0 : 1.0;

        return multiplier * (
            inputTokens       * inCost +
            outputTokens      * outCost +
            cacheCreationTokens * cw +
            cacheReadTokens   * cr +
            webSearchRequests * 0.01);
    }

    private static (string Key, double In, double Out, double? CW, double? CR)? FindEntry(string canonical)
    {
        foreach (var entry in Table)
            if (entry.Key == canonical) return entry;
        foreach (var entry in Table)
            if (canonical.StartsWith(entry.Key)) return entry;
        return null;
    }

    private static string Canonicalize(string model)
    {
        // Strip @pin:   claude-sonnet-4-6@20250929 → claude-sonnet-4-6
        var atIdx = model.IndexOf('@');
        if (atIdx >= 0) model = model[..atIdx];

        // Strip -YYYYMMDD date suffix:  claude-haiku-4-5-20251001 → claude-haiku-4-5
        model = DateSuffix.Replace(model, "");

        // Strip provider prefix:  anthropic/claude-opus → claude-opus
        var slashIdx = model.LastIndexOf('/');
        if (slashIdx >= 0) model = model[(slashIdx + 1)..];

        // Apply known aliases
        return Aliases.TryGetValue(model, out var alias) ? alias : model;
    }
}
