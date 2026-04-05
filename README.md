# DTXManiaXYZ

A drum simulation game that replicates gameplay from Konami's Gitadora (Drummania/GuitarFreaks). Plays DTX chart files (and older formats like BMS/BME, GDA/G2D) using a game controller, electronic drum kit, keyboard, or MIDI controller.

## Project Scope

DTXManiaXYZ is a modernization fork of [DTXManiaNX](https://github.com/limyz/DTXmaniaXG), focused on:

- **High-resolution support** — configurable 720p / 1080p / 1440p / 4K with proper coordinate scaling
- **High frame rate** — unlocked frame rate with configurable target (120+ FPS), VSync options, and spin-wait frame pacing
- **Borderless fullscreen** — proper multi-monitor borderless windowed mode
- **.NET 8 migration** — modern runtime with better JIT, lower GC pauses (in progress)
- **D3D11 rendering** — future migration from Direct3D 9 to Direct3D 11 via Vortice.Windows (planned)

The game is fully playable today on the current codebase. Modernization work is incremental and does not break existing DTX file compatibility.

## Lineage

This project descends from a line of community forks:

- [DTXMania](https://osdn.net/projects/dtxmania) (yyagi) — the original open-source DTX simulator
- [DTXMania2](https://dtxmania.net) ([FROM](https://github.com/DTXMania)) — ground-up rewrite by the original author
- [DTXMania AL](http://senamih.com/dtxal) (Sena)
- [DTXManiaXG verK](https://osdn.net/projects/dtxmaniaxg-verk) (kairera0467)
- [DTXManiaNX](https://github.com/limyz/DTXmaniaXG) (limyz/fisyher) — direct parent of this fork

For information on creating DTX chart files, see the [DTXMania Wiki](https://osdn.net/projects/dtxmania/wiki/DTX%20data%20format).

## Requirements

### To play (prebuilt release)

1. Windows 10 or later (x64)
2. [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (download the **.NET Desktop Runtime** installer for Windows x64)
3. [DirectX End-User Runtime (June 2010)](https://www.microsoft.com/en-us/download/details.aspx?id=35) — required for Direct3D 9

### To build from source

1. All of the above, plus:
2. [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (x64)
3. Git

## Building

```powershell
git clone https://github.com/broidmode/DTXManiaXYZ.git
cd DTXManiaXYZ
dotnet build DTXMania.sln -c Debug -p:Platform=x64
```

The build output goes to the `Runtime/` directory. Run `Runtime/DTXManiaNX.exe` to start the game.

DTX chart files go in `Runtime/DTXFiles/` or any folder you configure in the game's options.

## Community

For help or discussion, join the DTXMania Discord:
[https://discord.gg/ST5MWHe](https://discord.gg/ST5MWHe)
