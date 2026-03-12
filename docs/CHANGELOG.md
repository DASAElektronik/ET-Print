# ET-Printer - Versionshistorie

## [Unreleased]
### Geplant
- Keine offenen Features

---

## 2026-03-12 - Projekttag 1

### Phase 0: Planung & Setup
- Siemens PDF-Dokumentation (Beitrags-ID 81524595) analysiert
- Excel Template (Excel_Template_ET200SP.xls) analysiert: 6 Sheets, Zellengroessen, Spaltenbreiten, Zeilenhoehen, Textrotation
- Physisches Etikettenblatt identifiziert: 6ES7193-6LA10-0AA0 (12,5mm x 32mm, 100 Stueck pro A4)
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
- Standard-Seitenraender vereinheitlicht (23.5/25/23.5/25mm = exakt 12.5x32mm Etiketten)
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
