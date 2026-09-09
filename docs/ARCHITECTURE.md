# ET-Printer – Technische Architektur

Stand: 2026-09-09 (v3.1.1). Beschreibt den tatsächlichen Code unter `src/ETPrinter`
und `tests/ETPrinter.Tests`. Historie in `CHANGELOG.md`, Geometrie-Herleitung in
`PRINT-FORMATS.md`, Features in `FEATURES.md`.

## Technologie-Stack

| Bereich | Wahl |
|---------|------|
| Sprache | C# 13 (`Nullable` + `ImplicitUsings` aktiv) |
| Framework | .NET 9, Zielplattform `net9.0-windows`, WPF (`UseWPF`) |
| Assembly | `ET-Printer.exe` (AssemblyName in `ETPrinter.csproj`), Company DASA |
| PDF-Parsing | NuGet `PdfPig` 0.1.16 (UglyToad.PdfPig) |
| Excel-Import | NuGet `ClosedXML` 0.105.1 |
| Icon | `Assets/ETPrinter.ico` (generiert, `<ApplicationIcon>`) |
| Serialisierung | `System.Text.Json` (JSON, Enums als Strings) |
| Druck | WPF `FixedDocument` + `PrintDialog` (System.Printing, PrintTicket A4) |
| Tests | xUnit 2.9.3, `Microsoft.NET.Test.Sdk` 17.12.0, `xunit.runner.visualstudio` 2.8.2; Testprojekt mit `UseWPF` und `InternalsVisibleTo` |
| Publish | `Properties/PublishProfiles/win-x64.pubxml`: Release, `win-x64`, self-contained, single-file, ReadyToRun, eingebettete PDB, Ausgabe nach `publish/` |
| Build | `dotnet build` / `dotnet test` / `dotnet publish` (VS2022-Solution `ET-Printer.sln`) |

Versionsnummer: `<Version>` in `ETPrinter.csproj` ist die einzige Quelle. Der
Info-Dialog liest sie zur Laufzeit aus der Assembly (`MainWindow.MenuAbout_Click`).

## Projektstruktur

```
Beschriftung/
├── ET-Printer.sln
├── README.md
├── test-send.ps1                      # Einzelbefehl an die Test-Automation-Pipe
├── publish/ET-Printer.exe             # Deploy-Ziel (gitignored)
├── Excel_Template_ET200SP .xls        # Siemens-Vorlagen (Referenz)
├── Excel_Template_S71500_ET200MP.xls
├── Excel_Template_S71500_ET200MP_25mm.xls
├── excel_template_et200sp_d.pdf
├── excel_template_s71500_et200mp_d.pdf
├── docs/
│   ├── ABSCHLUSSPLAN.md               # AP0–AP9 des Release-Durchlaufs
│   ├── ARCHITECTURE.md                # dieses Dokument
│   ├── CHANGELOG.md
│   ├── FEATURES.md
│   ├── PRINT-FORMATS.md               # Geometrie + Klemmenbelegungen (Datenblatt-verifiziert)
│   ├── PRODUCT.md
│   └── TODO.md
├── tools/
│   ├── smoke.ps1                      # Smoke-Test gegen publish/ET-Printer.exe
│   ├── register-etprint.ps1           # .etprint-Dateizuordnung (HKCU), manuell ausfuehren
│   ├── dump-xls.ps1                   # Excel-Vorlagen zellgenau dumpen (COM)
│   └── extract-wiring.ps1             # Klemmenbelegung aus Datenblatt-PDF ziehen
├── src/ETPrinter/
│   ├── ETPrinter.csproj
│   ├── App.xaml / App.xaml.cs         # Startup, globale Exception-Handler, Recovery, Startargument
│   ├── MainWindow.xaml / .xaml.cs     # Hauptfenster, KeyBindings, Fensterzustand, Drag&Drop, Zoom
│   ├── AssemblyInfo.cs
│   ├── Assets/ETPrinter.ico (+ ETPrinter_256.png)
│   ├── Properties/PublishProfiles/win-x64.pubxml
│   ├── Models/
│   │   ├── LabelCell.cs               # ET200SP-Etikett (Header, Line1, Line2, Schrift, IsPrintEnabled)
│   │   ├── LabelFormat.cs             # Enum der 10 Formate + FormatInfo-Record
│   │   ├── LabelPage.cs               # Liste von LabelCell
│   │   ├── LabelProject.cs            # Projektdatei (Version 5, Pages, MpPages, Settings, Kalibrierung)
│   │   ├── LabelSettings.cs           # Adress-/Header-Schrift, Ränder, Familien-Defaults
│   │   ├── MpModule.cs                # ET200MP-Modul, MpAddressCell, MpModulePage, HasPrintableContent
│   │   ├── MpModuleType.cs            # MpModuleVariant (9 Varianten), MpCellDefinition, MpModuleLayout
│   │   ├── ProductFamily.cs           # Enum + ProductFamilyInfo (Ränder, Bänder, Spaltenanteile, Bogen-Nr)
│   │   └── SchematicParseResult.cs    # ParsedModule / ParsedChannel des PDF-Parsers
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs           # INotifyPropertyChanged + SetProperty
│   │   ├── RelayCommand.cs            # RelayCommand und RelayCommand<T>
│   │   ├── MainViewModel.cs           # Koordination: Familie/Format, Auswahl, Seiten, Generator-Anwendung, Druck, Undo-Hooks
│   │   ├── AddressGeneratorViewModel.cs # Tab "Adress-Generator" (Generator.*): Felder, Vorschau, Auto-Advance
│   │   ├── SettingsPanelViewModel.cs  # Panels "Seite"/"Schrift" (Panel.*): Live-Apply, Wertebereiche
│   │   ├── MpEditorViewModel.cs       # Tab "MP Modul" (MpEditor.*): Modul-/Zellauswahl, Variante, Katalog-Artikel
│   │   ├── ProjectSession.cs          # Datei, Dirty, Zuletzt geöffnet, Neu/Öffnen/Speichern (Session.*)
│   │   ├── ImportCoordinator.cs       # CSV/Excel/PDF-Abläufe (Import.*), IImportTarget, Zuordnungsregeln
│   │   ├── PageDocument.cs            # Seiten + sichtbare Seite je Modus (PageDocument<T>)
│   │   ├── UndoHistory.cs             # Undo/Redo-Verlauf (Memento), EditorState
│   │   ├── LabelViewModel.cs          # SP-Etikett: Slots, Display-Strings, Auswahl/Markierung
│   │   ├── MpModuleViewModel.cs       # MP-Modul + MpAddressCellViewModel, Zell-Neuaufbau, ContentChanged
│   │   └── PdfImportViewModel.cs      # Modulauswahl im PDF-Import-Dialog
│   ├── Services/
│   │   ├── SheetGeometry.cs           # EINZIGE Geometriequelle (mm, RectMm) für Druck + Vorschau
│   │   ├── FormatDefinitions.cs       # Die 10 FormatInfo-Einträge, Default-Format je Familie
│   │   ├── PrintService.cs            # FixedDocument-Erzeugung, Druckdialog, PNG-Rendering
│   │   ├── AddressGenerator.cs        # SP-Adressgenerator (digital/analog), Kanalzahlen, Auto-Advance
│   │   ├── MpModuleLayoutFactory.cs   # Zellen-Layouts je Variante, generische Struktur-Labels
│   │   ├── MpModuleCatalog.cs         # 18 konkrete Siemens-Module mit Datenblatt-Belegung (35 mm + 25 mm)
│   │   ├── ProjectService.cs          # .etprint laden/speichern, Migration v1–v5, WriteAtomic, Recent
│   │   ├── DialogService.cs           # IDialogService: WpfDialogService (MessageBox/Win32/PDF-Dialog), SilentDialogService
│   │   ├── CalibrationService.cs      # calibration.json (maschinenspezifisch)
│   │   ├── UiStateService.cs          # ui.json (Fenster, Zoom, Splitter) + Plausibilisierung
│   │   ├── ClipboardService.cs        # App-interner Copy-Puffer (Etiketten ODER Module)
│   │   ├── CsvImportService.cs        # Zeichenweiser CSV-Parser, Encoding-Erkennung
│   │   ├── ExcelImportService.cs      # ClosedXML-Import (erstes sichtbares Blatt)
│   │   ├── SchematicParserService.cs  # PDF-Schaltplan-Parser (PdfPig + Regex)
│   │   ├── TestAutomationService.cs   # Named-Pipe-Server mit Dispatch-Tabelle
│   │   └── Log.cs                     # Datei-Logger mit Rotation
│   ├── Controls/
│   │   ├── MpPreviewControl.xaml      # Canvas
│   │   └── MpPreviewControl.xaml.cs   # MP-Vorschau: eine Canvas-Ebene je Modul, inkrementell + entprellt, Klick-Selektion
│   ├── Views/
│   │   ├── PdfImportDialog.xaml       # DataGrid mit Modulauswahl + Warnungen
│   │   └── PdfImportDialog.xaml.cs
│   └── Converters/
│       ├── BindingProxy.cs            # Freezable-Brücke für ContextMenu/RowDefinition
│       └── NullToBoolConverter.cs     # NullToBool, BoolToVisibility, InverseBoolToVisibility
└── tests/ETPrinter.Tests/
    ├── ETPrinter.Tests.csproj
    ├── Sta.cs                         # STA-Helfer für FixedDocument-Tests
    ├── AddressGeneratorTests.cs
    ├── Catalog25mmTests.cs            # AP8/AP9: 25-mm-BA-Module, Generator-Golden-Tests je Variante
    ├── ClipboardAndCloneTests.cs
    ├── CriticalFixTests.cs            # AP1: Druckentscheidung, Leerseiten, Import-Blöcke, atomares Speichern
    ├── CsvImportServiceTests.cs
    ├── ImportRobustnessTests.cs       # AP4: CSV/Excel/PDF-Parser, SuggestVariant
    ├── MediumFixTests.cs              # AP2
    ├── MpModuleCatalogTests.cs
    ├── MpModuleLayoutFactoryTests.cs
    ├── MpModuleTests.cs
    ├── PersistenceTests.cs            # Roundtrip v5, Migration v1/v2/v3
    ├── ProductFamilyTests.cs
    ├── ProjectMigrationTests.cs       # v4 → v5
    ├── SheetGeometryTests.cs          # AP3
    └── UxTests.cs                     # AP5
```

Stand der Tests: 239 (CHANGELOG AP9), Smoke-Test 82/82 Prüfungen.

## MVVM-Skizze

```
MainWindow.xaml  (DataContext = MainViewModel)
├── Menü / Toolbar / Statusleiste
├── LINKS (ScrollViewer, Spalte "LeftPanelColumn", Splitter 200–800 px)
│   ├── GroupBox "Bearbeite: …"           EditTargetInfo, LayoutInfo
│   ├── GroupBox Produktfamilie / Druckformat
│   ├── TabControl (IsEnabled = HasSelection, SelectedIndex = InputTabIndex)
│   │   ├── Tab 0 "Adress-Generator"      Generator.*: Modulname, Modultyp, Start-Byte, Anzahl, Vorschau
│   │   ├── Tab 1 "Manuell"               nur SP (Kopfzeile, Zeile 1, Zeile 2); im MP-Modus ausgeblendet
│   │   └── Tab 2 "MP Modul"              MpEditor.*: Siemens-Modul (Katalog), Variante, Header, Netzadresse,
│   │                                     Netzname, CPU-Name, "Modul drucken", Adresszelle
│   ├── GroupBox "Seite (gilt für alle Etiketten)"   Panel.*
│   │       Kopfzeilen-Schrift, Ränder, Kalibrierung + Testseite, Blanko A4, Seite zurücksetzen
│   ├── GroupBox "Schrift: <Auswahl>"     Panel.*: Schriftart/Größe/fett/kursiv (Live-Apply), "Auf alle anwenden"
│   └── Buttons "Auswahl leeren" / "Alle löschen"
└── RECHTS (PreviewHost)
    ├── Zoom-Slider 0,3–4 + "Ganze Seite", Seitennavigation (◀ ▶ + Seite − Seite)
    ├── SpScrollViewer  (sichtbar wenn !IsModuleBased)
    │   └── A4-Border 630×891 px (3 px/mm), ScaleTransform(Zoom)
    │       ├── ItemsControl(Labels) in UniformGrid, doppelt gespiegelt (Position 1 = unten rechts)
    │       │   └── Etikett-Border: Kopfspalte (Breite aus SheetGeometry via BindingProxy),
    │       │       horizontal: Line1Display/Line2Display in Viewbox (DownOnly),
    │       │       vertikal:   Line1Parts / EffectiveLine2Parts als rotierte Slots
    │       └── Zeilennummern 20..1 im rechten Seitenrand
    └── MpScrollViewer  (sichtbar wenn IsModuleBased)
        └── A4-Border → MpPreviewControl (Canvas, eine Ebene je Modul, DispatcherTimer 40 ms Debounce)

View (XAML) ──bindet──> MainViewModel ──nutzt──> Models
                         ├── Generator  (AddressGeneratorViewModel)   Tab "Adress-Generator"
                         ├── Panel      (SettingsPanelViewModel)      Panels "Seite" / "Schrift"
                         ├── MpEditor   (MpEditorViewModel)           Tab "MP Modul"
                         ├── Session    (ProjectSession)              Datei, Dirty, Recent, Neu/Öffnen/Speichern
                         ├── Import     (ImportCoordinator)           CSV/Excel/PDF-Abläufe
                         ├── History    (UndoHistory<EditorState>)    Undo/Redo
                         ├── _spDoc/_mpDoc (PageDocument<T>)          alle Seiten + sichtbare Seite
                         └──> Services (SheetGeometry, PrintService, ProjectService, IDialogService, Importe, …)
```

Zwei Vorschau-Pfade, ein Druckpfad:

- **ET 200SP**: rein deklarativ in XAML. Das `ItemsControl` ist per `ScaleTransform(-1,-1)`
  gespiegelt und jedes Etikett zurückgespiegelt, damit Index 0 unten rechts liegt.
  Rasterränder (`PreviewMargin`), Kopfspaltenbreite (`SpHeaderPreviewWidth`) und
  Zeilennummern-Versatz (`RowNumbersMargin`) kommen aus dem ViewModel; `ColumnDefinition`
  und `RowDefinition` hängen nicht im Visual Tree und werden über den `BindingProxy` gebunden.
- **ET 200MP**: `MpPreviewControl` zeichnet Rechtecke und Textblöcke manuell auf einen Canvas,
  je Modul in eine eigene Canvas-Ebene. Es abonniert jedes sichtbare Modul (`PropertyChanged`,
  `AddressCells.CollectionChanged`) und dessen Zellen und zeichnet bei Änderungen nur die
  betroffene Ebene neu (Tippen, Zell-/Modulauswahl, Druckflag, Variante). Ein Vollaufbau
  läuft nur bei `MpModules.CollectionChanged` (Seitenwechsel), `MpPreviewRefreshToken`
  (Familie/Format, Ränder, Kopfzeilen-Schrift, Druckauswahl für alle) und `IsModuleBased`.
  Beides ist über einen 40-ms-`DispatcherTimer` gebündelt; `FlushRender` erzwingt den Aufbau
  für Screenshots. Beim DataContext-Wechsel werden alle Handler abgemeldet.
- **Druck**: `PrintService` baut aus denselben ViewModels ein `FixedDocument`; die Vorschau
  zeigt das unverschobene Raster, der Druck addiert den Kalibrier-Versatz.

Beide Modi teilen sich `MainViewModel`: `IsModuleBased` (aus `FormatInfo`) ist die Weiche,
`HasSelection` deckt `SelectedLabel` und `SelectedMpModule` ab.

## Schlüsselkonzepte

### SheetGeometry – die einzige Geometriequelle
`Services/SheetGeometry.cs` rechnet ausschließlich in Millimetern (`RectMm` mit `X, Y, W, H`,
`Right`, `Bottom`). `SheetGeometry.For(format, settings, calX, calY)` liefert:

- SP: `SpGroupWidth = PrintWidth / LabelsPerRow`, `SpHeaderWidth = 20 %` der Gruppenbreite bei
  Formaten mit Kopfzeile, `SpCellHeight = PrintHeight / LabelRows`, `SpPosition(index)` spiegelt
  (Index 0 = unten rechts), `SpLabelRect / SpHeaderRect / SpContentRect`.
- MP: `ModuleWidth = PrintWidth / ColumnsPerPage`, Spaltenbreiten aus den Familien-Ratios,
  `MpHeaderRect`, `MpModuleRect`, `MpCellRect(def)`, `MpNetAddressRect(block)`, `MpCpuRect`;
  Bänder werden von oben mit festen Höhen gerastert, "Rand unten" ist wirkungslos.
- `GridRect` für die Fadenkreuze der Kalibrierseite.

Die Umrechnung in Geräteeinheiten macht der Aufrufer über `RectMm.Scale(factor)`:
Druck `96 / 25.4` DIP je mm, Vorschau `3 px` je mm. `FormatDefinitions.GetCellSize` ist
nur noch ein Wrapper.

### PrintService
- `BuildDocument(pages, …)` (SP) und `BuildMpDocument(pages, …)` (MP) erzeugen ein
  `FixedDocument` ohne Dialog; Seiten ohne druckbaren Inhalt werden übersprungen.
- `BuildCalibrationDocument` zeichnet zwei Fadenkreuze (Rasterecke oben links, unten rechts)
  plus Infotext mit Format, Rändern und Versatz.
- `Print(document, jobTitle)` zeigt den Windows-Druckdialog, erzwingt danach
  `PageMediaSize = ISOA4` und `Portrait` im PrintTicket und liefert `false` bei Abbruch.
- `IsPrintable(LabelViewModel)` = `HasText && IsPrintEnabled`;
  `IsPrintable(MpModuleViewModel)` = `IsPrintEnabled && HasPrintableContent`.
  Das ist die einzige Druckentscheidung (Seite, Schnittkanten, Inhalt, Test-Automation).
- `RenderToPng(document, dir, dpi = 150)` rendert jede Seite als `page_NN.png` auf weißem Grund.
- `FitBox`: Viewbox mit `StretchDirection.DownOnly` – Text wird verkleinert statt mit „…“
  gekappt; die XAML-Vorschau nutzt dieselbe Konstruktion.
- Schriftgrößen sind Punkt; der Druck rechnet `pt × 96/72` in DIP um, die Vorschau
  `pt × 3 × 25.4/72` in px.

### HasPrintableContent (SIWAREX)
`MpModule.HasText` zählt nur Benutzertext (Header, editierbare Zellen, Netzadressen, CPU).
`HasPrintableContent` ist `HasText` **oder** `IsFixedPinout(definitions)`: alle Zellen
nicht editierbar und mindestens ein Label. Damit druckt ein SIWAREX-Streifen ohne Eingabe,
ein leeres DI-16-Modul (nur „L+“/„M“) dagegen nicht.

### Produktfamilien, Formate, Varianten, Katalog
- `ProductFamily` (ET200SP, S71500_ET200MP, S71500_ET200MP_25mm) → `ProductFamilyInfo`
  (Anzeigename, Bogen-Artikelnummer, Default-Ränder, geschätzte Maße, `ModulesPerPage`,
  `ColumnsPerPage`, Spaltenanteile `Col0–Col3Ratio`, `RowsPerHalf`, `BandOf/ColumnOf`,
  `HasNetAddressColumn`).
- `LabelFormat` (10 Werte) → `FormatInfo` (Spalten, Zeilen je Etikett, Kopfzeile, vertikal,
  `LabelsPerRow × LabelRows`, Familie, `BandsPerPage`, `ChannelRowsPerBand`, `IsModuleBased`).
  `FormatDefinitions.GetFormatsForFamily` filtert die ComboBox, `GetDefaultFormat` wählt
  HorizontalDouble / MP_Horizontal / MP25_Horizontal.
- `MpModuleVariant` (9 Werte) = **Layout**: `MpModuleLayoutFactory` liefert je Variante ein
  `MpCellDefinition[]` (StartRow, RowSpan, StartCol, ColSpan, IsEditable, Label).
  `VariantsForFamily` trennt 35-mm- und 25-mm-Varianten, `DefaultVariantFor` liefert
  DI_DQ_16 bzw. MP25_16. `GetGenericDefinitions(variant, ioType)` setzt bei DO-Modulen
  die Versorgungslabels je Kanalgruppe (K9/K10, K29/K30 zusätzlich).
- `MpModuleCatalog` = **Struktur-Labels**: konkreter Artikel → Variante + verifizierte
  Zellen. `GetDefinitions(module)` nimmt den Katalogeintrag, wenn `ArticleNumber` gesetzt ist
  und seine Variante zur Modulvariante passt, sonst die generische Belegung zum `IoType`.
  Der Variant-Setter im ViewModel löscht einen nicht mehr passenden Artikel.

### Persistenz (.etprint, Version 5)
`LabelProject` enthält `Version`, `ProductFamily`, `Format`, `Settings`, `Pages` (SP),
`MpPages` (MP, sonst null), Kalibrier-Offsets und `PrintGridLines`. `ProjectService.Load`
migriert beim Lesen:

| von | nach | Aktion |
|-----|------|--------|
| v1 | v2 | flache `Labels`-Liste → eine `LabelPage` |
| v2 | v3 | `ProductFamily = ET200SP` |
| v3 | v4 | `MpPages` möglich (bleibt null für SP) |
| v4 | v5 | MP-Seiten von 5 auf `ModulesPerPage` Module auffüllen (Default-Variante der Familie) |

Zusätzlich: `Pages`/`Settings` null-sicher, `ModuleIndex` immer auf die Listenposition
normalisiert, `AddressCells` null-sicher, mindestens eine Seite, 50-MB-Größenlimit.
`Save` schreibt über `WriteAtomic` (`.tmp` + `File.Move`, vorherige Version als `.bak`).
`MainViewModel.BuildProject()` und `ApplyLoadedProject()` sind die gemeinsamen Ein-/Ausgänge
für Dialoge, Recovery und Test-Automation.

### Kalibrierung
`CalibrationService` liest/schreibt `%LOCALAPPDATA%\ETPrinter\calibration.json`
(`OffsetX`, `OffsetY` in mm, positiv = rechts/unten). Die Werte fließen nur in den Druck
ein. Beim Laden eines Projekts hat eine vorhandene lokale Datei Vorrang vor den
Projektwerten (maschinenspezifisch). Gespeichert wird erst nach tatsächlich gesendetem Druck.

### Wiederherstellung nach Absturz
`App.xaml.cs` registriert `DispatcherUnhandledException`, `AppDomain.UnhandledException`
und `TaskScheduler.UnobservedTaskException`. Bei einer unbehandelten Exception wird geloggt,
das Projekt nach `%LOCALAPPDATA%\ETPrinter\recovery.etprint` gesichert und ein Hinweis
mit Logpfad angezeigt. Beim nächsten Start bietet `OfferRecovery` die Wiederherstellung an
(Ja: laden + als geändert markieren; die Datei wird danach immer gelöscht).

### Fensterzustand (ui.json)
`UiStateService` speichert Position, Größe, Maximiert, Zoom und Splitterbreite in
`%LOCALAPPDATA%\ETPrinter\ui.json`. `Sanitize` prüft die Position gegen den virtuellen
Bildschirm (abgesteckter Zweitmonitor) und fällt sonst auf die Windows-Platzierung zurück.
Ohne gespeicherten Zoom wird beim Start „Ganze Seite“ berechnet.

### Logging
`Services/Log.cs`: `Info/Warn/Error` nach `%LOCALAPPDATA%\ETPrinter\log.txt` mit Zeitstempel,
Klassen- und Methodenname (CallerFilePath/CallerMemberName). Über 1 MB wird nach
`log.old.txt` rotiert. Der Logger wirft nie; IO-Fehler gehen an `Debug.WriteLine`.

### Test-Automation
`TestAutomationService` ist ein Named-Pipe-Server (`ETPrinter_TestAutomation`), der nur
mit `--test-automation` oder `ETPRINTER_TEST=1` startet. Befehle sind Zeilen
`command arg…`, Antworten JSON `{ "ok": true, "result": … }` bzw. `{ "ok": false, "error": … }`.
Eine Dispatch-Tabelle (`Commands`) enthält Usage, Beschreibung und Handler; `help` wird
daraus generiert. Handler laufen über `RunOnUI` auf dem Dispatcher (mit Abbruch-Hook
beim Shutdown). Pipe-Fehler lösen 1 s Backoff aus. Im Automation-Modus werden modale
Rückfragen (`SuppressContentLossConfirm`) und Import-Infoboxen unterdrückt.
Clients: `test-send.ps1` (Einzelbefehl) und `tools/smoke.ps1` (Szenarien SP/MP/25mm/
Import/UX/Roundtrip/Undo-Redo, Screenshots + Druck-PNGs unter `test_results/<Name>/`,
Exit-Code 1 bei Fehlern).

### Zerlegung des Haupt-ViewModels (AP9b)
`MainViewModel` ist Koordinator und hält nur noch Familie/Format, Auswahl, Seiten und die
Anwendung des Generators auf Etiketten/Module. Sechs Teile sind eigenständige, einzeln
testbare Objekte, die das Hauptfenster als Unter-Bindungen anspricht:

| Objekt | Bindung | Aufgabe |
|--------|---------|---------|
| `AddressGeneratorViewModel` | `Generator.*` | Felder, Vorschau, Auto-Advance, `Generate()` |
| `SettingsPanelViewModel` | `Panel.*` | Schrift/Kopfzeile/Ränder mit Live-Apply, Wertebereiche, `SuspendLiveApply` |
| `MpEditorViewModel` | `MpEditor.*` | Modul-/Zellauswahl, Variante, Katalog-Artikel, Generator-Vorbelegung |
| `ProjectSession` | `Session.*` | Dateipfad, `IsDirty`, Recent-Liste, Neu/Öffnen/Speichern mit Rückfragen |
| `ImportCoordinator` | `Import.*` | CSV/Excel/PDF-Abläufe; das Haupt-ViewModel ist `IImportTarget` |
| `PageDocument<T>` | `Labels` / `MpModules` = `Visible` | alle Seiten + sichtbare Seite, Navigation |

Alle Dialoge laufen über `IDialogService` (`WpfDialogService` produktiv, `SilentDialogService`
in Tests und Test-Automation). Die Teile melden Statusmeldungen und Änderungen per Events
(`StatusRequested`, `FontChanged`, `MarginsChanged`, `PropertyChanged`) an den Koordinator;
`SelectedMpModule`/`SelectedMpCell` bleiben als Weiterleitung auf `MpEditor` erhalten, damit
Vorschau, Test-Automation und Tests unverändert darauf zugreifen.

### Undo/Redo (AP9c)
`UndoHistory<EditorState>` arbeitet nach dem Memento-Prinzip: jede Änderung meldet über
`MainViewModel.MarkChanged(label)` den **neuen** Zustand (`Capture()` = tiefe Kopie von
`BuildProject()` + Seite + Auswahl); der vorherige wandert auf den Undo-Stapel.
`BeginChange(label)` fasst alle Meldungen bis zum Dispose zu einem Schritt zusammen –
Commands sind damit ein Schritt (auch „Generieren“ mit 32 Zellschreibungen), ebenso Import
und Artikel-/Variantenwahl. Direkte Bindungen (Tippen in Modulzellen, Randfelder, Schrift)
werden über Schlüssel innerhalb von 1,5 s zusammengefasst. Wiederherstellen läuft über
`ApplyProjectState` (der Ladepfad ohne Datei-/Dirty-Verwaltung) mit unterdrückter
Aufzeichnung; die Rückkehr auf den gespeicherten Snapshot setzt `IsDirty` zurück. Laden
und „Neu“ beginnen den Verlauf neu, Format-/Familienwechsel eines leeren Projekts wird
aufgezeichnet, ohne das Projekt als geändert zu markieren.

## Geometrie

### ET 200SP (alle 6 Formate identisch: 5 × 20 = 100 Etiketten)

| Größe | Wert |
|-------|------|
| Papier | A4 Hochformat 210 × 297 mm |
| Ränder oben/links/unten/rechts | 20,5 / 27,5 / 20,5 / 27,5 mm |
| Druckbereich | 155 × 256 mm |
| Etikett (Gruppe) | 31 × 12,8 mm (gemessen am Bogen 6ES7193-6LA10-0AA0) |
| Kopfspalte bei „+Kopfzeile“ | 20 % der Gruppenbreite = 6,2 mm, Textfeld 24,8 mm |
| Position 1 | unten rechts; Index steigt nach links und nach oben |
| Vertikale Formate | 8 Slots je Reihe (Klemmen der BaseUnit), zwei Reihen bei zweizeilig |

### ET 200MP 35 mm (6ES7592-1AX00-0AA0)

| Größe | Wert |
|-------|------|
| Ränder oben/links/unten/rechts | 14 / 25 / 19 / 12 mm (unten ohne Wirkung) |
| Streifen je Bogen | 2 Bänder × 5 Spalten = 10 Positionen (Index 0–4 Band 0, 5–9 Band 1) |
| Streifenbreite | (210 − 25 − 12) / 5 = 34,6 mm |
| Header Band 1 / Band 2 | 25,7 mm / 20,6 mm |
| Datenzeilen | 20 × 5,6 mm = 112 mm je Band |
| Gesamthöhe Raster | 25,7 + 112 + 20,6 + 112 = 270,3 mm |
| Spaltenanteile Klemmen links / rechts / Netzadresse / CPU | 1499 / 1499 / 804 / 768 von 4570 |
| Netzadress-Spalte | Block 0 = Zeilen 1–10, Block 1 = Zeilen 11–20 |

### ET 200MP 25 mm (6ES7592-2AX00-0AA0)

| Größe | Wert |
|-------|------|
| Ränder | wie 35 mm |
| Streifen je Bogen | 2 Bänder × 10 Spalten = 20 Positionen |
| Streifenbreite | (210 − 25 − 12) / 10 = 17,3 mm |
| Spaltenanteile links / rechts / Netzadresse / CPU | 17,7 / 17,7 / 0 / 13,8 von 49,2 (keine Netzadress-Spalte) |
| Varianten | MP25_16 (20 Zeilen colspan 2, K9/K10/K19/K20 Struktur) und MP25_32 (20 × 2 Zellen, gleiche Struktur je Spalte); Analogmodule 20 freie Zeilen |

## Schlüsselentscheidungen

1. **WPF + FixedDocument** – pixelgenaue mm-Positionierung auf A4, Druckdialog und
   PrintTicket aus dem Framework, kein Drittanbieter.
2. **Eine Geometrieklasse in mm** (AP3) – die MP-Geometrie war dreifach dupliziert und die
   SP-Vorschau wich ~2 % vom Druck ab; jetzt gilt „Preview = Druck“ per Konstruktion.
3. **Dialogfreie Dokumenterzeugung** (AP0) – `Build*Document` getrennt von `Print`, damit
   Test-Automation und Unit-Tests exakt die Druckseiten prüfen können (`RenderToPng`).
4. **Variante = Layout, Katalog = Struktur-Labels** – dieselbe Zellenzahl je Variante,
   damit Texte beim Wechsel migrieren; Katalog-Einträge nur nach Datenblatt-Verifikation.
5. **Modulbasiertes Datenmodell parallel zu SP** – `MpModule`/`MpPages` statt einer
   Verallgemeinerung von `LabelCell`; SP-Code blieb unverändert.
6. **Eine Druckentscheidung** (`IsPrintable`) und `HasPrintableContent` – verhindert,
   dass Vorschau und Druck unterschiedlich entscheiden (SIWAREX-Fall).
7. **JSON als Projektformat mit Versionsnummer und additiver Migration** – alte Dateien
   bleiben ladbar, neue Felder haben Defaults (`IoType` → DI, `ArticleNumber` → null).
8. **Atomares Schreiben + .bak + Recovery** – kein stiller Datenverlust bei Absturz oder
   voller Platte.
9. **Kalibrierung maschinenspezifisch** – lokale `calibration.json` schlägt Projektwerte,
   ein fremdes Projekt darf den eigenen Druckversatz nicht verstellen.
10. **Test-Automation nur auf Anforderung** – Named Pipe startet nur mit Flag/ENV, um die
    Angriffsfläche in Produktion klein zu halten; Regex-Parser mit `NonBacktracking` + Timeout.
11. **Inkrementelle, entprellte MP-Vorschau** (AP9d) – eine Canvas-Ebene je Modul; Modul-
    und Zell-Ereignisse zeichnen nur die betroffene Ebene, der DispatcherTimer bündelt.
    Ein Vollaufbau (1000–1700 Elemente) bleibt Seitenwechsel und Geometrieänderungen vorbehalten.
12. **Kein automatisches Seitenanlegen im MP-Modus** – Varianten und Start-Bytes werden
    je Modul geprüft; SP legt nach dem letzten Etikett automatisch eine Seite an.
13. **Koordinator + Teil-ViewModels statt Gott-Klasse** (AP9b) – Generator, Panel, MP-Editor,
    Session, Import und Seitendokument sind eigenständig testbar; Dialoge nur über
    `IDialogService`, damit Logik ohne Fenster läuft.
14. **Undo als Snapshot des Projektzustands** (AP9c) – kein Command-Pattern je Aktion:
    `BuildProject`/`ApplyProjectState` existieren ohnehin für Speichern/Laden, damit ist jede
    Änderung automatisch rückgängig machbar; Batches und Zeitfenster halten die Schritte
    benutzergerecht.
