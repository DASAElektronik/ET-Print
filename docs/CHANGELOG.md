# ET-Printer - Versionshistorie

## [Unreleased]
### Geplant
- Exakte Masse per Stahllineal (wenn Beschriftungsboegen geliefert)
- Feinabstimmung aller ET200MP-Varianten gegen die physischen Boegen

---

## 2026-09-09 - Projekttag 7 (Abschluss v3.1, siehe docs/ABSCHLUSSPLAN.md)

### v3.1.2: Umlaute in der Oberflaeche
- Alle sichtbaren Texte (Menue, Toolbar, Panels, Dialoge, Statusmeldungen, Katalog-/Familien-
  namen, Fehlermeldungen) verwenden echte Umlaute und ß statt ae/oe/ue/ss
  (z. B. "Öffnen", "Rückgängig", "Einfügen", "Seite (gilt für alle Etiketten)").
  Quellen sind UTF-8; `tools/smoke.ps1` mit UTF-8-BOM, damit PowerShell 5.1 die
  Vergleichstexte korrekt liest. Tests/Smoke auf die neuen Texte nachgezogen.

### v3.1.1: AP9b-AP9d (MainViewModel-Zerlegung, Undo/Redo, inkrementelle MP-Vorschau)

**AP9b - MainViewModel zerlegt (2714 -> 1818 Zeilen), sechs Schnitte + IDialogService**
- `Services/DialogService.cs`: `IDialogService` (Confirm, ConfirmSave, ShowInfo/Error,
  OpenFile, SaveFile, ShowPdfImport) mit `WpfDialogService` und `SilentDialogService`;
  keine MessageBox/Win32-Dialoge mehr in der ViewModel-Logik, Tests laufen stumm.
- `AddressGeneratorViewModel` (`Generator.*`): Generator-Felder, Vorschau, Auto-Advance.
- `SettingsPanelViewModel` (`Panel.*`): Schrift/Kopfzeile/Raender mit Live-Apply,
  Wertebereiche, `SuspendLiveApply`.
- `PageDocument<T>`: alle Seiten + sichtbare Seite (`Visible`), Show/AddPage/RemovePage/
  EnsurePage; `Labels`/`MpModules` sind die sichtbaren Seiten der beiden Dokumente.
- `ProjectSession` (`Session.*`): Dateipfad, Dirty, Zuletzt geoeffnet, Neu/Oeffnen/
  Speichern/Speichern unter mit Rueckfragen; das Haupt-ViewModel liefert nur
  Snapshot (`BuildProject`) und Restore (`ApplyLoadedProject`).
- `MpEditorViewModel` (`MpEditor.*`): Modul-/Zellauswahl, Variante, Katalog-Artikel,
  Generator-Vorbelegung, `SelectionInfo`, Zell-Neuaufloesung nach Rebuild.
- `ImportCoordinator` (`Import.*`): CSV/Excel/PDF-Ablaeufe (Dialog, Hintergrund-Parser,
  Wartecursor), dialogfreie Einstiege fuer die Test-Automation, statische Zuordnungsregeln
  (MapParsedModuleType, SuggestVariant, ParsedModuleUnits, BuildImportCells); das
  Haupt-ViewModel ist nur noch `IImportTarget`. `AddressGenerator.MergeAddressLines`.
- XAML/Test-Automation/Tests auf `Generator.*`, `Panel.*`, `MpEditor.*`, `Import.*` umgestellt;
  `Sta.RunAsync` (STA + Dispatcher-Frame) fuer async Import-Tests.

**AP9c - Undo/Redo**
- `UndoHistory<T>` (Memento-Verlauf, Kapazitaet 100, Zusammenfassen gleichartiger
  Aenderungen im 1,5-s-Fenster: Tippen in Modulzellen, Ziffern in Randfeldern, Schrift).
  `EditorState` = tiefe Projektkopie + Seite + Auswahl.
- `MainViewModel.MarkChanged()` ersetzt alle `IsDirty = true`; `BeginChange()`-Batches
  machen Commands, Import, Artikel-/Variantenwahl zu je einem Schritt; Format-/Familien-
  wechsel eines leeren Projekts ist rueckgaengig machbar, ohne dirty zu werden.
- Rueckkehr auf den gespeicherten Stand loescht das Dirty-Flag; Laden/Neu beginnen den
  Verlauf neu; Cursor (Seite/Auswahl) wird mit wiederhergestellt.
- Ctrl+Z / Ctrl+Y, Toolbar-Buttons, Menue Bearbeiten; Test-Automation `undo`, `redo`,
  `history-state`; Smoke-Szenario [5].

**AP9d - Inkrementelles Rendern der MP-Vorschau**
- `MpPreviewControl`: eine Canvas-Ebene je Modul; Modul-/Zell-`PropertyChanged` und
  Zell-Neuaufbau zeichnen nur die betroffene Ebene (Tippen, Auswahl, Druckflag, Variante).
  Vollaufbau nur bei Seitenwechsel, Familie/Format, Raendern/Kopfzeilen-Schrift,
  `MpPreviewRefreshToken`. Modul-lokale Aenderungen stossen keinen Vollaufbau mehr an.
- Tests: 284 (DecompositionTests, UndoRedoTests, MpPreviewTests); Smoke 98/98; Version 3.1.1.

### AP7: Release v3.1.0
- App-Icon `Assets/ETPrinter.ico` (generiert: Beschriftungsstreifen in DASA-Blau, 16-256 px).
- NuGet: PdfPig 0.1.16, ClosedXML 0.105.1.
- `tools/register-etprint.ps1`: .etprint-Dateizuordnung per HKCU (manuell, -Remove zum Entfernen).
- Publish-Profil win-x64 (self-contained, single-file, ReadyToRun) -> `publish/ET-Printer.exe`,
  ProductVersion 3.1.0+<commit>.
- GitHub-Release `v3.1.0` (DASAElektronik/ET-Print) mit EXE + Registrierungs-Script,
  Smoke-Test der Release-EXE 82/82.

### AP9a: Katalog-Rest (Analogmodule)
- Vier 35mm-Analogmodule per Modulname waehlbar: AI 8xU/I/RTD/TC ST (6ES7531-7KF00),
  AI 8xU/I HF (6ES7531-7NF00), AI 8xU/I/R/RTD BA (6ES7531-7QF00), AQ 4xU/I ST
  (6ES7532-5HD00) — Breite 35 mm laut TED-Datenblatt, Layout AI_AQ_8 bzw. AQ_4, komplett
  editierbar (keine feste Klemmen-Kanal-Zuordnung). Katalog: 18 Eintraege. Tests: 239.

### AP6: Doku-Konsolidierung + Version
- ARCHITECTURE.md, FEATURES.md (F01-F20), PRODUCT.md komplett neu auf Codestand v3.1:
  .NET 9 / C# 13, reale Projektstruktur (Phantomdateien LayoutEngine/ProjectFileService/
  PagePreviewViewModel entfernt), 10 Formate, alle SP-Formate 5x20, Raender 20,5/27,5,
  MVVM-Skizze mit beiden Vorschau-Pfaden, SheetGeometry, Persistenz v1-v5, Kalibrierung,
  Recovery, UiState, Test-Automation-Befehlstabelle, Tastaturkuerzel, Standardwerte.
- README.md neu (Zweck, Installation, 10 Bedienschritte, Boegen, Kalibrierung,
  Speicherorte, Entwicklung, Doku-Verweise).
- PRINT-FORMATS.md: Widerspruch 5 vs. 10 Module aufgeloest (Historie erklaert), 9 Varianten,
  Katalog-Liste, Zeilenhoehe 5,6 mm fest mit Hinweis auf Stahllineal, Datenblatt-Quellen der
  25mm-Module, Stand-der-Implementierung aktualisiert.
- TODO.md: Phase 12 als ueberholt markiert, Abschlusskriterien; CHANGELOG: Versionsplan
  v2.2-v3.1, Meilensteine bis 2026-09-09; NUL-Bytes aus einem frueheren Eintrag entfernt.
- Version: csproj 3.1.0, Description mit ET200MP, Info-Dialog aus der Assembly;
  Code-Kommentare "5 Module pro Seite" korrigiert.

### AP8: 25mm-Module datenblattverifiziert (Stahllineal-Masse bleiben offen)
- Blockdiagramme der 25mm-BA-Module ausgewertet (Klemmennummern stehen im Siemens-Diagramm
  UNTER der Klemme): DI 32 BA, DQ 32 BA, DQ 16 BA, DI16/DQ16 BA haben die 40-Klemmen-
  Struktur der 35mm-Module (K1-8 / K11-18 Kanaele, K9/K10 / K19/K20 Struktur, rechte
  Reihe analog). TED-Datenblaetter bestaetigen 25 mm Breite fuer alle BA-Digitalmodule —
  **DI 16x24VDC BA (6ES7521-1BH10) war bisher faelschlich in der 35mm-Familie**.
- Generische Layouts MP25_16/MP25_32 = DI_DQ_16/DI_DQ_32 ohne Labels; damit landet Byte 2
  eines 32-Kanal-Moduls in der rechten Spalte (K21-28) statt in K17-20 links
  (Review-Finding Services #4). Analogmodule nutzen `CreateLayout_MP25_Plain20`.
- Katalog: DI 16 BA nach 25mm verschoben, neu DQ 16 BA, DI 32 BA, DQ 32 BA und das
  gemischte DI16/DQ16 BA (`MixedOutputRightColumn`: rechte Spalte mit A-Praefix,
  Bytezaehlung beginnt rechts neu). 14 Eintraege.
- Generator einheitlich: Bytezahl = min(GenCount, editierbare Zellen / 8) fuer alle
  Familien; Katalogwahl setzt GenCount auf die volle Bytezahl des Moduls. DO-Typ liefert
  auch bei MP25-Varianten die Versorgungslabels je Kanalgruppe.
- Laden: ein Artikel, der nicht zur Familie passt (DI 16 BA in einem 35mm-Projekt), wird
  abgewaehlt — Texte bleiben, Belegung wird generisch.
- Offen: exakte Masse der Boegen 6ES7592-1AX00 / -2AX00 per Stahllineal.
- Tests: 234 (24 neu in Catalog25mmTests + angepasste Layout-Tests).

### AP5: Komfort / UX
- **Fensterzustand** (Groesse, Position, Maximiert, Zoom, Splitter) in
  `%LOCALAPPDATA%\ETPrinter\ui.json`; Position wird gegen den virtuellen Bildschirm
  plausibilisiert (abgesteckter Zweitmonitor).
- **Drag & Drop** von .etprint auf das Fenster und **Startargument** (Doppelklick/"Oeffnen mit").
- **Tastatur**: Enter in Zeile 1/2/Kopfzeile = Uebertragen, Enter im Start-Byte = Generieren,
  Entf = Auswahl leeren, Ctrl+Shift+S = Speichern unter, F5 = Drucken, Ctrl+0 = ganze Seite.
- **Zoom**: Button "Ganze Seite", Strg+Mausrad ueber der Vorschau, freier Zoom 0,3-4
  (kein 0,5er-Raster), Startzoom passt die Seite ins Fenster.
- **Wertebereiche**: Raender 0-60 mm, Kalibrierung +/-10 mm, Start-Byte >= 0 — ausserhalb
  liegende Werte werden begrenzt und in der Statusleiste gemeldet.
- **Einstellungen-Panel** umgebaut: "Seite (gilt fuer alle Etiketten)" mit Kopfzeilen-Schrift,
  Raendern, Kalibrierung, Blanko-A4 und "Seite zuruecksetzen"; darunter "Schrift: <Auswahl>"
  mit Schriftart/Groesse/fett/kursiv (Live-Apply) und "Auf alle Etiketten/Module anwenden".
  Der wirkungslose "Uebernehmen"-Button ist weg. Buttons "Auswahl leeren" / "Alle loeschen".
- **MP-Modus**: Wechsel in ein Modul-Format aktiviert den Tab "MP Modul" automatisch.
- Systemschriften werden im Hintergrund geladen (schnellerer Start), ComboBox virtualisiert.
- **Druck**: "Nur aktuelle Seite drucken..."; Status "N von M Seiten, K leere uebersprungen".
- **Info-Dialog**: Version aus der Assembly, ET200SP + ET200MP, Bogen-Artikelnummern, Logpfad.
- Test-Automation: `set-margin`, `set-calibration`, `clear-selected`, `apply-font-all`,
  `open-file`; `state` mit inputTabIndex/zoom/status.
- Tests: 210 (13 neu in UxTests). Smoke AP5: 74/74 gruen.

### AP4: Import-Robustheit (CSV / Excel / PDF)
- **CSV** neu als zeichenweiser Parser ueber den gesamten Text (`Parse`/`ParseRecords`):
  Zeilenumbrueche in Anfuehrungszeichen bleiben im Feld, `""` ist ein escaptes
  Anfuehrungszeichen, letzte Zeile ohne Zeilenende wird importiert. Trenner ; , oder
  Tab (Excel "Text (Tabstopp)"). Encoding per BOM (UTF-8, UTF-16 LE/BE; Excel "Unicode
  Text" kam frueher als "K\0o\0p\0f" an), UTF-16-LE-Heuristik ohne BOM, sonst strikt
  UTF-8 mit Windows-1252-Fallback. Zusaetzliche Spaltennamen "Kopf"/"Adresse(n)".
- **Excel**: erstes SICHTBARES Blatt mit Inhalt (verstecktes Deckblatt lieferte "keine
  Daten"); Zahlen-/Datumszellen ueber `GetFormattedString` (1,5 bzw. Datum wie in Excel
  angezeigt).
- **PDF-Parser**: Wortgrenze vor dem Mnemonic ("SIZE 1.5", "TYPE 2.0", "NEW 12" sind keine
  Adressen mehr), englische Mnemonics I/Q/IW/QW werden erkannt und auf E/A/EW/AW
  normalisiert, Start-Byte = kleinste Byte-Adresse statt zuerst gelesener; `ParseLines`
  als testbare Text-Schnittstelle.
- **MP-Import**: benutzerdefinierte Module bekommen die Layout-Variante passend zur
  Kanalzahl (`SuggestVariant`: 32 Kanaele -> 2 Spalten, Analog -> Analogblock).
- Importe laufen asynchron mit Wartecursor (Parser im Hintergrund, UI friert nicht ein).
- Test-Automation: `import-file <csv|xlsx>`, `import-lines <txt>` (Schaltplan-Text) ohne
  Dialog; Info-Boxen der Importe werden im Automation-Modus unterdrueckt.
- Tests: 197 (24 neu: CSV Multiline/Escapes/Tab/BOMs/ANSI, Excel verstecktes Blatt/
  Zahlformat/Spalte B, PDF Phantomadressen/Min-Byte/I-Q/Modultrennung, SuggestVariant).
  Smoke AP4: 62/62 gruen inkl. UTF-16-CSV und Schaltplan-Text in SP und MP.

### AP3: Geometrie-Klasse — Preview = Druck
- Neu `Services/SheetGeometry.cs` (mm-basiert, `RectMm`): einzige Quelle fuer SP-Raster
  (Position 1 = unten rechts, Kopfspalte 20 %), MP-Baender (Header 25,7/20,6 mm, 20 x 5,6 mm),
  Zellen, Netzadress-/CPU-Spalten und das Kalibrier-Rasterrechteck. Druck (`CreatePage`,
  `CreateMpPage`, Kalibrierseite) und MP-Vorschau rechnen nur noch hierueber; die
  Umrechnung (96/25.4 DIP bzw. 3 px je mm) passiert beim Aufrufer.
- SP-XAML-Vorschau: Kopfspalten-Breite aus der Geometrie (frueher fest 18 px), Zeilennummern
  im rechten Seitenrand statt im Raster (das Raster fuellt jetzt exakt den Druckbereich),
  Bindungen fuer ColumnDefinition/RowDefinition ueber den BindingProxy.
- Toter MP-Zweig in `FormatDefinitions.GetCellSize` (5,44 mm) und in `CreatePage`/
  Kalibrierseite entfernt; `GetCellSize` ist ein Wrapper um SheetGeometry.
- Tests: 173 (16 Geometrie: Position 1 unten rechts, Kopfspalte, Baender, Zellen, 25mm,
  Rand-unten-Unabhaengigkeit; 7 Persistenz: Migration v1/v2/v3, Roundtrip aller v5-Felder,
  kein Legacy-Feld beim Speichern). Smoke AP3: 48/48 gruen.

### AP2: Mittlere Bugs + UX
- **Strg+C/V in Textfeldern**: Window-KeyBindings sind nicht ausfuehrbar, solange eine
  TextBox den Fokus hat — die Taste geht an das Textfeld (frueher wurden Etiketten kopiert).
- **Kontextmenue-Commands** ueber `BindingProxy` (ContextMenu haengt nicht im Visual Tree,
  `RelativeSource AncestorType=Window` lieferte null); Rechtsklick selektiert das Etikett.
  Kontextmenue auch auf der MP-Vorschau (Kopieren/Einfuegen/Druck umschalten).
- **Rueckfragen** vor "Alle loeschen" und "- Seite" bei vorhandenem Inhalt.
- **Druckauswahl im MP-Modus**: Alle/Keine/Nur befuellte wirken auf Module, "Druck
  umschalten" fuer das ausgewaehlte Modul, Checkbox "Modul drucken" im MP-Tab, ausgegraute
  Module in der Vorschau.
- **Dirty-Tracking**: Druckflag per Kontextmenue, Blanko-A4-Option und Format-/Familienwechsel
  mit Inhalt markieren das Projekt als geaendert.
- **Recent-Files**: fehlende Datei -> Rueckfrage "Eintrag entfernen?"; Ladefehler als
  MessageBox; Eintrag erst nach erfolgreichem Laden.
- **Shrink-to-fit statt "..."** (2.21): vertikale SP-Slots, horizontale Zeilen und
  Kopfzeile verkleinern den Text per Viewbox (DownOnly) in Druck UND Vorschau —
  "EW 10" in 6,4 mm hohen Slots ist wieder lesbar. Horizontale Formate ohne Leerslots
  (`Line1Display`/`CompactSlots`).
- **MP-Header mehrzeilig** (Device / Module / Slot): Zeilenumbrueche bleiben, Umbruch statt
  Kappen, FitBox bei Platzmangel — Druck und Vorschau identisch.
- **MP-Vorschau entprellt** (DispatcherTimer 40 ms, Abmeldung beim DataContext-Wechsel):
  kein kompletter Canvas-Neuaufbau mehr pro Tastendruck; `FlushRender` fuer Screenshots.
- `PrintTicket` erzwingt A4 Hochformat; "Rand unten" im MP-Modus gesperrt (Tooltip);
  "Manuell"-Tab im MP-Modus ausgeblendet; Infozeilen und Statusleiste familienbewusst
  ("Modul 3 / 10", "Bogen: 2 Baender x 5 Spalten = 10 Streifen").
- Strg+Klick markiert den Anker mit; Paste meldet "N von M (Seitenende)"; GenCount folgt
  dem Modultyp (DI 1/2/4 -> AI 2/4/8).
- Test-Automation: `toggle-print`; `clear-all`/`remove-page` unterdruecken die Rueckfrage.
- Tests: 150 (17 neu in MediumFixTests). Smoke AP2: 48/48 gruen.

### AP1: Kritische Bugs (falscher/fehlender Druck, Datenverlust)
- **"Neu" bei aktivem Standardformat war ein No-op**: der Format-Setter kehrte bei
  gleichem Format sofort zurueck, `InitializeLabels` lief nie, `IsDirty=false` — alte
  Etiketten galten als gespeichert. Jetzt werden Etiketten/Module immer neu angelegt,
  PrintGridLines zurueckgesetzt.
- **SIWAREX-Streifen wurden nicht gedruckt**: `HasText` zaehlte nur editierbare Zellen.
  Neu `MpModule.HasPrintableContent` (Text ODER reiner Pinout-Streifen ohne editierbare
  Zellen); einzige Druck-Entscheidung ist `PrintService.IsPrintable` fuer Seite,
  Schnittkanten und Inhalt. Leere DI/DQ-16-Module (nur "L+"/"M") bleiben ungedruckt.
- **Variantenwechsel behielt den Katalog-Artikel**: Belegung fiel still auf generisch
  zurueck, UI/Datei zeigten weiter den Artikel. Der Variant-Setter loescht jetzt einen
  nicht passenden Artikel.
- **Leere Seiten wurden gedruckt** (Auto-Advance legt nach dem letzten Etikett immer eine
  neue Seite an): `BuildDocument`/`BuildMpDocument` ueberspringen Seiten ohne druckbaren
  Inhalt; bei 0 Seiten Hinweis statt Leerauftrag.
- **PDF-Import kappte 32-Kanal-Module auf 2 Bytes**: `BuildImportCells` verteilt in
  Bloecken von `GetEffectiveCount` auf mehrere Etiketten, einzeilige Formate bekommen
  die Adressen gemerged (`MergeAddressLines` jetzt gemeinsam).
- **PDF-Import im ET200MP-Modus** fuellt jetzt Module (Header = Modulname, Adressen ueber
  `FillMpModuleAddresses`, Seiten bei Bedarf); CSV/Excel sind im MP-Modus deaktiviert
  (Tooltip) statt unsichtbare SP-Seiten zu erzeugen.
- **Atomares Speichern** (`ProjectService.WriteAtomic`: .tmp + Move, vorherige Version
  als .bak) fuer Projekt und calibration.json.
- **Globaler Exception-Handler** (App.xaml.cs): Log + Notfall-Sicherung nach
  `%LOCALAPPDATA%\ETPrinter
ecovery.etprint` + Hinweis; beim naechsten Start wird die
  Wiederherstellung angeboten. `TaskScheduler.UnobservedTaskException` wird geloggt.
- **Zellauswahl** wird bei Modulwechsel zurueckgesetzt und nach Zell-Neuaufbau
  (Variante/Artikel/Modultyp/Paste) per CellIndex neu aufgeloest (`CellsRebuilt`).
- **Schrift-Einstellungen wirken im MP-Modus** (Groesse/fett/kursiv/Schriftart auf das
  Modul; Modulwechsel laedt die Werte in die Eingabefelder). Neu `MpModule.IsItalic`
  (additiv, Preview + Druck).
- **Familienwechsel ueberall gleich** (`ApplyFamilyCore`): Formate, Varianten und
  Katalog-Artikel; `ApplyLoadedProject` zeigte nach dem Laden eines 25mm-Projekts die
  35mm-Varianten. Familienwechsel setzt nur noch die Raender zurueck (nicht die
  Header-Schrift) und markiert das Projekt als geaendert.
- Texte in gesperrten Struktur-Zellen werden beim Zell-Neuaufbau geleert (Geistertexte).
- Laden: `Pages`/`Settings` null-sicher, v4-zu-v5-Auffuellung mit Familien-Default-Variante,
  ModuleIndex immer normalisiert, unvollstaendige MP-Seiten werden aufgefuellt.
- Erneute Auswahl desselben Etiketts/Moduls loescht die Markierung nicht mehr (2.6);
  Generator-Statusmeldung bleibt sichtbar (2.10); Log mit Klassenname (Finding 21).
- Test-Automation: `set-font`, `print-state`; `state` mit Varianten/Artikeln/Dateipfad,
  `mp-state` mit Schrift/Zellauswahl/Zelltexten.
- Tests: 133 (24 neu in CriticalFixTests, STA-Helfer fuer FixedDocument-Tests,
  Testprojekt mit UseWPF + InternalsVisibleTo). Smoke AP1: 41/41 gruen, SIWAREX im
  Druck-PNG sichtbar.

### AP0: Testinfrastruktur
- PrintService in dialogfreie Dokument-Erzeugung (`BuildDocument`, `BuildMpDocument`,
  `BuildCalibrationDocument`) und Dialog (`Print` -> bool) getrennt. Druck und
  Test-Automation nutzen exakt dieselben Seiten.
- `PrintService.RenderToPng`: rendert jede Druckseite als PNG (150 dpi) — der Druckpfad
  ist damit ohne Drucker prueffbar und mit dem Preview-Screenshot vergleichbar.
- Druck-Abbruch wird erkannt: Status "Druck abgebrochen" statt "gesendet", Kalibrierung
  wird erst nach tatsaechlichem Druck gespeichert (Review-Finding 2.8/2.18).
- Test-Automation: Dispatch-Tabelle (Hilfe wird generiert, frueher fehlten 8 Befehle),
  neue Befehle `render-print`, `render-calibration`, `quit`, `set-input`, `remove-page`;
  `apply` fuehrt jetzt wirklich den Uebertragen-Command aus, `new-project` ist ein
  echtes "Neu" ohne Rueckfrage (frueher nur ClearAll).
- `tools/smoke.ps1`: startet `publish/ET-Printer.exe`, faehrt SP/MP/25mm/Roundtrip durch,
  legt Screenshots + Druck-PNGs unter `test_results/<Name>/` ab, Exit-Code bei Fehler.
- Publish-Profil `Properties/PublishProfiles/win-x64.pubxml` (self-contained, single-file).
- Smoke AP0: 24/24 gruen. Sichtbar bestaetigt: SIWAREX-Modul fehlt im Druck (AP1 1.2),
  analoge Adressen in vertikalen SP-Slots werden mit "..." gekappt (neu AP2 2.21).

---

## 2026-08-04 - Projekttag 6 (Katalog-Lueckenschluss + typabhaengige Struktur-Labels)

### Drei neue Katalog-Eintraege (datenblattverifiziert)
- **DQ 8x24VDC/2A HF** (6ES7522-1BF00-0AB0, Manual 59193089): nutzt die
  40-Klemmen-Frontbaugruppe, belegt aber nur K1-K8 (CH0-7) plus 1L+/1M auf K9/K10
  und 2L+/2M auf K19/K20. K11-K18 und die komplette rechte Klemmenreihe sind
  unbelegt und im Streifen **gesperrt** — dadurch fuellt der Adress-Generator
  automatisch ein Byte statt der vier des 32-Kanal-Rasters (neues
  `CreateLayout_DQ_8_2A`, gleiche Zellenzahl wie DI_DQ_32).
- **DI 16x230VAC BA** (6ES7521-1FH00-0AA0, Manual 59193398): Kanaele auf den
  ungeraden Klemmen, xN auf K8/K18/K28/K38, K9/K10 + K19/K20 unbelegt. Bestaetigt
  die leeren Struktur-Bloecke des Excel-Templates — der frueher offene Vermerk
  "Datenblatt noch nicht extrahiert" ist damit erledigt.
- **AQ 2xU/I ST** (6ES7532-5NB00-0AB0, Manual 91688388): erster **25mm**-Eintrag.
  Wie alle Analogmodule ohne feste Klemmen-Kanal-Zuordnung (Spannung 2-/4-Draht und
  Strom belegen unterschiedliche Klemmen), belegt nur K1-K7, Versorgung am
  Einspeiseelement K41/K43. Variante MP25_16, Streifen komplett editierbar.
- `EntriesForFamily` filtert jetzt ueber die Variante des Eintrags statt die
  25mm-Familie hart auf "Benutzerdefiniert" zu setzen.

### Struktur-Labels haengen am Modultyp (generischer Fall)
- Neu `MpModule.IoType` (DI/DO/AI/AO). Ohne Katalog-Artikel bestimmt der Modultyp
  die Struktur-Klemmen: Eingabemodule fuehren die Versorgung nur am Gruppenende,
  Ausgabemodule je Kanalgruppe. DI_DQ_16 als DO zeigt damit 1L+/1M + 2L+/2M
  (wie DQ 16 ST), DI_DQ_32 als DO vier Versorgungsgruppen (wie DQ 32 HF).
- Gesetzt bei Katalogwahl, bei Wechsel des Generator-Modultyps (Live-Update der
  Vorschau) und beim Generieren. Ein Katalog-Artikel gewinnt immer.
- Persistenz additiv: alte Projektdateien ohne das Feld laden als DI — exakt das
  bisherige Verhalten (per Roundtrip mit entferntem Feld geprueft).

### Automation + Tests
- `mp-state` liefert zusaetzlich `article`, `ioType`, `editableCells` und die
  gesetzten `structureLabels`; `set-module-article` akzeptiert nur noch Artikel der
  aktiven Produktfamilie (sonst bekaeme ein 35mm-Streifen eine 25mm-Variante).
- 109 Tests (13 neu: Katalog-Belegungen, Familienfilter, Typabhaengigkeit,
  Zellenzahl-Invariante ueber alle Modultypen).
- Smoke-Test: DQ8 fuellt 8 Adressen und sperrt den Rest, DI/DO-Wechsel schaltet die
  Struktur-Labels live um, 25mm-Familie bietet nur den AQ-2-Eintrag, Speichern/Laden
  erhaelt Artikel + Modultyp.

---

## 2026-07-03 - Projekttag 5 (v3.0: Modul-Katalog + Review-Findings)

### AP2: Modul-Katalog (konkrete Siemens-Module)
- Neu MpModuleCatalog: konkrete Artikelnummern mit exakter Klemmenbelegung.
  Variante = Layout (Merges), Katalog-Eintrag = Struktur-Labels (M/L+/leer).
- Parametrisierte Layout-Factory (CreateLayout_DI_DQ_16/32 mit Klemmen-Labels);
  GetDefinitions(module) waehlt Katalog- oder generische Belegung.
- Verifizierte Eintraege: DI 32x24VDC HF, DI 16x24VDC BA, DI 16x24VDC HF,
  DQ 16x24VDC/0.5A ST (K9/K10 = 1L+/1M — Unterschied zu DI16 jetzt korrekt).
- UI: "Siemens-Modul"-ComboBox ueber der Varianten-ComboBox; Katalogwahl setzt
  Variante + Belegung + Generator-Modultyp (DI/DO/AI/AO). MpModule.ArticleNumber (v5).
- TAS-Befehl set-module-article; 8 Katalog-Tests. Smoke: DQ16 ST verifiziert.

### AP4: 7 offene Review-Findings gefixt
- Format-/Familienwechsel bei befuellten Etiketten: Rueckfrage (ConfirmContentLoss),
  bei Abbruch ComboBox-Reset; programmatische Pfade (Laden/Neu/Automation) unterdrueckt.
- Multi-Selection seitenlokal: NavigateToPage hebt Checks auf ALLEN Seiten auf.
- Excel-Import ohne Kopfzeile: Spalten ab UsedRange.FirstColumn statt fix A/B/C.
- TAS load-project nutzt jetzt ApplyLoadedProject (laedt SP-Labels + Settings + alle
  Seiten; Gegenstueck zu BuildProject) statt nur MpPages[0].
- RunOnUI: operation.Aborted-Hook — Dispose haengt beim Shutdown nicht mehr 2s.
- Projekt-Laden: lokale calibration.json hat Vorrang (maschinenspezifisch).
- CSV-Separator-Erkennung ignoriert Trenner in Anfuehrungszeichen.
- 71 Tests (11 neu: Katalog, CSV, Migration). Smoke: SP-Roundtrip, Format-Wechsel.

### AP3: Datenblatt-Verifikation AI/AQ, 230V, DQ (2026-07-03)
- **AI 8xU/I / AQ 4xU/I sind modusabhaengig** — kein festes Klemmen-Label. AI_AQ_8
  und AQ_4 auf Excel-Struktur korrigiert: 5 editierbare 4-Zeilen-Bloecke pro Spalte,
  KEIN hartkodiertes MANA (App zeigte vorher 4 Bloecke + MANA + leer).
- MP-Analog-Generator: spaltenausgeglichene Verteilung von GenCount Kanaelen
  (AI 8: CH0-3 links, CH4-7 rechts), Restbloecke bleiben leer.
- **DQ 8x230VAC/5A Relay** verifiziert (A5E03485590-AD): 24V-versorgt, Versorgung nur
  K19/20 + K39/40. Die geratenen 230V-Labels ("M", "L1/N/PE") entfernt — Excel laesst
  diese Bloecke leer, die App jetzt auch.
- **DQ 32x24VDC/0.5A HF** verifiziert (109480716) und als Katalog-Eintrag ergaenzt
  (K9/10=1L+/1M, K19/20=2L+/2M — anders als DI 32).
- 73 Tests (2 neue: AI/AQ-Struktur). Smoke: AI-4+4-Verteilung + DQ32-Katalog verifiziert.

### AP5 (Teil 1): SIWAREX Waegemodule (2026-07-03)
- Neue Variante SIWAREX_WP52x: fester Frontstecker-Pinout, verifiziert am Manual
  A5E36695151A (04/2016). 20 Klemmen/Spalte, beide Spalten identisch (Waegezelle A/B):
  EXC+/-, SIG+/-, SEN+/-, RS485 D+/-, DQ.L+/DQ.M, DQ.0-3, DI.0-2, DI.M, L+, M.
- Katalog-Eintraege WP521 ST (7MH4980-1AA01) + WP522 ST (7MH4980-2AA01).
- 77 Tests (4 neue: SIWAREX-Pinout). Smoke: Pinout-Streifen visuell verifiziert.

### AP5 (Teil 2): 25mm-Template (6ES7592-2AX00) — IMPLEMENTIERT (2026-07-03)
Excel-Dump aller 5 Mappen ausgewertet, 25mm-Modell umgesetzt:
- **20 Module pro Bogen** (10 Spalten x 2 Baender), Modulbreite 17.36mm.
- Modulstruktur: **Adresse + CPU-Name**, KEINE Net-Address-Spalte (Col2Ratio=0).
- 2 neue Varianten: **MP25_16** (20 Zeilen colspan-2, keine M/L+-Struktur) und
  **MP25_32** (20 Zeilen x 2 Spalten = 40 Slots; deckt 16_DI_16_DQ mit ab).
- Spalten-Ratios sind jetzt **familienabhaengig** (ProductFamilyInfo.Col0-3Ratio +
  HasNetAddressColumn); Renderer (PrintService/MpPreviewControl) lesen sie von dort,
  35mm unveraendert. Kein separater Renderpfad noetig.
- Varianten- UND Katalog-Auswahl familiengefiltert (25mm zeigt nur MP25-Varianten
  + "Benutzerdefiniert"); neue 25mm-Seiten defaulten auf MP25_16.
- Generator: 25mm-Digital nutzt GenCount (Bytes) statt Auto-Ableitung -> ein 32-DI
  fuellt 32 statt 40 Slots (die 8 generischen Reserve-Slots bleiben leer).
- 96 Tests (9 neu: MP25-Layouts, Familien-Filter, Geometrie/Band-Spalte). Smoke:
  25mm-Familie -> 20 Module, MP25_16/MP25_32 befuellt, Rendering + v5-Roundtrip
  visuell + per JSON verifiziert.

Offen bleibt nur: AQ 2xU/I ST (25mm) als Katalog-Eintrag + Feinmasse per Stahllineal.

---

## 2026-07-02 - Projekttag 4b (v3.0-Umbau: 10 Streifen-Positionen pro A4)

### AP1: 10-Positionen-Architektur (User-Entscheidung nach Excel-Beleg)
- **Der A4-Bogen hat jetzt 10 Streifen-Positionen** (Band 0 oben mit 25.7mm-Header,
  Band 1 unten mit 20.6mm-Header, je 5 Spalten) — wie das Siemens-Excel-Template.
  Vorher wurde nur das obere Band bedruckt (50% Bogen-Verschnitt).
- ProductFamilyInfo: neues Feld ColumnsPerPage + BandOf()/ColumnOf();
  EstimatedSeparatorHeight → EstimatedBand2HeaderHeight (war real der Band-2-Header)
- PrintService/MpPreviewControl: Position aus Band+Spalte, Header-Hoehe je Band;
  untere Position ist echtes Modul (leere Scaffolding-Logik entfernt)
- Preview: Klick auf den Modul-Header selektiert das Modul (wichtig fuer Band 2)
- NetAddress3/4 stillgelegt (Aera "1 Modul = 2 Baender"); nur noch v4-Deserialisierung
- Persistenz v5: v4-MP-Seiten werden beim Laden von 5 auf 10 Module aufgefuellt
- MP-Tab-Label: "Netzname (Block 2)" (Excel: unterer Net-Block = Net Name)
- 60 Tests gruen (3 neue Migrationstests); Smoke-Test: Band-1+2-Module befuellt,
  v5-Roundtrip und v4-Migration in laufender App verifiziert

---

## 2026-07-02 - Projekttag 4 (Phase 12 Feinvergleich + grosses Bugfix-Review)

### Phase 12: Feinvergleich gegen Excel-Template + Datenblaetter
- **DI 32/16: Klemmen 9/10 (bzw. 29/30) sind UNBELEGT** — App zeigte dort faelschlich "M".
  Verifiziert am Blockdiagramm des Equipment Manuals (A5E03485935-AH); Layouts korrigiert.
- PRINT-FORMATS.md: Klemmenbelegungstabelle DI32 korrigiert (K38=CH31, K39=2L+, K40=2M),
  16er-Modultyp-Matrix ergaenzt (DI16 BA / DI16 HF / DQ16 ST unterscheiden sich!)
- Alle 12 Excel-Mappen zellgenau gedumpt (COM): 230V-Merge-Strukturen exakt wie App,
  Spalten-Ratios <0.1% Abweichung, vertikale Mappen identisch zu horizontalen (nur rotiert)
- Excel-Fakten dokumentiert: Streifenbreite 33.97mm, Net-Spalte = Net Address (oben) +
  Net Name (unten), PageSetup-Raender des Templates unbrauchbar

### Bugfixes (Multi-Agent-Review, 23 bestaetigte Findings)
- **KRITISCH: MP-Edits setzten IsDirty nie** — Schliessen ohne Speichern-Dialog = stiller
  Datenverlust. Fix: ContentChanged-Callback durch Modul-/Zell-VMs (markiert dirty UND
  refresht die MP-Preview live — behebt auch "Preview zeigt veraltete Daten").
- **KRITISCH: Produktfamilien-Wechsel korrumpierte Raender** (Live-Apply-Regression aus
  06d52ad): erster Margin-Setter schrieb die alten Familienraender zurueck. Fix: Guard.
- **Culture-Bug: "20,5" wurde als 205mm geparst** (WPF-Bindings nutzten en-US) — Fix:
  FrameworkElement.LanguageProperty auf OS-Culture; Felder zeigen jetzt Komma.
- **Schriftgroesse = Punkt**: Druck interpretierte FontSize als DIP und druckte ~25%
  kleiner als das Siemens-Template. Fix: pt->DIP (96/72) im Druck; SP-/MP-Preview auf
  denselben pt-Faktor vereinheitlicht (MP-Preview nutzte fest 5px/Arial/normal).
- **Einzeilige Formate**: Preview zeigte Line2, Druck liess sie weg. Fix: Preview blendet
  Line2 formatabhaengig aus; Generator merged Adressen kanal-aufsteigend in Line1.
- **GenerateDigital festes 8-Klemmen-Raster** (wie Analog): Adressen sitzen jetzt ueber
  den richtigen Klemmen; max 2 Bytes pro Etikett, Auto-Advance um effektive Anzahl.
- **MpModule.HasText ignorierte NetAddress2/3/4** — Modul wurde beim Druck uebersprungen,
  obwohl die Preview Inhalt zeigte.
- **MP-Druck/Preview-Abgleich**: Zellen-/Headertexte vertikal zentriert (Druck) bzw.
  horizontal zentriert (Preview); rotierte Preview-Texte sassen um ihre eigene Groesse
  versetzt (Border-Container wie im Druck); untere Bandposition zeigt keine
  CPU/NetAddress-Duplikate mehr (Druck gab sie nie aus).
- **MP-Kalibrierseite** nutzte anderes Geometriemodell als der Druck (~6mm Versatz) —
  rechnet jetzt mit denselben festen Massen wie CreateMpPage.
- **230V-Layouts spaltenweise**: sequenzielle Befuellung legt Byte 0 links, Byte 1 rechts
  ab (vorher gerade Bits links / ungerade rechts vermischt).
- **DoSave meldet Fehler**: Speicherfehler beim Schliessen brachen den Vorgang vorher
  nicht ab (Datenverlust trotz "Ja, speichern") — jetzt bool-Rueckgabe + MessageBox.
- **'Alle loeschen' im MP-Modus** loeschte nichts und desynchronisierte den Seitenindex —
  eigener MP-Zweig.
- **Test-Automation save-project** speicherte nur die sichtbare Seite — nutzt jetzt
  BuildProject() (gemeinsame Serialisierung mit DoSave, alle Seiten).
- **ListenLoop-Backoff**: Pipe-Fehler (z.B. zweite Instanz) erzeugten Busy-Spin mit
  100% CPU + Log-Rotation-Flut — jetzt 1s Delay.
- **PDF-Import**: ModuleTypePattern matchte reale Siemens-Bezeichnungen ("DI 8x24VDC ST")
  nicht (trailing \b) — Import fand 0 Module.
- **CSV-Import**: Encoding-Erkennung prueft jetzt die ganze Datei (strikte UTF-8-
  Dekodierung mit cp1252-Fallback) statt nur der ersten Zeile — Umlaute blieben kaputt.
- Input-Margin-Startwerte an LabelSettings-Defaults angeglichen (20.5/27.5).

### Tests
- 59 Tests (14 neu): GenerateDigital-Raster, GetEffectiveCount, MpModule.HasText
- Smoke-Test via TestAutomation: Vertikal-einzeilig-Merge, MP-Familienwechsel-Raender,
  Dirty-Tracking, Mehrseiten-Save verifiziert (Screenshots)

### Offen (Review-Findings ohne abgeschlossene Verifikation, Session-Limit)
- Format-/Familienwechsel verwirft befuellte Seiten ohne Rueckfrage
- Multi-Selection ueberlebt Seitenwechsel unsichtbar (Ctrl+C kopiert alte Markierung)
- Excel-Import ohne Kopfzeile nutzt feste Spalten A/B/C statt UsedRange
- load-project (Automation) laedt SP-Labels/Settings nicht
- RunOnUI-TaskCompletionSource haengt bei abgebrochener DispatcherOperation
- Projekt-Laden ueberschreibt maschinenspezifische calibration.json
- CSV-Separator-Erkennung zaehlt Zeichen in quoted Feldern mit

---

## 2026-04-23 - Projekttag 3 (Feature: Auto-Live-Preview)

### Feature
- **Live-Vorschau fuer Einstellungsaenderungen** — "Uebernehmen"-Klick
  ist nicht mehr noetig; jede Aenderung einer Einstellung wirkt sich sofort
  in der Vorschau aus.
  - Schriftart / Groesse / Fett / Kursiv wirken sofort auf das aktuell
    ausgewaehlte Etikett.
  - Header-Groesse / Header-Fett wirken sofort global auf die ganze Seite.
  - Seitenraender wirken sofort global.
- Guard `_suspendLiveApply` verhindert Doppel-Apply beim programmatischen
  Laden der Input-Felder (Label-Wechsel, Projekt-Laden, Reset).

---

## 2026-04-23 - Projekttag 3 (Feature: Header-Style getrennt)

### Feature
- **Header-Schrift separat konfigurierbar** — global pro Projekt.
  Bisher war die Kopfzeile auf den gleichen Style wie die Adressen
  gezwungen (Größe, Fett, Kursiv, Font). Jetzt:
  - `LabelSettings.HeaderFontSize` (Default 9 pt)
  - `LabelSettings.HeaderIsBold` (Default true)
  - Bezugsart: global, gilt fuer ALLE Etiketten/Module im Projekt
  - UI: Settings-Panel bekommt Bereich "Header" (Groesse + Fett-Checkbox)
  - Betrifft ET200SP (Vertikal-mit-Kopf) und ET200MP (Header-Zeile)

### Implementierung
- Models/LabelSettings: HeaderFontSize + HeaderIsBold + in Reset/Clone
- Services/PrintService: RenderHeader nimmt fontSize/isBold aus settings.
  PrintMp-Header rendert mit settings.HeaderFontSize/HeaderIsBold.
- ViewModels/MainViewModel: InputHeaderFontSize/InputHeaderIsBold als
  Eingabefelder; HeaderPreviewFontSize/HeaderPreviewFontWeight als
  Convenience-Bindings fuer die XAML-Preview.
- Controls/MpPreviewControl: Header-Zelle rendert mit Settings-Werten.
- MainWindow.xaml: Settings-Panel um Header-Schrift-Bereich erweitert.
- Alte Projekte: Defaults greifen automatisch beim Deserialisieren
  (keine explizite Migration noetig).

---

## 2026-04-23 - Projekttag 3 (Feature: Copy/Paste)

### Feature
- **Copy/Paste fuer Etiketten und Module** — app-intern (kein Windows-
  Zwischenablage-Austausch).
  - **ET200SP**: Etiketten auswaehlen und duplizieren (z.B. gleichartige
    Schaltschraenke mehrfach auf ein A4-Blatt).
  - **ET200MP**: Ganze Module mit Variante + AddressCells + NetAdr + CPU kopieren.
  - **Multi-Selection**:
    - Strg+Klick: Etikett/Modul zur Copy-Auswahl hinzufuegen/entfernen
      (orangefarbener Rahmen kennzeichnet markierte Elemente).
    - Shift+Klick: Bereich zwischen zuletzt ausgewaehltem und geklicktem
      Element markieren.
    - Normaler Klick: alle Markierungen aufheben.
  - **Bedienung**: Strg+C (Kopieren), Strg+V (Einfuegen), Toolbar-Buttons
    "Kopieren"/"Einfuegen", Rechtsklick-Kontextmenue auf ET200SP-Etiketten.
  - **Einfuegen**: Ueberschreibt ab dem Single-Selected-Etikett/Modul
    sequentiell; stoppt am Seitenende (keine automatische Seitenerweiterung).
  - **Umfang**: kompletter Inhalt (Text, Schrift, Druckmarke, bei MP die Variante).
  - **Modus-exklusiv**: Clipboard-Buffer fuer ET200SP und ET200MP sind
    getrennt; Wechsel leert den anderen.

### Implementierung
- Neu: Models/LabelCell.CloneContent, CopyFrom, MpModule.CloneContent, MpAddressCell.Clone
- Neu: Services/ClipboardService.cs (static, app-intern)
- LabelViewModel/MpModuleViewModel: IsChecked-Property + SetCell/SetModule
- MainViewModel: CopyCommand, PasteCommand mit GetCopySource*-Helpern
  (Multi-Select falls IsChecked, sonst Fallback auf Single-Selected)
- MainWindow.xaml.cs: Label_Click mit Modifier-Key-Detection
- MpPreviewControl.xaml.cs: SelectCell mit Modifier-Key-Detection
- XAML: DataTrigger fuer IsChecked (oranger Rahmen), Toolbar, ContextMenu
- 7 neue Unit-Tests (Clone + ClipboardService), 45/45 gruen.

---

## 2026-04-23 - Projekttag 3 (Feature: Blanko A4)

### Fix
- Eingabe-Tabs (Generator, Manuell, MP Modul) waren im S7-1500/ET200MP-Modus
  komplett deaktiviert. Ursache: TabControl-IsEnabled war an SelectedLabel
  gebunden, das im MP-Modus immer null bleibt. Neue Property HasSelection
  deckt beide Modi ab (SelectedLabel OR SelectedMpModule).
- MP-Preview refreshte nicht nach Generator/Apply/Header-Aenderung. Das
  Canvas-basierte MpPreviewControl rendert manuell und hoerte nur auf
  Module-/Selection-Wechsel — Zell-PropertyChanged allein triggerte kein
  Re-Render. Refresh-Token in MainViewModel eingefuehrt; Control hoert
  zusaetzlich auf MpPreviewRefreshToken.
- Nach Generate im MP-Modus wird nun zum naechsten Modul gesprungen
  (analog AdvanceToNextLabel bei ET200SP). Am Ende der Seite wird zur
  naechsten Seite gewechselt. Adressen werden NICHT automatisch neu
  generiert — User prueft Variante + Start-Byte und klickt erneut
  "Generieren + Uebertragen".
- ET200SP Analog-Adressen jetzt alternierend wie digital verteilt:
  Ungerade Kanaele (K1, K3, K5, K7) in Line1 (oben), gerade (K0, K2,
  K4, K6) in Line2 (unten). Fix 8 Plaetze pro Reihe; nicht genutzte
  Plaetze als leere Slots. Beispiel AI 8xI (8 Kanaele):
    Oben:  EW 2   EW 6   EW 10  EW 14  (leer) (leer) (leer) (leer)
    Unten: EW 0   EW 4   EW 8   EW 12  (leer) (leer) (leer) (leer)
  Die 8 Plaetze entsprechen den Klemmen pro Reihe auf der BU A0/A1.
- LabelViewModel.Line1Parts/Line2Parts: StringSplitOptions.None statt
  RemoveEmptyEntries, damit leere Slots in der Reihe erhalten bleiben.

### Feature
- **Blanko-A4-Druck**: Checkbox "Gitterlinien drucken (Normalpapier)" umbenannt zu
  "Blanko A4 (alle Rahmen drucken)". Aktiviert druckt auf leeres A4-Papier
  (z.B. 120g Karton, hellblau = Standard-Baugruppen, gelb = Safety-Baugruppen)
  mit kompletten Schnittkanten + Adress-Rahmen + Kopfzeilen-Rahmen.
- Rahmenfarbe von Hellgrau auf Schwarz: Schnittkanten 0.5pt, innere Rahmen 0.3pt.
- Position der Rahmen stimmt 1:1 mit der Vorschau ueberein (Preview = Druck).

### Fix
- Schnittkanten wurden urspruenglich fuer ALLE 100 Etiketten gedruckt, auch leere.
  Jetzt konsistent zu inneren Rahmen: nur befuellte und druckaktive Etiketten bekommen
  Schnittkanten. Bei ET200MP zusaetzliches `!mod.HasText continue;` neben
  `!IsPrintEnabled`.

---

## 2026-04-23 - Projekttag 3 (Code-Qualitaet)

### Bugfixes (Session Code-Review)
- CalibrationService: bare catch{} durch getypte Exception-Filter ersetzt; Save() meldet Fehler via StatusMessage zurueck
- ProjectService: bare catch{} getypt; Load() prueft File.Exists vorab und erzwingt 50MB File-Size-Cap gegen JSON-DoS
- TestAutomationService.Dispose(): wartet bis 2s auf ListenLoop-Completion vor CTS.Dispose, vermeidet Race beim Shutdown
- TestAutomationService.RunOnUI: voller Stacktrace ins Log, Exception-Typ in Response
- TestAutomationService.ResizeWindow: Bounds-Check (400..10000), InvariantCulture beim Parsen
- TestAutomationService: neue ValidateProjectPath() fuer save/load-project (erzwingt .etprint-Extension)

### Neue Features / Hardening
- **Logger**: neuer Log-Service (`Services/Log.cs`) schreibt nach `%LOCALAPPDATA%\ETPrinter\log.txt` mit 1MB-Rotation zu `.old.txt`. Thread-safe, IO-sicher mit Debug.WriteLine-Fallback. Debug.WriteLine-Calls in 3 Services migriert.
- **Test-Automation-Flag**: Named-Pipe-Server startet nur bei `--test-automation` CLI-Arg oder `ETPRINTER_TEST=1` ENV-Var. Reduziert Attack-Surface in Production.
- **Regex-Hardening**: alle 4 Patterns in SchematicParserService mit `RegexOptions.NonBacktracking` + 1s Timeout gegen pathologische PDF-Inhalte.
- **Unit-Tests**: neues Projekt `tests/ETPrinter.Tests` (xUnit, net9.0-windows), in Solution integriert. 41 Tests fuer AddressGenerator (digital/analog Adressgenerierung, odd/even-Split, Byte-Stepping) und MpModuleLayoutFactory (Half==0-Invariante fuer alle 6 Varianten).

### Code-Decisions
- Half-1 Rendering-Pfad bleibt erhalten (Unit-Test pinnt Half==0-Invariante). Entfernen waere ohne Rendering-Tests zu riskant; Git-History bewahrt Code falls spaeter 2-Half-Layouts gebraucht werden.

---

## 2026-03-23 - Projekttag 2

### Phase 12: Architektur-Korrektur + Generator-Fix (v2.5)
- ARCHITEKTUR-FIX: 5 Module pro Seite (nicht 10)
  - Band 1 + Band 2 derselben Spalte = EIN physischer Beschriftungsstreifen
  - MpCellDefinition mit Half-Feld (0=obere Haelfte, 1=untere Haelfte)
- EIN zusammenhaengender Streifen in Preview und Druck
  - Header nur einmal oben (nicht mehr doppelt)
  - Trennlinie statt zweiter Header zwischen den Haelften
- Adress-Generator fuer ET200MP komplett neu:
  - Sequenzielle Bit-Adressen (E x.0 bis E x.7) statt ET200SP odd/even Split
  - Korrekte Byte-Zuordnung nach physischer Klemmenbelegung (0, 2, 1, 3)
  - Gruppe a (Byte 0) oben-links, Gruppe c (Byte 2) oben-rechts
  - Gruppe b (Byte 1) unten-links, Gruppe d (Byte 3) unten-rechts
- DI 32x24VDC HF Klemmenbelegung aus Siemens Equipment Manual
  - 40 Klemmen, CH0-CH31, M/L+/GND Positionen als Strukturzellen
  - Strukturzellen zeigen Labels (M, L+, 1M, 2L+, MANA)
- AI 8xU/I/RTD/TC ST Klemmenbelegung dokumentiert (4 Zeilen pro Kanal)
- AQ 4xU/I ST Klemmenbelegung dokumentiert (nur linke Seite benutzt)
- TestAutomation: zoom, maximize, resize Befehle
- Alle S7-1500 Modultypen in PRINT-FORMATS.md katalogisiert (DI/DQ/AI/AQ/SIWAREX)

### Phase 11: ET200MP modulbasiertes Layout (v2.3)
- MpModule Datenmodell (HeaderText, AddressCells, NetAddress1-4, CpuName)
- 6 MpModuleVariant mit deklarativen Zellen-Merge-Definitionen
- MpModuleLayoutFactory: exakte Zellenstruktur pro Variante aus Excel
- Canvas-basiertes MpPreviewControl (statt UniformGrid)
- IsModuleBased-Flag als Weiche zwischen ET200SP- und ET200MP-Codepfad
- Modul-Editor Tab (Variante, Header, Adressen, NetAddr, CpuName)
- PrintService: PrintMp() mit modulbasiertem Rendering
- Speichern/Laden v4 mit MpPages, Zellen-Persistenz
- Paralleles Datenmodell — ET200SP komplett unveraendert

### Phase 10: S7-1500 / ET200MP Grundgeruest (v2.2)
- ProductFamily-Konzept eingefuehrt (ET200SP, S71500_ET200MP, S71500_ET200MP_25mm)
- ProductFamilyInfo Record mit Massen, Raendern, Beschriftungsbogen-Artikelnr
- 4 neue LabelFormat-Enum-Werte (MP_Horizontal, MP_Vertical, MP25_Horizontal, MP25_Vertical)
- FormatInfo erweitert: BandsPerPage, ChannelRowsPerBand, Family
- ProductFamily-ComboBox in der UI mit automatischer Format-Filterung
- Default-Raender wechseln pro Produktfamilie (ET200MP: O:14 L:25 U:19 R:12)
- A4-Vorschau: 2-Band-Layout fuer ET200MP (40 Zeilen = 2x20)
- PrintService: 2-Band-Rendering mit Header/Separator-Berechnung
- Speichern/Laden: v3 Format mit ProductFamily, v2-Migration
- ResetSettings beruecksichtigt aktuelle ProductFamily
- Format-Family-Validierung beim Laden
- Beschriftungsbogen-Nr in Statusleiste
- Test-Automation: Named-Pipe-Server (TestAutomationService)
  - Befehle: ping, state, screenshot, select-family/format/label, set-text, generate, navigate
  - PowerShell-Client (test-send.ps1)
- Geschaetzte Masse aus Excel-Template (werden spaeter mit Stahllineal korrigiert)
- Excel-Templates analysiert: Siemens Beitrags-ID 83681795
  - Excel_Template_S71500_ET200MP.xls (12 Sheets, 6 Modultypen x H/V)
  - Excel_Template_S71500_ET200MP_25mm.xls (5 Sheets)
  - Beschriftungsboegen: 6ES7592-1AX00-0AA0, 6ES7592-2AX00-0AA0

---

## 2026-03-12 - Projekttag 1

### Phase 0: Planung & Setup
- Siemens PDF-Dokumentation (Beitrags-ID 81524595) analysiert
- Excel Template (Excel_Template_ET200SP.xls) analysiert: 6 Sheets, Zellengroessen, Spaltenbreiten, Zeilenhoehen, Textrotation
- Physisches Etikettenblatt identifiziert: 6ES7193-6LA10-0AA0 (12,8mm x 31mm, 100 Stueck pro A4)
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
- Standard-Seitenraender vereinheitlicht (20.5/27.5/20.5/27.5mm = exakt 12.8x31mm Etiketten, gemessen)
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

### v2.2.0 - v2.5 [DONE] (2026-03-23)
- [x] Produktfamilien S7-1500/ET200MP 35mm + 25mm, MP-Formate, Band-Layout
- [x] Modulbasiertes Layout (MpModule, Varianten, MpPreviewControl), Persistenz v4
- [x] Test-Automation (Named Pipe)

### v3.0 [DONE] (2026-07-02 bis 2026-08-04)
- [x] 10 Streifen-Positionen pro A4, Persistenz v5
- [x] Modul-Katalog mit datenblattverifizierten Klemmenbelegungen, 25mm-Template
- [x] Copy/Paste, Header-Schrift, Live-Vorschau, Blanko-A4, Review-Fixes, 109 Tests

### v3.1.0 - Abschluss [DONE] (2026-09-09, ABSCHLUSSPLAN AP0-AP7)
- [x] Testinfrastruktur (render-print, smoke.ps1, Publish-Profil)
- [x] 12 kritische + 20 mittlere Bugs (Druck, Datenverlust, MP-Modus, UX)
- [x] SheetGeometry (Preview = Druck), Import-Robustheit, Komfort/UX
- [x] Doku konsolidiert, README, Release-EXE, GitHub-Release

### v3.1.1 - AP8/AP9 [DONE] (2026-09-09)
- [x] 25mm-BA-Module datenblattverifiziert, Katalog 18 Eintraege
- [x] MainViewModel zerlegt (6 Sub-Objekte + IDialogService)
- [x] Undo/Redo (Ctrl+Z/Ctrl+Y), inkrementelle MP-Vorschau
- [x] 284 Tests, Smoke 98/98

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
| 2026-03-23 | Projekttag 2: ET200MP-Familien, modulbasiertes Layout |
| 2026-04-23 | Projekttag 3: Blanko-A4, Copy/Paste, Header-Schrift, Live-Vorschau, 41 Tests |
| 2026-07-02 | Projekttag 4: Bugfix-Review (23 Fixes), v3.0 10 Positionen |
| 2026-07-03 | Projekttag 5: Modul-Katalog, Datenblatt-Verifikation, 25mm-Template, 96 Tests |
| 2026-08-04 | Projekttag 6: Katalog-Lueckenschluss, typabhaengige Struktur-Labels, 109 Tests |
| 2026-09-09 | Projekttag 7: Vollreview + Abschlussplan AP0-AP9, v3.1 (210+ Tests, Smoke 74/74) |
| 2026-09-09 | Projekttag 7 (Forts.): AP8/AP9 - 25mm-Katalog, MainViewModel-Zerlegung, Undo/Redo, inkrementelle MP-Vorschau, v3.1.1 (284 Tests, Smoke 98/98) |
