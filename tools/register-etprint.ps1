<#
.SYNOPSIS
  Verknuepft die Dateiendung .etprint mit ET-Printer (nur fuer den aktuellen Benutzer, HKCU).

.DESCRIPTION
  Schreibt die Dateizuordnung in HKCU:\Software\Classes — keine Adminrechte noetig.
  Danach oeffnet ein Doppelklick auf eine .etprint-Datei ET-Printer mit diesem Projekt.
  Mit -Remove wird die Zuordnung wieder entfernt.

.EXAMPLE
  .\register-etprint.ps1                      # nutzt ..\publish\ET-Printer.exe
  .\register-etprint.ps1 -Exe D:\Tools\ET-Printer.exe
  .\register-etprint.ps1 -Remove
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot "..\publish\ET-Printer.exe"),
    [switch]$Remove
)

$ErrorActionPreference = "Stop"
$progId = "ETPrinter.Project"
$classes = "HKCU:\Software\Classes"

if ($Remove) {
    Remove-Item -Path "$classes\.etprint" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "$classes\$progId" -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Zuordnung .etprint entfernt."
    exit 0
}

$Exe = [System.IO.Path]::GetFullPath($Exe)
if (-not (Test-Path $Exe)) { throw "ET-Printer.exe nicht gefunden: $Exe" }

New-Item -Path "$classes\.etprint" -Force | Out-Null
Set-ItemProperty -Path "$classes\.etprint" -Name "(default)" -Value $progId

New-Item -Path "$classes\$progId" -Force | Out-Null
Set-ItemProperty -Path "$classes\$progId" -Name "(default)" -Value "ET-Printer Projekt"
New-Item -Path "$classes\$progId\DefaultIcon" -Force | Out-Null
Set-ItemProperty -Path "$classes\$progId\DefaultIcon" -Name "(default)" -Value "`"$Exe`",0"
New-Item -Path "$classes\$progId\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "$classes\$progId\shell\open\command" -Name "(default)" -Value "`"$Exe`" `"%1`""

# Explorer ueber die Aenderung informieren (Icons/Zuordnung sofort aktiv)
$sig = '[DllImport("shell32.dll")] public static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);'
$shell = Add-Type -MemberDefinition $sig -Name "Shell32Notify" -Namespace "ETPrinter" -PassThru
$shell::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)

Write-Host ".etprint ist jetzt mit $Exe verknuepft (HKCU)."
