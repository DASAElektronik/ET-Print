# ET-Printer - Druckformat-Spezifikation

> Abgeleitet aus: Excel_Template_ET200SP.xls (Siemens Beitrags-ID: 81524595)

## Seitengeometrie
- **Papier:** A4 Hochformat, 210 x 297 mm
- **Papiersorte:** Karton 176-220g (empfohlen)
- **Druckart:** Keine Skalierung, Originalgroesse
- **Etikettengroesse:** 12,8mm (Hoehe) x 31mm (Breite) pro Etikett (gemessen am Bogen 6ES7193-6LA10-0AA0)
- **Raster:** 5 Spalten x 20 Zeilen = 100 Etiketten pro A4
- **Etikettenfeld:** 5 x 31mm = 155mm Breite, 20 x 12,8mm = 256mm Hoehe

### Standard-Seitenraender
| Rand   | Wert   |
|--------|--------|
| Oben   | 20.5 mm |
| Links  | 27.5 mm |
| Unten  | 20.5 mm |
| Rechts | 27.5 mm |

### Druckbereich
- **Breite:** 210 - 27.5 - 27.5 = **155 mm** (5 x 31mm)
- **Hoehe:** 297 - 20.5 - 20.5 = **256 mm** (20 x 12,8mm)

---

## Format-Details (aus Excel-Analyse)

### Gemeinsame Parameter
- **Schriftart:** Arial (Standard Excel-Font)
- **Schriftgroesse:** 7pt (Standard), waehlbar 6-10pt
- **Alle Formate:** 40 Excel-Zeilen, bis zu 10 Excel-Spalten

### Excel-Einheiten-Umrechnung
- **Spaltenbreite:** In 1/256 Zeichenbreite (bei Arial 10pt: 1 Zeichen ~ 2.1mm)
  - Formel: `mm = width * 2.1 / 256`
- **Zeilenhoehe:** In 1/20 Punkt (Twips)
  - Formel: `mm = height / 20 * 0.3528`

---

## 1. Horizontal zweizeilig + Header

| Eigenschaft       | Wert                                          |
|-------------------|-----------------------------------------------|
| Sheet-Name        | "horizontal double lines+header"              |
| Textrotation      | Header: 90 Grad, Textzellen: 0 Grad           |
| Spalten           | 10 (5 Gruppen: [Header\|Text])                |
| Zeilen            | 40 (20 Etikettenpaare a 2 Zeilen)             |
| Etiketten/Seite   | 100 (5 x 20)                                  |
| Merged Cells      | Header-Spalten: je 2 Zeilen vertikal gemergt  |

**Spaltenbreiten (Excel-Einheiten / ~mm):**
| Spalte | Typ    | Width | ~mm   |
|--------|--------|-------|-------|
| 0      | Header | 804   | 6.6   |
| 1      | Text   | 3254  | 26.7  |
| 2      | Header | 804   | 6.6   |
| 3      | Text   | 3254  | 26.7  |
| 4      | Header | 804   | 6.6   |
| 5      | Text   | 3254  | 26.7  |
| 6      | Header | 804   | 6.6   |
| 7      | Text   | 3328  | 27.3  |
| 8      | Header | 804   | 6.6   |
| 9      | Text   | 3181  | 26.1  |

**Zeilenhoehe:** 375 twips = 18.75pt = ~6.6mm

**Zellenstruktur pro Etikett:**
```
+----------+-----------+
| Station  | ET 200SP  |   <- Zeile 1
| 1        | Base Unit |   <- Zeile 2
| (90 Grad)| A0        |
+----------+-----------+
```

---

## 2. Horizontal zweizeilig

| Eigenschaft       | Wert                                          |
|-------------------|-----------------------------------------------|
| Sheet-Name        | "horizontal double lines"                     |
| Textrotation      | 0 Grad                                         |
| Spalten           | 5                                              |
| Zeilen            | 40 (20 Etikettenpaare a 2 Zeilen)             |
| Etiketten/Seite   | 100 (5 x 20)                                  |
| Merged Cells      | Keine                                          |

**Spaltenbreiten:**
| Spalte | Width | ~mm  |
|--------|-------|------|
| 0-2    | 4096  | 33.6 |
| 3-4    | 4022  | 33.0 |

**Zeilenhoehe:** 375 twips = ~6.6mm

**Zellenstruktur pro Etikett:**
```
+-------------+
| ET 200SP    |  <- Zeile 1
| Base Unit A0|  <- Zeile 2
+-------------+
```

---

## 3. Horizontal einzeilig

| Eigenschaft       | Wert                                          |
|-------------------|-----------------------------------------------|
| Sheet-Name        | "horizontal single line"                      |
| Textrotation      | 0 Grad                                         |
| Spalten           | 7                                              |
| Zeilen            | 20 (+ 20 leere Zeilen)                        |
| Etiketten/Seite   | 140 (7 x 20)                                  |
| Merged Cells      | Keine                                          |

**Spaltenbreiten:**
| Spalte | Width | ~mm  |
|--------|-------|------|
| 0-2, 5-6 | 4096 | 33.6 |
| 3      | 4059  | 33.3 |
| 4      | 3986  | 32.7 |

**Zeilenhoehe:** 750 twips = 37.5pt = ~13.2mm (Zeilen 0-19), Rest: 255 twips

**Zellenstruktur pro Etikett:**
```
+------------------------+
| ET 200SP Base Unit A0  |  <- 1 Zeile, hoeher
+------------------------+
```

---

## 4. Vertikal zweizeilig + Header
- **Identisch zu Format 1**, aber alle Texte 90 Grad gedreht
- Header-Spalten: 90 Grad (gleich wie horizontal)
- Text-Spalten: ebenfalls 90 Grad

---

## 5. Vertikal zweizeilig
- **Identisch zu Format 2**, aber alle Texte 90 Grad gedreht

---

## 6. Vertikal einzeilig
- **Identisch zu Format 3**, aber alle Texte 90 Grad gedreht

---

## S7-1500 / ET 200MP Formate (Beitrags-ID: 83681795)

### Beschriftungsboegen
- **6ES7592-1AX00-0AA0** - Standard (35mm Module)
- **6ES7592-2AX00-0AA0** - 25mm Module

### Seitengeometrie ET200MP
- **Papier:** A4 Hochformat, 210 x 297 mm
- **Papiersorte:** Mittleres Gewicht 96-110g
- **2 Baender pro A4** (obere + untere Haelfte)
- **5 Module pro Band** = 10 Module pro Seite (Standard)
- **10 Module pro Band** = 20 Module pro Seite (25mm)

### Standard-Seitenraender (aus Siemens-Doku)
| Rand   | Wert   |
|--------|--------|
| Oben   | 14.0 mm |
| Links  | 25.0 mm |
| Unten  | 19.0 mm |
| Rechts | 12.0 mm |

### Modulstruktur (4 Spalten pro Modul)
| Spalte | Excel-Breite | Anteil | Inhalt |
|--------|-------------|--------|--------|
| Col 0  | 1499        | 32.8%  | Adresse Links |
| Col 1  | 1499        | 32.8%  | Adresse Rechts |
| Col 2  | 804         | 17.6%  | Netzadresse (90 Grad, merged) |
| Col 3  | 768         | 16.8%  | CPU-Name (90 Grad, merged) |

### Zeilenhoehen
| Zeile | Twips | ~mm | Inhalt |
|-------|-------|-----|--------|
| 0     | 1455  | 25.7 | Modul-Header (Device/Module/Slot) |
| 1-20  | 315   | 5.6  | Kanalzeilen (Daten) |
| 21    | 1170  | 20.6 | Separator / Band-2-Header |
| 22-41 | 315   | 5.6  | Kanalzeilen Band 2 |

### 6 Modultyp-Varianten (Zellen-Merges)
| Variante | Col 0+1 | Zeilen/Adresse | Beschreibung |
|----------|---------|---------------|--------------|
| 32 DI/DQ | getrennt | 1 | 2 Spalten, 1 Zeile pro Bit |
| 16 DI/DQ | gemergt | 1 | 1 breite Spalte, 1 Zeile pro Kanal |
| 16 DI 230V | getrennt | 2 | 2 Spalten, 2 Zeilen pro Kanal-Paar |
| 8 DQ 230V | getrennt | 2 | 2 Spalten, 2 Zeilen + Leergruppen |
| 8 AI/AQ | getrennt | 4 | 2 Spalten, 4 Zeilen pro Kanal |
| 4 AQ | gemergt | 4 | 1 breite Spalte, 4 Zeilen pro Kanal |

Jede Variante existiert horizontal (0 Grad) und vertikal (90 Grad) = 12 Formate.

---

## Druckhinweise

### ET 200SP
- Drucker auf "Keine Skalierung" / "Originalgroesse" einstellen
- Papiersorte: Karton 176-220g
- Bei Verschiebungen: Seitenraender anpassen oder Einzelblatteinzug verwenden
- Beschriftungsstreifen muessen nach dem Druck zugeschnitten werden

### S7-1500 / ET 200MP
- Drucker auf "Keine Skalierung" einstellen
- Papiersorte: Mittleres Gewicht 96-110g
- Einzelblatteinzug empfohlen
- Masse sind geschaetzt (werden nach Lieferung der Boegen per Stahllineal korrigiert)
