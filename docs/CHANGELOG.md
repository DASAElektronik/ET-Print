# ET-Printer - Versionshistorie

## [Unreleased]
### Geplant
- Mappe-fuer-Mappe Feinabstimmung der Modultypen gegen Excel-Template
- Weitere Modultypen: DQ, AI, AQ Klemmenbelegungen aus Datenblaettern
- SIWAREX Waegemodule
- Exakte Masse per Stahllineal (wenn Beschriftungsboegen geliefert)

---

## 2026-04-23 - Projekttag 3 (Feature: Blanko A4)

### Fix
- Eingabe-Tabs (Generator, Manuell, MP Modul) waren im S7-1500/ET200MP-Modus
  komplett deaktiviert. Ursache: TabControl-IsEnabled war an SelectedLabel
  gebunden, das im MP-Modus immer null bleibt. Neue Property HasSelection
  deckt beide Modi ab (SelectedLabel OR SelectedMpModule).
- MP-Preview refreshte nicht nach Generator/Apply/Header-Aenderung. Das
  Canvas-basierte MpPreviewControl rendert manuell und hoerte nur auf
  Module-/Selection-Wechsel — Zell-PropertyChanged allein triggerte kein
  Re-Render. Refresh-Token in MainViewModel eingefuehrt; Control hoert
  zusaetzlich auf MpPreviewRefreshToken.
- Nach Generate im MP-Modus wird nun zum naechsten Modul gesprungen
  (analog AdvanceToNextLabel bei ET200SP). Am Ende der Seite wird zur
  naechsten Seite gewechselt. Adressen werden NICHT automatisch neu
  generiert — User prueft Variante + Start-Byte und klickt erneut
  "Generieren + Uebertragen".
- ET200SP Analog-Adressen jetzt alternierend wie digital verteilt:
  Ungerade Kanaele (K1, K3, K5, K7) in Line1 (oben), gerade (K0, K2,
  K4, K6) in Line2 (unten). Fix 8 Plaetze pro Reihe; nicht genutzte
  Plaetze als leere Slots. Beispiel AI 8xI (8 Kanaele):
    Oben:  EW 2   EW 6   EW 10  EW 14  (leer) (leer) (leer) (leer)
    Unten: EW 0   EW 4   EW 8   EW 12  (leer) (leer) (leer) (leer)
  Die 8 Plaetze entsprechen den Klemmen pro Reihe auf der BU A0/A1.
- LabelViewModel.Line1Parts/Line2Parts: StringSplitOptions.None statt
  RemoveEmptyEntries, damit leere Slots in der Reihe erhalten bleiben.

### Feature
- **Blanko-A4-Druck**: Checkbox "Gitterlinien drucken (Normalpapier)" umbenannt zu
  "Blanko A4 (alle Rahmen drucken)". Aktiviert druckt auf leeres A4-Papier
  (z.B. 120g Karton, hellblau = Standard-Baugruppen, gelb = Safety-Baugruppen)
  mit kompletten Schnittkanten + Adress-Rahmen + Kopfzeilen-Rahmen.
- Rahmenfarbe von Hellgrau auf Schwarz: Schnittkanten 0.5pt, innere Rahmen 0.3pt.
- Position der Rahmen stimmt 1:1 mit der Vorschau ueberein (Preview = Druck).

### Fix
- Schnittkanten wurden urspruenglich fuer ALLE 100 Etiketten gedruckt, auch leere.
  Jetzt konsistent zu inneren Rahmen: nur befuellte und druckaktive Etiketten bekommen
  Schnittkanten. Bei ET200MP zusaetzliches `!mod.HasText continue;` neben
  `!IsPrintEnabled`.

---

## 2026-04-23 - Projekttag 3 (Code-Qualitaet)

### Bugfixes (Session Code-Review)
- CalibrationService: bare catch{} durch getypte Exception-Filter ersetzt; Save() meldet Fehler via StatusMessage zurueck
- ProjectService: bare catch{} getypt; Load() prueft File.Exists vorab und erzwingt 50MB File-Size-Cap gegen JSON-DoS
- TestAutomationService.Dispose(): wartet bis 2s auf ListenLoop-Completion vor CTS.Dispose, vermeidet Race beim Shutdown
- TestAutomationService.RunOnUI: voller Stacktrace ins Log, Exception-Typ in Response
- TestAutomationService.ResizeWindow: Bounds-Check (400..10000), InvariantCulture beim Parsen
- TestAutomationService: neue ValidateProjectPath() fuer save/load-project (erzwingt .etprint-Extension)

### Neue Features / Hardening
- **Logger**: neuer Log-Service (`Services/Log.cs`) schreibt nach `%LOCALAPPDATA%\ETPrinter\log.txt` mit 1MB-Rotation zu `.old.txt`. Thread-safe, IO-sicher mit Debug.WriteLine-Fallback. Debug.WriteLine-Calls in 3 Services migriert.
- **Test-Automation-Flag**: Named-Pipe-Server startet nur bei `--test-automation` CLI-Arg oder `ETPRINTER_TEST=1` ENV-Var. Reduziert Attack-Surface in Production.
- **Regex-Hardening**: alle 4 Patterns in SchematicParserService mit `RegexOptions.NonBacktracking` + 1s Timeout gegen pathologische PDF-Inhalte.
- **Unit-Tests**: neues Projekt `tests/ETPrinter.Tests` (xUnit, net9.0-windows), in Solution integriert. 41 Tests fuer AddressGenerator (digital/analog Adressgenerierung, odd/even-Split, Byte-Stepping) und MpModuleLayoutFactory (Half==0-Invariante fuer alle 6 Varianten).

### Code-Decisions
- Half-1 Rendering-Pfad bleibt erhalten (Unit-Test pinnt Half==0-Invariante). Entfernen waere ohne Rendering-Tests zu riskant; Git-History bewahrt Code falls spaeter 2-Half-Layouts gebraucht werden.

---

## 2026-03-23 - Projekttag 2

### Phase 12: Architektur-Korrektur + Generator-Fix (v2.5)
- ARCHITEKTUR-FIX: 5 Module pro Seite (nicht 10)
  - Band 1 + Band 2 derselben Spalte = EIN physischer Beschriftungsstreifen
  - MpCellDefinition mit Half-Feld (0=obere Haelfte, 1=untere Haelfte)
- EIN zusammenhaengender Streifen in Preview und Druck
  - Header nur einmal oben (nicht mehr doppelt)
  - Trennlinie statt zweiter Header zwischen den Haelften
- Adress-Generator fuer ET200MP komplett neu:
  - Sequenzielle Bit-Adressen (E x.0 bis E x.7) statt ET200SP odd/even Split
  - Korrekte Byte-Zuordnung nach physischer Klemmenbelegung (0, 2, 1, 3)
  - Gruppe a (Byte 0) oben-links, Gruppe c (Byte 2) oben-rechts
  - Gruppe b (Byte 1) unten-links, Gruppe d (Byte 3) unten-rechts
- DI 32x24VDC HF Klemmenbelegung aus Siemens Equipment Manual
  - 40 Klemmen, CH0-CH31, M/L+/GND Positionen als Strukturzellen
  - Strukturzellen zeigen Labels (M, L+, 1M, 2L+, MANA)
- AI 8xU/I/RTD/TC ST Klemmenbelegung dokumentiert (4 Zeilen pro Kanal)
- AQ 4xU/I ST Klemmenbelegung dokumentiert (nur linke Seite benutzt)
- TestAutomation: zoom, maximize, resize Befehle
- Alle S7-1500 Modultypen in PRINT-FORMATS.md katalogisiert (DI/DQ/AI/AQ/SIWAREX)

### Phase 11: ET200MP modulbasiertes Layout (v2.3)
- MpModule Datenmodell (HeaderText, AddressCells, NetAddress1-4, CpuName)
- 6 MpModuleVariant mit deklarativen Zellen-Merge-Definitionen
- MpModuleLayoutFactory: exakte Zellenstruktur pro Variante aus Excel
- Canvas-basiertes MpPreviewControl (statt UniformGrid)
- IsModuleBased-Flag als Weiche zwischen ET200SP- und ET200MP-Codepfad
- Modul-Editor Tab (Variante, Header, Adressen, NetAddr, CpuName)
- PrintService: PrintMp() mit modulbasiertem Rendering
- Speichern/Laden v4 mit MpPages, Zellen-Persistenz
- Paralleles Datenmodell — ET200SP komplett unveraendert

### Phase 10: S7-1500 / ET200MP Grundgeruest (v2.2)
- ProductFamily-Konzept eingefuehrt (ET200SP, S71500_ET200MP, S71500_ET200MP_25mm)
- ProductFamilyInfo Record mit Massen, Raendern, Beschriftungsbogen-Artikelnr
- 4 neue LabelFormat-Enum-Werte (MP_Horizontal, MP_Vertical, MP25_Horizontal, MP25_Vertical)
- FormatInfo erweitert: BandsPerPage, ChannelRowsPerBand, Family
- ProductFamily-ComboBox in der UI mit automatischer Format-Filterung
- Default-Raender wechseln pro Produktfamilie (ET200MP: O:14 L:25 U:19 R:12)
- A4-Vorschau: 2-Band-Layout fuer ET200MP (40 Zeilen = 2x20)
- PrintService: 2-Band-Rendering mit Header/Separator-Berechnung
- Speichern/Laden: v3 Format mit ProductFamily, v2-Migration
- ResetSettings beruecksichtigt aktuelle ProductFamily
- Format-Family-Validierung beim Laden
- Beschriftungsbogen-Nr in Statusleiste
- Test-Automation: Named-Pipe-Server (TestAutomationService)
  - Befehle: ping, state, screenshot, select-family/format/label, set-text, generate, navigate
  - PowerShell-Client (test-send.ps1)
- Geschaetzte Masse aus Excel-Template (werden spaeter mit Stahllineal korrigiert)
- Excel-Templates analysiert: Siemens Beitrags-ID 83681795
  - Excel_Template_S71500_ET200MP.xls (12 Sheets, 6 Modultypen x H/V)
  - Excel_Template_S71500_ET200MP_25mm.xls (5 Sheets)
  - Beschriftungsboegen: 6ES7592-1AX00-0AA0, 6ES7592-2AX00-0AA0

---

## 2026-03-12 - Projekttag 1

### Phase 0: Planung & Setup
- Siemens PDF-Dokumentation (Beitrags-ID 81524595) analysiert
- Excel Template (Excel_Template_ET200SP.xls) analysiert: 6 Sheets, Zellengroessen, Spaltenbreiten, Zeilenhoehen, Textrotation
- Physisches Etikettenblatt identifiziert: 6ES7193-6LA10-0AA0 (12,8mm x 31mm, 100 Stueck pro A4)
- GravurApp-Layout als Referenz analysiert (C:\claude\Gravur)
- Dokumentation erstellt: PRODUCT.md, FEATURES.md, ARCHITECTURE.md, PRINT-FORMATS.md
- UI-Konzept: Zwei-Spalten-Layout (Eingabe links, A4-Vorschau rechts) adaptiert von GravurApp

### Phase 1: VS2022-Projekt & Grundgeruest
- VS2022 Solution angelegt (ET-Printer.sln)
- .NET 9 WPF-Projekt erstellt (ETPrinter.csproj)
- MVVM-Basisklassen implementiert: ViewModelBase, RelayCommand
- MainWindow: Zwei-Spalten-Layout mit GridSplitter, Menueleiste, Toolbar, Statusleiste

### Phase 2: Datenmodell
- LabelFormat Enum + FormatInfo Record (6 Formate: horizontal/vertikal x einzeilig/zweizeilig/zweizeilig+header)
- LabelSettings Klasse (Schriftgroesse, Fett, Kursiv, Seitenraender)
- LabelCell Klasse (Header, Line1, Line2, per-Etikett Font-Einstellungen)
- LabelProject Klasse
- FormatDefinitions: Rasterparameter fuer alle 6 Formate mit Zellengroessen-Berechnung

### Phase 3: Eingabe-Panel (links)
- Format-Auswahl ComboBox (6 Formate)
- Tab-basierte Eingabe: "Adress-Generator" | "Manuell"
- SPS-Adress-Generator (F08):
  - Modulname, Modultyp (DI/DO/AI/AO), Start-Byte, Anzahl
  - Digital (DI/DO): Bit-Adressierung mit ET200SP Klemmenanordnung (oben=ungerade, unten=gerade)
  - Analog (AI/AO): Wort-Adressierung (EW 0, EW 2, EW 4...)
  - Auto-Weiterschalten der Startadresse
  - Live-Vorschau der generierten Adressen
  - "Kopfzeile aendern"-Button (Kopfzeile nachtraeglich editieren ohne Neugenerierung)
- Manuelle Eingabe: Header, Zeile 1, Zeile 2 (kontextabhaengig sichtbar)
- Einstellungen: Schriftgroesse (4-10), Fett, Kursiv, Seitenraender
- Per-Etikett Schrift-Einstellungen (individuell pro Etikett anpassbar)
- Buttons: Uebertragen, Alle loeschen, Zuruecksetzen, Uebernehmen

### Phase 4: A4-Vorschau (rechts)
- WYSIWYG A4-Blatt (630x891px = 210x297mm bei 3px/mm) mit DropShadow
- Etikettenraster via ItemsControl + UniformGrid
- Klickbare Etiketten mit Farbkodierung:
  - Leer: hellgrau (#F0F0F0)
  - Befuellt: hellgruen (#D0E8D0)
  - Ausgewaehlt: hellblau (#B8D4F0) mit blauem Rahmen
  - Hover-Effekt (#E0E8F0)
- Positionsnummern in jedem Etikett
- Zoom-Slider (0.5x - 4.0x)
- Dynamische Seitenraender (gebunden an Einstellungen)
- Horizontale Formate: Text als Zeilen mit TextTrimming
- Vertikale Formate: Einzelne Adress-Rechtecke pro Zeile (wie im Excel-Template)
  - Jede Adresse in eigenem umrandeten Rechteck
  - Text um 90 Grad gedreht
  - UniformGrid-basierte Anordnung (z.B. 8 Rechtecke pro Zeile bei 2-Byte DI)
- Kopfzeilen-Spalte: Schmale lila Spalte mit 90-Grad-gedrehtem Modulnamen
- Schrift-Einstellungen (Groesse, Fett, Kursiv) wirken in der Vorschau pro Etikett

### Phase 4b: A4-Vorschau - Physisches Blatt-Layout
- Layout an physisches Etikettenblatt 6ES7193-6LA10-0AA0 angepasst
- Grid-Spiegelung: Nummerierung 1 = unten rechts (wie physisches A4-Blatt)
- Zeilennummern 1-20 am rechten Rand hinzugefuegt
- Alle Formate korrigiert auf einheitlich 5x20 = 100 Etiketten (identisches A4-Blatt)
- Positionsnummern aus den Zellen entfernt (Zeilennummern am Rand genuegen)

### Phase 5: Druckfunktion
- PrintService implementiert (Services/PrintService.cs)
  - Canvas-basiertes FixedDocument mit exakten mm-zu-WPF-Einheiten (96/25.4)
  - Positionsberechnung mit Grid-Spiegelung (Position 1 = unten rechts)
  - Horizontale Formate: Zentrierter Text, ein- und zweizeilig
  - Vertikale Formate: Einzelne Adress-Rechtecke mit 90-Grad-Rotation
  - Header-Spalten mit rotiertem Modulnamen
  - Per-Etikett Schrift-Einstellungen (FontSize 4-10, Bold, Italic)
  - Schriftart: Arial
- Windows-Druckdialog-Integration
- Option "Gitterlinien drucken":
  - Deaktiviert (Standard): Nur Text, fuer perforierte Siemens-Etikettenboegen
  - Aktiviert: Mit Gitterlinien, fuer Normalpapier zum Ausschneiden
- Testdruck als PDF erfolgreich verifiziert

### Phase 5b: Druckkalibrierung
- CalibrationService implementiert (Services/CalibrationService.cs)
  - X/Y-Offset in mm (positiv/negativ) fuer Feinjustierung
  - Persistente Speicherung als calibration.json im App-Verzeichnis
- Kalibrierungs-Testseite mit Fadenkreuzen:
  - Fadenkreuz an Ecke Reihe 1 (unten rechts) und Reihe 20 (oben links)
  - Fadenkreuze mit Linien, Kreis und Positionslabel
  - Workflow: Testseite auf Normalpapier drucken, mit Siemens-Bogen uebereinander gegen Licht halten
- Kalibrierungs-Eingabefelder im Einstellungen-Panel (Offset X/Y in mm)
- Kalibrier-Offsets fliessen in PrintService-Druckberechnung ein

### Phase 6: Speichern/Laden (v1.1)
- ProjectService implementiert (Services/ProjectService.cs)
  - .etprint JSON-Dateiformat (alle Projektdaten inkl. Kalibrierung)
  - JsonStringEnumConverter fuer LabelFormat-Serialisierung
- Speichern (Ctrl+S) und Speichern unter...
- Oeffnen (Ctrl+O) mit Dateiauswahl-Dialog
- Zuletzt geoeffnete Dateien (max. 10, im Datei-Menue)
  - Gespeichert in %LocalAppData%\ETPrinter\recent.json
- Toolbar erweitert: Oeffnen- und Speichern-Buttons
- Titelleiste zeigt aktuellen Dateinamen
- Gespeicherte Daten: Format, 100 Etiketten, Raender, Kalibrierung, Druckoptionen
- RelayCommand<T> fuer typisierte Command-Parameter (OpenRecentCommand)

### Phase 7: Mehrseitenunterstuetzung (v1.2)
- LabelPage Datenmodell (Models/LabelPage.cs)
- LabelProject auf Pages-basiert umgestellt (Version 2)
- ProjectService: v1-zu-v2 Migration (flache Labels -> LabelPage)
- Seitennavigation: Vor/Zurueck-Buttons, Seitenanzeige
- Tastenkuerzel: Ctrl+PageUp/Down fuer Seitennavigation
- Seiten hinzufuegen/entfernen (+ Seite / - Seite)
- Auto-Seitenwechsel bei letztem Etikett einer Seite
- Mehrseitendruck: Alle Seiten in einem FixedDocument
- PrintService auf IReadOnlyList<IReadOnlyList<LabelViewModel>> umgestellt

### Phase 8: PDF-Schaltplan-Parser (v2.0)
- PdfPig NuGet-Paket (UglyToad.PdfPig) integriert
- SchematicParserService (Services/SchematicParserService.cs)
  - Texterkennung mit Positionsinformationen pro PDF-Seite
  - Erkennung von ET200SP Modulmustern (BMK, Adresstypen)
  - Regex-Patterns fuer Digital- (E/A x.x) und Analog-Adressen (EW/AW x)
  - Raeumliche Gruppierung nach Y-Koordinaten
- SchematicParseResult Modell (ParsedModule, ParsedChannel)
- PdfImportDialog (Views/PdfImportDialog.xaml)
  - DataGrid mit Modulauswahl (Checkbox, Name, Typ, Startadresse, Kanalanzahl)
  - Alle/Keine auswaehlen Buttons
  - Warnungsanzeige
- PdfImportViewModel mit SelectableParsedModule
- Integration: Importieren-Untermenue im Datei-Menue

### Phase 9: Erweiterte Features (v2.1)
- Variable Schriftarten:
  - FontFamily-Property in LabelCell, LabelSettings, LabelViewModel
  - Schriftart-ComboBox mit Vorschau aller Windows-Fonts
  - Per-Etikett Schriftart in Vorschau und Druck (PrintService)
- Selektiver Etikettendruck:
  - IsPrintEnabled-Property pro Etikett
  - Visuelle Dimming (Opacity 0.4) fuer deaktivierte Etiketten
  - Druckauswahl-Menue: Alle/Keine/Befuellte drucken
  - Kontextmenue: Druck umschalten per Rechtsklick
- CSV-Import (Services/CsvImportService.cs):
  - Auto-Detect Trennzeichen (Semikolon/Komma)
  - Header-Erkennung (Header, Zeile1/Line1, Zeile2/Line2)
  - Encoding-Fallback (UTF-8 -> Windows-1252)
- Excel-Import (Services/ExcelImportService.cs):
  - ClosedXML NuGet-Paket fuer .xlsx-Dateien
  - Auto-Detect Spalten-Mapping
- PopulateFromImportedCells: Seitenuebergreifendes Befuellen

### Critic-Review Fixes
- FontFamily-Binding fuer vertikale Vorschau und Header ergaenzt
- Standard-Seitenraender vereinheitlicht (20.5/27.5/20.5/27.5mm = exakt 12.8x31mm Etiketten, gemessen)
- CalibrationService: Speicherpfad auf %LocalAppData%\ETPrinter verschoben
- RelayCommand<T>: Sichere Type-Checks statt direkter Casts
- DoOpen: Font-Settings werden korrekt restauriert
- ApplySettings: Font-Settings werden in _settings geschrieben
- PopulateFromParsedModules: Adress-Duplikation bei unbekannten Modultypen behoben

---

## Versionsplan

### v1.0.0 - MVP [DONE]
- [x] Editor mit 6 Druckformaten
- [x] A4-Vorschau mit WYSIWYG (Layout wie physisches Blatt)
- [x] SPS-Adress-Generator
- [x] Per-Etikett Schrift-Einstellungen
- [x] Druckfunktion (A4 ueber Windows-Druckdialog, mit/ohne Gitterlinien)

### v1.1.0 [DONE]
- [x] Druckkalibrierung (X/Y-Offset, Fadenkreuz-Testseite)
- [x] Projekt speichern/laden (.etprint JSON)
- [x] Zuletzt geoeffnete Dateien

### v1.2.0 [DONE]
- [x] Mehrseitenunterstuetzung
- [x] Seiten hinzufuegen/entfernen
- [x] Mehrseitendruck

### v2.0.0 [DONE]
- [x] PDF-Schaltplan-Parser (automatische SPS-Adress-Extraktion)
- [x] PdfImportDialog mit Modulauswahl

### v2.1.0 [DONE]
- [x] Variable Schriftarten (alle Windows-Fonts)
- [x] Selektiver Etikettendruck
- [x] CSV-Import
- [x] Excel-Import (.xlsx)

---

## Projekt-Meilensteine

| Datum      | Meilenstein                                          |
|------------|------------------------------------------------------|
| 2026-03-12 | Projektstart, Analyse, Konzept & Dokumentation       |
| 2026-03-12 | VS2022-Projekt angelegt, .NET 9 WPF                  |
| 2026-03-12 | Phase 1-4 fertig: Editor, Generator, A4-Vorschau     |
| 2026-03-12 | Phase 5 fertig: Druckfunktion mit PrintService        |
| 2026-03-12 | v1.0.0 MVP komplett                                  |
| 2026-03-12 | Druckkalibrierung mit Fadenkreuz-Testseite            |
| 2026-03-12 | v1.1.0: Speichern/Laden (.etprint), Recent Files      |
| 2026-03-12 | v1.2.0: Mehrseitenunterstuetzung                      |
| 2026-03-12 | v2.0.0: PDF-Schaltplan-Parser                         |
| 2026-03-12 | v2.1.0: Variable Fonts, Selektiver Druck, CSV/Excel   |
