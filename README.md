# ET-Printer

ET-Printer druckt Beschriftungsstreifen für Siemens **ET 200SP** und **S7-1500 / ET 200MP**
(35 mm und 25 mm) auf die originalen A4-Beschriftungsbögen. Die WPF-Anwendung ersetzt die
Siemens-Excel-Vorlagen durch eine klickbare A4-Vorschau, einen SPS-Adressgenerator, einen
Modul-Katalog mit datenblattverifizierter Klemmenbelegung sowie Importe aus CSV, Excel und
Schaltplan-PDFs. Vorschau und Druck rechnen über dieselbe Geometrie.

## Screenshot

Screenshots entstehen im Smoke-Test unter `test_results/<Name>/` (`sp_preview.png`,
`mp_preview.png`, `mp25_preview.png`); der Ordner ist per `.gitignore` ausgeschlossen.
Aufbau: links Eingabe-Panel (Familie/Format, Tabs Adress-Generator / Manuell / MP Modul,
Seite, Schrift), rechts die A4-Vorschau mit Zoom und Seitennavigation.

## Installation

- Fertige Anwendung: `publish/ET-Printer.exe` (self-contained, single-file, win-x64).
  Keine .NET-Installation nötig, einfach starten. Windows 10/11.
- Selbst bauen:

```
dotnet publish src/ETPrinter/ETPrinter.csproj -c Release -p:PublishProfile=win-x64
```

  Ergebnis liegt in `publish/`.

## Bedienung in 10 Schritten

1. Produktfamilie wählen: ET 200SP, S7-1500 / ET 200MP (35 mm) oder ET 200MP 25 mm.
2. Druckformat wählen (SP: horizontal/vertikal, ein-/zweizeilig, mit Kopfzeile; MP: horizontal/vertikal).
3. In der Vorschau ein Etikett bzw. einen Streifen anklicken (Position 1 = unten rechts wie auf dem Bogen).
4. Adress-Generator: Modulname, Modultyp (DI/DO/AI/AO), Start-Byte, Anzahl → „Generieren + Übertragen“.
   Die Startadresse schaltet automatisch weiter, die Auswahl springt zum nächsten Etikett/Modul.
5. Alternativ SP: Tab „Manuell“ (Kopfzeile, Zeile 1, Zeile 2, Enter = Übertragen).
   MP: Tab „MP Modul“ mit Siemens-Modul aus dem Katalog, Variante, Header, Netzadresse, CPU-Name.
6. Schrift je Etikett/Modul (Schriftart, 4–10 pt, fett, kursiv) wirkt sofort; „Auf alle anwenden“ überträgt sie.
7. Bei Bedarf importieren: Datei → Importieren → CSV / Excel (nur SP) / PDF-Schaltplan (SP und MP).
8. Weitere Seiten mit „+ Seite“; Etiketten kopieren mit Strg+Klick/Shift+Klick, Strg+C, Strg+V.
9. Projekt speichern (Ctrl+S) als `.etprint`.
10. Drucken (Ctrl+P): Drucker auf „Keine Skalierung / Originalgröße“, A4 Hochformat.
    Leere Seiten werden übersprungen, abgewählte Etiketten/Module nicht gedruckt.

## Beschriftungsbögen

| System | Artikelnummer | Positionen je A4 | Papier |
|--------|---------------|------------------|--------|
| ET 200SP | 6ES7193-6LA10-0AA0 | 100 Etiketten (5 × 20, 12,8 × 31 mm) | Karton 176–220 g |
| S7-1500 / ET 200MP 35 mm | 6ES7592-1AX00-0AA0 | 10 Streifen (2 Bänder × 5) | 96–110 g, Einzelblatteinzug |
| ET 200MP 25 mm | 6ES7592-2AX00-0AA0 | 20 Streifen (2 Bänder × 10) | 96–110 g, Einzelblatteinzug |

Ohne Siemens-Bogen: Option „Blanko A4 (alle Rahmen drucken)“ druckt Schnittkanten und
Rahmen auf Normalpapier oder 120-g-Karton zum Ausschneiden.

## Kalibrierung

Panel „Seite“ → „Testseite drucken (Fadenkreuze)“ auf Normalpapier, mit dem Siemens-Bogen
übereinanderlegen und gegen Licht halten. Die Fadenkreuze müssen auf die Rasterecken oben
links und unten rechts treffen. Abweichung in mm bei „Druckkalibrierung X/Y“ (±10 mm,
positiv = rechts/unten) eintragen. Die Werte werden nach dem nächsten erfolgreichen Druck
maschinenspezifisch in `%LOCALAPPDATA%\ETPrinter\calibration.json` gespeichert und haben
Vorrang vor Werten aus Projektdateien.

## Projektdateien und Speicherorte

- Projekte: `.etprint` (JSON, Version 5; ältere Versionen werden beim Laden migriert).
  Speichern ist atomar, die vorherige Version bleibt als `.bak` daneben.
  Öffnen per Dialog, Drag & Drop auf das Fenster oder als Startargument.
- `%LOCALAPPDATA%\ETPrinter\`:
  - `calibration.json` – Druckversatz dieses Rechners
  - `recent.json` – zuletzt geöffnete Dateien (max. 10)
  - `ui.json` – Fenstergröße, Position, Zoom, Splitter
  - `recovery.etprint` – Notfall-Sicherung nach einem Absturz (wird beim Start angeboten)
  - `log.txt` / `log.old.txt` – Protokoll (Rotation bei 1 MB)

## Entwicklung

- Voraussetzungen: .NET 9 SDK, Windows (WPF). Solution `ET-Printer.sln`.
- Build und Tests:

```
dotnet build ET-Printer.sln
dotnet test ET-Printer.sln
```

- Publish (siehe oben) nach `publish/`.
- Smoke-Test gegen die veröffentlichte EXE (startet die App mit Test-Automation, fährt
  SP/MP/25 mm/Import/Roundtrip durch, rendert Druckseiten als PNG):

```
.\tools\smoke.ps1 -Name AP5
```

- Test-Automation manuell: App mit `--test-automation` (oder `ETPRINTER_TEST=1`) starten,
  dann Befehle über die Named Pipe `ETPrinter_TestAutomation` senden:

```
.\test-send.ps1 "help"
.\test-send.ps1 "state"
.\test-send.ps1 "render-print C:\temp\druck"
```

## Dokumentation

- `docs/PRODUCT.md` – Vision, Zielgruppe, Kernfunktionen, Status
- `docs/FEATURES.md` – Feature-Spezifikation F01–F20, Tastaturkürzel, Test-Automation-Befehle, Standardwerte
- `docs/ARCHITECTURE.md` – Stack, Projektstruktur, MVVM, Geometrie, Persistenz, Entscheidungen
- `docs/PRINT-FORMATS.md` – Bogengeometrie und Klemmenbelegungen mit Datenblatt-Quellen
- `docs/CHANGELOG.md` – Versionshistorie
- `docs/ABSCHLUSSPLAN.md` – Arbeitspakete AP0–AP9 des Release-Durchlaufs

## Lizenz / Firma

Interne Anwendung der **DASA Elektronik**. Die Bogengeometrie basiert auf den Siemens
Excel-Vorlagen (Beitrags-IDs 81524595 und 83681795); Klemmenbelegungen stammen aus den
Siemens Equipment Manuals. Siemens, ET 200SP, ET 200MP und S7-1500 sind Marken der
Siemens AG.
