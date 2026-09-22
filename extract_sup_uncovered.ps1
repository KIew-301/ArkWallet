$ErrorActionPreference = "Stop"
$rp = "C:\Users\nikit\source\repos\ArkWallet\"
$cov = Get-ChildItem "$rp\cov11" -Recurse -Filter "coverage.cobertura.xml" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Output ("cov=" + $cov.FullName + " len=" + $cov.Length)
[xml]$doc = Get-Content $cov.FullName
$out = New-Object System.Collections.Generic.List[string]
# collect ALL classes into a flat list by filename
$all = @()
$pkgs = @($doc.coverage.packages.packages.package)
foreach ($pkg in $pkgs) {
    foreach ($c in @($pkg.classes.classes.class)) {
        $all += ,$c
    }
}
$interesting = @($all | Where-Object {
    $_.filename -match "SubscriptionContext|Payment|YooKassa|SubscriptionsController|SubscriptionPurchasesController|YooKassaPayment|InstantSuccess|PaymentConfirmation|SubscriptionExpiry|SubscriptionExpiry"
})
Write-Output ("interesting classes: " + $interesting.Count)
foreach ($c in $interesting) {
    $un = @($c.lines.line | Where-Object { $_.hits -eq 0 })
    $tot = @($c.lines.line).Count
    if ($tot -eq 0) { continue }
    $fn = $c.filename
    $short = $fn.Replace($rp, "").Replace("source\repos\ArkWallet\", "")
    $unNums = ($un | ForEach-Object { $_.number }) -join ","
    # read source lines for context
    $srcLines = @()
    $abs = $fn
    if (-not (Test-Path $abs)) { $abs = Join-Path $rp $fn; if (-not (Test-Path $abs)) { $abs = Join-Path ($rp) ($fn -replace '^.*ArkWallet\\','') } }
    if (Test-Path $abs) { $srcLines = Get-Content $abs }
    $out.Add("=== " + $short)
    $out.Add("    total=" + $tot + " uncovered=" + ($un.Count) + "  UN: " + $unNums)
    foreach ($u in $un) {
        $n = [int]$u.number
        $t = ""
        if ($n -le $srcLines.Count) { $t = $srcLines[$n-1].Trim() }
        $out.Add("       L" + $n + ": " + $t)
    }
    $out.Add("")
}
$out | Set-Content "C:\Users\nikit\source\repos\ArkWallet\sup_uncovered.txt" -Encoding UTF8
Write-Output ("WROTE " + $out.Count + " lines")
