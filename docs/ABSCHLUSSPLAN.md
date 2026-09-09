# ET-Printer – Abschlussplan

Stand: 2026-09-09. Grundlage: Doku-Code-Abgleich, Bughunt Services/Models (22 Befunde),
Bughunt ViewModels/UI (27 Befunde, 20 Verbesserungsvorschläge), Build + 109 Tests grün.

Ziel: Das Projekt in einen Zustand bringen, in dem es als **v3.1 „Release“** abgeschlossen
werden kann. Danach bleiben nur noch hardwareabhängige Feinmaße (Stahllineal) offen.

## Entscheidungen (mit Daniel abgestimmt, 2026-09-09)

| Thema | Entscheidung |
|-------|--------------|
| Deploy-Ziel | Nur `publish/ET-Printer.exe` im Repo (dotnet publish, single-file, win-x64). Kein Kopieren auf andere Rechner. |
| Git | Ein Commit pro abgeschlossenem AP auf `master`, sofort Push nach GitHub. AP7 zusätzlich GitHub-Release v3.1.0 mit EXE. |
| Icon | Eigenes Symbol: stilisierter Beschriftungsstreifen mit Etikettenraster in DASA-Blau #2b58a3, programmatisch als ICO 16–256 px. |
| Hardware (AP8) | Bögen liegen nicht vor. AP8 = nur MP25_32-Datenblatt-Verifikation über Siemens-Online-Doku, Stahllineal-Maße bleiben offen. |
| Refactoring (AP9) | MainViewModel vollständig zerlegen (6 Schnitte + IDialogService), abgesichert durch Tests aus AP0–AP3. |
| Import im MP-Modus | Implementieren: PDF-Import erzeugt je erkanntem Modul ein MP-Modul und füllt es über den Generator. CSV/Excel bleiben im MP-Modus gesperrt. |
| .etprint-Zuordnung | Script `publish/register-etprint.ps1` (HKCU), kein automatischer Registry-Eintrag. |
| Einstellungen-Panel | Umbau in „Seite“ (Header-Schrift, Ränder, Kalibrierung, Blanko) und „Ausgewähltes Etikett/Modul“ (Schrift) mit „Auf alle anwenden“; „Übernehmen“-Button entfällt. |

## Ausführungsprotokoll (autonom, je AP)

1. Änderungen umsetzen, Unit-Tests für jeden Fix ergänzen.
2. `dotnet build` + `dotnet test` grün.
3. `dotnet publish` nach `publish/` (Deploy).
4. Smoke-Test gegen die veröffentlichte EXE über Test-Automation (`tools/smoke.ps1`):
   Screenshots SP/MP/25mm, Druckseiten als PNG rendern (`render-print`), Persistenz-Roundtrip.
5. Abweichung gefunden: korrigieren, zurück zu Schritt 2. Erst bei grün weiter.
6. TODO.md + CHANGELOG.md nachziehen, Commit, Push.
7. Nächstes AP.

Nach AP9: Gesamt-Doku (AP6 hat den Endstand bereits, Kontrolle) und Abschluss-Memory.

## Definition „abgeschlossen“

1. Kein bekannter Bug, der zu falschem/fehlendem Druck oder stillem Datenverlust führt.
2. Preview = Druck, aus einer gemeinsamen Geometrie-Quelle.
3. Doku beschreibt den tatsächlichen Code (kein ET200SP-only-Stand von März mehr).
4. Eine Versionsnummer überall (csproj, Info-Dialog, CHANGELOG, GitHub-Release).
5. Installierbares Release-Artefakt (Single-File-EXE) + README.
6. Tests decken Druckgeometrie, Generator je Variante, Migration v1–v5, Import ab.

---

## Arbeitspakete

| AP | Inhalt | Aufwand | Blockiert durch |
|----|--------|---------|-----------------|
| AP0 | Testinfrastruktur: TAS-Befehle `render-print`, `quit`, `is-dirty`; `tools/smoke.ps1`; Publish-Profil | 0,5 Sitzung | – |
| AP1 | Kritische Bugs: falscher/fehlender Druck, Datenverlust | 1 Sitzung | – |
| AP2 | Mittlere Bugs + Laufzeit-Verifikation (Ctrl+C/V, Kontextmenü) | 1 Sitzung | – |
| AP3 | Geometrie-Klasse Preview = Druck + Tests | 1 Sitzung | – |
| AP4 | Import-Robustheit (CSV/Excel/PDF) | 0,5 Sitzung | – |
| AP5 | Komfort/UX (Rückfragen, Fensterzustand, Tastatur, Zoom-Fit, Drag&Drop) | 1 Sitzung | – |
| AP6 | Doku-Konsolidierung + Version + README | 0,5 Sitzung | AP1–AP5 (Endstand) |
| AP7 | Release-Build (Icon, Publish-Profil, NuGet, GitHub-Release, .etprint-Zuordnung) | 0,5 Sitzung | AP6 |
| AP8 | Hardware: Stahllineal-Maße, Feinabstimmung MP-Varianten, MP25_32-Belegung | offen | Beschriftungsbögen 6ES7592-1AX00 / -2AX00 |
| AP9 | Optional: Katalog-Rest (5 Analogmodule), MainViewModel-Zerlegung, Undo/Redo | 1–2 Sitzungen | – |

Reihenfolge: AP0 → AP1 → AP2 → AP3 → AP4 → AP5 → AP6 → AP7 → AP8 (nur Datenblatt) → AP9.

---

## AP0 – Testinfrastruktur

1. TAS-Befehl `render-print <ordner>`: rendert alle Druckseiten (SP und MP, gleicher Code wie
   `Print`, ohne Dialog) per `RenderTargetBitmap` als PNG `page_01.png` … und liefert die
   Seitenzahl. Damit ist der Druckpfad ohne Drucker prüfbar und mit dem Preview-Screenshot vergleichbar.
2. TAS-Befehle `quit` (sauberes Beenden ohne Rückfrage), `is-dirty`, `page-count`.
3. `tools/smoke.ps1`: startet `publish/ET-Printer.exe --test-automation`, führt Szenarien aus
   (SP-Generator, MP-Katalog, 25mm, SIWAREX, Speichern/Laden, render-print), legt Screenshots
   und PNGs unter `test_results/<AP>/` ab, beendet die App, Exit-Code ≠ 0 bei Fehler.
4. Publish-Profil `src/ETPrinter/Properties/PublishProfiles/win-x64.pubxml`.

---

## AP1 – Kritische Bugs (falscher Druck / Datenverlust)

Alle Befunde im Code bestätigt.

| # | Stelle | Befund | Fix |
|---|--------|--------|-----|
| 1.1 | MainViewModel.cs:1490 / :359 | „Neu“ setzt `SelectedFormat` auf Default. Ist es schon aktiv, kehrt der Setter sofort zurück, `InitializeLabels` läuft nie, `IsDirty=false`. Alte Etiketten bleiben, gelten als gespeichert. | In `NewProject` immer `InitializeLabels()` (+ MP-Seiten) aufrufen, unabhängig vom Format. |
| 1.2 | PrintService.cs:548 + MpModule.cs:52 | `HasText` zählt nur editierbare Zellen. SIWAREX (alle 40 Zellen fest) ohne Header wird in der Preview gezeigt, im Druck übersprungen. | `HasPrintableContent(definitions)`: HasText oder mindestens ein festes Label. Preview und Druck dieselbe Methode. |
| 1.3 | MpModuleViewModel.cs:70 | Variantenwechsel löscht `ArticleNumber` nicht. `GetDefinitions` fällt still auf generisch zurück, UI/Datei zeigen weiter den Artikel. | Im Variant-Setter: passt `Catalog.Find(ArticleNumber)?.Variant` nicht, `ArticleNumber = null` + Notify. |
| 1.4 | PrintService.cs:38 / :503 + MainViewModel.cs:1117 | Leere Seiten werden gedruckt (Kommentar sagt „skip“, Code nicht). Auto-Advance legt nach dem letzten Etikett immer eine Leerseite an, praktisch jeder Druck endet mit Leerbogen. | Seiten ohne druckbaren Inhalt überspringen; bei 0 Seiten Meldung statt Leerauftrag; `Print` gibt `bool` zurück (Abbruch ist nicht „gesendet“). |
| 1.5 | MainViewModel.cs:2013 + AddressGenerator.cs:41 | PDF-Import kappt 32-Kanal-Module still auf 2 Bytes (ein Etikett statt zwei). | In Blöcken von `GetEffectiveCount` iterieren, je Block ein LabelCell; `MergeAddressLines` auch im Import. |
| 1.6 | ProjectService.cs:24 / CalibrationService.cs:44 | Nicht-atomares Speichern (`WriteAllText` direkt). Absturz beim Schreiben zerstört die Projektdatei. | In `.tmp` schreiben, dann `File.Move(overwrite)`; vorherige Version als `.bak`. |
| 1.7 | App.xaml.cs | Kein globaler Exception-Handler, obwohl `Log` existiert. Jede Laufzeit-Exception = Absturz ohne Log. | `DispatcherUnhandledException` + `AppDomain.UnhandledException`: loggen, MessageBox, Notfall-Speicherung `%LOCALAPPDATA%\ETPrinter\recovery.etprint`. |
| 1.8 | MainViewModel.cs:245 / MpModuleViewModel.cs:235 | `SelectedMpCell` wird bei Modulwechsel und bei Zell-Neuaufbau (Variante/Artikel) nicht zurückgesetzt. Eingabe landet unsichtbar im falschen/abgehängten Modul. | Im `SelectedMpModule`-Setter `SelectedMpCell = null`; nach Rebuild per `CellIndex` neu auflösen. |
| 1.9 | MainViewModel.cs:1352 | Schrift-Einstellungen (Größe/fett/kursiv/Font) wirken im MP-Modus nicht (`MpModuleViewModel.FontSize` wird nie gesetzt). Vier Bedienelemente sind Leerlauf. | `ApplyInputFontToSelected` auch auf `SelectedMpModule`; beim Modulwechsel Inputs aus dem Modul laden. |
| 1.10 | MainViewModel.cs:1952 | CSV/Excel/PDF-Import schreibt im MP-Modus in unsichtbare SP-Seiten, meldet Erfolg, Datei enthält Geisterseiten. | CSV/Excel: `CanExecute = !IsModuleBased` mit Tooltip. PDF: im MP-Modus je erkanntem Modul ein MP-Modul (Header = Modulname, Generator mit Typ/StartByte/Bytes), Seiten bei Bedarf anlegen. |
| 1.11 | MainViewModel.cs:1614 | `ApplyLoadedProject` aktualisiert `AvailableMpVariants`/`AvailableMpArticles` nicht. Nach Laden eines 25mm-Projekts zeigen ComboBoxen die falsche Familie. | `ApplyFamily(family)` als gemeinsame Methode für Setter, NewProject, ApplyLoadedProject. |
| 1.12 | MpModuleViewModel.cs:242 | Text-Migration bei Variantenwechsel kopiert in nicht editierbare Zellen. Unsichtbare Geistertexte, `HasText` true. | Nach Migration Texte in `!IsEditable`-Zellen leeren. |

Tests dazu: NewProject-Reset, HasPrintableContent (SIWAREX), Variant-Setter löscht Artikel,
Leerseiten-Skip, PDF-Import 32 Kanäle ergibt 2 Etiketten, atomares Speichern.

---

## AP2 – Mittlere Bugs + Laufzeit-Verifikation

Zuerst App starten und die zwei „plausibel“-Befunde in 5 Minuten prüfen:

| # | Stelle | Befund | Prüfung / Fix |
|---|--------|--------|---------------|
| 2.1 | MainWindow.xaml:20 | Window-KeyBinding Ctrl+C/V fängt Tastatur in TextBoxen ab (Text lässt sich nicht kopieren, stattdessen Etikett). | Prüfen: Ctrl+V in „Modulname“. Fix: `CanCopy` false wenn `Keyboard.FocusedElement is TextBoxBase`. |
| 2.2 | MainWindow.xaml:537 | Kontextmenü-Commands binden `AncestorType=Window`. ContextMenu hängt nicht im Tree, Command = null. | Prüfen: VS-Output „Cannot find source for binding“. Fix: `BindingProxy`-Resource; Rechtsklick selektiert Etikett. |

Dann ohne Laufzeitabhängigkeit:

| # | Stelle | Befund | Fix |
|---|--------|--------|-----|
| 2.3 | MainViewModel.cs:1295 / :893 | „Alle löschen“ und „- Seite“ ohne Rückfrage, kein Undo. | Ja/Nein-Rückfrage wenn Inhalt vorhanden. |
| 2.4 | MainViewModel.cs:187 | Familienwechsel setzt Header-Font zurück, notifiziert Input-Felder nicht (ComboBox zeigt 6, Druck nutzt 9 fett). | Nur Ränder zurücksetzen oder alle Inputs synchronisieren. |
| 2.5 | MainViewModel.cs:2067 | Druckauswahl-Menü ignoriert MP-Module; kein UI für `MpModule.IsPrintEnabled`. | MP-Zweig + Checkbox „Modul drucken“ + Kontextmenü auf Canvas. |
| 2.6 | MainViewModel.cs:390 | Klick auf bereits markiertes Etikett entfernt die Markierung. | `ReferenceEquals`-Check vor dem Setter. |
| 2.7 | MainWindow.xaml.cs:59 / MainViewModel.cs:672 / :366 | `IsPrintEnabled` per Kontextmenü, `PrintGridLines`, Format-/Familienwechsel setzen `IsDirty` nicht. | Alle drei Pfade dirty markieren. |
| 2.8 | MainViewModel.cs:1778 | Kalibrierung wird vor dem Druckdialog gespeichert (auch bei Abbruch); Status meldet „gesendet“. | Erst nach erfolgreichem `PrintDocument` speichern. |
| 2.9 | MainViewModel.cs:1586 | Gelöschte Recent-Datei bleibt; Ladefehler nur in Statusleiste; Recent-Eintrag vor Erfolg. | Rückfrage „Eintrag entfernen?“, MessageBox, Eintrag nach Erfolg. |
| 2.10 | MainViewModel.cs:1051 | Generator-Statusmeldung wird sofort vom Auto-Advance überschrieben. | Status nach `AdvanceToNextLabel` setzen. |
| 2.11 | MainViewModel.cs:1216 | Ctrl+Klick-Auswahl enthält Anker-Etikett nicht; Paste am Seitenende stumm. | Anker mit-checken; „N von M eingefügt (Seitenende)“. |
| 2.12 | MainViewModel.cs:526 | DI-zu-AI-Wechsel: `GenCount=1` nicht in [2,4,8], ComboBox leer. | Bei Typwechsel auf ersten gültigen Wert. |
| 2.13 | MainViewModel.cs:913 / MainWindow.xaml:231 | „Manuell“-Tab im MP-Modus irreführend; Infozeile „200 Etiketten“. | Tab im MP-Modus ausblenden; Info „Modul 3/10, Band 1“. |
| 2.14 | PrintService.cs:529 | `MarginBottom` bei MP wirkungslos; toter MP-Zweig in `GetCellSize` (5,44 statt 5,6 mm) ist Wartungsfalle. | Feld im MP-Modus sperren; toten Zweig entfernen (in AP3). |
| 2.15 | PrintService.cs:31 | Kein `PrintTicket.PageMediaSize = ISOA4`/Portrait, Letter-Drucker skalieren. | Nach `ShowDialog` setzen. |
| 2.16 | PrintService.cs:205 + AddressGenerator.cs:56 | Horizontale Formate: 2-Byte-Adressstring mit Leerslots (~50 Zeichen) wird mit „…“ abgeschnitten. | Leerslots im SP-Horizontal-Pfad entfernen, Strings trimmen. |
| 2.17 | PrintService.cs:564 / MpPreviewControl.cs:221 | Mehrzeiliger MP-Header wird zu „ / “ geflattet, ab ~20 Zeichen „…“. | `TextWrapping.Wrap`, `MaxHeight = HeaderH`, Zeilenumbrüche behalten. |
| 2.18 | MpPreviewControl.xaml.cs:44 | Jeder Tastendruck = kompletter Canvas-Neuaufbau (1000–1700 Elemente); Seitenwechsel 11–21 Renders; Event-Abo ohne Abmeldung. | `DispatcherTimer`-Debounce 50 ms; Bulk-Replace; Abmeldung in `BindToModules`. |
| 2.19 | TestAutomationService.cs:320 | `apply` No-op, `new-project` = ClearAll, `help` unvollständig, `set-text` umgeht IsDirty. | Auf VM-Commands umstellen, Help aus Dispatch-Tabelle. |
| 2.20 | ProjectService.cs:44 / :77 | `Pages:null`/`Settings:null` ergibt NRE; Migration v4-zu-v5 ignoriert Familie (DI_DQ_16 statt MP25_16); ModuleIndex nur im Migrationszweig normalisiert. | Null-Defaults; `DefaultVariantFor(family)`; Normalisierung immer. |

---

## AP3 – Geometrie-Klasse: Preview = Druck

Heute dreifach dupliziert: `MpPreviewControl.Render`, `PrintService.CreateMpPage`,
`PrintService.PrintCalibrationPage`; SP-Preview hat feste 18 px Header-Spalte gegen 20 %
im Druck (ca. 2 % Abweichung).

1. `Services/SheetGeometry.cs` (mm-basiert): `CellRect(def, moduleIndex)`, `HeaderRect(band)`,
   `SpCellRect(index, format)`, `PageSize`, `Margins`. Eine Instanz je Familie + Settings.
2. `MpPreviewControl`, `CreateMpPage`, `PrintCalibrationPage` und die SP-XAML-Preview
   (per Binding) nutzen ausschließlich diese Klasse.
3. Toten MP-Zweig aus `FormatDefinitions.GetCellSize`, `CreatePage`, `PrintCalibrationPage` entfernen.
4. `ChannelRowsPerBand` vs. `LabelRows` vereinheitlichen (heute zufällig beide 20).
5. Tests: für jedes Format/jede Variante Preview- und Druckkoordinaten identisch; SP Position 1
   = unten rechts; Band 2 Header 20,6 mm; 25mm 20 Positionen. Golden-Tests je Variante für den
   MP-Generator (Byte-Verteilung DQ_8_2A, DI_230V_16, MP25_16/32).
6. Migrationstests v1, v2, v3 ergänzen (heute nur v4/v5); Roundtrip mit ArticleNumber, IoType,
   IsPrintEnabled, PrintGridLines.

---

## AP4 – Import-Robustheit

| # | Datei | Befund | Fix |
|---|-------|--------|-----|
| 4.1 | CsvImportService.cs:206 | Zeilenumbruch in quoted Feld zerreißt Datensatz. | Zeichenweiser Parser über den ganzen Text. |
| 4.2 | CsvImportService.cs:192 | UTF-16 (Excel „Unicode Text“) wird als UTF-8 akzeptiert, Ergebnis Müll; Tab nicht als Trenner. | BOM-Sniffing (FF FE / FE FF / EF BB BF); Tab in Trenner-Erkennung. |
| 4.3 | ExcelImportService.cs:311 | `Worksheets.First()` auch bei verstecktem Deckblatt; Zahl/Datum kulturabhängig („1.1“ wird 01.Jan). | Erstes sichtbares Blatt mit `RangeUsed`; `GetFormattedString`/Invariant. |
| 4.4 | SchematicParserService.cs:22 | Adress-Regex ohne Wortgrenze („SIZE 1.5“ matcht), StartByte = erste statt kleinste Adresse, kein I/Q. | Lookbehind `(?<![A-Z])`, `Min()`, optional I/Q/IW/QW-Mapping. |
| 4.5 | MainViewModel.cs:1846 | Parser laufen synchron im UI-Thread. | `async` + Wartecursor. |
| 4.6 | Tests | CSV BOM/CRLF/escaped Quotes/Leerzeilen, Excel, PDF-Parser: 0 Tests. | Fixture-Dateien + Tests. |

---

## AP5 – Komfort / UX

| # | Vorschlag | Aufwand |
|---|-----------|---------|
| 5.1 | Fensterzustand (Größe, Position, Zoom, Splitter) in `%LOCALAPPDATA%\ETPrinter\ui.json`. | S |
| 5.2 | `.etprint` per Drag&Drop und Kommandozeilenargument öffnen. | S |
| 5.3 | Tastatur: Enter in Zeile 1/2 = Übertragen, Enter in Start-Byte = Generieren, Entf leert Etikett, Ctrl+Shift+S, F5 Drucken. | S–M |
| 5.4 | Zoom: „An Fenster anpassen“ + Ctrl+Mausrad; Startzoom aus Fensterhöhe (heute immer Scrollen). | S |
| 5.5 | Validierung: Ränder 0–60 mm, Kalibrierung ±10 mm, Start-Byte ≥ 0, Tooltip statt Absturz. | S |
| 5.6 | Einstellungen-Panel: „Global“ (Header, Ränder, Blanko) vs. „Etikett“ (Schrift) + „Auf alle anwenden“; „Übernehmen“-Button entfernen (alles Live). | S |
| 5.7 | MP-Modus: beim Wechsel in MP-Format automatisch Tab „MP Modul“; Fonts lazy laden. | S |
| 5.8 | Druck: „Nur aktuelle Seite“ / Seitenbereich; Rückfrage „2 von 3 Seiten drucken?“. | S |
| 5.9 | Info-Dialog: Version aus Assembly, Text ET200SP + ET200MP. | S |

---

## AP6 – Doku-Konsolidierung + Version

| Datei | Aktion |
|-------|--------|
| ARCHITECTURE.md | Neu schreiben: .NET 9, C# 13, PdfPig + ClosedXML, reale Struktur (Models 9, ViewModels 6, Services 12, Controls, Views, Tests), Phantomdateien raus, 10 Formate, Ränder 20,5/27,5, alle Formate 5×20. |
| FEATURES.md | Neu schreiben: F01–F09 aktualisieren (PDF-Parser ist da, Schrift 4–10, Ränder mm), ergänzen F10 ET200MP/25mm, F11 Katalog, F12 Copy/Paste, F13 Kalibrierung, F14 CSV/Excel-Import, F15 Header-Style, F16 Einzeldruck, F17 Blanko-A4, F18 Test-Automation. |
| PRODUCT.md | Vision auf ET200SP + S7-1500/ET200MP erweitern, Kernfunktionen ergänzen, Papiersorten je Familie. |
| PRINT-FORMATS.md | Widerspruch 5 vs. 10 Module auflösen (Zeile 164–166, 198), „Aktueller Stand (TODO)“ bereinigen, Varianten 9 statt 6, Katalog-Tabelle 10 Einträge, Zeilenhöhe 5,6 mm fest. |
| TODO.md | Phase 12 als „überholt durch Phase 19“ kennzeichnen; AP1–AP7 dieses Plans als Phasen 21–27 eintragen; Abschlusskriterien. |
| CHANGELOG.md | Versionsplan bis v3.1 fortschreiben, Meilensteine 2026-03-23 bis 2026-09, Kalibrierpfad korrigieren. |
| README.md | Neu: Zweck, Screenshot, Installation, Bedienung in 10 Zeilen, Bögen-Artikelnummern, Build/Test. |
| Code-Kommentare | MpModule.cs:94, MpModuleViewModel.cs:68, FormatDefinitions.cs:52 („5 Module“) korrigieren. |
| Version | csproj `<Version>3.1.0</Version>`, `<Description>` mit ET200MP, Info-Dialog aus Assembly. |

---

## AP7 – Release-Build

1. App-Icon (`ETPrinter.ico`, generiert: Beschriftungsstreifen-Raster in DASA-Blau) + `<ApplicationIcon>`.
2. Publish-Profil aus AP0 verwenden, Ausgabe `publish/ET-Printer.exe` (ist bereits Deploy-Ziel).
3. NuGet: ClosedXML 0.105.1, PdfPig 0.1.16, Tests grün.
4. `publish/register-etprint.ps1` für die `.etprint`-Zuordnung (HKCU, manuell auszuführen).
5. GitHub-Release v3.1.0 mit EXE + CHANGELOG-Auszug.
6. Smoke-Test der Release-EXE über Test-Automation (Screenshot SP + MP + 25mm).

---

## AP8 – Hardware-abhängig (aus TODO übernommen)

| Aufgabe | Voraussetzung | In diesem Durchlauf |
|---------|---------------|---------------------|
| Exakte Maße 6ES7592-1AX00 (35 mm) und -2AX00 (25 mm) per Stahllineal | Bögen geliefert | nein, bleibt offen |
| Feinabstimmung aller ET200MP-Varianten gegen physische Bögen (Testdruck + Kalibrierseite) | Bögen geliefert | nein, bleibt offen |
| MP25_32-Klemmenbelegung am Datenblatt verifizieren (Befund: Byte 2 läuft in Spalte 0 Zeile 16–19, Spalte 1 um 4 Zeilen verschoben; GenCount 5 zulässig) | Siemens-Online-Doku | ja: Datenblatt holen, Generator spaltenweise füllen, Test |

---

## AP9 – Erweiterungen (vollständig umsetzen, Entscheidung 2026-09-09)

| Aufgabe | Nutzen | Aufwand |
|---------|--------|---------|
| Katalog-Rest: DQ 16 BA, AI 8 HF/BA/ST, AQ 4 ST | Auswahl per Modulname statt Variante | S |
| MainViewModel-Zerlegung (PageDocument, AddressGeneratorVM, SettingsVM, ProjectSession, ImportCoordinator, MpEditorVM, IDialogService) | ca. 600 statt 2148 Zeilen, VM-Tests möglich | L |
| Undo/Redo (Memento-Stack, Ctrl+Z/Y) | Beseitigt Datenverlust-Klasse grundsätzlich | M |
| Inkrementelles MP-Rendering (nur geändertes Modul) | Flüssig bei 20 Modulen | M |
