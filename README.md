<div align="center">

<img src="help/img/logo.png" width="96" height="96" alt="A3-Trainer logo">

# A3-Trainer

[![⬇️ Download](https://img.shields.io/badge/⬇️_Download-latest_version-73CB39?style=for-the-badge)](https://github.com/transfairs/A3-Trainer/releases/latest/download/A3-Trainer-Latest.zip)
[![📖 Online help](https://img.shields.io/badge/📖_Online_help-A3--Trainer-4A9A21?style=for-the-badge)](https://transfairs.github.io/A3-Trainer/)

</div>

Trainer for **Anstoss 3**. Edits players, club and manager.

## Requirements

- **Anstoss 3 version 1.40**. The exes on the CD (1.0 and the official patch 1.10) are separate
  builds with a different memory layout; the trainer reports them as unsupported.
- A loaded save game (career). In the game's main menu the trainer waits.

To get the game running on Windows 10/11 at all:

- `gl.cfg` in the game folder (normally written by the 3D setup `SGLSetup.exe`; without it the
  game quits silently), e.g. Direct3D in a window:
  ```
  DLL = d3d_dll.dll
  ColorDepth = 16
  FullScreen = FALSE

  BEGIN D3D
  forcewindow = TRUE
  TextureCacheSize = 8000000
  Driver = ""
  MipMapMode = 1
  TexelCenter = 0
  END
  ```
- Compatibility mode **"Reduced color mode: 16-bit"** („Mit 16-Bit-Farbe ausführen“) for
  `anstoss3.exe` (otherwise the window stays white).

## Features

**Players** (own squad and next opponent), laid out in the tabs of the game's data editor:
date of birth, strength, talent, strong foot, shirt number, nationality and second nationality,
skin colour, main and secondary positions, skills ("+" and "−", outfield players and goalkeepers
separately), traits, character, fitness, freshness, heal injury, salary, appearance bonus,
contract end. Age, motivation and crowd standing (favourite/hate figure) are shown read-only.

**Club** (player 1): wealth at the start of the season (determines the bank balance).
**Manager** (player 1): private wealth.

**Cheat Engine table** `A3-Trainer.ct` (included in every release): manager, wealth at the start
of the season and base year as fixed entries. The script „Kader (eigener Verein und nächster
Gegner)“ reads both squads when activated and creates the trainer's fields for every player
(positions, talent, foot, nationalities and skin colour as drop-downs, skills "+"/"−", traits and
character as single bits). For loaded players the entries point to the working copy the game
writes back when saving. While the script is active it checks every two seconds whether the save
game, squad or opponent has changed and rebuilds the entries; expanded groups stay open. A fixed
squad list as in A2G (entries without a script) hasn't been found for A3 yet.

## How it works (short)

- Player records `CDatenSpieler` are at a fixed address from `0x00C02550` (0x354 bytes per player).
- For the players of the loaded teams (own squad, next opponent) the game works on **working
  copies** („CSpieler“ cache, tree at `0x00A3D3C4`) and writes them back to the records when
  saving. The trainer therefore writes to the copy **and** the record.
- All addresses and field meanings are in `Controller/A3Addresses.cs`.

## Project layout

- `A3-Trainer/` (trainer), `A3-Trainer.Tests/` (xUnit), `help/` (online help, German).
- `Shared/` is a **git submodule** pointing to
  [Anstoss-Trainer-Shared](https://github.com/transfairs/Anstoss-Trainer-Shared): memory access,
  logger, window and view base, game observer, scaling, manifest, help CSS/JS and the changelog
  scripts, shared with the A2G-Trainer. Clone with
  `git clone --recursive https://github.com/transfairs/A3-Trainer.git`.
- Changes to Shared arrive automatically: the Shared repo's CI tests them with both trainers, then
  `.github/workflows/shared-update.yml` pulls in the new state (straight to main when the tests
  pass, otherwise as a pull request). Without a trigger the update runs daily.

## Build and test

As with the A2G-Trainer, use the full Visual Studio MSBuild (plain `dotnet build` doesn't work):
```bash
msbuild A3-Trainer.Tests/A3-Trainer.Tests.csproj /t:Restore,Build /p:Configuration=Debug
dotnet test A3-Trainer.Tests/A3-Trainer.Tests.csproj --no-build --no-restore
```

## Help, release

- Online help: [help/](help/index.html), published at
  <https://transfairs.github.io/A3-Trainer/> (in the trainer: `? → Onlinehilfe`).
  On deploy, `pages.yml` copies `style.css` and `script.js` from `Shared/help/` next to it.
- Release: push a `vX.Y.Z` tag; `.github/workflows/release.yml` builds, tests and publishes
  `A3-Trainer-<tag>.exe`, `A3-Trainer-<tag>.ct` and `A3-Trainer-Latest.zip` (trainer, table,
  changelog). Run the pre-release check first.
- `A3-Trainer.Tests/CheatTableTests.cs` checks the addresses in `A3-Trainer.ct` (Lua tables `A`,
  `R`, `C`, name lists, fixed entries) against `A3Addresses.cs` and `Tables.cs`. Save the file
  without a BOM, otherwise Cheat Engine won't load it.
- Permanent download link for the latest version:
  <https://github.com/transfairs/A3-Trainer/releases/latest/download/A3-Trainer-Latest.zip>
- Analysis material (Ghidra project, dumps, structure notes `re/anstoss3/STRUKTUREN.md`) is kept
  locally in `re/` and not versioned (derived from the game code).

## Disclaimer

This tool accesses the game's memory directly. Use at your own risk. The project is not affiliated
with Ascaron or the developers of *Anstoss 3*.

## Known limitations

- **Motivation** is recalculated by the game from seven factors every time it is shown
  (`FUN_0070147E`); a written value doesn't stick. Hence read-only.
- A **player info window open in the game** writes its own copy back when it is closed. Close the
  player's info window in the game before saving in the trainer.
- Fitness: for goalkeepers the game shows a lower value than the stored one (display formula not
  decoded yet).
- Manager and finances: player 1 only (single-player career).
- Suspensions, detailed skill values, clubs from countries other than Germany: not checked yet.
- The UI is built in code (no designer).
