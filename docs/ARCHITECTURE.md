# ET-Printer - Technische Architektur

## Technologie-Stack
- **Sprache:** C# 12
- **UI-Framework:** WPF (.NET 8)
- **IDE:** Visual Studio 2022
- **Druck:** System.Printing / System.Drawing.Printing
- **Serialisierung:** System.Text.Json
- **Build:** MSBuild / dotnet CLI

## Projektstruktur
```
ET-Printer/
├── ET-Printer.sln
├── src/
│   └── ETPrinter/
│       ├── ETPrinter.csproj
│       ├── App.xaml / App.xaml.cs
│       ├── MainWindow.xaml / MainWindow.xaml.cs
│       ├── Models/
│       │   ├── LabelProject.cs          # Projekt-Datenmodell
│       │   ├── LabelFormat.cs           # Enum der 6 Formate
│       │   ├── LabelSettings.cs         # Einstellungen (Schrift, Raender)
│       │   ├── LabelCell.cs             # Einzelne Beschriftungszelle
│       │   └── LabelPage.cs             # Seitenmodell mit Zellen
│       ├── ViewModels/
│       │   ├── MainViewModel.cs         # Hauptlogik, Kommandos
│       │   ├── LabelViewModel.cs        # Einzelnes Etikett (Text, IsSelected, HasText)
│       │   └── PagePreviewViewModel.cs  # A4-Seiten-Vorschau mit Etiketten-Liste
│       ├── Services/
│       │   ├── PrintService.cs          # Drucklogik (FixedDocument)
│       │   ├── LayoutEngine.cs          # Berechnung Zellengroessen/Positionen
│       │   ├── ProjectFileService.cs    # Speichern/Laden
│       │   └── FormatDefinitions.cs     # Definition der 6 Druckformate
│       └── Converters/
│           └── NullToBoolConverter.cs   # Fuer IsEnabled-Bindings
├── tests/
│   └── ETPrinter.Tests/
│       └── ETPrinter.Tests.csproj
└── docs/
    └── *.md
```

## MVVM-Architektur (wie GravurApp)
```
MainWindow.xaml
├── Links: Eingabe-Panel (ScrollViewer > StackPanel > GroupBoxen)
│   ├── Format-Auswahl (ComboBox)
│   ├── Etikett-Eingabe (TextBoxen, gebunden an SelectedLabel)
│   ├── Einstellungen (Schrift, Raender)
│   └── Aktions-Buttons (Uebertragen, Loeschen, Drucken)
│
├── GridSplitter
│
└── Rechts: A4-Vorschau (ScrollViewer > Viewbox > Canvas)
    ├── Zoom-Slider
    ├── A4-Blatt (weisser Border mit Schatten)
    └── Etikettenraster (ItemsControl > UniformGrid)
        └── Einzelnes Etikett (Border, klickbar, farbkodiert)

View (XAML) ──bindet──> ViewModel (C#) ──nutzt──> Model (C#)
                              │
                              └──> Services (Druck, Layout, IO)
```

## Druckformat-Definitionen (aus Excel-Analyse)

### Seitengeometrie (A4 Hochformat)
- **Papier:** 210 x 297 mm
- **Standard-Raender:** Oben 20mm, Links 30mm, Unten 21mm, Rechts 25mm
- **Druckbereich:** 155 x 256 mm

### Zellengroessen pro Format

| Format                            | Spalten | Zeilen | Zellen/Seite | Spaltenbreite    | Zeilenhoehe |
|-----------------------------------|---------|--------|--------------|------------------|-------------|
| Horizontal zweizeilig + Header    | 5x2     | 20     | 100          | ~6.6 + 26.7 mm  | ~6.6 mm     |
| Horizontal zweizeilig             | 5       | 20     | 100          | ~31.0 mm         | ~6.6 mm     |
| Horizontal einzeilig              | 7       | 20     | 140          | ~22.1 mm         | ~12.8 mm    |
| Vertikal zweizeilig + Header      | 5x2     | 20     | 100          | ~6.6 + 26.7 mm  | ~6.6 mm     |
| Vertikal zweizeilig               | 5       | 20     | 100          | ~31.0 mm         | ~6.6 mm     |
| Vertikal einzeilig                | 7       | 20     | 140          | ~22.1 mm         | ~12.8 mm    |

> Hinweis: Die exakten Masse werden aus den Excel-Spaltenwerten berechnet und muessen
> beim Testdruck kalibriert werden. Drucker-Einzugsabweichungen koennen ueber die
> Seitenraender kompensiert werden.

### Textausrichtung
- **Horizontal-Formate:** Text normal (0 Grad)
- **Vertikal-Formate:** Text 90 Grad gedreht
- **Header-Spalten:** Immer 90 Grad gedreht, zentriert

## UI-Konzept (analog GravurApp)
Das Layout orientiert sich am bestehenden Gravur-Programm (C:\claude\Gravur):

| Element              | GravurApp                    | ET-Printer                        |
|----------------------|------------------------------|-----------------------------------|
| Linke Spalte         | Schildformat + Text-Editor   | Format-Auswahl + Etikett-Editor   |
| Rechte Spalte        | Magazin-Raster (klickbar)    | A4-Blatt-Vorschau (klickbar)      |
| Einzelelement        | Schild in Canvas             | Etikett als Border mit Text       |
| Auswahl              | Blau markiert                | Blau markiert                     |
| Befuellt             | Gruen                        | Gruen                             |
| Zoom                 | Slider 0.5x - 4.0x          | Slider 0.5x - 4.0x               |
| Uebertragen-Button   | Ja                           | Ja                                |

## Schluessel-Entscheidungen
1. **WPF statt WinForms** - Bessere Druckunterstützung via FixedDocument/FlowDocument,
   MVVM-Pattern, moderne UI
2. **.NET 8** - Aktuelles LTS, guter WPF-Support, System.Text.Json eingebaut
3. **JSON als Projektformat** - Einfach, menschenlesbar, kein Drittanbieter noetig
4. **FixedDocument fuer Druck** - Pixelgenaue Positionierung der Beschriftungen auf A4
5. **Zwei-Spalten-Layout** - Bewaehrtes Pattern aus GravurApp: Eingabe links, Vorschau rechts
