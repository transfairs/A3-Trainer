<div align="center">

<img src="help/img/logo.png" width="96" height="96" alt="A3-Trainer logo">

# A3-Trainer (Proof of Concept)

[![⬇️ Download](https://img.shields.io/badge/⬇️_Download-neueste_Version-73CB39?style=for-the-badge)](https://github.com/transfairs/A3-Trainer/releases/latest/download/A3-Trainer-Latest.zip)
[![📖 Onlinehilfe](https://img.shields.io/badge/📖_Onlinehilfe-A3--Trainer-4A9A21?style=for-the-badge)](https://transfairs.github.io/A3-Trainer/)

</div>

Trainer für **Anstoss 3**, gebaut auf demselben Grundgerüst wie der
[A2G-Trainer](https://github.com/transfairs/A2G-Trainer). Stand: erster PoC – Spieler, Verein und Manager.

## Voraussetzungen

- **Anstoss 3 Version 1.10 mit ungeschützter `anstoss3.exe`** (4.980.803 Bytes). Nur in dieser
  EXE liegen alle Spieldaten an festen Adressen. Die mit VOB ProtectCD geschützten EXEs (1.0 und
  die geschützte 1.10) erkennt der Trainer und meldet sie als nicht unterstützt.
- Ein geladener Spielstand (Karriere). Im Hauptmenü wartet der Trainer.

Damit das Spiel unter Windows 10/11 überhaupt startet:

- `gl.cfg` im Spielordner (wird sonst vom 3D-Setup `SGLSetup.exe` angelegt; ohne sie beendet sich
  das Spiel kommentarlos), z. B. Direct3D im Fenster:
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
- Kompatibilitätsmodus **„Mit 16-Bit-Farbe ausführen“** für `anstoss3.exe` (sonst bleibt das
  Fenster weiß).

## Was geht

**Spieler** (eigener Kader und nächster Gegner):
Hauptposition, Nebenpositionen, Stärke (1–13), Talent, Kondition, Frische, starker Fuß,
Rückennummer, Fähigkeiten (Feldspieler und Torhüter getrennt), Eigenschaften, Charakter,
Festgehalt, Auflaufprämie, Vertragsende, Verletzung heilen. Alter und Motivation werden angezeigt.

**Verein und Manager** (Spieler 1): Vermögen am Saisonbeginn (bestimmt das Bankguthaben),
privates Vermögen des Managers.

**Cheat-Engine-Tabelle** `A3-Trainer.ct` (liegt jedem Release bei): Manager, Vermögen am
Saisonbeginn und Basisjahr als feste Einträge. Das Skript „Kader (eigener Verein und nächster
Gegner)“ liest beim Aktivieren beide Kader ein und legt je Spieler die Felder des Trainers an
(Positionen, Talent und Fuß als Auswahl, Fähigkeiten, Eigenschaften und Charakter als einzelne
Bits). Bei geladenen Spielern zeigen die Einträge auf die Arbeitskopie, die das Spiel beim
Speichern zurückschreibt. Solange das Skript aktiv ist, prüft es alle zwei Sekunden, ob sich
Spielstand, Kader oder Gegner geändert haben, und baut die Einträge dann neu auf; aufgeklappte
Gruppen bleiben offen. Eine feste Kaderliste wie bei A2G (Einträge ohne Skript) ist für A3 noch
nicht gefunden.

## Wie es funktioniert (kurz)

- Spielerdatensätze `CDatenSpieler` liegen fest ab `0x00C02550` (0x354 Byte je Spieler).
- Für die Spieler der geladenen Mannschaften (eigener Kader, nächster Gegner) arbeitet das Spiel
  auf **Arbeitskopien** („CSpieler“-Cache, Baum ab `0x00A3D3C4`) und schreibt sie beim Speichern
  in die Datensätze zurück. Der Trainer schreibt deshalb in Kopie **und** Datensatz.
- Alle Adressen und Feldbedeutungen stehen in `Controller/A3Addresses.cs`.

## Projektaufbau

- `A3-Trainer/` (Trainer), `A3-Trainer.Tests/` (xUnit), `help/` (Onlinehilfe).
- `Shared/` ist ein **Git-Submodule** auf
  [Anstoss-Trainer-Shared](https://github.com/transfairs/Anstoss-Trainer-Shared): Speicherzugriff,
  Logger, Fenster- und Ansichtsbasis, Spielbeobachter, Skalierung, Manifest, Hilfe-CSS/-JS und die
  Changelog-Skripte, gemeinsam mit dem A2G-Trainer. Klonen mit
  `git clone --recursive https://github.com/transfairs/A3-Trainer.git`.
- Änderungen an Shared kommen automatisch an: Die CI des Shared-Repos testet sie mit beiden
  Trainern, danach zieht `.github/workflows/shared-update.yml` den neuen Stand nach (bei grünen
  Tests direkt auf master, sonst als Pull Request). Ohne Anstoß läuft das Update täglich.

## Bauen und testen

Wie beim A2G-Trainer mit der vollen Visual-Studio-MSBuild (kein reines `dotnet build`):
```bash
msbuild A3-Trainer.Tests/A3-Trainer.Tests.csproj /t:Restore,Build /p:Configuration=Debug
dotnet test A3-Trainer.Tests/A3-Trainer.Tests.csproj --no-build --no-restore
```

## Hilfe, Release

- Onlinehilfe: [help/](help/index.html), veröffentlicht unter
  <https://transfairs.github.io/A3-Trainer/> (im Trainer: `? → Onlinehilfe`).
  `pages.yml` kopiert beim Deploy `style.css` und `script.js` aus `Shared/help/` daneben.
- Release: Tag `vX.Y.Z` pushen, `.github/workflows/release.yml` baut, testet und veröffentlicht
  `A3-Trainer-<tag>.exe`, `A3-Trainer-<tag>.ct` und `A3-Trainer-Latest.zip` (Trainer, Tabelle,
  Changelog). Vorher den Pre-Release-Check laufen lassen.
- Die Adressen in `A3-Trainer.ct` (Lua-Tabellen `A`, `R`, `C`, Namenslisten, feste Einträge)
  prüft `A3-Trainer.Tests/CheatTableTests.cs` gegen `A3Addresses.cs` und `Tables.cs`. Die Datei
  ohne BOM speichern, sonst lädt Cheat Engine sie nicht.
- Fester Download der neuesten Version:
  <https://github.com/transfairs/A3-Trainer/releases/latest/download/A3-Trainer-Latest.zip>
- Analysematerial (Ghidra-Projekt, Dumps, Strukturnotizen `re/anstoss3/STRUKTUREN.md`) liegt lokal
  in `re/` und wird nicht versioniert (abgeleitet aus dem Spielcode).

## Haftungsausschluss

Dieses Tool greift direkt auf den Arbeitsspeicher des Spiels zu. Nutzung auf eigene Gefahr. Das
Projekt steht in keiner Verbindung zu Ascaron oder den Entwicklern von *Anstoss 3*.

## Bekannte Grenzen

- **Motivation** errechnet das Spiel bei jeder Anzeige aus sieben Faktoren neu
  (`FUN_0070147E`); ein geschriebener Wert hält nicht. Deshalb nur Anzeige.
- Eine im Spiel **geöffnete Spielerinfo** schreibt beim Schließen ihre eigene Kopie zurück.
  Vor dem Speichern im Trainer die Spielerinfo des Spielers im Spiel schließen.
- Kondition: Das Spiel zeigt beim Torwart einen niedrigeren Wert als gespeichert (Anzeigeformel
  noch nicht entschlüsselt).
- Manager und Finanzen: nur Spieler 1 (Einzelspieler-Karriere).
- Sperren, Fähigkeitswerte im Detail, Vereine anderer Länder als Deutschland: noch nicht geprüft.
- Die Oberfläche ist im Code aufgebaut (kein Designer).
