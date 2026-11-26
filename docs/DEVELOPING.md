# Developing Widglass

This page is for working on the code. To use the app, just download `Widglass.exe` from the [Releases page](https://github.com/gigilucas/Widglass/releases/latest).

## Tech stack

- **C# / .NET 10 / WPF**, targeting the Windows 10 SDK (for location and media controls). Published as one self-contained `.exe`, so users don't need .NET installed.
- Windows APIs: `Windows.Devices.Geolocation` (location), `GlobalSystemMediaTransportControlsSessionManager` (music), performance counters and `GlobalMemoryStatusEx` (CPU / RAM).
- Pinning: each widget window is owned by the desktop window that holds the icons (`Progman`, or a `WorkerW` with a wallpaper slideshow), kept just above it in the z-order, and re-pinned if Explorer restarts. See [`DesktopPin.cs`](../src/Widglass/DesktopPin.cs).
- Tray icon: Windows Forms `NotifyIcon` ([`TrayIcon.cs`](../src/Widglass/TrayIcon.cs)). Start with Windows: the current user's `Run` registry key ([`StartupRegistration.cs`](../src/Widglass/StartupRegistration.cs)).

## Build

Requirements: Windows 10/11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

Run from source:

```powershell
dotnet run --project src/Widglass
```

Build the `.exe`:

```powershell
dotnet publish src/Widglass -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

The app ends up in `publish/Widglass.exe`.

## Releases

Pushing a version tag builds the `.exe` on GitHub and publishes it as a release ([`.github/workflows/release.yml`](../.github/workflows/release.yml)):

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The download link `https://github.com/gigilucas/Widglass/releases/latest/download/Widglass.exe` always points to the newest release.

Installed copies update themselves from the same place ([`UpdateService.cs`](../src/Widglass/Services/UpdateService.cs)): they read `releases/latest` from the GitHub API, and if its tag is a higher version than their own and it has a `Widglass.exe` asset, they offer it. Updating downloads it next to the running `.exe` as `Widglass.exe.new`, renames the running file to `Widglass.exe.old`, moves the new one into place, starts it with `--wait-for <pid> --updated` and quits; the new copy deletes the `.old` file once the old process is gone. So a release only needs the tag pushed: the version number comes from the tag.

## Developer options

| Option | What it does |
| --- | --- |
| `--screenshot out.png` | Saves an image of the manager window and exits. |
| `--screenshot out.png --page clock` | Same, showing that widget's settings. |
| `--screenshot out.png --widget weather` | Saves an image of one widget instead. |
| `--background` | Starts with only the widgets and the tray icon, no menu (used by *Start with Windows*). |
| `--location 38.72,-9.14` | Uses these coordinates for the weather instead of the PC's location. |
| `--update-feed feed.json` | Reads the "latest release" from a local JSON file (same shape as GitHub's API; the download URL can be a `file:///` path) to test updates. |
| `--music-test FakePlayer:Spotify` | Lets the Music widget follow a test media session whose id contains `FakePlayer`, labelled *Spotify*. |

The screenshots in the README were made with these options. [`tools/make-icon.ps1`](../tools/make-icon.ps1) rebuilds the app icon from `docs/logo.png`.

## Project structure

```
src/Widglass/
  MainWindow.xaml        Manager window: widget list, settings pages
  WidgetWindow.xaml      One widget on the desktop (glass card)
  DesktopPin.cs          Keeps widgets pinned to the desktop
  TrayIcon.cs            Icon in the notification area
  StartupRegistration.cs Start with Windows
  Widgets/               Content and settings view of each widget
  Services/              Weather, music, CPU / RAM data and updates
  Themes/                Light and dark colors (menu and widgets)
  Settings.cs            Saved choices
docs/                    Logo, screenshots, this page
design/                  Early HTML mockups of the widgets
tools/                   Helper scripts
.github/workflows/       Release build
```
