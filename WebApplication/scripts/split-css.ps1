$ErrorActionPreference = "Stop"
$root = Join-Path (Join-Path $PSScriptRoot "..") "wwwroot\css"
$site = Get-Content (Join-Path $root "site.css")
$add = Get-Content (Join-Path $root "additions.css")

function Slice-Site([int]$start, [int]$end) {
    $s = $start - 1
    $e = [Math]::Min($end - 1, $site.Count - 1)
    ($site[$s..$e] -join "`n").TrimEnd() + "`n"
}

function Slice-Add([int]$start, [int]$end) {
    $s = $start - 1
    $e = [Math]::Min($end - 1, $add.Count - 1)
    ($add[$s..$e] -join "`n").TrimEnd() + "`n"
}

function Merge-Site([object[]]$ranges) {
    ($ranges | ForEach-Object { Slice-Site $_[0] $_[1] }) -join "`n"
}

function Merge-Add([object[]]$ranges) {
    ($ranges | ForEach-Object { Slice-Add $_[0] $_[1] }) -join "`n"
}

Set-Content (Join-Path $root "base/fonts.css") -Value $site[0] -Encoding utf8
Set-Content (Join-Path $root "base/variables.css") -Value (Slice-Site 3 20) -Encoding utf8
Set-Content (Join-Path $root "base/reset.css") -Value (Slice-Site 22 33) -Encoding utf8
Set-Content (Join-Path $root "components/checkbox.css") -Value (Slice-Site 175 228) -Encoding utf8

Set-Content (Join-Path $root "components/buttons.css") -Value (Merge-Site @(
    @(293, 332), @(351, 358), @(791, 817), @(1120, 1136), @(1373, 1443)
)) -Encoding utf8

$formsCss = ((Merge-Site @(@(686, 697), @(1048, 1118), @(1232, 1260), @(1361, 1371), @(1445, 1453))).TrimEnd() + "`n`n" + (Slice-Site 1138 1169).TrimEnd() + "`n")
Set-Content (Join-Path $root "components/forms.css") -Value $formsCss -Encoding utf8

Set-Content (Join-Path $root "components/tables.css") -Value (Merge-Site @(
    @(642, 680), @(1223, 1230), @(1540, 1564), @(1600, 1687)
)) -Encoding utf8

$tabsCss = ((Slice-Site 601 640).TrimEnd() + "`n`n" + (Slice-Add 3 29).TrimEnd() + "`n")
Set-Content (Join-Path $root "components/tabs.css") -Value $tabsCss -Encoding utf8

Set-Content (Join-Path $root "components/modals.css") -Value (Slice-Site 1566 1598) -Encoding utf8
Set-Content (Join-Path $root "components/toast.css") -Value (Slice-Site 1262 1359) -Encoding utf8
Set-Content (Join-Path $root "components/toolbar.css") -Value (Slice-Site 1455 1538) -Encoding utf8
Set-Content (Join-Path $root "components/alerts.css") -Value (Slice-Add 31 52) -Encoding utf8
Set-Content (Join-Path $root "components/utilities.css") -Value (Slice-Add 54 64) -Encoding utf8

$shellCss = ((Merge-Site @(@(418, 640), @(833, 848))).TrimEnd() + "`n`n" + (Slice-Site 1041 1047).TrimEnd() + "`n")
Set-Content (Join-Path $root "layout/shell.css") -Value $shellCss -Encoding utf8

Set-Content (Join-Path $root "features/auth.css") -Value (Merge-Site @(
    @(35, 174), @(230, 291), @(333, 345), @(360, 416)
)) -Encoding utf8

$dashboardCss = ((Merge-Site @(@(819, 1039), @(1171, 1173))).TrimEnd() + "`n`n" + (Slice-Add 849 1103).TrimEnd() + "`n")
Set-Content (Join-Path $root "features/dashboard.css") -Value $dashboardCss -Encoding utf8

$usersCss = ((Merge-Site @(@(699, 817), @(1175, 1221))).TrimEnd() + "`n`n" + (Slice-Add 66 212).TrimEnd() + "`n")
Set-Content (Join-Path $root "features/users.css") -Value $usersCss -Encoding utf8

$reportsCss = ((Slice-Site 346 349).TrimEnd() + "`n`n" + (Slice-Add 214 847).TrimEnd() + "`n")
Set-Content (Join-Path $root "features/reports.css") -Value $reportsCss -Encoding utf8

Write-Host "CSS split complete."
