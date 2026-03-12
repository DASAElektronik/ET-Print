# ET-Printer - TODO-Liste

## Phase 0: Planung & Setup [DONE]
- [x] Siemens PDF-Dokumentation analysieren
- [x] Excel Template analysieren (6 Sheets, Zellengroessen, Formate)
- [x] Produktdokumentation erstellen (PRODUCT.md)
- [x] Feature-Spezifikation erstellen (FEATURES.md)
- [x] Architektur definieren (ARCHITECTURE.md)
- [x] Druckformat-Dokumentation (PRINT-FORMATS.md)
- [x] Versionshistorie anlegen (CHANGELOG.md)
- [x] TODO-Liste erstellen
- [x] GravurApp-Layout analysieren (Eingabe links / Vorschau rechts)
- [x] UI-Konzept auf ET-Printer adaptieren

## Phase 1: MVP - Grundgeruest [DONE]
- [x] VS2022-Projekt anlegen (.NET 9 WPF)
- [x] Projektstruktur anlegen (Models, ViewModels, Services, Converters)
- [x] MVVM-Basisklassen (RelayCommand, ViewModelBase, INotifyPropertyChanged)
- [x] MainWindow: Zwei-Spalten-Layout mit GridSplitter
  - [x] Links: ScrollViewer > StackPanel mit GroupBoxen
  - [x] Rechts: A4-Vorschau (ScrollViewer > Border > ItemsControl)
- [x] Menu-Leiste (Datei: Neu/Drucken/Beenden, Bearbeiten, Hilfe)
- [x] Toolbar (Neu, Drucken)
- [x] Statusleiste (ausgewaehltes Etikett, Format-Info)

## Phase 2: MVP - Datenmodell [DONE]
- [x] LabelFormat Enum (6 Formate)
- [x] FormatInfo Record (Spalten, Zeilen, Header, Vertikal, LabelsPerRow, LabelRows)
- [x] LabelSettings Klasse (Schrift, Raender)
- [x] LabelCell Klasse (Header, Line1, Line2, per-Etikett FontSize/Bold/Italic)
- [x] LabelProject Klasse (Format, Settings, Labels)
- [x] FormatDefinitions: Rasterparameter fuer alle 6 Formate

## Phase 3: MVP - Eingabe-Panel (links) [DONE]
- [x] Format-Auswahl (ComboBox mit 6 Formaten)
- [x] Tab-basierte Eingabe: Adress-Generator | Manuell
- [x] SPS-Adress-Generator (F08):
  - [x] Modulname, Modultyp (DI/DO/AI/AO), Start-Byte, Anzahl
  - [x] Digital: Bit-Adressierung (ET200SP Klemmenanordnung)
  - [x] Analog: Wort-Adressierung
  - [x] Auto-Weiterschalten Startadresse
  - [x] Live-Vorschau
  - [x] Kopfzeile nachtraeglich aendern (ohne Neugenerierung)
- [x] Manuelle Eingabe (Header, Zeile 1, Zeile 2)
- [x] Button "Uebertragen" + auto-weiter zum naechsten Etikett
- [x] Einstellungen GroupBox:
  - [x] Schriftgroesse ComboBox (4-10)
  - [x] Fett/Kursiv CheckBoxen
  - [x] Seitenraender (Oben/Links/Unten/Rechts)
  - [x] Zuruecksetzen/Uebernehmen Buttons
- [x] Per-Etikett Schrift-Einstellungen
- [x] Button "Alle loeschen"

## Phase 4: MVP - A4-Vorschau (rechts) [DONE]
- [x] A4-Blatt als weisser Border mit DropShadow auf grauem Hintergrund
- [x] Etikettenraster (ItemsControl > UniformGrid)
- [x] Klickbare Etiketten mit Farbkodierung (leer/befuellt/ausgewaehlt/hover)
- [x] Zoom-Slider (0.5x - 4.0x)
- [x] Horizontale Formate: Textzeilen mit TextTrimming
- [x] Vertikale Formate: Einzelne Adress-Rechtecke (je Adresse ein Rechteck, 90 Grad)
- [x] Kopfzeilen-Spalte (schmal, 90 Grad gedreht, bedingt sichtbar)
- [x] Dynamische Seitenraender (gebunden an Einstellungen)
- [x] Schrift-Einstellungen wirken pro Etikett in der Vorschau
- [x] Layout wie physisches A4-Blatt (6ES7193-6LA10-0AA0): Nummerierung 1=unten rechts
- [x] Zeilennummern 1-20 am rechten Rand (wie physisches Blatt)
- [x] Alle Formate: einheitlich 5x20 = 100 Etiketten

## Phase 5: MVP - Druckfunktion [DONE]
- [x] PrintService: Canvas-basiertes FixedDocument mit exakten mm-Positionen
- [x] Umrechnung mm zu WPF-Einheiten (96/25.4 DPI)
- [x] Positionsberechnung: Grid-Spiegelung (Position 1 = unten rechts)
- [x] Horizontale Formate: Zentrierter Text (ein-/zweizeilig)
- [x] Vertikale Formate: Adress-Rechtecke mit 90-Grad-Rotation
- [x] Header-Spalten-Rendering im Druck
- [x] Per-Etikett Schrift-Einstellungen im Druck (Groesse, Fett, Kursiv)
- [x] Windows-Druckdialog-Integration
- [x] Option "Gitterlinien drucken" (aus=Siemens-Etikettenbogen, an=Normalpapier)
- [x] Schrift: Arial
- [x] Testdruck als PDF verifiziert
- [x] Druckkalibrierung (X/Y-Offset in mm, gespeichert als calibration.json)
- [x] Kalibrierungs-Testseite mit Fadenkreuzen (Ecken Reihe 1 + Reihe 20)

## Phase 6: v1.1 - Speichern/Laden [DONE]
- [x] Projekt speichern (.etprint JSON) - Ctrl+S
- [x] Projekt speichern unter... (neuer Dateiname)
- [x] Projekt laden - Ctrl+O
- [x] Zuletzt geoeffnete Dateien (bis zu 10, im Datei-Menue)
- [x] Gespeicherte Daten: Format, 100 Etiketten, Raender, Kalibrierung, Druckoptionen
- [x] Titelleiste zeigt Dateinamen
- [x] Toolbar: Neu, Oeffnen, Speichern, Drucken

## Phase 7: v1.2 - Mehrseiten
- [ ] Seiten hinzufuegen/entfernen
- [ ] Seitennavigation
- [ ] Mehrseitendruck

## Phase 8: v2.0 - PDF-Schaltplan-Parser
- [ ] PDF-Parser-Modul (z.B. PdfPig)
- [ ] Erkennung von Modulnamen, Adressen, Kanalzuordnungen
- [ ] Automatisches Befuellen der Etiketten aus Schaltplan

---

## Offene Fragen
- [ ] Exakte Zellengroessen durch Testdruck verifizieren
- [ ] Soll die Schriftart waehlbar sein oder fest auf Arial?
- [ ] Sollen Etiketten auch einzeln gedruckt werden koennen?
- [ ] Import aus CSV/Excel-Dateien gewuenscht?
