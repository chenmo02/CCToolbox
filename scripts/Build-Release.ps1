<#
  CCToolbox 一键打包
  用法：双击 scripts\一键打包.bat 后按提示输入版本号；或命令行：
    powershell -ExecutionPolicy Bypass -File scripts\Build-Release.ps1 -Version 2.3.9
    powershell -ExecutionPolicy Bypass -File scripts\Build-Release.ps1 -Version 2.3.9 -DryRun

  会做：
    1. 同步 WpfApp1.csproj 的 <Version> / <AssemblyVersion> / <FileVersion> / <InformationalVersion>
    2. dotnet publish 单文件自包含发布（单节点编译，规避 BG1002）
    3. 校验产物 FileVersion 与新版本号一致
    4. 计算大小 / SHA256，更新 updates\stable\latest.json
    5. 同步官网下载区静态快照（版本 / 日期 / 大小）
    6. 在 artifacts\releases\ 归档 CCToolbox-vX.Y.Z.exe 与 .sha256
  不会做（需人工确认或证书）：
    git commit / tag / push、上传 Releases、代码签名
#>
[CmdletBinding()]
param(
    [string]$Version = "",
    [string[]]$Changelog = @(),
    [switch]$SkipWebsite,
    [switch]$SkipManifest,
    [switch]$SkipArchive,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $root "WpfApp1\WpfApp1.csproj"
$exePath = Join-Path $root "artifacts\publish\win-x64-single\CCToolbox.exe"
$manifestPath = Join-Path $root "updates\stable\latest.json"
$websitePath = Join-Path $root "index.html"
$manifestScript = Join-Path $PSScriptRoot "publish-update-manifest.ps1"
$archiveDir = Join-Path $root "artifacts\releases"
$releaseDate = Get-Date -Format "yyyy-MM-dd"

function Write-TextPreservingBom {
    param([string]$Path, [string]$Text)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($hasBom)))
}

function Get-CsprojVersion {
    $node = (Select-Xml -Path $csproj -XPath "/Project/PropertyGroup/Version/text()").Node
    if ($null -eq $node) { throw "无法从 WpfApp1.csproj 读取 <Version>" }
    return $node.Value.Trim()
}

if (-not (Test-Path $csproj)) { throw "找不到项目文件：$csproj" }
$currentVersion = Get-CsprojVersion

if ([string]::IsNullOrWhiteSpace($Version)) {
    $typedVersion = Read-Host "请输入版本号（当前 $currentVersion，直接回车沿用）"
    if ([string]::IsNullOrWhiteSpace($typedVersion)) { $Version = $currentVersion } else { $Version = $typedVersion }
}
$Version = $Version.Trim() -replace '^[vV]', ''
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "版本号必须是三段数字（例如 2.3.9），当前输入：$Version" }

try {
    $existingTag = @(& git tag --list "v$Version" 2>$null)
    if ($existingTag.Count -gt 0) { Write-Host "警告：本地已存在标签 v$Version，请确认是否重复发版。" -ForegroundColor Yellow }
} catch { }

Write-Host ""
Write-Host "=== CCToolbox 一键打包 ===" -ForegroundColor Cyan
Write-Host "目标版本 : v$Version（当前 $currentVersion）"
Write-Host "发布配置 : SingleFile-win-x64（自包含 / 单文件 / 压缩）"
Write-Host "发布日期 : $releaseDate"
if ($DryRun) { Write-Host "模式     : 演练（不修改文件、不执行发布）" -ForegroundColor Yellow }
Write-Host ""

try {
    $dirty = @(& git status --porcelain 2>$null)
    if ($dirty.Count -gt 0) {
        Write-Host "提示：工作区有 $($dirty.Count) 处未提交改动，打包不会自动提交。" -ForegroundColor Yellow
    }
} catch { }

# 1. csproj 四处版本号
$originalCsproj = Get-Content -Path $csproj -Raw -Encoding UTF8
$updatedCsproj = $originalCsproj
$updatedCsproj = $updatedCsproj -replace '(?<=<Version>)[^<]+', $Version
$updatedCsproj = $updatedCsproj -replace '(?<=<AssemblyVersion>)[^<]+', "$Version.0"
$updatedCsproj = $updatedCsproj -replace '(?<=<FileVersion>)[^<]+', "$Version.0"
$updatedCsproj = $updatedCsproj -replace '(?<=<InformationalVersion>)[^<]+', $Version

Write-Host "[1/6] WpfApp1.csproj 版本号 -> $Version / $Version.0"

if ($DryRun) {
    Write-Host ""
    Write-Host "将要改动：WpfApp1.csproj、updates\stable\latest.json、index.html，并归档 CCToolbox-v$Version.exe"
    Write-Host "演练结束：未写入任何文件，也未执行发布。去掉 -DryRun 即可正式打包。" -ForegroundColor Yellow
    return
}

try {
    Write-TextPreservingBom $csproj $updatedCsproj
    if ((Get-CsprojVersion) -ne $Version) { throw "csproj 版本号写回校验失败" }

    # 2. 发布
    Write-Host "[2/6] dotnet publish ..." -ForegroundColor Cyan
    Push-Location $root
    try {
        & dotnet publish "WpfApp1/WpfApp1.csproj" -c Release -p:PublishProfile=SingleFile-win-x64 -m:1 -nr:false -tl:false --nologo -v minimal
    } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败（退出码 $LASTEXITCODE）" }
    if (-not (Test-Path $exePath)) { throw "未找到发布产物：$exePath" }

    # 3. 产物版本与哈希校验
    $fileInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
    if ($fileInfo.FileVersion -ne "$Version.0") {
        throw "产物 FileVersion = $($fileInfo.FileVersion)，期望 $Version.0；版本号未正确写入 exe"
    }
    $sizeBytes = (Get-Item $exePath).Length
    $sizeMB = ([math]::Round($sizeBytes / 1MB, 1)).ToString("0.0", [System.Globalization.CultureInfo]::InvariantCulture)
    $sha256 = (Get-FileHash -Path $exePath -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Host "[3/6] 产物校验通过：FileVersion $($fileInfo.FileVersion) / $sizeMB MB / SHA256 $($sha256.Substring(0, 16))..." -ForegroundColor Green

    # 4. 更新清单（复用 publish-update-manifest.ps1，从 csproj 读版本号）
    if (-not $SkipManifest) {
        if (-not (Test-Path $manifestScript)) { throw "找不到清单生成脚本：$manifestScript" }
        $items = @()
        if ($Changelog.Count -gt 0) {
            $items = $Changelog
        } elseif (Test-Path $manifestPath) {
            $oldManifest = Get-Content -Path $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($oldManifest.changelog) { $items = @($oldManifest.changelog) }
        }
        & $manifestScript -ReleaseDate $releaseDate -ExePath $exePath -Changelog $items
        Write-Host "[4/6] 已更新 updates\stable\latest.json（version / 日期 / size / sha256）" -ForegroundColor Green
        if ($Changelog.Count -eq 0 -and $items.Count -gt 0) {
            Write-Host "      提示：沿用上一版 changelog；如有变更请传入 -Changelog 或手工编辑 latest.json" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[4/6] 已跳过清单更新（-SkipManifest）" -ForegroundColor Yellow
    }

    # 5. 官网下载区静态快照
    if (-not $SkipWebsite) {
        $site = Get-Content -Path $websitePath -Raw -Encoding UTF8
        $regex = [regex]'(<p class="dl-meta">页面为静态快照：<strong>)v\d+\.\d+\.\d+(</strong>（)\d{4}-\d{2}-\d{2}(）· CCToolbox\.exe · )[\d.]+ MB'
        if ($regex.IsMatch($site)) {
            $replacement = '${1}v' + $Version + '${2}' + $releaseDate + '${3}' + $sizeMB + ' MB'
            $site = $regex.Replace($site, $replacement)
            Write-TextPreservingBom $websitePath $site
            Write-Host "[5/6] 已同步官网下载区快照：v$Version / $releaseDate / $sizeMB MB" -ForegroundColor Green
        } else {
            Write-Host "[5/6] 未匹配到官网下载区快照，已跳过（请手工检查 index.html）" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[5/6] 已跳过官网同步（-SkipWebsite）" -ForegroundColor Yellow
    }

    # 6. 归档到发布目录之外：pubxml 的 DeleteExistingFiles=true 会清空发布目录
    if (-not $SkipArchive) {
        New-Item -ItemType Directory -Force -Path $archiveDir | Out-Null
        $archived = Join-Path $archiveDir "CCToolbox-v$Version.exe"
        Copy-Item -Path $exePath -Destination $archived -Force
        [System.IO.File]::WriteAllText("$archived.sha256",
            "$sha256  CCToolbox-v$Version.exe`r`n",
            (New-Object System.Text.UTF8Encoding($false)))
        Write-Host "[6/6] 已归档 artifacts\releases\CCToolbox-v$Version.exe 与 .sha256" -ForegroundColor Green
    } else {
        Write-Host "[6/6] 已跳过归档（-SkipArchive）" -ForegroundColor Yellow
    }
}
catch {
    Write-Host ""
    Write-Host "打包失败：$($_.Exception.Message)" -ForegroundColor Red
    Write-TextPreservingBom $csproj $originalCsproj
    Write-Host "已还原 WpfApp1.csproj 的原始版本号；其余可能已改动的文件请用 git diff 检查。" -ForegroundColor Yellow
    throw
}

Write-Host ""
Write-Host "================ 打包完成 v$Version ================" -ForegroundColor Cyan
Write-Host "产物      : $exePath"
Write-Host "大小/哈希 : $sizeMB MB / $sha256"
Write-Host "归档      : $archiveDir\CCToolbox-v$Version.exe"
Write-Host "已改动    : WpfApp1.csproj、updates\stable\latest.json、index.html"
Write-Host ""
Write-Host "---------------- 还需人工完成 ----------------" -ForegroundColor Yellow
Write-Host "1) 检查改动：git status / git diff（注意不要提交 .workbuddy 内部日志）"
Write-Host "2) 提交并打标签：git tag v$Version ; git push origin main --tags"
Write-Host "3) 上传 artifacts\releases\CCToolbox-v$Version.exe 到 GitHub Releases（附件名保持 CCToolbox.exe）"
Write-Host "4) 推送后确认 Pages 与 raw 两个清单 URL 都返回 v$Version"
Write-Host "5) 代码签名：当前 exe 未签名，SmartScreen 可能拦截；有证书后建议加入本脚本"
Write-Host ""
