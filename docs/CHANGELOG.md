# ET-Printer - Versionshistorie

## [Unreleased]
### Geplant
- Druckfunktion (Phase 5)
- Projekt speichern/laden (v1.1)

---

## 2026-03-12 - Projekttag 1

### Phase 0: Planung & Setup
- Siemens PDF-Dokumentation (Beitrags-ID 81524595) analysiert
- Excel Template (Excel_Template_ET200SP.xls) analysiert: 6 Sheets, Zellengroessen, Spaltenbreiten, Zeilenhoehen, Textrotation
- Physisches Etikettenblatt identifiziert: 6ES7193-6LA10-0AA0 (13mm x 31mm, 100 Stueck pro A4)
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

---

## Versionsplan

### v1.0.0 - MVP
- [x] Editor mit 6 Druckformaten
- [x] A4-Vorschau mit WYSIWYG
- [x] SPS-Adress-Generator
- [x] Per-Etikett Schrift-Einstellungen
- [ ] Druckfunktion (A4 ueber Windows-Druckdialog)

### v1.1.0
- Projekt speichern/laden (.etprint JSON)
- Zuletzt geoeffnete Dateien

### v1.2.0
- Mehrseitenunterstuetzung
- Seiten hinzufuegen/entfernen

### v2.0.0
- PDF-Schaltplan-Parser (automatische SPS-Adress-Extraktion)

---

## Projekt-Meilensteine

| Datum      | Meilenstein                                          |
|------------|------------------------------------------------------|
| 2026-03-12 | Projektstart, Analyse, Konzept & Dokumentation       |
| 2026-03-12 | VS2022-Projekt angelegt, .NET 9 WPF                  |
| 2026-03-12 | Phase 1-4 fertig: Editor, Generator, A4-Vorschau     |
| TBD        | Phase 5: Druckfunktion                               |
| TBD        | v1.1 Release                                         |
