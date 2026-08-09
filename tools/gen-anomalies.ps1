# 从 docs/AnomalyItems.md 生成 30 个异常物品 JSON
# 用法: powershell -NoProfile -File tools\gen-anomalies.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$md   = Join-Path $root "docs\AnomalyItems.md"
$out  = Join-Path $root "unity\Assets\Resources\Data\anomalies"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$MULT = @{1=1.0; 2=1.5; 3=2.0; 4=3.0; 5=4.5; 6=7.0; 7=11.0; 8=18.0; 9=30.0}
$SLOT = @{ "大脑"="brain"; "躯干"="torso"; "手"="hand"; "腿"="leg"; "携带"="carry" }
$STAT = @{ "感知"="perception"; "防御"="defense"; "攻击"="attack"; "闪避"="dodge";
           "韧性"="resilience"; "速度"="speed"; "机缘"="fortune"; "效果"=$null }
$RARE = @{ "普通"=@("normal",3); "不对劲"=@("uneasy",4); "出问题了"=@("glitch",5);
           "离谱了"=@("absurd",6); "要命了"=@("lethal",7); "已经无所谓了"=@("void",9) }
$TIERS = @{
  3=@(@(1,2),@(3)); 4=@(@(1,2),@(3,4)); 5=@(@(1,2),@(3,4),@(5))
  6=@(@(1,2),@(3,4),@(5,6)); 7=@(@(1,2,3),@(4,5),@(6,7)); 9=@(@(1,2),@(3,4),@(5,6,7),@(8,9))
}
$IDS = @{
  1="borrowed_reading_glasses"; 2="waistband_elastic";      3="household_scissors"
  4="flip_flops";               5="pilled_nylon_socks";     6="tin_thermos"
  7="roast_goose_cleaver";      8="overprescribed_glasses"; 9="fake_leather_belt"
 10="fast_digital_watch";      11="liberation_shoes";      12="bailing_lighter"
 13="indoor_umbrella";         14="swapped_photo_badge";   15="stiff_suit"
 16="borrowed_ring";           17="foot_soaked_shoes";     18="still_ringing_pager"
 19="unaffordable_key";        20="renovation_crew_vest";  21="real_umbrella"
 22="walking_sneakers";        23="correct_wall_calendar"; 24="a_bit_of_the_child"
 25="stamped_earring";         26="landlord_keyring";      27="backdated_notebook"
 28="kuntong_card";            29="ground_down_lens";      30="final_payslip"
}
$CANON = @{ 7="GDD v1.2 §20.4"; 13="GDD v1.2 §20.4"; 28="GDD v1.2 §20.4 (L1/4/7/9)" }

function Esc([string]$s) {
  if ($null -eq $s) { return "null" }
  $s = $s -replace '\\','\\' -replace '"','\"' -replace "`r",'' -replace "`n",'\n'
  return '"' + $s + '"'
}
function RoundHalfUp([double]$v) { return [int][Math]::Floor($v + 0.5) }

# --- 解析 ---
$lines = Get-Content -LiteralPath $md -Encoding UTF8
$items = @(); $cur = $null; $rarity = $null; $maxLv = 0
foreach ($ln in $lines) {
  if ($ln -match '^## ') {
    # 任何二级标题都终止上一条，避免附录表格被算进最后一条
    if ($cur) { $items += $cur; $cur = $null }
    if ($ln -match '(普通|不对劲|出问题了|离谱了|要命了|已经无所谓了)\s+\w+') {
      $r = $RARE[$Matches[1]]; $rarity = $r[0]; $maxLv = $r[1]
    }
    continue
  }
  if ($ln -match '^### (\d+)\.\s*(.+?)\s*(\*\(.*\)\*)?\s*$') {
    if ($cur) { $items += $cur }
    $cur = [ordered]@{ num=[int]$Matches[1]; name=$Matches[2].Trim(); rarity=$rarity
                       maxLevel=$maxLv; levels=@() }
    continue
  }
  if (-not $cur) { continue }
  if ($ln -match '^\*\*位\*\*:\s*(.+?)\s*\|\s*\*\*层1基础值\*\*:\s*(.+?)\s*\|\s*\*\*钩子\*\*:\s*(.+?)\s*$') {
    $cur.slot = $SLOT[$Matches[1].Trim()]; $cur.base = $Matches[2].Trim(); $cur.hook = $Matches[3].Trim()
    continue
  }
  if ($ln -match '^\|\s*层级\s*\|\s*(\S+?)\s*\|') { $cur.statCn = $Matches[1]; continue }
  if ($ln -match '^\|\s*(\d+)\s*\|') {
    $c = ($ln -split '\|') | ForEach-Object { $_.Trim() }
    $c = $c[1..($c.Count-2)]
    if ($c.Count -lt 5) { continue }
    $cur.levels += ,[ordered]@{ lv=[int]$c[0]; val=$c[1]
      buff=$(if ($c[2] -eq '无') { $null } else { $c[2] })
      debuff=$(if ($c[3] -eq '无') { $null } else { $c[3] })
      desc=$c[4].Trim('"') }
  }
}
if ($cur) { $items += $cur }
Write-Output "解析到 $($items.Count) 条，层数合计 $(($items | ForEach-Object { $_.levels.Count } | Measure-Object -Sum).Sum)"
$script:items = $items
. (Join-Path $PSScriptRoot "gen-anomalies-emit.ps1")
