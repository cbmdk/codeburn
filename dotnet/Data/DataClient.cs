using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using CodeBurnMenubar.Models;

namespace CodeBurnMenubar.Data;

public static class DataClient
{
    private static readonly Regex SafeArgPattern = new Regex(@"^[A-Za-z0-9 ._/\\\-]+$", RegexOptions.Compiled);

    private static string[] BaseArgv()
    {
        var raw = Environment.GetEnvironmentVariable("CODEBURN_BIN");
        if (!string.IsNullOrEmpty(raw))
        {
            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.All(IsSafe))
            {
                return parts;
            }
            // Log or ignore unsafe
            Console.WriteLine("CodeBurn: refusing unsafe CODEBURN_BIN; using default 'codeburn'");
        }
        return ["codeburn"];
    }

    private static bool IsSafe(string arg)
    {
        return SafeArgPattern.IsMatch(arg);
    }

    public static async Task<MenubarPayload> FetchAsync(string period, string provider)
    {
        var baseArgv = BaseArgv();
        var subcommand = $"status --format menubar-json --period {period} --provider {provider}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{string.Join(" ", baseArgv.Concat([subcommand]))}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Ensure PATH includes npm global bin
        var env = startInfo.EnvironmentVariables;
        var path = env["PATH"] ?? "";
        var npmPath = @"C:\Users\chrimo\AppData\Roaming\npm";
        if (!path.Contains(npmPath))
        {
            env["PATH"] = path + ";" + npmPath;
        }

        using var process = Process.Start(startInfo);
        if (process == null)
            throw new Exception("Failed to start CLI process");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception($"CLI error: {error}");

        return JsonSerializer.Deserialize<MenubarPayload>(output)
            ?? throw new Exception("Failed to deserialize payload");
    }
}