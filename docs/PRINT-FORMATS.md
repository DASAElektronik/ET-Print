# ET-Printer - Druckformat-Spezifikation

> Abgeleitet aus: Excel_Template_ET200SP.xls (Siemens Beitrags-ID: 81524595)

## Seitengeometrie
- **Papier:** A4 Hochformat, 210 x 297 mm
- **Papiersorte:** Karton 176-220g (empfohlen)
- **Druckart:** Keine Skalierung, Originalgroesse
- **Etikettengroesse:** 12,5mm (Hoehe) x 32mm (Breite) pro Etikett
- **Raster:** 5 Spalten x 20 Zeilen = 100 Etiketten pro A4
- **Etikettenfeld:** 5 x 32mm = 160mm Breite, 20 x 12,5mm = 250mm Hoehe

### Standard-Seitenraender
| Rand   | Wert   |
|--------|--------|
| Oben   | 23.5 mm |
| Links  | 25 mm   |
| Unten  | 23.5 mm |
| Rechts | 25 mm   |

### Druckbereich
- **Breite:** 210 - 25 - 25 = **160 mm** (5 x 32mm)
- **Hoehe:** 297 - 23.5 - 23.5 = **250 mm** (20 x 12,5mm)

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

## Druckhinweise
- Drucker auf "Keine Skalierung" / "Originalgroesse" einstellen
- Papiersorte: Karton 176-220g
- Bei Verschiebungen: Seitenraender anpassen oder Einzelblatteinzug verwenden
- Beschriftungsstreifen muessen nach dem Druck zugeschnitten werden
