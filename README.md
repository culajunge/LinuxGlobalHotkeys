# Linux Global Hotkeys

A .NET library for registering global hotkeys/shortcuts in Linux X11 environments.

## Features

- Register global keyboard shortcuts that work system-wide
- Support for common modifiers (Alt, Ctrl, Shift, Super/Meta)
- Simple API with callback-based event handling
- Proper resource management with IDisposable pattern

## Requirements

- Linux with X11/XWayland
- .NET 6.0 or higher
- libX11 installed on the system

## Installation

```bash
dotnet add package LinuxGlobalHotkeys
```

## Usage

```csharp
using LinuxGlobalHotkeys;

// Create the hotkey manager
using (var hotkeyManager = new GlobalHotkeyManager())
{
    // Register Alt+Q shortcut
    hotkeyManager.RegisterShortcut("Alt+Q", () => {
        Console.WriteLine("Alt+Q was pressed!");
        // Do something when Alt+Q is pressed
    });
    
    // Register Ctrl+S shortcut
    hotkeyManager.RegisterShortcut("Ctrl+S", () => {
        Console.WriteLine("Ctrl+S was pressed!");
        // Do something when Ctrl+S is pressed
    });
    
    // Wait for program to exit
    Console.WriteLine("Press Enter to exit");
    Console.ReadLine();
}
```

## Supported Modifiers

- `Alt`
- `Ctrl` or `Control`
- `Shift`
- `Super`, `Meta`, `Win`, or `Windows` (Windows/Command key)

ok bye