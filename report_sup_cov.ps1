$ErrorActionPreference = "Stop"
$rp = "C:\Users\nikit\source\repos\ArkWallet"
$xmlPath = (Get-ChildItem "$rp\cov11" -Recurse -Filter "coverage.cobertura.xml" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
Write-Output ("using: " + $xmlPath)
[xml]$doc = Get-Content $xmlPath
$allClasses = @($doc.SelectNodes('//class'))
Write-Output ("total classes in cov: " + $allClasses.Count)
$targets = @('SubscriptionContext','Payment','YooKassa','PaymentConfirmation','SubscriptionExpiry','InstantSuccess','SubscriptionsController','SubscriptionsQuery','SubscriptionsPurchasesController','SubscriptionsPurchase')
$rows = New-Object System.Collections.Generic.List[string]
$clsMatch = 0
foreach ($c in $allClasses) {
    $n = [string]$c.name
    if (-not ($targets | Where-Object { $n -match $_ })) { continue }
    $clsMatch++
    $linesNode = $c.SelectNodes('./lines/line')
    if (-not $linesNode) { continue }
    $total = 0; $uncovered = New-Object System.Collections.Generic.List[int]
    foreach ($l in $linesNode) {
        $total++
        if ([int]$l.hits -eq 0) { [void]$uncovered.Add([int]$l.number) }
    }
    if ($total -eq 0) { continue }
    $cov = if ($total -gt 0) { [math]::Round(100 * ($total - $uncovered.Count) / $total, 1) } else { 100 }
    $mf = ("{0}  tot={1} un={2} ({3}%)" -f $n, $total, $uncovered.Count, $cov)
    if ($uncovered.Count -gt 0) {
        $mf += "  lines=" + (($uncovered -join ','))
    }
    $rows.Add($mf)
}
Write-Output ("classes matched targets: " + $clsMatch)
$rows | Sort-Object | Set-Content "$rp\sup_uncovered_report.txt" -Encoding UTF8
Write-Output ("WROTE " + $rows.Count + " rows -> " + $rp + "\sup_uncovered_report.txt")