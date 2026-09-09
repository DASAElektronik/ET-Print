<#
.SYNOPSIS
  Smoke-Test gegen die veroeffentlichte ET-Printer.exe ueber die Test-Automation-Pipe.

.DESCRIPTION
  Startet die EXE mit --test-automation, faehrt die Szenarien SP / MP / 25mm /
  Persistenz-Roundtrip durch, rendert Druckseiten als PNG (render-print) und legt
  Screenshots + PNGs unter -OutDir ab. Exit-Code 1 bei der ersten fehlgeschlagenen
  Pruefung (alle Befehle laufen trotzdem bis zum Ende, damit Artefakte vollstaendig sind).

.EXAMPLE
  .\tools\smoke.ps1 -Name AP1
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot "..\publish\ET-Printer.exe"),
    [string]$Name = (Get-Date -Format "yyyyMMdd_HHmmss"),
    [string]$OutDir = (Join-Path $PSScriptRoot "..\test_results\$Name"),
    [int]$StartupTimeoutSec = 30
)

$ErrorActionPreference = "Stop"
$pipeName = "ETPrinter_TestAutomation"
$script:failures = New-Object System.Collections.Generic.List[string]
$script:checks = 0

function Send-Cmd([string]$Command) {
    $pipe = $null
    try {
        $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", $pipeName, [System.IO.Pipes.PipeDirection]::InOut)
        $pipe.Connect(5000)
        $writer = New-Object System.IO.StreamWriter($pipe)
        $writer.AutoFlush = $true
        $reader = New-Object System.IO.StreamReader($pipe)
        $writer.WriteLine($Command)
        $line = $reader.ReadLine()
        if (-not $line) { throw "Keine Antwort auf '$Command'" }
        return ($line | ConvertFrom-Json)
    }
    finally { if ($pipe) { $pipe.Dispose() } }
}

function Invoke-Cmd([string]$Command) {
    $r = Send-Cmd $Command
    if (-not $r.ok) { throw "Befehl fehlgeschlagen: '$Command' -> $($r.error)" }
    return $r.result
}

function Assert-True([bool]$Condition, [string]$Message) {
    $script:checks++
    if ($Condition) { Write-Host "  OK   $Message" -ForegroundColor Green }
    else {
        Write-Host "  FAIL $Message" -ForegroundColor Red
        $script:failures.Add($Message)
    }
}

function Assert-Eq($Expected, $Actual, [string]$What) {
    Assert-True ($Expected -eq $Actual) "$What = $Actual (erwartet $Expected)"
}

function Get-State { return (Invoke-Cmd "state" | ConvertFrom-Json) }
function Get-MpState { return (Invoke-Cmd "mp-state" | ConvertFrom-Json) }

function Count-Png([string]$Dir) {
    if (-not (Test-Path $Dir)) { return 0 }
    return @(Get-ChildItem $Dir -Filter "page_*.png").Count
}

# --- Start ---------------------------------------------------------------
$Exe = [System.IO.Path]::GetFullPath($Exe)
$OutDir = [System.IO.Path]::GetFullPath($OutDir)
if (-not (Test-Path $Exe)) { throw "EXE nicht gefunden: $Exe (erst dotnet publish)" }
New-Item -ItemType Directory -Force $OutDir | Out-Null
Write-Host "Smoke-Test: $Exe" -ForegroundColor Cyan
Write-Host "Ausgabe:    $OutDir" -ForegroundColor Cyan

Get-Process -Name "ET-Printer" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$proc = Start-Process -FilePath $Exe -ArgumentList "--test-automation" -PassThru

$deadline = (Get-Date).AddSeconds($StartupTimeoutSec)
$ready = $false
while ((Get-Date) -lt $deadline) {
    try { if ((Send-Cmd "ping").result -eq "pong") { $ready = $true; break } } catch { Start-Sleep -Milliseconds 500 }
}
if (-not $ready) { $proc | Stop-Process -Force; throw "App antwortet nicht innerhalb von $StartupTimeoutSec s" }

try {
    Invoke-Cmd "resize 1600x1000" | Out-Null
    Invoke-Cmd "zoom 1.0" | Out-Null

    # --- Szenario 0: "Neu" bei aktivem Standardformat (AP1 1.1) ---------------
    Write-Host "`n[0] Neu bei aktivem Standardformat" -ForegroundColor Yellow
    Invoke-Cmd "new-project" | Out-Null
    Invoke-Cmd "select-format HorizontalDouble" | Out-Null
    Invoke-Cmd "select-label 0" | Out-Null
    Invoke-Cmd "set-text Zeile A|Zeile B" | Out-Null
    Assert-Eq 1 (Get-State).filledLabels "Vor Neu: 1 Etikett befuellt"
    Invoke-Cmd "new-project" | Out-Null
    $s = Get-State
    Assert-Eq 0 $s.filledLabels "Nach Neu (gleiches Format): 0 Etiketten"
    Assert-True (-not $s.isDirty) "Nach Neu nicht dirty"

    # --- Szenario 1: ET200SP ------------------------------------------------
    Write-Host "`n[1] ET200SP Vertikal + Kopfzeile" -ForegroundColor Yellow
    Invoke-Cmd "new-project" | Out-Null
    Invoke-Cmd "select-family ET200SP" | Out-Null
    Invoke-Cmd "select-format VerticalDoubleHeader" | Out-Null
    Invoke-Cmd "select-label 0" | Out-Null
    Invoke-Cmd "set-generator M1 DI 0 2" | Out-Null
    Invoke-Cmd "trigger-generate" | Out-Null
    Invoke-Cmd "set-generator M2 AI 10 4" | Out-Null
    Invoke-Cmd "trigger-generate" | Out-Null
    Invoke-Cmd "select-label 5" | Out-Null
    Invoke-Cmd "set-input Kopf|Zeile 1|Zeile 2" | Out-Null
    Invoke-Cmd "apply" | Out-Null
    $s = Get-State
    Assert-Eq "ET200SP" $s.productFamily "SP Familie"
    Assert-Eq 3 $s.filledLabels "SP befuellte Etiketten"
    Assert-True $s.isDirty "SP isDirty nach Eingabe"
    Invoke-Cmd "screenshot $OutDir\sp_preview.png" | Out-Null
    # Leere Seite anhaengen: darf im Druck nicht auftauchen (AP1 1.4)
    Invoke-Cmd "add-page" | Out-Null
    Invoke-Cmd "prev-page" | Out-Null
    Assert-Eq 2 (Get-State).pageCount "SP 2 Seiten im Projekt"
    $ps = Invoke-Cmd "print-state" | ConvertFrom-Json
    Assert-Eq 1 $ps.pagesInDocument "SP Druckdokument ohne Leerseite"
    $rp = Invoke-Cmd "render-print $OutDir\sp_print" | ConvertFrom-Json
    Assert-Eq 1 $rp.pages "SP render-print Seiten"
    Assert-Eq $rp.pages (Count-Png "$OutDir\sp_print") "SP PNG-Dateien"
    $spFile = "$OutDir\sp.etprint"
    Invoke-Cmd "save-project $spFile" | Out-Null
    Assert-True (Test-Path $spFile) "SP Projekt gespeichert"

    # --- Szenario 2: ET200MP 35mm -------------------------------------------
    Write-Host "`n[2] ET200MP 35mm: Katalog DI32 + SIWAREX" -ForegroundColor Yellow
    Invoke-Cmd "new-project" | Out-Null
    Invoke-Cmd "select-family S71500_ET200MP" | Out-Null
    Invoke-Cmd "select-module 0" | Out-Null
    Invoke-Cmd "set-module-article 6ES7521-1BL00-0AB0" | Out-Null
    Invoke-Cmd "set-generator DI32 DI 0 4" | Out-Null
    Invoke-Cmd "trigger-generate" | Out-Null
    Invoke-Cmd "select-module 0" | Out-Null
    $m = Get-MpState
    Assert-Eq "6ES7521-1BL00-0AB0" $m.article "MP Modul 0 Artikel"
    Assert-Eq 32 $m.filledCells "MP Modul 0 befuellte Zellen"
    Invoke-Cmd "select-module 6" | Out-Null
    Invoke-Cmd "set-module-variant SIWAREX_WP52x" | Out-Null
    $m6 = Get-MpState
    Assert-Eq "SIWAREX_WP52x" $m6.variant "MP Modul 6 Variante"
    Assert-Eq 0 $m6.editableCells "SIWAREX ohne editierbare Zellen"
    Invoke-Cmd "select-module 7" | Out-Null
    Invoke-Cmd "set-module-article 6ES7522-1BH00-0AB0" | Out-Null
    Invoke-Cmd "set-module-header DQ16 Band2" | Out-Null
    Invoke-Cmd "set-module-cpu PLC1" | Out-Null
    Invoke-Cmd "set-generator DQ16 DO 4 2" | Out-Null
    Invoke-Cmd "trigger-generate" | Out-Null
    # AP1 1.2: SIWAREX (nur feste Labels) zaehlt als druckbar -> 3 Module auf Seite 1
    $ps = Invoke-Cmd "print-state" | ConvertFrom-Json
    Assert-Eq 3 $ps.printablePerPage[0] "MP druckbare Module (DI32 + SIWAREX + DQ16)"
    # AP1 1.3: Variantenwechsel loescht nicht passenden Artikel
    Invoke-Cmd "select-module 7" | Out-Null
    Assert-Eq "6ES7522-1BH00-0AB0" (Get-MpState).article "Modul 7 Artikel vor Variantenwechsel"
    Invoke-Cmd "set-module-variant DI_DQ_32" | Out-Null
    Assert-Eq "" (Get-MpState).article "Modul 7 Artikel nach Variantenwechsel geloescht"
    Invoke-Cmd "set-module-article 6ES7522-1BH00-0AB0" | Out-Null
    Assert-Eq "DI_DQ_16" (Get-MpState).variant "Modul 7 Variante folgt Artikel"
    # AP1 1.8: Zellauswahl folgt Modulwechsel
    Invoke-Cmd "select-module 0" | Out-Null
    Invoke-Cmd "select-cell 0" | Out-Null
    Assert-Eq 0 (Get-MpState).selectedCell "Zelle 0 in Modul 0 gewaehlt"
    Invoke-Cmd "select-module 1" | Out-Null
    Assert-True ($null -eq (Get-MpState).selectedCell) "Zellauswahl nach Modulwechsel leer"
    # AP1 1.9: Schrift wirkt auf MP-Modul
    Invoke-Cmd "select-module 0" | Out-Null
    Invoke-Cmd "set-font 9 1 1" | Out-Null
    $m = Get-MpState
    Assert-Eq 9 $m.fontSize "MP Modul 0 Schriftgroesse 9"
    Assert-True ($m.isBold -and $m.isItalic) "MP Modul 0 fett + kursiv"
    Invoke-Cmd "set-font 7 0 0" | Out-Null
    Invoke-Cmd "screenshot $OutDir\mp_preview.png" | Out-Null
    $rp = Invoke-Cmd "render-print $OutDir\mp_print" | ConvertFrom-Json
    Assert-Eq 1 $rp.pages "MP render-print Seiten"
    $mpFile = "$OutDir\mp.etprint"
    Invoke-Cmd "save-project $mpFile" | Out-Null
    Assert-True (Test-Path $mpFile) "MP Projekt gespeichert"

    # --- Szenario 3: ET200MP 25mm -------------------------------------------
    Write-Host "`n[3] ET200MP 25mm" -ForegroundColor Yellow
    Invoke-Cmd "new-project" | Out-Null
    Invoke-Cmd "select-family S71500_ET200MP_25mm" | Out-Null
    $s = Get-State
    Assert-Eq 20 $s.moduleCount "25mm Module pro Seite"
    Invoke-Cmd "select-module 0" | Out-Null
    Invoke-Cmd "set-module-variant MP25_16" | Out-Null
    Invoke-Cmd "set-generator M25 DI 10 2" | Out-Null
    Invoke-Cmd "trigger-generate" | Out-Null
    Invoke-Cmd "select-module 0" | Out-Null
    $m = Get-MpState
    Assert-Eq 16 $m.filledCells "25mm Modul 0 befuellte Zellen"
    Invoke-Cmd "select-module 1" | Out-Null
    Invoke-Cmd "set-module-variant MP25_32" | Out-Null
    Invoke-Cmd "set-generator M32 DI 20 4" | Out-Null
    Invoke-Cmd "trigger-generate" | Out-Null
    Invoke-Cmd "screenshot $OutDir\mp25_preview.png" | Out-Null
    $rp = Invoke-Cmd "render-print $OutDir\mp25_print" | ConvertFrom-Json
    Assert-True ($rp.pages -ge 1) "25mm render-print Seiten = $($rp.pages)"
    $mp25File = "$OutDir\mp25.etprint"
    Invoke-Cmd "save-project $mp25File" | Out-Null

    # --- Szenario 4: Persistenz-Roundtrip -----------------------------------
    Write-Host "`n[4] Persistenz-Roundtrip" -ForegroundColor Yellow
    Invoke-Cmd "new-project" | Out-Null
    $s = Get-State
    Assert-Eq 0 $s.filledLabels "Nach new-project keine Etiketten"
    Assert-True (-not $s.isDirty) "Nach new-project nicht dirty"
    Invoke-Cmd "load-project $spFile" | Out-Null
    $s = Get-State
    Assert-Eq "VerticalDoubleHeader" $s.formatEnum "SP Format nach Laden"
    Assert-Eq 3 $s.filledLabels "SP Etiketten nach Laden"
    Invoke-Cmd "load-project $mpFile" | Out-Null
    $s = Get-State
    Assert-Eq "S71500_ET200MP" $s.productFamily "MP Familie nach Laden"
    Invoke-Cmd "select-module 0" | Out-Null
    $m = Get-MpState
    Assert-Eq "6ES7521-1BL00-0AB0" $m.article "MP Artikel nach Laden"
    Assert-Eq 32 $m.filledCells "MP Zellen nach Laden"
    Invoke-Cmd "select-module 6" | Out-Null
    Assert-Eq "SIWAREX_WP52x" (Get-MpState).variant "SIWAREX nach Laden"
    # AP1 1.11: 25mm-Projekt aus 35mm-Zustand laden -> Varianten/Artikel der 25mm-Familie
    Invoke-Cmd "load-project $mp25File" | Out-Null
    $s = Get-State
    Assert-Eq "S71500_ET200MP_25mm" $s.productFamily "25mm Familie nach Laden"
    Assert-True ($s.availableVariants -contains "MP25_16") "25mm Varianten enthalten MP25_16"
    Assert-True (-not ($s.availableVariants -contains "DI_DQ_16")) "25mm Varianten ohne DI_DQ_16"
    Assert-True ($s.availableArticles -contains "6ES7532-5NB00-0AB0") "25mm Artikel enthalten AQ 2"

    # --- Kalibrierseite -------------------------------------------------------
    $rp = Invoke-Cmd "render-calibration $OutDir\mp_calibration" | ConvertFrom-Json
    Assert-Eq 1 $rp.pages "Kalibrierseite gerendert"
}
catch {
    $script:failures.Add("Abbruch: $($_.Exception.Message)")
    Write-Host "  ABBRUCH $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    try { Send-Cmd "quit" | Out-Null } catch { }
    if (-not $proc.WaitForExit(5000)) { $proc | Stop-Process -Force }
}

Write-Host "`n$($script:checks - $script:failures.Count) / $($script:checks) Pruefungen bestanden" -ForegroundColor Cyan
if ($script:failures.Count -gt 0) {
    Write-Host "Fehlgeschlagen:" -ForegroundColor Red
    $script:failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
exit 0
