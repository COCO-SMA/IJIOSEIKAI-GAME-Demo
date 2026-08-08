# 每日同步到 GitHub：本地内容覆盖远端，保留提交历史。
#
#   .\tools\sync-github.ps1                 # 预演：只看会推什么，不改远端
#   .\tools\sync-github.ps1 -Push           # 真的提交并推送
#   .\tools\sync-github.ps1 -Push -Message "结局系统"
#   .\tools\sync-github.ps1 -Push -SkipVerify   # 跳过 Unity 编译验证（省 10 分钟）
#
# 不使用 force push —— 历史保留，任何一天都能 revert 回去。

param(
    [switch]$Push,
    [switch]$SkipVerify,
    [string]$Message = ""
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$UnityExe = "E:\2022.3.62f3c1\Editor\Unity.exe"
$MaxMB = 50

$env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
            [Environment]::GetEnvironmentVariable("Path", "User")

Set-Location $RepoRoot

function Fail($msg) { Write-Host "[中止] $msg" -ForegroundColor Red; exit 1 }
function Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

# --- 1. 环境 ---
Step "环境检查"
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { Fail "git 不在 PATH。" }
if (-not (Test-Path (Join-Path $RepoRoot ".git"))) { Fail "$RepoRoot 不是 git 仓库。" }
git remote get-url origin | ForEach-Object { Write-Host "远端: $_" }

# --- 2. Unity 编译验证 ---
if ($SkipVerify) {
    Write-Host "已跳过编译验证（-SkipVerify）。" -ForegroundColor Yellow
} elseif (-not (Test-Path $UnityExe)) {
    Write-Host "未找到 Unity（$UnityExe），跳过编译验证。" -ForegroundColor Yellow
} else {
    Step "Unity 编译验证（冷启动约 10 分钟）"
    $log = Join-Path $env:TEMP "unity_sync_verify.log"
    if (Test-Path $log) { Remove-Item $log -Force }
    $p = Start-Process -FilePath $UnityExe -PassThru -Wait -ArgumentList @(
        "-batchmode", "-quit", "-projectPath", (Join-Path $RepoRoot "unity"), "-logFile", $log
    )
    $errs = @()
    if (Test-Path $log) {
        $errs = Select-String -Path $log -Pattern "error CS" |
                Select-Object -ExpandProperty Line -Unique
    }
    if ($errs.Count -gt 0) {
        $errs | Select-Object -First 15 | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        Fail "存在编译错误，不推送。先修好再来。"
    }
    if ($p.ExitCode -ne 0) { Fail "Unity 退出码 $($p.ExitCode)。日志: $log" }
    Write-Host "编译通过。" -ForegroundColor Green
}

# --- 3. 暂存并核对 ---
Step "核对将要推送的内容"
git add -A

$staged = @(git diff --cached --name-only)
if ($staged.Count -eq 0) {
    Write-Host "没有任何改动，无需推送。" -ForegroundColor Yellow
    exit 0
}

# 硬闸：这些绝不能上公开仓库
$banned = @{
    "unity/Library" = "^unity/Library/"
    "node_modules"  = "node_modules/"
    "data/"         = "^data/"
    ".workbuddy"    = "^\.workbuddy/"
    "settings.local"= "settings\.local\.json$"
}
foreach ($name in $banned.Keys) {
    $hit = @($staged | Where-Object { $_ -match $banned[$name] })
    if ($hit.Count -gt 0) {
        $hit | Select-Object -First 5 | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        Fail "$name 被暂存了（共 $($hit.Count) 个）。.gitignore 出问题了，先修它。"
    }
}

$totalMB = 0
foreach ($f in $staged) {
    $p2 = Join-Path $RepoRoot $f
    if (Test-Path -LiteralPath $p2) { $totalMB += (Get-Item -LiteralPath $p2).Length }
}
$totalMB = [math]::Round($totalMB / 1MB, 2)
if ($totalMB -gt $MaxMB) { Fail "暂存体积 $totalMB MB 超过上限 $MaxMB MB。可能误加了大文件。" }

# .meta 完整性：新增 Assets 资源必须带 .meta
$assetsNew = @(git diff --cached --name-only --diff-filter=A |
    Where-Object { $_ -match "^unity/Assets/" -and $_ -notmatch "\.meta$" })
$metaMissing = @($assetsNew | Where-Object { -not (Test-Path -LiteralPath (Join-Path $RepoRoot "$_.meta")) })
if ($metaMissing.Count -gt 0) {
    $metaMissing | Select-Object -First 10 | ForEach-Object { Write-Host "  缺 .meta: $_" -ForegroundColor Yellow }
    Write-Host "提示：用 Unity 打开一次工程会自动生成 .meta。缺了会导致引用断裂。" -ForegroundColor Yellow
}

Write-Host "文件数: $($staged.Count)    体积: $totalMB MB"
Write-Host "`n改动明细:"
git diff --cached --stat | Select-Object -Last 25

# --- 4. 提交并推送 ---
if (-not $Push) {
    Step "预演结束"
    git reset --quiet
    Write-Host "以上是会被推送的内容，远端未做任何改动。"
    Write-Host "确认无误后加 -Push 真正执行。" -ForegroundColor Cyan
    exit 0
}

Step "提交并推送"
if (-not (git config user.email)) { Fail "git user.email 未设置。" }

if ([string]::IsNullOrWhiteSpace($Message)) {
    $Message = "每日迭代 " + (Get-Date -Format "yyyy-MM-dd")
} else {
    $Message = (Get-Date -Format "yyyy-MM-dd") + " " + $Message
}

git commit -m $Message
if ($LASTEXITCODE -ne 0) { Fail "提交失败。" }

git push -u origin main
if ($LASTEXITCODE -ne 0) {
    Write-Host "推送失败。提交已在本地，没有丢。" -ForegroundColor Yellow
    Write-Host "若因未登录: gh auth login" -ForegroundColor Yellow
    Write-Host "若远端有本地没有的提交: git pull --rebase origin main" -ForegroundColor Yellow
    exit 1
}

Write-Host "`n完成: $Message" -ForegroundColor Green
Write-Host "https://github.com/COCO-SMA/IJIOSEIKAI-GAME-Demo"
