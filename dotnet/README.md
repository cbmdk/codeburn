# CodeBurn Menubar (.NET)

This is a .NET implementation of the CodeBurn menubar application, ported from the Swift macOS version.

## Prerequisites

- .NET 8.0
- Node.js (for the CLI backend)

## Building

```bash
dotnet build
```

## Running

```bash
dotnet run
```

The application will appear in the system tray. Click the icon to show the popover window.

## Features

- Displays current AI coding costs
- Refreshes data from the CodeBurn CLI
- Basic popover UI

## TODO

- Implement full UI matching the Swift version
- Add tray icon with cost display
- Handle multiple periods and providers
- Add charts and sections
- Cross-platform support (currently Windows-focused, but Avalonia supports macOS too)