# ET-Printer - Feature-Spezifikation

## F01: Druckformate
Sechs Formatvorlagen, abgeleitet aus dem Siemens Excel Template:

### F01.1: Horizontal zweizeilig mit Kopfzeile
- **Layout:** [Header | Zeile 1] + [Header | Zeile 2] pro Modul
- **Header:** Schmale Spalte, Text 90 Grad gedreht (z.B. "Station 1")
- **Textzellen:** Zwei Zeilen pro Modul (z.B. "ET 200SP" / "Base Unit A0")
- **Raster:** 5 Etikettengruppen nebeneinander, 20 Etiketten untereinander = 100 pro Seite
- **Spalten:** 10 (abwechselnd schmal ~6.6mm Header / breit ~26.7mm Text)
- **Zeilenhoehe:** ~18.75pt (375 twips)

### F01.2: Horizontal zweizeilig
- **Layout:** 2 Zeilen pro Modul, kein Header
- **Raster:** 5 Spalten, 20 Etikettenreihen = 100 pro Seite
- **Spaltenbreite:** Gleichmaessig ~33.6mm
- **Zeilenhoehe:** ~18.75pt

### F01.3: Horizontal einzeilig
- **Layout:** 1 Zeile pro Modul (z.B. "ET 200SP Base Unit A0")
- **Raster:** 7 Spalten, 20 Etikettenreihen = 140 pro Seite
- **Spaltenbreite:** Gleichmaessig ~33.6mm
- **Zeilenhoehe:** ~37.5pt (750 twips)

### F01.4: Vertikal zweizeilig mit Kopfzeile
- **Layout:** Identisch zu F01.1, aber alle Texte 90 Grad gedreht
- **Raster:** 5 Etikettengruppen, 20 Reihen = 100 pro Seite

### F01.5: Vertikal zweizeilig
- **Layout:** Identisch zu F01.2, aber alle Texte 90 Grad gedreht
- **Raster:** 5 Spalten, 20 Reihen = 100 pro Seite

### F01.6: Vertikal einzeilig
- **Layout:** Identisch zu F01.3, aber alle Texte 90 Grad gedreht
- **Raster:** 7 Spalten, 20 Reihen = 140 pro Seite

---

## F02: Hauptfenster-Layout (wie GravurApp)
Zwei-Spalten-Layout mit GridSplitter:

### Linke Seite: Eingabepanel
- **Druckformat-Auswahl** (ComboBox mit 6 Formaten)
- **Etikett-Eingabe** (GroupBox):
  - Bei einzeilig: 1 Textfeld
  - Bei zweizeilig: 2 Textfelder (Zeile 1, Zeile 2)
  - Bei +Header: zusaetzliches Header-Textfeld
- **Einstellungen** (GroupBox):
  - Schriftgroesse (ComboBox: 6-10, Standard 7)
  - Fett / Kursiv (CheckBoxen)
  - Seitenraender (Oben/Links/Unten/Rechts mit Spinner)
  - Button "Zuruecksetzen" / "Uebernehmen"
- **Button "Auf Etikett uebertragen"** - uebertraegt Eingabe auf das ausgewaehlte Etikett
- **Button "Alle loeschen"** - leert alle Etiketten

### Rechte Seite: A4-Blatt-Vorschau
- WYSIWYG-Darstellung des A4-Blatts (weiss auf grauem Hintergrund)
- Etikettenraster gemaess gewaehltem Format
- **Klickbare Etiketten** - Klick auf ein Etikett waehlt es aus (blau markiert)
- Befuellte Etiketten farblich hervorgehoben (gruen)
- Zoom-Slider (0.5x - 4.0x)
- ScrollViewer fuer groessere Zoom-Stufen
- Positionsnummern in den Etiketten
- Rahmenlinien zwischen den Etiketten

### Workflow (wie GravurApp)
1. Format waehlen (z.B. "Horizontal zweizeilig")
2. Etikett in der A4-Vorschau anklicken
3. Text links eingeben
4. "Uebertragen" klicken (oder Enter)
5. Naechstes Etikett anklicken, wiederholen
6. Drucken

---

## F03: Einstellungen (im linken Panel integriert)
Konfigurierbare Parameter (analog zum Excel Template):

| Einstellung        | Bereich       | Standardwert |
|--------------------|---------------|--------------|
| Schriftgroesse     | 6 - 10 pt     | 7            |
| Fett               | An / Aus      | Aus          |
| Kursiv             | An / Aus      | Aus          |
| Seitenrand oben    | 0.0 - 5.0 cm  | 2.0 cm       |
| Seitenrand links   | 0.0 - 5.0 cm  | 3.0 cm       |
| Seitenrand unten   | 0.0 - 5.0 cm  | 2.1 cm       |
| Seitenrand rechts  | 0.0 - 5.0 cm  | 2.5 cm       |

- Button "Zuruecksetzen" setzt alle Werte auf Standard
- Button "Uebernehmen" wendet Aenderungen an und aktualisiert Vorschau

---

## F04: A4-Vorschau (in Hauptfenster integriert - rechte Seite)
- WYSIWYG-Darstellung des A4-Blatts als Canvas in Viewbox
- Zoom-Funktion (Slider 0.5x - 4.0x)
- Seitenraender als gestrichelte Linien
- Etikettenraster mit Rahmenlinien
- Farbkodierung: leer=hellgrau, befuellt=hellgruen, ausgewaehlt=hellblau
- Echtzeit-Aktualisierung bei Texteingabe

---

## F05: Druckfunktion
- Druck auf jeden installierten Windows-Drucker
- A4 Hochformat, keine Skalierung
- Druckerdialog mit Druckerauswahl
- Hinweis auf empfohlene Papiersorte (Karton 176-220g)

---

## F06: Projekt speichern/laden
- **Dateiformat:** JSON (.etprint)
- **Inhalt:** Beschriftungstexte, gewaehltes Format, Einstellungen
- **Speichern unter / Oeffnen** Dialoge
- Zuletzt geoeffnete Dateien

---

## F07: Mehrere Seiten
- Wenn mehr Etiketten benoetigt werden als auf eine Seite passen
- Seiten hinzufuegen/entfernen
- Seitennavigation

---

## Prioritaeten

| Prioritaet | Features                                              |
|------------|-------------------------------------------------------|
| **MVP**    | F01 (6 Formate), F02 (Layout+Editor), F03, F04, F05   |
| **v1.1**   | F06 (Speichern/Laden)                                 |
| **v1.2**   | F07 (Mehrere Seiten)                                  |
