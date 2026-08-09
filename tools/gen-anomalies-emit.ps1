# 由 gen-anomalies.ps1 dot-source 调用：把解析结果写成 JSON
$effects = @{}
$report  = @()

foreach ($it in $script:items) {
  $id = $IDS[$it.num]
  $statKey = $STAT[$it.statCn]
  $isPct = $it.base -match '%'
  $baseNum = $null
  if ($statKey -and $it.levels[0].val -match '([\d.]+)') { $baseNum = [double]$Matches[1] }

  # --- visualTiers ---
  $procRarity = ($it.rarity -in @('normal','uneasy'))
  $tierJson = @()
  $bands = $TIERS[$it.maxLevel]
  for ($t = 0; $t -lt $bands.Count; $t++) {
    $lv = ($bands[$t] | ForEach-Object { $_ }) -join ', '
    if ($t -eq 0) {
      $sp = "Art/Anomaly/${id}_t0"; $pr = "null"
    } elseif ($procRarity) {
      $sp = "Art/Anomaly/${id}_t0"
      $pr = '{ "tint": null, "outline": null, "shake": null }'
    } else {
      $sp = "Art/Anomaly/${id}_t$t"; $pr = "null"
    }
    $tierJson += @"
        {
            "tier": $t,
            "levels": [$lv],
            "sprite": "$sp",
            "procedural": $pr
        }
"@
  }

  # --- levels ---
  $lvJson = @()
  foreach ($L in $it.levels) {
    $m = $MULT[$L.lv]
    $ov = "null"
    if ($null -ne $baseNum) {
      $actual = if ($L.val -match '([\d.]+)') { [double]$Matches[1] } else { $null }
      $expect = RoundHalfUp ($baseNum * $m)
      if ($null -ne $actual -and $actual -ne $expect) {
        $av = if ($isPct) { [Math]::Round($actual / 100, 4) } else { [int]$actual }
        $ov = "{ ""$statKey"": $av }"
        $report += "  override  #$($it.num) $($it.name) L$($L.lv): 表算 $expect / 设定集 $actual"
      }
    }
    $bf = "null"; $df = "null"
    if ($L.buff)   { $effects[$L.buff] = 1;   $bf = "{ ""effectId"": null, ""text"": $(Esc $L.buff) }" }
    if ($L.debuff) { $effects[$L.debuff] = 1; $df = "{ ""effectId"": null, ""text"": $(Esc $L.debuff) }" }
    $extra = ""
    if (-not $statKey) { $extra = "`n            ""effectText"": $(Esc $L.val)," }
    $lvJson += @"
        {
            "level": $($L.lv),
            "multiplier": $m,
            "statOverride": $ov,$extra
            "buff": $bf,
            "debuff": $df,
            "desc": $(Esc $L.desc)
        }
"@
  }

  $baseStats = "{}"
  if ($statKey -and $null -ne $baseNum) {
    $bv = if ($isPct) { [Math]::Round($baseNum / 100, 4) } else { [int]$baseNum }
    $baseStats = "{ ""$statKey"": $bv }"
  }
  $canonJson = if ($null -ne $CANON[$it.num]) { Esc $CANON[$it.num] } else { "null" }
  $nemesisOnly = if ($it.rarity -in @('lethal','void')) { "true" } else { "false" }

  $json = @"
{
    "id": "$id",
    "name": $(Esc $it.name),
    "rarity": "$($it.rarity)",
    "category": "$(if ($it.slot -eq 'carry') { 'carriable' } else { 'equipment' })",
    "slot": "$($it.slot)",
    "maxLevel": $($it.maxLevel),
    "inheritable": true,
    "hook": $(Esc $it.hook),
    "statKey": $(if ($statKey) { """$statKey""" } else { "null" }),
    "baseStats": $baseStats,
    "canonSource": $canonJson,
    "nemesisOnly": $nemesisOnly,
    "visualTiers": [
$($tierJson -join ",`n")
    ],
    "levels": [
$($lvJson -join ",`n")
    ],
    "source": { "events": [], "nemeses": [], "quests": [] }
}
"@
  $p = Join-Path $out "$id.json"
  [IO.File]::WriteAllText($p, $json, (New-Object Text.UTF8Encoding $false))
}

Write-Output "写出 $($script:items.Count) 个 JSON -> $out"
Write-Output "--- statOverride 命中 ($($report.Count)) ---"
$report | ForEach-Object { Write-Output $_ }
$inv = Join-Path $root "docs\_effect-inventory.txt"
$effects.Keys | Sort-Object | Out-File -LiteralPath $inv -Encoding UTF8
Write-Output "不重复 buff/debuff 文案: $($effects.Count) 条 -> $inv"
