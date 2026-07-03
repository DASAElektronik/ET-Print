$ErrorActionPreference = 'Stop'
$outDir = Join-Path $PSScriptRoot 'xls_dump'
New-Item -ItemType Directory -Force $outDir | Out-Null

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$excel.AutomationSecurity = 3  # msoAutomationSecurityForceDisable — keine Makros
try {
    $wb = $excel.Workbooks.Open('C:\claude\Beschriftung\Excel_Template_S71500_ET200MP.xls', 0, $true)
    try {
        $names = @($wb.Worksheets | ForEach-Object { $_.Name })
        Set-Content (Join-Path $outDir '_sheets.txt') ($names -join "`r`n")
        Write-Output ("Sheets: " + ($names -join ' | '))

        foreach ($ws in $wb.Worksheets) {
            if ($ws.Name -notmatch 'horizontal|vertical') { continue }
            $ur = $ws.UsedRange
            $rows = $ur.Rows.Count; $cols = $ur.Columns.Count
            $r0 = $ur.Row; $c0 = $ur.Column
            $sb = [System.Text.StringBuilder]::new()
            [void]$sb.AppendLine("SHEET: $($ws.Name)  UsedRange: R$r0 C$c0 rows=$rows cols=$cols")

            [void]$sb.AppendLine("--- Spaltenbreiten (Spalte: Width) ---")
            for ($c = $c0; $c -lt $c0 + $cols; $c++) {
                $w = $ws.Columns.Item($c).ColumnWidth
                [void]$sb.AppendLine("Col $c : $w")
            }
            [void]$sb.AppendLine("--- Zeilenhoehen (Zeile: Punkte) ---")
            for ($r = $r0; $r -lt $r0 + $rows; $r++) {
                $h = $ws.Rows.Item($r).RowHeight
                [void]$sb.AppendLine("Row $r : $h")
            }

            [void]$sb.AppendLine("--- Zellen (Row,Col) [Merge] {Rot} = Wert ---")
            $mergesSeen = [System.Collections.Generic.HashSet[string]]::new()
            for ($r = $r0; $r -lt $r0 + $rows; $r++) {
                for ($c = $c0; $c -lt $c0 + $cols; $c++) {
                    $cell = $ws.Cells.Item($r, $c)
                    $isMerged = $cell.MergeCells
                    $mergeInfo = ''
                    if ($isMerged) {
                        $addr = $cell.MergeArea.Address($false, $false)
                        if (-not $mergesSeen.Add($addr)) { continue }  # nur Anker ausgeben
                        $mergeInfo = " [MERGE $addr]"
                    }
                    $v = $cell.Value2
                    $rot = $cell.Orientation
                    $rotInfo = if ($rot -ne 0 -and $rot -ne -4128) { " {rot=$rot}" } else { '' }
                    if ($null -ne $v -and "$v" -ne '') {
                        [void]$sb.AppendLine("($r,$c)$mergeInfo$rotInfo = $v")
                    } elseif ($mergeInfo) {
                        [void]$sb.AppendLine("($r,$c)$mergeInfo$rotInfo = (leer)")
                    }
                }
            }
            $safe = $ws.Name -replace '[^\w\-]', '_'
            Set-Content (Join-Path $outDir "$safe.txt") $sb.ToString()
            Write-Output "Dumped: $($ws.Name) ($rows x $cols)"
        }
    } finally { $wb.Close($false) }
} finally {
    $excel.Quit()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
}
Write-Output "Fertig -> $outDir"
