# Writes effectId into every buff/debuff of the 30 anomaly definitions.
#
# Uses targeted regex replacement rather than ConvertFrom-Json/ConvertTo-Json:
# PowerShell 5.1's ConvertTo-Json escapes non-ASCII to \uXXXX, which would
# mangle 281 lines of readable Chinese. Regex preserves formatting byte-for-byte.
#
#   .\tools\write-effect-ids.ps1            # write
#   .\tools\write-effect-ids.ps1 -DryRun    # report only

param([switch]$DryRun)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
. "$PSScriptRoot\effect-rules.ps1"

$dir = Join-Path $root "unity\Assets\Resources\Data\anomalies"
$pattern = '("(?:buff|debuff)":\s*\{\s*"effectId":\s*)null(\s*,\s*"text":\s*")((?:[^"\\]|\\.)*)(")'

$counts = @{}
$narrative = @()
$touched = 0
$total = 0

foreach ($f in Get-ChildItem "$dir\*.json") {
    $raw = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)

    $evaluator = [System.Text.RegularExpressions.MatchEvaluator] {
        param($m)
        $script:total++
        $text = $m.Groups[3].Value
        $kind = Get-EffectKind -Text $text
        if (-not $kind) {
            $script:narrative += "$($f.BaseName): $text"
            return $m.Value          # leave null; flavour text has no hook
        }
        if ($script:counts.ContainsKey($kind)) { $script:counts[$kind]++ }
        else { $script:counts[$kind] = 1 }
        return $m.Groups[1].Value + '"' + $kind + '"' +
               $m.Groups[2].Value + $text + $m.Groups[4].Value
    }

    $new = [regex]::Replace($raw, $pattern, $evaluator)

    if ($new -ne $raw) {
        $touched++
        if (-not $DryRun) {
            # No BOM: Unity's JsonUtility chokes on a leading BOM.
            [System.IO.File]::WriteAllText($f.FullName, $new,
                (New-Object System.Text.UTF8Encoding($false)))
        }
    }
}

$classified = ($counts.Values | Measure-Object -Sum).Sum
Write-Output "mode:       $(if ($DryRun) { 'DRY RUN' } else { 'WRITE' })"
Write-Output "effects:    $total"
Write-Output "classified: $classified"
Write-Output "narrative:  $($narrative.Count)"
Write-Output "files:      $touched / 30"
Write-Output "distinct:   $($counts.Keys.Count)"
Write-Output ""
$counts.GetEnumerator() | Sort-Object Value -Descending |
    ForEach-Object { Write-Output ("  {0,-16} {1}" -f $_.Key, $_.Value) }

if ($narrative.Count) {
    Write-Output ""
    Write-Output "left as narrative (effectId stays null):"
    $narrative | ForEach-Object { Write-Output "  $_" }
}
