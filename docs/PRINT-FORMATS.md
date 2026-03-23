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
- **5 Spalten pro A4** (jede Spalte = 1 physischer Beschriftungsstreifen/Modul)
- Jede Spalte hat **2 Haelften** (obere + untere), beide gehoeren zum SELBEN Modul
- **5 Module pro Seite** (NICHT 10 — Band 1+2 derselben Spalte = 1 Modul)

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
| Col 0  | 1499        | 32.8%  | Klemmen links (linke Modulseite) |
| Col 1  | 1499        | 32.8%  | Klemmen rechts (rechte Modulseite) |
| Col 2  | 804         | 17.6%  | Netzadresse (90 Grad, merged 10 Zeilen) |
| Col 3  | 768         | 16.8%  | CPU-Name (90 Grad, merged 20 Zeilen) |

### Zeilenhoehen
| Zeile  | Twips | ~mm  | Inhalt |
|--------|-------|------|--------|
| 0      | 1455  | 25.7 | Modul-Header (Device/Module/Slot) |
| 1-8    | 315   | 5.6  | Kanalgruppe a/c (8 Kanaele) |
| 9      | 330   | 5.8  | M (Masse) Trennklemme |
| 10-17  | 315   | 5.6  | Kanalgruppe b/d (8 Kanaele) |
| 18     | 315   | 5.6  | L+ (Power) |
| 19     | 315   | 5.6  | M (Ground) |
| 20     | 330   | 5.8  | (leer) |
| 21     | 1170  | 20.6 | Modul-Header (gleicher Text wie Zeile 0) |
| 22-41  |       |      | (gleiche Struktur wie 1-20 fuer untere Haelfte) |

### Physische Klemmenbelegung (Quelle: Siemens Equipment Manual)

Jedes S7-1500/ET200MP Modul hat **40 Klemmen** (20 links, 20 rechts).
Die Klemmenbelegung bestimmt, welche Zeilen im Beschriftungsstreifen
Kanaladressen, Masse (M) oder Versorgung (L+) sind.

#### DI 32x24VDC HF (6ES7521-1BL00-0AB0) — 40 Klemmen

**Obere Haelfte des Beschriftungsstreifens:**
```
Linke Seite (Col 0)      Rechte Seite (Col 1)
Klemme | Belegung         Klemme | Belegung
───────┼──────────        ───────┼──────────
  1    | CH0  (DI a.0)      21   | CH16 (DI c.0)
  2    | CH1  (DI a.1)      22   | CH17 (DI c.1)
  3    | CH2  (DI a.2)      23   | CH18 (DI c.2)
  4    | CH3  (DI a.3)      24   | CH19 (DI c.3)
  5    | CH4  (DI a.4)      25   | CH20 (DI c.4)
  6    | CH5  (DI a.5)      26   | CH21 (DI c.5)
  7    | CH6  (DI a.6)      27   | CH22 (DI c.6)
  8    | CH7  (DI a.7)      28   | CH23 (DI c.7)
  9    | (M - Masse)        29   | (M - Masse)
 10    | (leer)              30   | (leer)
```

**Untere Haelfte des Beschriftungsstreifens:**
```
Linke Seite (Col 0)      Rechte Seite (Col 1)
Klemme | Belegung         Klemme | Belegung
───────┼──────────        ───────┼──────────
 11    | CH8  (DI b.0)      31   | CH24 (DI d.0)
 12    | CH9  (DI b.1)      32   | CH25 (DI d.1)
 13    | CH10 (DI b.2)      33   | CH26 (DI d.2)
 14    | CH11 (DI b.3)      34   | CH27 (DI d.3)
 15    | CH12 (DI b.4)      35   | CH28 (DI d.4)
 16    | CH13 (DI b.5)      36   | CH29 (DI d.5)
 17    | CH14 (DI b.6)      37   | CH30 (DI d.6)
 18    | CH15 (DI b.7)      38   | CH31 (DI d.7) / 2L+
 19    | 1L+  (Power)        39   | 2M   (Ground)
 20    | 1M   (Ground)       40   | (leer)
```

**Kanalgruppen und Byte-Zuordnung:**
- Gruppe a: CH0-CH7   = Byte 0 (E x.0 bis E x.7) — Klemmen 1-8 links
- Gruppe b: CH8-CH15  = Byte 1 (E x+1.0 bis E x+1.7) — Klemmen 11-18 links (= untere Haelfte)
- Gruppe c: CH16-CH23 = Byte 2 (E x+2.0 bis E x+2.7) — Klemmen 21-28 rechts
- Gruppe d: CH24-CH31 = Byte 3 (E x+3.0 bis E x+3.7) — Klemmen 31-38 rechts (= untere Haelfte)

**Wichtig:** Die Kanalnummerierung ist 0-basiert (CH0 = E x.0, NICHT E x.1)!
Das Excel-Template zeigt Q 0.1 bis Q 0.7 — das ist FALSCH/ein Platzhalter.
Korrekte Adressen starten bei .0 (z.B. E 0.0, E 0.1, ..., E 0.7).

### 6 Modultyp-Varianten (Zellen-Merges im Excel)
| Variante | Col 0+1 | Zeilen/Adresse | Kanaele | Bytes | Beschreibung |
|----------|---------|---------------|---------|-------|--------------|
| 32 DI/DQ | getrennt | 1 | 32 | 4 | 8 CH links + 8 CH rechts pro Haelfte, M/L+ Zeilen |
| 16 DI/DQ | gemergt | 1 | 16 | 2 | 1 breite Spalte, 1 Zeile pro Kanal |
| 16 DI 230V | getrennt | 2 | 16 | 2 | 2 Spalten, 2 Zeilen pro Kanal-Paar (Schutzleiter) |
| 8 DQ 230V | getrennt | 2 | 8 | 1 | 2 Spalten, 2 Zeilen + Versorgungszellen |
| 8 AI/AQ | getrennt | 4 | 8 | 16 | 2 Spalten, 4 Zeilen pro Analogkanal (2 Worte/Kanal) |
| 4 AQ | gemergt | 4 | 4 | 8 | 1 breite Spalte, 4 Zeilen pro Analogkanal |

Jede Variante existiert horizontal (0 Grad) und vertikal (90 Grad) = 12 Formate.

### Aktueller Stand der Implementierung (TODO)
- [ ] Architektur-Fix: 5 Module pro Seite (statt 10), Band 1+2 = ein Modul
- [ ] Klemmenbelegung pro Modultyp: Zeilen-Mapping mit M/L+/leer Positionen
- [ ] Adress-Generator: 0-basierte Kanaele, korrekte Byte-Verteilung auf Haelften
- [ ] Weitere Modultypen: Klemmenbelegung aus Siemens-Datenblatt recherchieren
- [ ] Exakte Masse: Beschriftungsboegen mit Stahllineal nachmessen

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
