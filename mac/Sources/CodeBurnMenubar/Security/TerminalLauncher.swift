import AppKit
import Foundation

/// Opens a codeburn subcommand in the user's Terminal. The argv is validated through
/// `CodeburnCLI.isSafe` before it's interpolated into AppleScript so there's no path for a
/// rogue environment variable to smuggle shell metacharacters into the `do script` call.
/// Falls back to a detached headless spawn on machines without Terminal.app (iTerm/Ghostty/Warp
/// users) so the subcommand still runs.
enum TerminalLauncher {
    private static let terminalPaths = [
        "/System/Applications/Utilities/Terminal.app",
        "/Applications/Utilities/Terminal.app",
    ]

    static func open(subcommand: [String]) {
        let argv = CodeburnCLI.baseArgv() + subcommand
        guard argv.allSatisfy(CodeburnCLI.isSafe) else {
            NSLog("CodeBurn: refusing to open terminal with unsafe argv")
            return
        }
        let command = argv.joined(separator: " ")

        if terminalPaths.contains(where: FileManager.default.fileExists(atPath:)) {
            let script = """
            tell application "Terminal"
                activate
                do script "\(command)"
            end tell
            """
            let process = Process()
            process.executableURL = URL(fileURLWithPath: "/usr/bin/osascript")
            process.arguments = ["-e", script]
            try? process.run()
            return
        }

        let headless = CodeburnCLI.makeProcess(subcommand: subcommand)
        try? headless.run()
    }
}
