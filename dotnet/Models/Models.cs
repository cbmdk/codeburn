using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace CodeBurnMenubar.Models;

public class MenubarPayload
{
    [JsonPropertyName("current")]
    public required CurrentBlock Current { get; set; }

    [JsonPropertyName("optimize")]
    public required OptimizeBlock Optimize { get; set; }

    [JsonPropertyName("history")]
    public required HistoryBlock History { get; set; }
}

public class CurrentBlock
{
    [JsonPropertyName("cost")]
    public double Cost { get; set; }

    [JsonPropertyName("calls")]
    public int Calls { get; set; }

    [JsonPropertyName("sessions")]
    public int Sessions { get; set; }

    [JsonPropertyName("activities")]
    public List<ActivityEntry>? Activities { get; set; }
}

public class ActivityEntry
{
    [JsonPropertyName("label")]
    public required string Label { get; set; }

    [JsonPropertyName("cost")]
    public double Cost { get; set; }

    [JsonPropertyName("turns")]
    public int? Turns { get; set; }

    [JsonPropertyName("oneShotPct")]
    public int? OneShotPct { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }
}

public class OptimizeBlock
{
    [JsonPropertyName("findingCount")]
    public int FindingCount { get; set; }
}

public class HistoryBlock
{
    [JsonPropertyName("daily")]
    public required List<DailyHistoryEntry> Daily { get; set; }
}

public class DailyHistoryEntry
{
    [JsonPropertyName("date")]
    public required string Date { get; set; }

    [JsonPropertyName("cost")]
    public double Cost { get; set; }

    [JsonPropertyName("calls")]
    public int Calls { get; set; }

    [JsonPropertyName("inputTokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("outputTokens")]
    public int OutputTokens { get; set; }
}

public class ChartBarItem
{
    public double BarHeight { get; set; }
    public long Tokens { get; set; }
    public bool IsHighlighted { get; set; }
}

public class ActivityBarItem
{
    public required string Label { get; set; }
    public double Cost { get; set; }
    public int? Turns { get; set; }
    public int? OneShotPct { get; set; }
    public double BarWidth { get; set; }
}
