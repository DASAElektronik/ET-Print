# ET-Printer - Produktdokumentation

## Vision
**ET-Printer** ist eine eigenstaendige Windows-Desktop-Anwendung (C# / WPF), die das Bedrucken von Beschriftungsstreifen fuer die Siemens ET 200SP auf A4-Blaettern ermoeglicht. Sie ersetzt das bisherige Excel-Makro-Template durch eine moderne, benutzerfreundliche Anwendung.

## Hintergrund
Die Siemens ET 200SP ist ein dezentrales Peripheriesystem. Um die Module uebersichtlich in einer Anlage zu integrieren, muessen die Beschriftungsstreifen individuell bedruckt werden. Bisher geschieht dies ueber ein Excel-Template mit VBA-Makros (Siemens Beitrags-ID: 81524595, V1.0 von 09/2013).

## Zielgruppe
- Elektrotechniker / Automatisierungstechniker
- Schaltschrankbauer
- Inbetriebnehmer

## Kernfunktionen
1. **Tabellarischer Editor** - Eingabe der Beschriftungstexte in einer uebersichtlichen Tabelle
2. **6 Druckformate** - Horizontal/Vertikal, Einzeilig/Zweizeilig/Zweizeilig+Header
3. **Druckvorschau** - WYSIWYG-Vorschau vor dem Druck
4. **Einstellungsdialog** - Schriftgroesse, Fett/Kursiv, Seitenraender
5. **Druck auf A4** - Hochformat, kalibriert fuer Beschriftungsstreifen
6. **Projekt speichern/laden** - Beschriftungsdaten persistent speichern

## Technische Rahmenbedingungen
- **Plattform:** Windows 10/11
- **Framework:** .NET 8, WPF
- **IDE:** Visual Studio 2022
- **Papierformat:** A4 Hochformat (210 x 297 mm)
- **Papiersorte:** Karton 176-220g (empfohlen)
- **Zielaufloesung:** Drucker-nativ (keine Skalierung)
