# ET-Printer – Feature-Spezifikation

Stand: 2026-09-09 (v3.1). Beschreibt das implementierte Verhalten; die Geometrie steht
in `PRINT-FORMATS.md` und `ARCHITECTURE.md`.

## F01: Produktfamilien und Formate

Drei Produktfamilien, zehn Druckformate (`LabelFormat`). Die ComboBox „Druckformat“
zeigt nur die Formate der gewählten Familie.

| Familie | Beschriftungsbogen | Formate | Positionen je A4 |
|---------|--------------------|---------|------------------|
| ET 200SP (12,8 × 31 mm) | 6ES7193-6LA10-0AA0 | Horizontal zweizeilig + Kopfzeile, Horizontal zweizeilig, Horizontal einzeilig, Vertikal zweizeilig + Kopfzeile, Vertikal zweizeilig, Vertikal einzeilig | 5 × 20 = 100 Etiketten |
| S7-1500 / ET 200MP (35 mm) | 6ES7592-1AX00-0AA0 | ET200MP Horizontal, ET200MP Vertikal | 2 Bänder × 5 Spalten = 10 Streifen |
| ET 200MP 25 mm | 6ES7592-2AX00-0AA0 | ET200MP 25mm Horizontal, ET200MP 25mm Vertikal | 2 Bänder × 10 Spalten = 20 Streifen |

- „+Kopfzeile“ = schmale Kopfspalte (20 %) mit 90° gedrehtem Modulnamen.
- Einzeilige SP-Formate drucken nur Zeile 1; der Generator merged die Adressen dort
  kanal-aufsteigend in Zeile 1.
- Vertikale Formate: jede Adresse in einem eigenen Slot (8 je Reihe), 90° gedreht.
- Format- oder Familienwechsel bei befülltem Inhalt fragt nach (Inhalt wird verworfen);
  Abbruch setzt die ComboBox zurück. Familienwechsel setzt die Ränder auf die
  Familien-Defaults (Schrift bleibt) und wählt das Standardformat der Familie.
- Wechsel in ein MP-Format aktiviert den Tab „MP Modul“, zurück den „Adress-Generator“.

## F02: Hauptfenster und Workflow

Zwei Spalten mit GridSplitter: links Eingabe, rechts A4-Vorschau. Menü (Datei, Bearbeiten,
Hilfe), Toolbar (Neu, Öffnen, Speichern, Rückgängig, Wiederholen, Kopieren, Einfügen, Drucken), Statusleiste
(Meldung, „Etikett 3 / 100“ bzw. „Modul 2 / 10“, „Seite 1 / 2“, Format, Bogen-Artikelnummer).
Der Fenstertitel zeigt Dateiname bzw. Format und `*` bei ungespeicherten Änderungen.

Workflow:
1. Produktfamilie und Druckformat wählen.
2. Etikett bzw. Modul in der Vorschau anklicken.
3. Adressen generieren (Tab „Adress-Generator“), manuell eingeben (SP: Tab „Manuell“)
   oder im Modul-Editor pflegen (MP: Tab „MP Modul“).
4. Übertragen – die Auswahl springt automatisch weiter.
5. Drucken (Ctrl+P / F5).

Die Eingabe-Tabs sind nur aktiv, wenn ein Etikett oder Modul ausgewählt ist.

## F03: Adress-Generator

Eingaben: Modulname (= Kopfzeile), Modultyp, Start-Byte (≥ 0), Anzahl, „Startadresse
automatisch weiterschalten“, Live-Vorschau der beiden Zeilen.

| Modultyp | Präfix | Anzahl-Auswahl | Auto-Advance |
|----------|--------|----------------|--------------|
| DI – Digital Input | E | 1 / 2 / 4 Bytes | + belegte Bytes |
| DO – Digital Output | A | 1 / 2 / 4 Bytes | + belegte Bytes |
| AI – Analog Input | EW | 2 / 4 / 8 Kanäle | + Kanäle × 2 |
| AO – Analog Output | AW | 2 / 4 / 8 Kanäle | + Kanäle × 2 |

Beim Typwechsel wird eine ungültige Anzahl auf den ersten gültigen Wert gesetzt.

**ET 200SP** (`AddressGenerator`):
- Festes Raster von 8 Slots je Reihe (Klemmen der BaseUnit A0/A1). Digital: Byte 0 belegt
  Slot 0–3, Byte 1 Slot 4–7; ungerade Bits oben (Zeile 1), gerade Bits unten (Zeile 2).
  Analog: ungerade Kanäle oben, gerade unten, Wortadressen `EW 0, EW 2, …`.
- Maximal 2 Bytes bzw. 16 Kanäle je Etikett; Auto-Advance schaltet nur um die
  tatsächlich aufs Etikett gepasste Anzahl weiter (`GetEffectiveCount`).
- Nach „Generieren + Übertragen“ springt die Auswahl zum nächsten Etikett, am Seitenende
  zur nächsten Seite, auf der letzten Seite wird eine neue angelegt.
- „Ändern“ neben dem Modulnamen ersetzt nur die Kopfzeile des gewählten Etiketts.

**ET 200MP** (`MainViewModel.FillMpModuleAddresses`):
- Header = Modulname; der Modultyp wird bei benutzerdefinierten Modulen als `IoType`
  übernommen (bestimmt die Versorgungslabels), Katalog-Artikel behalten ihre Belegung.
- Digital (35 mm und 25 mm): Bytezahl = min(Anzahl-Auswahl, editierbare Zellen / 8)
  (DI_DQ_16/MP25_16 → max. 2 Bytes, DI_DQ_32/MP25_32 → 4, DQ 8x2A → 1). Adressen
  `E x.0 … E x.7` sequenziell auf die editierbaren Zellen in Layout-Reihenfolge
  (spaltenweise: links Byte 0/1, rechts Byte 2/3). Die Katalogwahl setzt die Anzahl auf
  die volle Bytezahl des Moduls.
- Gemischtes DI16/DQ16 BA (25 mm): linke Spalte `E`, rechte Spalte `A`, Bytezählung
  beginnt rechts wieder beim Start-Byte.
- Analog: GenCount Kanäle spaltenausgeglichen (AI 8 → 4 links, 4 rechts), Restblöcke leer.
- Danach springt die Auswahl zum nächsten Modul bzw. zur nächsten Seite; am Ende der
  letzten Seite wird **keine** Seite automatisch angelegt.

## F04: Manuelle Eingabe (nur ET 200SP)

Tab „Manuell“ mit Kopfzeile (nur bei „+Kopfzeile“), Zeile 1 und Zeile 2 (nur zweizeilig).
Enter in einem der Felder oder „Übertragen (nächstes Etikett)“ schreibt die Felder samt
Schrift-Einstellungen ins gewählte Etikett und schaltet weiter. Ein Klick auf ein Etikett
lädt dessen Texte und Schrift in die Felder und fokussiert Zeile 1. Im MP-Modus ist der
Tab ausgeblendet.

## F05: Modul-Editor (ET 200MP)

Tab „MP Modul“ für das ausgewählte Modul („Modul 3 / 10 (Spalte 3, Band 1)“):
- **Siemens-Modul**: Katalog-ComboBox (F06). Die Wahl setzt Variante, Klemmenbelegung,
  `IoType` und den Generator-Modultyp. „Benutzerdefiniert“ nutzt nur die Variante.
- **Modulvariante (Layout)**: manuelle Wahl setzt den Artikel auf „Benutzerdefiniert“;
  Zelltexte werden soweit möglich migriert, Texte in gesperrten Strukturzellen geleert.
- **Header (Device / Module / Slot)**: mehrzeilig, Zeilenumbrüche bleiben in Vorschau und
  Druck, lange Zeilen werden umbrochen, bei Platzmangel verkleinert.
- **Netzadresse (Block 1)** / **Netzname (Block 2)**: 90° gedreht in der Netzadress-Spalte
  (nur 35 mm). **CPU-Name**: 90° gedreht über die volle Datenhöhe.
- **Modul drucken**: Checkbox für das Druckflag; abgewählte Module sind in der Vorschau
  ausgegraut (Opacity 0,4).
- **Ausgewählte Adresszelle**: Text der per Klick gewählten editierbaren Zelle. Die
  Zellauswahl wird beim Modulwechsel zurückgesetzt und nach Zell-Neuaufbau per
  Zellindex wieder aufgelöst. Strukturzellen (M, L+, …) sind nicht editierbar.
- Klick auf den Modul-Header selektiert das Modul (wichtig für Band 2).

## F06: Modul-Katalog (`MpModuleCatalog`)

Konkrete Siemens-Module mit datenblattverifizierter Klemmenbelegung. Die Liste ist nach
Familie gefiltert (35 mm / 25 mm); „Benutzerdefiniert (nur Layout-Variante)“ ist immer dabei.

| Artikelnummer | Modul | Typ | Variante | Struktur-Labels |
|---------------|-------|-----|----------|-----------------|
| 6ES7521-1BL00-0AB0 | DI 32x24VDC HF | DI | DI_DQ_32 | K19/K20 = 1L+/1M, K39/K40 = 2L+/2M |
| 6ES7522-1BL01-0AB0 | DQ 32x24VDC/0.5A HF | DO | DI_DQ_32 | K9/10 = 1L+/1M, K19/20 = 2L+/2M, K29/30 = 3L+/3M, K39/40 = 4L+/4M |
| 6ES7521-1BH00-0AB0 | DI 16x24VDC HF | DI | DI_DQ_16 | K19/K20 = L+/M |
| 6ES7522-1BH00-0AB0 | DQ 16x24VDC/0.5A ST | DO | DI_DQ_16 | K9/10 = 1L+/1M, K19/20 = 2L+/2M |
| 6ES7522-1BF00-0AB0 | DQ 8x24VDC/2A HF | DO | DI_DQ_32 | K1–K8 Kanäle, K9/10 = 1L+/1M, K19/20 = 2L+/2M, Rest gesperrt |
| 6ES7521-1FH00-0AA0 | DI 16x230VAC BA | DI | DI_230V_16 | Strukturblöcke leer |
| 6ES7531-7KF00-0AB0 | AI 8xU/I/RTD/TC ST | AI | AI_AQ_8 | keine (modusabhängig, komplett editierbar) |
| 6ES7531-7NF00-0AB0 | AI 8xU/I HF | AI | AI_AQ_8 | keine (komplett editierbar) |
| 6ES7531-7QF00-0AB0 | AI 8xU/I/R/RTD BA | AI | AI_AQ_8 | keine (komplett editierbar) |
| 6ES7532-5HD00-0AB0 | AQ 4xU/I ST | AO | AQ_4 | keine (komplett editierbar) |
| 7MH4980-1AA01 | SIWAREX WP521 ST (1 Kanal) | DI | SIWAREX_WP52x | fester Pinout, alle Zellen gesperrt |
| 7MH4980-2AA01 | SIWAREX WP522 ST (2 Kanal) | DI | SIWAREX_WP52x | fester Pinout, alle Zellen gesperrt |
| **25 mm** | | | | |
| 6ES7521-1BH10-0AA0 | DI 16x24VDC BA | DI | MP25_16 | K20 = M |
| 6ES7522-1BH10-0AA0 | DQ 16x24VDC/0.5A BA | DO | MP25_16 | K9/10 = 1L+/1M, K19/20 = 2L+/2M |
| 6ES7521-1BL10-0AA0 | DI 32x24VDC BA | DI | MP25_32 | K20 = M, K40 = M |
| 6ES7522-1BL10-0AA0 | DQ 32x24VDC/0.5A BA | DO | MP25_32 | 1L+/1M … 4L+/4M je Kanalgruppe |
| 6ES7523-1BL00-0AA0 | DI 16x24VDC / DQ 16x24VDC/0.5A BA | DI | MP25_32 | links Eingänge (K20 = 1M), rechts Ausgänge (K29/30 = 2L+/2M, K39/40 = 3L+/3M); Generator setzt rechts das A-Präfix |
| 6ES7532-5NB00-0AB0 | AQ 2xU/I ST | AO | MP25_16 | keine (20 freie Zeilen) |

18 Einträge. Die BA-Digitalmodule sind laut Siemens-Datenblatt 25 mm breit und gehören auf
den 25-mm-Bogen; ein Artikel, der nicht zur Familie passt, wird beim Laden abgewählt.

Varianten (Layouts): DI_DQ_32, DI_DQ_16, DI_230V_16, DQ_230V_8, AI_AQ_8, AQ_4,
SIWAREX_WP52x, MP25_16, MP25_32. MP25_16/MP25_32 haben dieselbe 40-Klemmen-Struktur wie
DI_DQ_16/DI_DQ_32 (K9/K10 und K19/K20 sind Strukturzeilen). Ohne Katalogartikel hängen die
Versorgungslabels dieser vier Varianten am Modultyp (DO: Versorgung je Kanalgruppe).
Die Katalogwahl setzt die Generator-Anzahl auf die volle Bytezahl des Moduls.

## F07: Einstellungen

**Panel „Seite (gilt für alle Etiketten)“** – wirkt sofort auf die ganze Seite:
- Kopfzeilen-Schrift: Größe 4–10 pt, fett (SP-Kopfspalte und MP-Header).
- Seitenränder oben/links/unten/rechts in mm (0–60); „unten“ ist im MP-Modus gesperrt.
- Druckkalibrierung X/Y in mm (±10) und „Testseite drucken (Fadenkreuze)“.
- „Blanko A4 (alle Rahmen drucken)“: Schnittkanten 0,5 pt und innere Rahmen 0,3 pt
  schwarz für Druck auf Normalpapier/Karton.
- „Seite zurücksetzen“: Ränder und Schrift auf die Defaults der Produktfamilie.

**Panel „Schrift: <Auswahl>“** – wirkt live auf das ausgewählte Etikett bzw. Modul:
Schriftart (alle installierten Windows-Schriften, im Hintergrund geladen), Größe 4–10 pt,
fett, kursiv. „Auf alle Etiketten/Module anwenden“ überträgt die Werte auf alle Seiten.
Etikett- oder Modulwechsel lädt dessen Schrift in die Felder.

Werte außerhalb der Bereiche werden begrenzt und in der Statusleiste gemeldet.
Eingaben folgen der Windows-Kultur (Komma auf de-DE).

## F08: A4-Vorschau

- Maßstab 3 px/mm (630 × 891 px), weißes Blatt mit Schatten, Raster füllt exakt den
  Druckbereich, Zeilennummern 20…1 im rechten Rand (je Band einmal).
- Farben SP: leer #F0F0F0, befüllt #D0E8D0, Hover #E0E8F0, ausgewählt #B8D4F0 mit blauem
  Rahmen, markiert (Multi-Selection) #FFF0D0 mit orangem Rahmen, Kopfspalte #D8D8E8.
  MP: Header #D8D8E8, Netzadresse #E8E8F8, CPU #F8F0E8, Strukturzellen #DCDCDC mit
  grauem Text, gewählte Zelle blau umrandet, markiertes Modul orange umrandet.
- Zoom: Slider 0,3–4 (frei, kein Raster), Strg+Mausrad in 0,1-Schritten, Button
  „Ganze Seite“ / Ctrl+0 passt die Seite in den Vorschaubereich; Startzoom aus `ui.json`
  oder „Ganze Seite“.
- Vom Druck ausgeschlossene Etiketten/Module erscheinen ausgegraut.
- Vorschau = Druck: gleiche Geometrie (`SheetGeometry`), gleiche Shrink-to-fit-Logik,
  gleiche Punkt-Umrechnung. Der Kalibrier-Versatz wird nur im Druck angewendet.

## F09: Druck

- „Drucken…“ (Ctrl+P, F5) druckt alle Seiten, „Nur aktuelle Seite drucken…“ die sichtbare.
- Seiten ohne druckbare Etiketten/Module werden übersprungen; ohne druckbaren Inhalt
  erscheint ein Hinweis statt eines Leerauftrags. Status: „Druckauftrag gesendet
  (2 von 3 Seiten, 1 leere übersprungen)“.
- Druckbar = befüllt **und** Druckflag gesetzt; MP-Streifen mit festem Pinout (SIWAREX)
  gelten als befüllt.
- Windows-Druckdialog; danach wird das PrintTicket auf ISO A4 Hochformat gesetzt.
  Abbruch meldet „Druck abgebrochen“.
- Kalibrier-Testseite: Fadenkreuze an der Rasterecke oben links und unten rechts,
  Infotext mit Format, Rändern und Versatz. Auf Normalpapier drucken, mit dem Siemens-Bogen
  gegen Licht vergleichen, X/Y anpassen. Kalibrierung wird nach erfolgreichem Druck in
  `calibration.json` gespeichert.
- Standardmodus druckt nur Text (perforierte Siemens-Bögen); „Blanko A4“ ergänzt Rahmen.

## F10: Druckauswahl

Menü Bearbeiten → Druckauswahl: „Alle drucken“, „Keine drucken“, „Nur befüllte drucken“
(alle Seiten, SP und MP), „Ausgewähltes Etikett/Modul umschalten“. Zusätzlich
Kontextmenü „Druck umschalten“ auf Etikett bzw. MP-Vorschau und Checkbox „Modul drucken“
im MP-Tab. Druckflags werden gespeichert und markieren das Projekt als geändert.

## F11: Kopieren / Einfügen und Multi-Selection

- App-interner Puffer (kein Windows-Clipboard), getrennt für Etiketten und Module;
  Kopieren im anderen Modus leert den vorherigen Puffer.
- Strg+Klick markiert zusätzlich (beim ersten Strg+Klick wird der Anker mitgenommen),
  Shift+Klick markiert den Bereich vom Anker bis zum Ziel, normaler Klick hebt alle
  Markierungen auf. Markierungen sind seitenlokal und werden beim Seitenwechsel gelöscht.
- Strg+C / Toolbar / Kontextmenü kopiert die markierten Elemente, sonst das ausgewählte.
  Strg+V fügt ab dem ausgewählten Element sequenziell ein und stoppt am Seitenende
  („3 von 5 eingefügt (Seitenende erreicht)“). Kopiert wird der komplette Inhalt inkl.
  Schrift und Druckflag, bei Modulen auch Variante, Artikel und Modultyp.
- Hat ein Textfeld den Fokus, gehen Strg+C/V an das Textfeld.

## F12: Speichern / Laden

- Dateiformat `.etprint` (JSON, Version 5): Familie, Format, Einstellungen, alle Seiten,
  MP-Module mit Zellen, Kalibrierung, Blanko-Option, Druckflags.
- Neu (Ctrl+N), Öffnen (Ctrl+O), Speichern (Ctrl+S), Speichern unter (Ctrl+Shift+S),
  „Zuletzt geöffnet“ (max. 10, `recent.json`; fehlende Datei → Rückfrage „Eintrag entfernen?“).
- Öffnen per Drag & Drop einer `.etprint` auf das Fenster oder als Startargument
  (Doppelklick / „Öffnen mit“).
- Speichern ist atomar (`.tmp` + Move), die vorherige Version bleibt als `.bak`.
  Speicherfehler erscheinen als Dialog.
- Alte Versionen v1–v4 werden beim Laden migriert. Ladefehler erscheinen als Dialog,
  der Recent-Eintrag entsteht erst nach erfolgreichem Laden.
- Dirty-Tracking: Schließen, Neu und Öffnen fragen „Speichern? Ja / Nein / Abbrechen“.
  Als Änderung gelten auch Druckflags, Blanko-Option, Ränder und Format-/Familienwechsel.

## F13: Mehrseitige Projekte

Seitennavigation (◀ ▶, Ctrl+Bild auf/ab), „+ Seite“, „− Seite“ (Rückfrage bei Inhalt,
letzte Seite bleibt). SP legt beim Übertragen auf dem letzten Etikett automatisch eine
Seite an; Importe erzeugen Seiten nach Bedarf. Der Druck umfasst alle Seiten.

## F14: Importe

**CSV** (Datei → Importieren, nur SP, ab dem ausgewählten Etikett):
- Trenner `;`, `,` oder Tab (automatisch, Trenner in Anführungszeichen zählen nicht).
- Encoding per BOM (UTF-8, UTF-16 LE/BE), UTF-16-LE-Heuristik ohne BOM, sonst strikt
  UTF-8 mit Windows-1252-Fallback.
- Kopfzeile erkannt an Spaltennamen `Header/Kopfzeile/Kopf`, `Zeile1/Line1/Zeile 1/
  Line 1/Adresse/Adressen`, `Zeile2/Line2/Zeile 2/Line 2`; ohne Kopfzeile Spalten 1–3.
- Anführungszeichen, `""`-Escapes und Zeilenumbrüche im Feld werden korrekt geparst,
  Leerzeilen übersprungen, die letzte Zeile ohne Zeilenende importiert.

**Excel (.xlsx)** (nur SP): erstes sichtbares Blatt mit Inhalt, gleiche Spaltennamen
wie CSV, ohne Kopfzeile die ersten drei belegten Spalten ab `UsedRange`; Zahlen und
Datumswerte im Excel-Anzeigeformat.

**PDF-Schaltplan** (SP und MP): Text wird je Seite zeilenweise gruppiert. Eine Zeile mit
Modultyp (`DI/DO/DQ/AI/AO/AQ` + Kanalzahl, z. B. „DI 32x24VDC HF“) und BMK (`+…`/`=…`)
eröffnet ein Modul; alle folgenden Adressen gehören dazu. Erkannt werden `E/A x.y`,
`EW/AW n` sowie englisch `I/Q`, `IW/QW` (normalisiert auf E/A/EW/AW); vor dem Mnemonic
darf kein Buchstabe oder keine Ziffer stehen („SIZE 1.5“ ist keine Adresse). Start-Byte
= kleinste Byteadresse. Dialog mit Modulauswahl (Alle/Keine) und Warnungen.
- SP: ein Etikett fasst 2 Bytes bzw. 16 Kanäle; größere Module werden auf mehrere
  Etiketten verteilt, einzeilige Formate bekommen die Adressen gemerged.
- MP: je erkanntem Modul ein Streifen ab dem ausgewählten Modul (Header = BMK), Variante
  aus der Kanalzahl (`SuggestVariant`: > 16 Kanäle → 2 Spalten, analog → Analogblock),
  Adressen über den Generator; Seiten werden bei Bedarf angelegt. CSV/Excel sind im
  MP-Modus deaktiviert (Tooltip).

Alle Importe laufen im Hintergrund mit Wartecursor; Fehler erscheinen als Dialog.

## F15: Wiederherstellung nach Absturz

Unbehandelte Fehler werden geloggt, das Projekt als `recovery.etprint` gesichert und
ein Dialog mit Logpfad angezeigt. Beim nächsten Start wird die Wiederherstellung
angeboten; das wiederhergestellte Projekt gilt als ungespeichert.

## F16: Fensterzustand

Größe, Position, Maximiert, Zoom und Splitterbreite werden beim Schließen in `ui.json`
gespeichert und beim Start plausibilisiert (Fenster muss auf einem sichtbaren Bildschirm
liegen, Splitter 200–800 px).

## F17: Tastaturkürzel

| Kürzel | Aktion |
|--------|--------|
| Ctrl+N | Neues Projekt |
| Ctrl+O | Öffnen |
| Ctrl+S | Speichern |
| Ctrl+Shift+S | Speichern unter |
| Ctrl+P, F5 | Drucken |
| Ctrl+C / Ctrl+V | Etiketten/Module kopieren / einfügen (nicht im Textfeld) |
| Ctrl+Z / Ctrl+Y | Rückgängig / Wiederholen (nicht im Textfeld, dort Text-Undo) |
| Ctrl+Bild ab / Ctrl+Bild auf | Nächste / vorherige Seite |
| Ctrl+0 | Ganze Seite anzeigen |
| Entf | Ausgewähltes Etikett/Modul leeren (nicht im Textfeld) |
| Enter in Kopfzeile / Zeile 1 / Zeile 2 | Übertragen (nächstes Etikett) |
| Enter im Start-Byte | Generieren + Übertragen |
| Strg+Mausrad über der Vorschau | Zoom ±0,1 |
| Strg+Klick / Shift+Klick | Markieren / Bereich markieren |
| Rechtsklick | Kontextmenü (Kopieren, Einfügen, Druck umschalten) |

## F18: Test-Automation

Named Pipe `ETPrinter_TestAutomation`, aktiv mit `--test-automation` oder `ETPRINTER_TEST=1`.
Befehl per Zeile, Antwort als JSON. Client: `test-send.ps1 "<befehl>"`.

| Befehl | Beschreibung |
|--------|--------------|
| `ping` | Verbindungstest („pong“) |
| `help` | Befehlsliste (aus der Dispatch-Tabelle) |
| `state` | App-Zustand (Familie, Format, Seiten, Auswahl, Dirty, Datei, Tab, Zoom, Status, Varianten, Artikel, Bogen) |
| `screenshot [pfad]` | Fenster als PNG |
| `render-print <ordner>` | Alle Druckseiten ohne Dialog als `page_NN.png` |
| `render-calibration <ordner>` | Kalibrierseite als PNG |
| `print-state` | Seiten im Druckdokument + druckbare Elemente je Seite |
| `zoom <faktor>` | Zoom 0,3–5,0 |
| `maximize`, `resize <b>x<h>` | Fenster |
| `select-family <name>`, `list-families` | Produktfamilie |
| `select-format <name>`, `list-formats` | Druckformat |
| `select-label <index>`, `set-text <header\|z1\|z2>` | SP-Etikett direkt setzen |
| `generate <name> <typ> <byte> <n>` | SP: Adressen direkt ins Etikett |
| `set-generator <name> <typ> <byte> <n>`, `trigger-generate` | Generator-Felder + „Generieren + Übertragen“ |
| `set-input <header\|z1\|z2>`, `apply` | Manuell-Tab + Übertragen |
| `set-font <größe> <fett> <kursiv> [schriftart]`, `apply-font-all` | Schrift (Live-Apply) |
| `set-margin <oben\|links\|unten\|rechts> <mm>` | Seitenrand |
| `set-calibration <x> <y>` | Kalibrier-Versatz |
| `clear-selected`, `clear-all`, `toggle-print` | Leeren / Druckflag |
| `next-page`, `prev-page`, `add-page`, `remove-page` | Seiten |
| `new-project`, `save-project <pfad>`, `load-project <pfad>`, `open-file <pfad>` | Projekt (ohne Rückfragen) |
| `import-file <csv\|xlsx>`, `import-lines <txt>` | Importe ohne Dialog |
| `select-module <index>`, `mp-state`, `list-variants` | MP-Modul |
| `set-module-header`, `set-module-net <n1\|n2>`, `set-module-cpu` | MP-Texte |
| `set-module-variant <name>`, `set-module-article <artnr>` | MP-Layout / Katalog |
| `select-cell <index>`, `set-cell-text <text>` | MP-Adresszelle |
| `undo`, `redo`, `history-state` | Rückgängig / Wiederholen; Verlaufszustand (canUndo, canRedo, nextUndo, isDirty) |
| `quit` | App beenden (verwirft Änderungen) |

`tools/smoke.ps1 -Name <Name>` startet `publish/ET-Printer.exe`, fährt SP/MP/25 mm/
Import/UX/Roundtrip/Undo-Redo durch und legt Screenshots und Druck-PNGs unter
`test_results/<Name>/` ab (98 Prüfungen).

## F19: Logging

`%LOCALAPPDATA%\ETPrinter\log.txt` mit Zeitstempel, Level, Klasse und Methode; Rotation
bei 1 MB nach `log.old.txt`. Der Pfad steht im Info-Dialog und in Fehlermeldungen.

## F20: Info-Dialog

Hilfe → Info: Version aus der Assembly, unterstützte Familien, Bogen-Artikelnummern,
Siemens-Beitrags-IDs (81524595, 83681795) und Logpfad.

## F21: Rückgängig / Wiederholen

- Ctrl+Z / Ctrl+Y, Toolbar „Rueckgaengig“ / „Wiederholen“, Menü Bearbeiten. Im Textfeld
  gilt das Text-Undo der TextBox.
- Jede Änderung am Projekt ist ein Schritt: Übertragen, Generieren (inkl. Auto-Advance),
  Kopfzeile, Auswahl leeren, Alle löschen, Einfügen, Seite hinzufügen/entfernen, Druckauswahl,
  Schrift auf alle, Seite zurücksetzen, Import, Siemens-Modul/Variante, Format-/Familienwechsel.
- Tippen in Modulzellen/Header/Netzadresse, Ziffern in den Randfeldern und Schriftänderungen
  am ausgewählten Element werden innerhalb von 1,5 s zu einem Schritt zusammengefasst.
- Wiederhergestellt werden Inhalt, Einstellungen, Seiten und der Cursor (Seite + Auswahl).
  Der Status zeigt „Rueckgaengig: <Schritt>“ bzw. „Wiederholen: <Schritt>“.
- Zurück auf dem gespeicherten Stand gilt das Projekt wieder als unverändert; Laden und
  „Neu“ beginnen den Verlauf neu. Kapazität 100 Schritte.

## Standardwerte

| Einstellung | Standard | Bereich |
|-------------|----------|---------|
| Produktfamilie / Format | ET 200SP / Horizontal zweizeilig | 3 Familien, 10 Formate |
| Adress-Schrift | Arial 7 pt, nicht fett, nicht kursiv | Größe 4–10 pt, alle Windows-Schriften |
| Kopfzeilen-Schrift | 9 pt, fett | 4–10 pt |
| Ränder ET 200SP (oben/links/unten/rechts) | 20,5 / 27,5 / 20,5 / 27,5 mm | 0–60 mm |
| Ränder ET 200MP 35 mm und 25 mm | 14 / 25 / 19 / 12 mm | 0–60 mm (unten gesperrt) |
| Kalibrierung X/Y | 0 / 0 mm (aus `calibration.json`) | ±10 mm |
| Blanko A4 | aus | – |
| Generator | Modultyp DI, Start-Byte 0, 2 Bytes, Auto-Advance an | Start-Byte ≥ 0 |
| MP-Variante neuer Module | DI_DQ_16 (35 mm), MP25_16 (25 mm) | 9 Varianten |
| Zoom | „Ganze Seite“ beim ersten Start, danach aus `ui.json` | 0,3–4 (Slider), bis 5,0 |
| Fenster | 1400 × 900, min. 1000 × 700, linkes Panel 300 px | Panel 200–800 px |
| Zuletzt geöffnet | – | max. 10 Einträge |
| Projektdatei | Version 5 | Laden ab v1 |

Speicherorte unter `%LOCALAPPDATA%\ETPrinter\`: `calibration.json`, `recent.json`,
`ui.json`, `recovery.etprint`, `log.txt`, `log.old.txt`.
