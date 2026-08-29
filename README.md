# Hardware Monitor Overlay

A small always-on-top HUD for Windows showing CPU usage/temperature and NVIDIA GPU
usage/temperature. Native WPF, click-through, tray-controlled.

```
CPU   14%   47°C
GPU    3%   38°C
```

## Requirements

- Windows 10/11, x64
- .NET 9 SDK to build (the .NET 9 Desktop Runtime is enough to run a framework-dependent build)
- Administrator rights at runtime — temperature sensors are read through a kernel
  driver (MSR/SMBus) that will not load otherwise

## Build and run

```bash
dotnet build HardwareMonitorOverlay.sln -c Release
```

The executable lands in `src/HardwareMonitorOverlay/bin/x64/Release/net9.0-windows/HardwareMonitorOverlay.exe`.
The app manifest requests elevation, so launching it raises a UAC prompt. Note that
`dotnet run` cannot elevate — launch the .exe directly.

For a self-contained single file that needs no runtime installed:

```bash
dotnet publish src/HardwareMonitorOverlay/HardwareMonitorOverlay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## Using it

The overlay ignores all mouse and keyboard input, so everything is controlled from
the thermometer icon in the notification area:

| Menu | Effect |
| --- | --- |
| Corner | Anchors the HUD to any of the four work-area corners |
| Opacity | 40% / 60% / 85% / 100% |
| Text size | 11 / 13 / 16 / 20 px |
| Refresh rate | 1 / 1.5 / 2 / 5 seconds |
| Show | Toggles the CPU and GPU rows |
| Exit | Quits |

Settings persist to `%APPDATA%\HardwareMonitorOverlay\config.json` and are applied
immediately.

Temperature text is colour-coded: green below 60 °C, amber below 80 °C, red above.
`--` means the sensor was not found.

## Design notes

**Click-through.** `OnSourceInitialized` adds `WS_EX_TRANSPARENT | WS_EX_LAYERED |
WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE` to the window. Transparent makes hit-testing
fall through to whatever is underneath, tool-window keeps the HUD out of Alt+Tab,
and no-activate stops it stealing focus. See `Interop/NativeMethods.cs`.

**Staying on top.** `Topmost="True"` alone loses to full-screen games, which claim
the topmost band for themselves. Each poll tick also calls `SetWindowPos(HWND_TOPMOST,
SWP_NOACTIVATE)` to take it back — a negligible cost at 1.5 s intervals.

**Polling.** A single `System.Threading.Timer` samples on the thread pool and marshals
one immutable `HardwareReadings` struct to the UI thread at `DispatcherPriority.Background`.
Overlapping ticks are dropped via `Interlocked`, so a slow sensor read cannot queue up
work behind itself.

**Redraw only on change.** `OverlayViewModel` formats each reading to its final display
string (integer percent, integer °C) and raises `PropertyChanged` only when that string
differs from the last one. Sub-degree sensor jitter therefore produces no layout and no
repaint. Column widths are fixed so a changing value repaints text without reflowing the
panel. There is no render loop and no animation.

**Sensor sources.** CPU usage comes from the `Processor \ % Processor Time \ _Total`
performance counter, which needs no driver. Temperatures and GPU load come from
LibreHardwareMonitor with only the CPU and GPU hardware nodes enabled, so a poll touches
as few sensors as possible. If the performance counter is unavailable — a corrupt counter
registry is a common Windows fault — the code falls back to LibreHardwareMonitor's
`CPU Total` load sensor.

CPU temperature is picked by name in preference order (`CPU Package` on Intel,
`Core (Tctl/Tdie)` on AMD), falling back to the hottest reported core. GPU values come
from the `GPU Core` sensors.

If the driver fails to open — usually because the app was started without elevation —
the overlay still runs and shows CPU usage, and the tray tooltip says sensors are
unavailable.

## Starting with Windows

An elevated app cannot be launched from the normal `Run` registry key. Create a scheduled
task that runs with highest privileges at logon:

```bash
schtasks /create /tn "Hardware Monitor Overlay" /tr "\"C:\path\to\HardwareMonitorOverlay.exe\"" /sc onlogon /rl highest /f
```

## Layout

```
src/HardwareMonitorOverlay/
  App.xaml(.cs)              composition root, single-instance guard
  OverlayWindow.xaml(.cs)    HUD window, click-through, corner anchoring
  Configuration/             persisted settings
  Interop/NativeMethods.cs   Win32 window styles and z-order
  Services/                  sensor reading and the poll timer
  ViewModels/                formatting and change detection
  Tray/                      notification-area icon and menu
```
