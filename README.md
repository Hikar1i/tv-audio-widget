# TV Audio Widget

A portable Windows 10/11 audio output panel for large-screen HDMI TV use.

## Features

- Lists active Windows output devices.
- Sets the selected device as the default console, multimedia, and communications output.
- Controls system master volume and mute state.
- Opens as a large borderless panel on the last-used display.
- Saves theme, opacity, volume step, and screen placement to `config.json` next to the executable.

## Build

This project targets `net10.0-windows` and is intended to be published by GitHub Actions as a self-contained `win-x64` ZIP.

Local build, when the .NET 10 SDK is installed:

```powershell
dotnet restore .\src\TvAudioWidget\TvAudioWidget.csproj
dotnet build .\src\TvAudioWidget\TvAudioWidget.csproj -c Release
dotnet publish .\src\TvAudioWidget\TvAudioWidget.csproj -c Release -r win-x64 --self-contained true -o .\publish\win-x64
```

The app does not request administrator privileges. If Windows rejects an audio or settings operation, the message is shown in the panel status area.
