using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodeBurnMenubar.Data;

public static class ActivityClassifier
{
    private static readonly Regex TestPat    = Pat(@"\b(test|pytest|vitest|jest|mocha|spec|coverage|npm\s+test|npx\s+vitest|npx\s+jest)\b");
    private static readonly Regex GitPat     = Pat(@"\bgit\s+(push|pull|commit|merge|rebase|checkout|branch|stash|log|diff|status|add|reset|cherry-pick|tag)\b");
    private static readonly Regex BuildPat   = Pat(@"\b(npm\s+run\s+build|npm\s+publish|docker|deploy|make\s+build|npm\s+run\s+dev|npm\s+start|pm2|systemctl|cargo\s+build)\b");
    private static readonly Regex InstallPat = Pat(@"\b(npm\s+install|pip\s+install|brew\s+install|apt\s+install|cargo\s+add)\b");
    private static readonly Regex DebugKw    = Pat(@"\b(fix|bug|error|broken|failing|crash|issue|debug|traceback|exception|stack\s*trace|not\s+working|wrong|unexpected|404|500|401|403)\b");
    private static readonly Regex FeatureKw  = Pat(@"\b(add|create|implement|new|build|feature|introduce|set\s*up|scaffold|generate|make\s+(a|me|the)|write\s+(a|me|the))\b");
    private static readonly Regex RefactorKw = Pat(@"\b(refactor|clean\s*up|rename|reorganize|simplify|extract|restructure|move|migrate|split)\b");
    private static readonly Regex BrainKw    = Pat(@"\b(brainstorm|idea|what\s+if|think\s+about|approach|strategy|consider|how\s+should|what\s+would|suggest|recommend)\b");
    private static readonly Regex ResearchKw = Pat(@"\b(research|investigate|look\s+into|find\s+out|analyze|review|understand|explain|how\s+does|what\s+is|show\s+me|list|compare)\b");
    private static readonly Regex FilePat    = Pat(@"\.(py|js|ts|tsx|jsx|json|yaml|yml|toml|sql|sh|go|rs|java|rb|php|css|html|md)\b");
    private static readonly Regex UrlPat     = Pat(@"https?://\S+");

    private static readonly HashSet<string> EditTools   = ["Edit", "Write", "FileEditTool", "FileWriteTool", "NotebookEdit"];
    private static readonly HashSet<string> ReadTools   = ["Read", "Grep", "Glob", "FileReadTool", "GrepTool", "GlobTool"];
    private static readonly HashSet<string> BashTools   = ["Bash", "BashTool", "PowerShellTool"];
    private static readonly HashSet<string> TaskTools   = ["TaskCreate", "TaskUpdate", "TaskGet", "TaskList", "TaskOutput", "TaskStop", "TodoWrite"];
    private static readonly HashSet<string> SearchTools = ["WebSearch", "WebFetch", "ToolSearch"];

    public static string Classify(string userMessage, IReadOnlyList<string> tools)
    {
        if (tools.Count == 0)
            return ClassifyConversation(userMessage);

        bool hasEdits  = tools.Any(t => EditTools.Contains(t));
        bool hasReads  = tools.Any(t => ReadTools.Contains(t));
        bool hasBash   = tools.Any(t => BashTools.Contains(t));
        bool hasTasks  = tools.Any(t => TaskTools.Contains(t));
        bool hasSearch = tools.Any(t => SearchTools.Contains(t));
        bool hasMcp    = tools.Any(t => t.StartsWith("mcp__"));
        bool hasAgent  = tools.Contains("Agent");

        if (hasAgent) return "delegation";

        if (hasBash && !hasEdits)
        {
            if (TestPat.IsMatch(userMessage))    return "testing";
            if (GitPat.IsMatch(userMessage))     return "git";
            if (BuildPat.IsMatch(userMessage) || InstallPat.IsMatch(userMessage)) return "build/deploy";
        }

        string cat;
        if      (hasEdits)            cat = "coding";
        else if (hasBash && hasReads) cat = "exploration";
        else if (hasBash)             cat = "coding";
        else if (hasSearch || hasMcp) cat = "exploration";
        else if (hasReads)            cat = "exploration";
        else if (hasTasks)            cat = "planning";
        else                          cat = ClassifyConversation(userMessage);

        return Refine(cat, userMessage);
    }

    private static string Refine(string category, string msg) => category switch
    {
        "coding" when DebugKw.IsMatch(msg)    => "debugging",
        "coding" when RefactorKw.IsMatch(msg) => "refactoring",
        "coding" when FeatureKw.IsMatch(msg)  => "feature",
        "exploration" when DebugKw.IsMatch(msg) => "debugging",
        _ => category,
    };

    private static string ClassifyConversation(string msg)
    {
        if (BrainKw.IsMatch(msg))   return "brainstorming";
        if (ResearchKw.IsMatch(msg)) return "exploration";
        if (DebugKw.IsMatch(msg))   return "debugging";
        if (FeatureKw.IsMatch(msg)) return "feature";
        if (FilePat.IsMatch(msg))   return "coding";
        if (UrlPat.IsMatch(msg))    return "exploration";
        return "conversation";
    }

    public static string Label(string category) => category switch
    {
        "coding"       => "Coding",
        "debugging"    => "Debugging",
        "feature"      => "Feature Dev",
        "refactoring"  => "Refactoring",
        "exploration"  => "Exploration",
        "testing"      => "Testing",
        "planning"     => "Planning",
        "delegation"   => "Delegation",
        "git"          => "Git",
        "build/deploy" => "Build/Deploy",
        "brainstorming"=> "Brainstorming",
        "conversation" => "Conversation",
        _ => category,
    };

    private static Regex Pat(string pattern) =>
        new(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
