param(
    [Parameter(Mandatory)][string]$Pdf,
    [Parameter(Mandatory)][string]$OutPrefix
)
$bin = 'C:\claude\Beschriftung\src\ETPrinter\bin\Debug\net9.0-windows'
foreach ($d in 'UglyToad.PdfPig.Core','UglyToad.PdfPig.Tokens','UglyToad.PdfPig.Tokenization','UglyToad.PdfPig.Fonts','UglyToad.PdfPig') {
    Add-Type -Path (Join-Path $bin "$d.dll") -ErrorAction SilentlyContinue
}
$doc = [UglyToad.PdfPig.PdfDocument]::Open($Pdf)
try {
    for ($i = 1; $i -le $doc.NumberOfPages; $i++) {
        $page = $doc.GetPage($i)
        if ($page.Text -notmatch 'terminal assignment|Block diagram|potential jumpers') { continue }
        Write-Output "=== Seite $i (relevanter Text) ==="
        # Umgebung der Schluesselstellen ausgeben
        foreach ($m in [regex]::Matches($page.Text, '.{0,220}(supply voltage to terminals|potential jumpers|Block diagram and terminal assignment).{0,220}')) {
            Write-Output ("TEXT: " + $m.Value)
        }
        $j = 0
        foreach ($img in $page.GetImages()) {
            $j++
            $raw = $img.RawMemory.ToArray()
            if ($raw.Length -gt 100 -and $raw[0] -eq 0xFF -and $raw[1] -eq 0xD8) {
                $p = "$OutPrefix-p$i-$j.jpg"
                [System.IO.File]::WriteAllBytes($p, $raw)
                Write-Output "BILD: $p ($($img.WidthInSamples)x$($img.HeightInSamples), $($raw.Length) B)"
            } else {
                $png = $null
                if ($img.TryGetPng([ref]$png) -and $png) {
                    $p = "$OutPrefix-p$i-$j.png"
                    [System.IO.File]::WriteAllBytes($p, $png)
                    Write-Output "BILD: $p (PNG, $($img.WidthInSamples)x$($img.HeightInSamples))"
                } else {
                    Write-Output "BILD uebersprungen (Filter nicht unterstuetzt, $($img.WidthInSamples)x$($img.HeightInSamples))"
                }
            }
        }
    }
} finally { $doc.Dispose() }
