using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace CodeBurnMenubar.Models;

// Data models (simplified from Swift)
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