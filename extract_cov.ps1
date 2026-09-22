$ErrorActionPreference = "Stop"
$cov = Get-ChildItem "C:\Users\nikit\source\repos\ArkWallet\cov11" -Recurse -Filter "coverage.cobertura.xml" | Select-Object -First 1
if (-not $cov) { Write-Output "NO COVERAGE FILE"; exit 1 }
Write-Output ("cov file: " + $cov.FullName + " len=" + $cov.Length)
[xml]$doc = Get-Content $cov.FullName
$lines = New-Object System.Collections.Generic.List[string]
$pkgs = $doc.SelectNodes("//packages")
Write-Output ("packages nodes: " + $pkgs.Count)
$totalUn = 0; $totalCls = 0
foreach ($pkg in $pkgs) {
    foreach ($classesNode in $pkg.SelectNodes("classes")) {
        foreach ($c in $classesNode.SelectNodes("class")) {
            $fn = $c.GetAttribute("filename")
            if ($fn -match "SubscriptionContext|Payment|YooKassa|ConfirmationWorker|ExpiryWorker|InstantSuccess|SubscriptionsController|SubscriptionPurchases") {
                $un = $c.SelectNodes("lines/line[.//@hits=0]")
                if ($un -and $un.Count -gt 0) {
                    $totalUn += $un.Count
                    $totalCls++
                    $short = $fn.Substring([Math]::Max(0,$fn.LastIndexOf("ArkWallet\")+9))
                    $lineNums = @()
                    $srcLines = @()
                    if (Test-Path $fn) { $srcLines = Get-Content $fn }
                    foreach ($l in $un) {
                        $num = [int]$l.GetAttribute("number")
                        $lineNums += $num
                        $txt = ""
                        if ($num -le $srcLines.Count) { $txt = $srcLines[$num-1].Trim() }
                        $lines.Add(("  L{0} {1}" -f $num, $txt))
                    }
                    $lines.Add("## " + $short + "  uncovered=" + (($lineNums | ForEach-Object { $_ }) -join ","))
                }
            }
        }
    }
}
$lines.Add("=== totals: classes=" + $totalCls + " uncoveredLines=" + $totalUn)
$lines | Set-Content "C:\Users\nikit\source\repos\ArkWallet\cov_subscription_uncovered.txt" -Encoding UTF8
Write-Output ("WROTE " + $lines.Count + " lines (totals: classes=" + $totalCls + " unLines=" + $totalUn + ")")