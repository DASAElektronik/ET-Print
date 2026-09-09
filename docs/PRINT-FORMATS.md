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
- **2 Baender x 5 Spalten = 10 Streifen-Positionen pro A4** (35mm); jede Position ist
  EIN eigener Beschriftungsstreifen/Modul (Band 1 mit hohem Header 25,7 mm, Band 2 mit
  flachem Header 20,6 mm). 25mm-Bogen: 2 Baender x 10 Spalten = 20 Positionen.
- Historie: bis v2.5 wurden Band 1+2 derselben Spalte als EIN Modul behandelt (5 Module
  pro Seite). Seit v3.0 (Phase 19) gilt die 10-Positionen-Architektur; Projektdateien
  v4 werden beim Laden auf 10 Positionen aufgefuellt.

### Standard-Seitenraender (aus Siemens-Doku)
| Rand   | Wert   |
|--------|--------|
| Oben   | 14.0 mm |
| Links  | 25.0 mm |
| Unten  | 19.0 mm |
| Rechts | 12.0 mm |

### Modulstruktur (4 Spalten pro Modul)
| Spalte | Excel-Breite (pt / mm) | Anteil | Inhalt |
|--------|------------------------|--------|--------|
| Col 0  | 31.5 / 11.11           | 32.8% (1499/4570) | Klemmen links (linke Modulseite) |
| Col 1  | 31.5 / 11.11           | 32.8% (1499/4570) | Klemmen rechts (rechte Modulseite) |
| Col 2  | 17.1 / 6.03            | 17.6% (804/4570)  | Net Address (Zeilen 1-10) + Net Name (11-20), 90 Grad |
| Col 3  | 16.2 / 5.71            | 16.8% (768/4570)  | CPU-Name (90 Grad, merged 20 Zeilen) |

Streifenbreite laut Excel: 96.3pt = **33.97mm** (App rechnet aktuell (210-25-12)/5 = 34.6mm).
Die PageSetup-Raender des Excel (L:35 R:22 O:25 U:20) sind unbrauchbar — der Inhalt
(169.9 x 269.7mm) passt damit nicht auf A4; vermutlich setzt das Druck-Makro eigene Werte.
Endgueltige Breite/Pitch bei Lieferung der Boegen per Stahllineal verifizieren.

### Zeilenhoehen (aus Excel_Template_S71500_ET200MP.xls, Sheet horizontal_32_DI_DQ)
| Zeile  | Punkte | ~mm  | Inhalt (Klemme links / rechts, DI 32) |
|--------|--------|------|----------------------------------------|
| 0      | 72.8   | 25.7 | Modul-Header (Device/Module/Slot) |
| 1-8    | 15.8   | 5.6  | K1-8 / K21-28: Kanalgruppe a bzw. c |
| 9-10   | 15.8   | 5.6  | K9-10 / K29-30: unbelegt (leer) |
| 11-18  | 15.8   | 5.6  | K11-18 / K31-38: Kanalgruppe b bzw. d |
| 19     | 15.8   | 5.6  | K19 / K39: L+ (Power) |
| 20     | 15.8   | 5.6  | K20 / K40: M (Ground) |
| 21     | 58.5   | 20.6 | Header des ZWEITEN Bandes (eigener Streifen, eigenes Modul) |
| 22-41  | 15.8   | 5.6  | Band 2: gleiche Struktur wie Zeilen 1-20 |

Hinweis: Jede 5. Klemmenzeile (5, 10, 15, 20 innerhalb eines Bandes) ist im Excel 16.5
Punkte (~5.8mm) statt 15.8 hoch. Die App rastert ALLE 20 Zeilen mit festen 5,6 mm
(SheetGeometry.DataRowHeight) — die Abweichung von 0,2 mm je 5. Zeile bleibt bis zur
Stahllineal-Vermessung der Boegen offen (ABSCHLUSSPLAN AP8). Im Excel hat jedes Band
eigene Header-, Net-Address-, Net-Name- und CPU-Name-Bloecke (Net Address = Zeilen 1-10,
Net Name = Zeilen 11-20).

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
  9    | (leer)              29   | (leer)
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
 18    | CH15 (DI b.7)      38   | CH31 (DI d.7)
 19    | 1L+  (Power)        39   | 2L+  (Power)
 20    | 1M   (Ground)       40   | 2M   (Ground)
```

> Verifiziert am Blockdiagramm (Figure 3-1) des Equipment Manuals A5E03485935-AH (05/2022):
> K9/K10/K29/K30 sind unbelegt; Versorgung liegt auf K19/K20 (Gruppe 1) und K39/K40 (Gruppe 2).
> Potentialbruecken verbinden K19-K39 (xL+) und K20-K40 (xM).

#### 16-Kanal-Module (Vorlage 16_DI_DQ) — Klemmenbelegung je Modultyp

Alle 16er-Typen: K1-8 = CH0-7, K11-18 = CH8-15, rechte Seite (K21-40) unbelegt.
Die Struktur-Klemmen unterscheiden sich aber je Modultyp (verifiziert an den Equipment Manuals):

| Klemme | DI 16 BA (-1BH10) | DI 16 HF (-1BH00) | DQ 16 ST (6ES7522-1BH00) |
|--------|-------------------|-------------------|---------------------------|
| 9      | (leer)            | (leer)            | 1L+ |
| 10     | (leer)            | (leer)            | 1M  |
| 19     | (leer)            | L+                | 2L+ |
| 20     | M                 | M                 | 2M  |

Seit v3.0 uebernimmt der **Modul-Katalog** (MpModuleCatalog) die exakten Labels je
konkretem Modul.

**Ohne Katalog-Auswahl** richtet sich die Belegung nach dem Modultyp des Adress-
Generators (`MpModule.IoType`, seit 2026-08-04):

| Variante | Modultyp DI (und AI/AO) | Modultyp DO |
|----------|------------------------|-------------|
| DI_DQ_16 | K19/K20 = L+/M (wie DI 16 HF) | K9/K10 = 1L+/1M, K19/K20 = 2L+/2M (wie DQ 16 ST) |
| DI_DQ_32 | K19/K20 = 1L+/1M, K39/K40 = 2L+/2M (wie DI 32 HF) | K9/K10 = 1L+/1M, K19/K20 = 2L+/2M, K29/K30 = 3L+/3M, K39/K40 = 4L+/4M (wie DQ 32 HF) |

Alle anderen Varianten haben keine typabhaengigen Struktur-Labels. Ein gesetzter
Katalog-Artikel gewinnt immer — dessen Belegung stammt aus dem Datenblatt.

#### DQ 32x24VDC/0.5A HF (6ES7522-1BL01-0AB0) — verifiziert (109480716)

Anders als DI 32 sind hier K9/K10 und K19/K20 belegt (Potentialbruecken 9-29/10-30/
19-39/20-40, 24V DC auf 19/20):

| Klemme | links | rechts |
|--------|-------|--------|
| 9/29   | 1L+   | 3L+ |
| 10/30  | 1M    | 3M  |
| 19/39  | 2L+   | 4L+ |
| 20/40  | 2M    | 4M  |

#### DQ 8x24VDC/2A HF (6ES7522-1BF00-0AB0) — verifiziert (59193089, Figure 3-1)

Nutzt dieselbe 40-Klemmen-Frontbaugruppe wie die 32-Kanal-Module, belegt davon aber
nur die ersten zehn Klemmen. Zwei Gruppen zu je vier Kanaelen:

| Klemme | Belegung |
|--------|----------|
| 1-8    | CH0-CH7 (Gruppe 1 = CH0-3, Gruppe 2 = CH4-7) |
| 9/10   | 1L+ / 1M (24 V DC) |
| 11-18  | unbelegt |
| 19/20  | 2L+ / 2M (24 V DC) |
| 21-40  | unbelegt (komplette rechte Reihe) |

Der Katalog-Eintrag sperrt die unbelegten Klemmen (`IsEditable=false`). Dadurch findet
der Adress-Generator nur acht editierbare Zellen und fuellt genau **ein** Byte statt
der vier des 32-Kanal-Rasters.

#### DI 16x230VAC BA (6ES7521-1FH00-0AA0) — verifiziert (59193398, Figure 3-1)

Vier Gruppen zu je vier Kanaelen. Die Kanaele liegen auf den **ungeraden** Klemmen,
die gemeinsame AC-Versorgung xN jeweils auf der letzten Klemme der Gruppe:

| Gruppe | Kanaele | Klemmen | xN |
|--------|---------|---------|-----|
| a oben | CH0-CH3 | 1, 3, 5, 7 | 8 |
| a unten | CH4-CH7 | 11, 13, 15, 17 | 18 |
| b oben | CH8-CH11 | 21, 23, 25, 27 | 28 |
| b unten | CH12-CH15 | 31, 33, 35, 37 | 38 |

K9/K10, K19/K20 (und K29/K30, K39/K40) sind unbelegt — das bestaetigt die leeren
colspan-2-Bloecke des Excel-Templates. xN teilt sich mit dem letzten Kanal der Gruppe
einen 2-Zeilen-Block, deshalb traegt die App dort **kein** Struktur-Label ein.

#### 8x230VAC/5A ST Relay (6ES7522-5HF00-0AB0) — verifiziert (A5E03485590-AD)

Relais-Modul: jeder Kanal = 2 Klemmen (Kontakt). Links CH0-3 = K1-2/K4-5/K11-12/K14-15,
rechts CH4-7 = K21-22/K24-25/K31-32/K35-36. Versorgung 24V DC auf K19/20 (L+/M) und
K39/40. Die Bloecke zwischen den Kanaelen sind Luecken; das Excel-Template laesst sie
(wie alle 230V-Struktur-Bloecke) LEER — daher keine App-Labels.

#### AI 8xU/I/RTD/TC ST + AQ 4xU/I ST — modusabhaengig (KEINE feste Belegung)

Die Analog-Manuals (A5E...KF / A5E03484696-AC) zeigen pro Verdrahtungsmodus
(U/I/RTD/TC bzw. Spannungs-/Stromausgang) ein EIGENES Diagramm — es gibt keine feste
Klemmen-Kanal-Zuordnung. Das Excel-Template bietet deshalb **5 generische 4-Zeilen-
Bloecke pro Spalte** (kein hartkodiertes MANA). Der Generator verteilt GenCount Kanaele
spaltenausgeglichen (AI 8: CH0-3 links, CH4-7 rechts), der 5. Block bleibt fuer MANA/
Reserve frei.

#### AQ 2xU/I ST (6ES7532-5NB00-0AB0, 25mm) — verifiziert (91688388, Figure 3-1/3-2)

Gleiche Modusabhaengigkeit wie die 35mm-Analogmodule: Spannungsausgang 2-Draht belegt
QV auf K1 und MANA auf K3, 4-Draht zusaetzlich S+/S- auf K5/K6, Stromausgang QI auf K1
bzw. K5. Belegt sind nur K1-K7, die Versorgung liegt am Einspeiseelement (K41 = L+,
K43 = M) und damit ausserhalb des Beschriftungsstreifens. Der Katalog-Eintrag laesst
den Streifen deshalb komplett editierbar (Variante MP25_16, 20 Zeilen).

**Kanalgruppen und Byte-Zuordnung:**
- Gruppe a: CH0-CH7   = Byte 0 (E x.0 bis E x.7) — Klemmen 1-8 links
- Gruppe b: CH8-CH15  = Byte 1 (E x+1.0 bis E x+1.7) — Klemmen 11-18 links (= untere Haelfte)
- Gruppe c: CH16-CH23 = Byte 2 (E x+2.0 bis E x+2.7) — Klemmen 21-28 rechts
- Gruppe d: CH24-CH31 = Byte 3 (E x+3.0 bis E x+3.7) — Klemmen 31-38 rechts (= untere Haelfte)

**Wichtig:** Die Kanalnummerierung ist 0-basiert (CH0 = E x.0, NICHT E x.1)!
Das Excel-Template zeigt Q 0.1 bis Q 0.7 — das ist FALSCH/ein Platzhalter.
Korrekte Adressen starten bei .0 (z.B. E 0.0, E 0.1, ..., E 0.7).

### 9 Layout-Varianten (MpModuleVariant, Zellen-Merges im Excel)
| Variante (Enum) | Col 0+1 | Zeilen/Adresse | Kanaele | Bytes | Beschreibung |
|----------|---------|---------------|---------|-------|--------------|
| DI_DQ_32 | getrennt | 1 | 32 | 4 | 8 CH links + 8 CH rechts pro Haelfte, M/L+ Zeilen (typabhaengig) |
| DI_DQ_16 | gemergt | 1 | 16 | 2 | 1 breite Spalte, 1 Zeile pro Kanal |
| DI_230V_16 | getrennt | 2 | 16 | 2 | 2 Spalten, 2 Zeilen pro Kanal-Paar (xN = AC-Neutral auf K8/K18) |
| DQ_230V_8 | getrennt | 2 | 8 | 1 | 2 Spalten, 2 Zeilen (Relaiskontakte) + Luecken-Bloecke |
| AI_AQ_8 | getrennt | 4 | 8 | 16 | 2 Spalten, 5 editierbare 4-Zeilen-Bloecke je Spalte (modusabhaengig, kein MANA-Label) |
| AQ_4 | gemergt | 4 | 4 | 8 | 5 gemergte 4-Zeilen-Bloecke |
| SIWAREX_WP52x | getrennt | 1 | - | - | fester Pinout (EXC/SIG/SEN/D +/-, DQ, DI, L+/M), alle 40 Zellen fest, wird als reiner Pinout-Streifen gedruckt |
| MP25_16 | gemergt | 1 | 16 | 2 | 25mm: 20 editierbare Zeilen, keine Strukturzeilen |
| MP25_32 | getrennt | 1 | 32 | 4 | 25mm: 20 Zeilen x 2 Spalten, alle editierbar |

Die Variante ist eine Eigenschaft des Moduls (nicht des Formats): es gibt nur die Formate
MP_Horizontal / MP_Vertical (35mm) und MP25_Horizontal / MP25_Vertical (25mm); vertikal
dreht die editierbaren Adresszellen um 90 Grad. Struktur-Klemmen (M/L+) kommen bei
Katalog-Artikeln aus dem Katalog, sonst typabhaengig (DI: Versorgung nur am Gruppenende,
DO: je Kanalgruppe) aus MpModuleLayoutFactory.GetGenericDefinitions.

### Modul-Katalog (MpModuleCatalog.cs, datenblattverifiziert) und weitere Modultypen

Im Katalog (Auswahl per Artikelnummer, exakte Klemmenbelegung) sind 14 Eintraege:
35mm: DI 32x24VDC HF, DQ 32x24VDC/0.5A HF, DI 16x24VDC HF, DQ 16x24VDC/0.5A ST,
DQ 8x24VDC/2A HF, DI 16x230VAC BA, SIWAREX WP521 ST, SIWAREX WP522 ST.
25mm (BA-Module, Breite laut TED-Datenblatt 25 mm): DI 16x24VDC BA, DQ 16x24VDC/0.5A BA,
DI 32x24VDC BA, DQ 32x24VDC/0.5A BA, DI 16x24VDC/DQ 16x24VDC/0.5A BA, AQ 2xU/I ST.
Alle uebrigen Module der Tabellen unten werden ueber die Layout-Variante beschriftet
(Analogmodule haben keine feste Klemmen-Kanal-Zuordnung, siehe ABSCHLUSSPLAN AP9).

#### Digital Input (DI) — 6ES7521
| Modul | Artikel-Nr | Kanäle | Breite | Excel-Mappe |
|---|---|---|---|---|
| DI 16x24VDC HF | 6ES7521-1BH00-0AB0 | 16 | 35mm | horizontal/vertical_16_DI_DQ (Katalog) |
| DI 16x24VDC BA | 6ES7521-1BH10-0AA0 | 16 | **25mm** | 25mm-Template 16_DI (Katalog, K20 = M) |
| DI 32x24VDC HF | 6ES7521-1BL00-0AB0 | 32 | 35mm | horizontal/vertical_32_DI_DQ (Katalog) |
| DI 32x24VDC BA | 6ES7521-1BL10-0AA0 | 32 | **25mm** | 25mm-Template 32_DI (Katalog, K20/K40 = M) |
| DI 16x230VAC BA | 6ES7521-1FH00-0AA0 | 16 | 35mm | horizontal/vertical_16_DI_230V (Katalog) |

#### Digital Output (DQ) — 6ES7522
| Modul | Artikel-Nr | Kanäle | Breite | Excel-Mappe |
|---|---|---|---|---|
| DQ 8x24VDC/2A HF | 6ES7522-1BF00-0AB0 | 8 | 35mm | horizontal/vertical_32_DI_DQ (*) — Katalog sperrt K11-40 |
| DQ 16x24VDC/0.5A ST | 6ES7522-1BH00-0AB0 | 16 | 35mm | horizontal/vertical_16_DI_DQ (Katalog) |
| DQ 16x24VDC/0.5A BA | 6ES7522-1BH10-0AA0 | 16 | **25mm** | 25mm-Template 16_DQ (Katalog, 1L+/1M, 2L+/2M) |
| DQ 32x24VDC/0.5A HF | 6ES7522-1BL01-0AB0 | 32 | 35mm | horizontal/vertical_32_DI_DQ (Katalog) |
| DQ 32x24VDC/0.5A BA | 6ES7522-1BL10-0AA0 | 32 | **25mm** | 25mm-Template 32_DQ (Katalog, 4 Versorgungsgruppen) |
| DI 16x24VDC / DQ 16x24VDC/0.5A BA | 6ES7523-1BL00-0AA0 | 16+16 | **25mm** | 25mm-Template 16_DI_16_DQ (Katalog, links E / rechts A) |
| DQ 8x230VAC/5A | 6ES7522-5HF00-0AB0 | 8 | 35mm | horizontal/vertical_8_DQ_230V |

#### Analog Input (AI) — 6ES7531
| Modul | Artikel-Nr | Kanäle | Breite | Excel-Mappe |
|---|---|---|---|---|
| AI 8xU/I/RTD/TC ST | 6ES7531-7KF00-0AB0 | 8 | 35mm | horizontal/vertical_8_AI_AQ |
| AI 8xU/I HF | 6ES7531-7NF00-0AB0 | 8 | 35mm | horizontal/vertical_8_AI_AQ |
| AI 8xU/I/R/RTD BA | 6ES7531-7QF00-0AB0 | 8 | 35mm | horizontal/vertical_8_AI_AQ |

#### Analog Output (AQ) — 6ES7532
| Modul | Artikel-Nr | Kanäle | Breite | Excel-Mappe |
|---|---|---|---|---|
| AQ 4xU/I ST | 6ES7532-5HD00-0AB0 | 4 | 35mm | horizontal/vertical_4_AQ |
| AQ 2xU/I ST | 6ES7532-5NB00-0AB0 | 2 | 25mm | ET200MP_25mm Template |

#### SIWAREX Waegemodule — 7MH4980
| Modul | Artikel-Nr | Kanäle | Breite | Excel-Mappe |
|---|---|---|---|---|
| WP521 ST (1-Kanal) | 7MH4980-1AA01 | 1 WZ + 3DI + 4DQ | 35mm | eigene Klemmenbelegung |
| WP522 ST (2-Kanal) | 7MH4980-2AA01 | 2 WZ + 3DI + 4DQ | 35mm | eigene Klemmenbelegung |

(*) DQ 8x24VDC nutzt das 32_DI_DQ Layout, da gleiche 40-Klemmen-Frontbaugruppe

#### Datenblatt-Quellen fuer Klemmenbelegungen
- DI 32x24VDC HF: https://cache.industry.siemens.com/dl/files/896/59192896/att_897449/v1/s71500_di_32x24vdc_hf_manual_en-US_en-US.pdf
- DI 16x24VDC BA: https://support.industry.siemens.com/cs/attachments/83501190/s71500_di_16x24vdc_ba_manual_en-US_en-US.pdf
- DQ 32x24VDC HF: https://cache.industry.siemens.com/dl/files/716/109480716/att_902641/v1/s71500_dq_32x24vdc_0_5a_hf_manual_en-US_en-US.pdf
- DQ 8x24VDC/2A HF: https://cache.industry.siemens.com/dl/files/089/59193089/att_902698/v1/s71500_dq_8x24vdc_2a_hf_manual_en-US_en-US.pdf
- DI 16x230VAC BA: https://cache.industry.siemens.com/dl/files/398/59193398/att_897452/v1/s71500_di_16x230vac_ba_manual_en-US_en-US.pdf
- DI 32x24VDC BA (25mm): https://cache.industry.siemens.com/dl/files/481/83499481/att_905045/v1/s71500_di_32x24vdc_ba_manual_en-US_en-US.pdf
- DQ 32x24VDC/0.5A BA (25mm): https://cache.industry.siemens.com/dl/files/404/83500404/att_902696/v1/s71500_dq_32x24vdc_0_5a_ba_manual_en-US_en-US.pdf
- DQ 16x24VDC/0.5A BA (25mm): https://cache.industry.siemens.com/dl/files/415/83500415/att_902638/v1/s71500_dq_16x24vdc_0_5a_ba_manual_en-US_en-US.pdf
- DI 16x24VDC/DQ 16x24VDC/0.5A BA (25mm): https://cache.industry.siemens.com/dl/files/523/83501523/att_897447/v1/s71500_di_16x24vdc_dq_16x24vdc_0.5a_ba_manual_en-US_en-US.pdf
- Modulbreiten: TED-Datenblaetter https://apim.industry.siemens.cloud/ted/datasheet?format=pdf&mlfbs=<MLFB>&language=en
- AQ 2xU/I ST (25mm): https://cache.industry.siemens.com/dl/files/388/91688388/att_75101/v1/s71500_aq_2xu_i_st_manual_en-US_en-US.pdf
- AI 8xU/I/RTD/TC ST: https://cache.industry.siemens.com/dl/files/205/59193205/att_112065/v1/s71500_ai_8xu_i_rtd_tc_st_manual_en-US_en-US.pdf
- AQ 4xU/I ST: https://cache.industry.siemens.com/dl/files/850/59191850/att_63218/v1/s71500_aq_4xu_i_st_manual_en-US_en-US.pdf
- SIWAREX WP521/522: https://support.industry.siemens.com/cs/attachments/109736583/Manual_SIWAREX_WP521_WP522_en_en-US.pdf

### Stand der Implementierung (2026-09)
- [x] 10 Streifen-Positionen pro A4 (v3.0), 25mm mit 20 Positionen
- [x] Klemmenbelegung pro Modul: Katalog (MpModuleCatalog) + typabhaengige generische Belegung
- [x] Adress-Generator: 0-basierte Kanaele, Byte 0 -> K1-8, Byte 1 -> K11-18, Byte 2 -> K21-28, Byte 3 -> K31-38
- [x] Datenblaetter fuer alle Katalog-Eintraege verifiziert (Quellen oben)
- [x] Geometrie zentral in SheetGeometry (Druck = Vorschau = Kalibrierseite)
- [ ] Exakte Masse: Beschriftungsboegen 6ES7592-1AX00 / -2AX00 mit Stahllineal nachmessen (AP8)
- [x] 25mm-Module (AP8, 2026-09-09): Blockdiagramme DI 32 BA (83499481), DQ 32 BA (83500404),
      DQ 16 BA (83500415), DI16/DQ16 BA (83501523) ausgewertet — 40-Klemmen-Struktur wie 35mm,
      Zellenreihenfolge Spalte 0 komplett, dann Spalte 1 (Byte 2 -> K21-28). Die generischen
      Layouts MP25_16/MP25_32 haben deshalb Strukturzeilen K9/K10/K19/K20 (unbeschriftet);
      Analogmodule (AQ 2) nutzen weiterhin 20 freie Zeilen.

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
