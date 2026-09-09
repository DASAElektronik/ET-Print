# ET-Printer – Produktdokumentation

Stand: 2026-09-09, Version 3.1.2 (Abschluss laut `ABSCHLUSSPLAN.md`).

## Vision

**ET-Printer** ist eine eigenständige Windows-Desktop-Anwendung (C# / WPF), mit der
Beschriftungsstreifen für die Siemens **ET 200SP** sowie für **S7-1500 / ET 200MP**
(35-mm- und 25-mm-Module) auf die originalen A4-Beschriftungsbögen gedruckt werden.
Sie ersetzt die Excel-Makro-Vorlagen von Siemens durch eine Anwendung mit klickbarer
A4-Vorschau, SPS-Adressgenerator, Modul-Katalog mit datenblattverifizierter
Klemmenbelegung und Importen aus CSV, Excel und Schaltplan-PDFs. Vorschau und Druck
rechnen über dieselbe Geometrie („Preview = Druck“).

## Hintergrund

Siemens liefert für beide Systeme Excel-Vorlagen mit VBA-Makros:

| System | Siemens-Beitrags-ID | Vorlage | Beschriftungsbogen |
|--------|---------------------|---------|--------------------|
| ET 200SP | 81524595 | `Excel_Template_ET200SP.xls` (6 Blätter: horizontal/vertikal × einzeilig/zweizeilig/zweizeilig+Header) | 6ES7193-6LA10-0AA0 (12,8 × 31 mm, 100 je A4) |
| S7-1500 / ET 200MP 35 mm | 83681795 | `Excel_Template_S71500_ET200MP.xls` (12 Blätter: 6 Modultypen × H/V) | 6ES7592-1AX00-0AA0 (10 Streifen je A4) |
| ET 200MP 25 mm | 83681795 | `Excel_Template_S71500_ET200MP_25mm.xls` (5 Blätter) | 6ES7592-2AX00-0AA0 (20 Streifen je A4) |

Die Vorlagen sind unhandlich (Makros, feste Zellen, keine Adressgenerierung, keine
Projektverwaltung). ET-Printer übernimmt ihre Geometrie und Zellenstruktur und ergänzt
die Klemmenbelegungen aus den Siemens Equipment Manuals (siehe `PRINT-FORMATS.md`).

## Zielgruppe

- Elektrotechniker und Automatisierungstechniker
- Schaltschrankbauer
- Inbetriebnehmer, die Beschriftungen aus Schaltplänen oder Listen übernehmen

## Kernfunktionen

1. **Drei Produktfamilien, zehn Druckformate** – ET 200SP (6 Formate) sowie ET 200MP
   35 mm und 25 mm (je horizontal/vertikal).
2. **Klickbare A4-Vorschau** – WYSIWYG, Layout wie der physische Bogen (Position 1 unten
   rechts), Zoom, Farbkodierung, Seitennavigation.
3. **SPS-Adressgenerator** – DI/DO/AI/AO, Start-Byte, Anzahl, klemmengerechte Verteilung
   (ET 200SP: 8 Slots je Reihe, ungerade Bits oben; ET 200MP: sequenziell je Byte auf die
   Kanalzellen), automatisches Weiterschalten.
4. **Modul-Editor und Modul-Katalog** – 9 Layout-Varianten, 18 konkrete Siemens-Module
   (35 mm: DI/DQ 16/32 HF/ST, DQ 8x2A, DI 230 V, AI 8 ST/HF/BA, AQ 4, SIWAREX WP521/522;
   25 mm: DI/DQ 16/32 BA, DI16/DQ16 BA, AQ 2) mit datenblattverifizierten Versorgungs- und
   Strukturklemmen; mehrzeiliger Header, Netzadresse/Netzname, CPU-Name.
5. **Schrift und Seite** – Schriftart, Größe, fett, kursiv je Etikett/Modul mit
   Live-Vorschau, globale Kopfzeilen-Schrift, Ränder, Blanko-A4-Druck mit Schnittkanten.
6. **Druck** – Windows-Druckdialog, A4 Hochformat erzwungen, leere Seiten übersprungen,
   selektiver Druck je Etikett/Modul, „Nur aktuelle Seite“, Kalibrierseite mit
   Fadenkreuzen und maschinenspezifischem X/Y-Versatz.
7. **Projekte** – `.etprint` (JSON) mit allen Seiten, Zuletzt-geöffnet-Liste, Drag & Drop,
   Doppelklick, atomares Speichern mit `.bak`, Wiederherstellung nach Absturz.
8. **Importe** – CSV (alle Excel-Exportvarianten), Excel `.xlsx`, PDF-Schaltplan
   (EPLAN/WSCAD-Export, deutsche und englische Adress-Mnemonics), im MP-Modus je Modul
   ein Streifen.
9. **Kopieren / Einfügen** – Etiketten oder Module inkl. Multi-Selection (Strg/Shift+Klick).
10. **Test-Automation** – Named-Pipe-Schnittstelle für Smoke-Tests und Druck-Rendering
11. **Rückgängig / Wiederholen** – jede Projektänderung ist ein Schritt (Ctrl+Z / Ctrl+Y)
    ohne Drucker.

## Technische Rahmenbedingungen

| Thema | Wert |
|-------|------|
| Plattform | Windows 10/11, x64 |
| Framework | .NET 9, WPF, C# 13 |
| Verteilung | `publish/ET-Printer.exe` (self-contained, single-file, keine .NET-Installation nötig) |
| Papierformat | A4 Hochformat 210 × 297 mm, keine Skalierung („Originalgröße“) |
| Papiersorte ET 200SP | Karton 176–220 g (Siemens-Bogen 6ES7193-6LA10-0AA0) |
| Papiersorte ET 200MP | mittleres Gewicht 96–110 g (Bögen 6ES7592-1AX00 / -2AX00), Einzelblatteinzug empfohlen |
| Blanko-Druck | 120 g Karton, hellblau (Standard) / gelb (Safety), mit Option „Blanko A4“ |
| Standard-Ränder | ET 200SP 20,5 / 27,5 / 20,5 / 27,5 mm; ET 200MP 14 / 25 / 19 / 12 mm |
| Benutzerdaten | `%LOCALAPPDATA%\ETPrinter\` (Kalibrierung, Recent, Fensterzustand, Log, Recovery) |
| Entwicklung | Visual Studio 2022 / `dotnet` CLI, xUnit-Tests |

## Status

- **v3.1 (2026-09-09)**: Abschluss der Arbeitspakete AP0–AP9 (Testinfrastruktur,
  kritische und mittlere Bugs, Geometrieklasse, Import-Robustheit, Komfort, Doku, Release,
  25-mm-Katalog). Stand v3.1.1: 284 Unit-Tests, Smoke-Test 98/98 Prüfungen grün.
- Historie: v1.0 MVP (März 2026), v1.1 Kalibrierung + Projekte, v1.2 Mehrseiten,
  v2.0 PDF-Parser, v2.1 Schriften/Importe, v2.2–2.5 ET 200MP-Grundgerüst und Modul-Layout,
  v3.0 (Juli 2026) 10 Streifen je Bogen, Modul-Katalog, SIWAREX, 25-mm-Template.
- AP8 (2026-09-09): 25-mm-BA-Module datenblattverifiziert (Katalog 18 Einträge).
- AP9 (2026-09-09, v3.1.1): Haupt-ViewModel in Teil-ViewModels zerlegt, Rückgängig/Wiederholen
  (Ctrl+Z/Ctrl+Y), inkrementelle ET 200MP-Vorschau.
- Offen (hardwareabhängig): exakte Maße der ET 200MP-Bögen per Stahllineal und
  Feinabstimmung der MP-Varianten gegen die physischen Bögen.
