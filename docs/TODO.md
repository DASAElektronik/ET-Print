# ET-Printer - TODO-Liste

## Phase 0: Planung & Setup [IN PROGRESS]
- [x] Siemens PDF-Dokumentation analysieren
- [x] Excel Template analysieren (6 Sheets, Zellengroessen, Formate)
- [x] Produktdokumentation erstellen (PRODUCT.md)
- [x] Feature-Spezifikation erstellen (FEATURES.md)
- [x] Architektur definieren (ARCHITECTURE.md)
- [x] Versionshistorie anlegen (CHANGELOG.md)
- [x] TODO-Liste erstellen
- [x] GravurApp-Layout analysieren (Eingabe links / Vorschau rechts)
- [x] UI-Konzept auf ET-Printer adaptieren
- [ ] VS2022-Projekt anlegen (.NET 8 WPF)
- [ ] Git-Repository initialisieren

## Phase 1: MVP - Grundgeruest
- [ ] Projektstruktur anlegen (Models, ViewModels, Services)
- [ ] MVVM-Basisklassen (RelayCommand, ViewModelBase, INotifyPropertyChanged)
- [ ] MainWindow: Zwei-Spalten-Layout mit GridSplitter
  - [ ] Links: ScrollViewer > StackPanel mit GroupBoxen
  - [ ] Rechts: A4-Vorschau (ScrollViewer > Viewbox > Canvas)
- [ ] Menu-Leiste (Datei: Neu/Oeffnen/Speichern/Drucken/Beenden)
- [ ] Toolbar (Neu, Oeffnen, Speichern, Drucken)
- [ ] Statusleiste (ausgewaehltes Etikett, Format-Info)

## Phase 2: MVP - Datenmodell
- [ ] LabelFormat Enum (6 Formate)
- [ ] LabelSettings Klasse (Schrift, Raender)
- [ ] LabelCell Klasse (Text, Position, IsSelected, HasText)
- [ ] LabelPage Klasse (Zellen-Grid)
- [ ] LabelProject Klasse (Seiten, Format, Einstellungen)
- [ ] FormatDefinitions: Rasterparameter fuer alle 6 Formate

## Phase 3: MVP - Eingabe-Panel (links)
- [ ] Format-Auswahl (ComboBox mit 6 Formaten)
- [ ] Etikett-Eingabe GroupBox:
  - [ ] Header-Textfeld (nur bei +Header-Formaten sichtbar)
  - [ ] Zeile-1-Textfeld
  - [ ] Zeile-2-Textfeld (nur bei zweizeiligen Formaten sichtbar)
- [ ] Button "Uebertragen" + auto-weiter zum naechsten Etikett
- [ ] Einstellungen GroupBox:
  - [ ] Schriftgroesse ComboBox (6-10)
  - [ ] Fett/Kursiv CheckBoxen
  - [ ] Seitenraender (4x Spinner: Oben/Links/Unten/Rechts)
  - [ ] Zuruecksetzen/Uebernehmen Buttons
- [ ] Button "Alle loeschen"

## Phase 4: MVP - A4-Vorschau (rechts)
- [ ] A4-Blatt als weisser Border mit DropShadow auf grauem Hintergrund
- [ ] Etikettenraster (ItemsControl > Raster-Panel)
- [ ] Klickbare Etiketten mit Farbkodierung:
  - [ ] Leer: hellgrau (#E8E8E8)
  - [ ] Befuellt: hellgruen (#D0E8D0)
  - [ ] Ausgewaehlt: hellblau (#B8D4F0) mit blauem Rahmen
- [ ] Positionsnummern in den Etiketten
- [ ] Zoom-Slider (0.5x - 4.0x)
- [ ] Textanzeige in den Etiketten (mit korrekter Rotation fuer vertikal)
- [ ] Echtzeit-Update bei Texteingabe

## Phase 5: MVP - Druckfunktion
- [ ] LayoutEngine: Berechnung der exakten Zellenpositionen auf A4
- [ ] PrintService: FixedDocument-Erzeugung
- [ ] Textrotation fuer vertikale Formate (90 Grad)
- [ ] Header-Spalten-Rendering
- [ ] Windows-Druckdialog-Integration
- [ ] Testdruck und Kalibrierung

## Phase 6: v1.1 - Speichern/Laden
- [ ] Projekt speichern (.etprint JSON)
- [ ] Projekt laden
- [ ] Zuletzt geoeffnete Dateien

## Phase 7: v1.2 - Mehrseiten
- [ ] Seiten hinzufuegen/entfernen
- [ ] Seitennavigation
- [ ] Mehrseitendruck

---

## Offene Fragen
- [ ] Exakte Zellengroessen durch Testdruck verifizieren
- [ ] Soll die Schriftart waehlbar sein oder fest auf Arial?
- [ ] Sollen Etiketten auch einzeln gedruckt werden koennen?
- [ ] Import aus CSV/Excel-Dateien gewuenscht?
