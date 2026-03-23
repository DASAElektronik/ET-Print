# Test-Automation Client fuer ET-Printer
# Verwendung: .\test-send.ps1 "command arg1 arg2"
# Beispiel:   .\test-send.ps1 "ping"
#             .\test-send.ps1 "state"
#             .\test-send.ps1 "screenshot C:\claude\Beschriftung\test_screenshot.png"

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$Command
)

$pipeName = "ETPrinter_TestAutomation"
$pipe = $null

try {
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", $pipeName, [System.IO.Pipes.PipeDirection]::InOut)
    $pipe.Connect(3000)  # 3 Sekunden Timeout

    $writer = New-Object System.IO.StreamWriter($pipe)
    $writer.AutoFlush = $true
    $reader = New-Object System.IO.StreamReader($pipe)

    $writer.WriteLine($Command)
    $response = $reader.ReadLine()

    Write-Output $response
}
catch {
    Write-Error "Fehler: $_"
    Write-Error "Ist die ET-Printer App gestartet?"
}
finally {
    if ($pipe) { $pipe.Dispose() }
}
