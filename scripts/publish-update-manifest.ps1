<#
  生成 updates/stable/latest.json（第 1 期检查更新清单）。
  版本号始终从 WpfApp1.csproj 的 <Version> 读取，避免清单与程序集版本不一致。

  示例：
    powershell -ExecutionPolicy Bypass -File scripts/publish-update-manifest.ps1 -ReleaseDate 2026-11-13 -Changelog "新增 XXX", "修复 YYY"
    powershell -ExecutionPolicy Bypass -File scripts/publish-update-manifest.ps1 -Mandatory -MinimumVersion 2.4.0
    powershell -ExecutionPolicy Bypass -File scripts/publish-update-manifest.ps1 -ExePath artifacts/publish/win-x64-single/CCToolbox.exe
#>
param(
    [string]$Csproj = "WpfApp1/WpfApp1.csproj",
    [string]$Output = "updates/stable/latest.json",
    [string]$ReleaseDate = (Get-Date -Format "yyyy-MM-dd"),
    [string[]]$Changelog = @(),
    [switch]$Mandatory,
    [string]$MinimumVersion = "2.3.0",
    [string]$ExePath = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csprojFull = Join-Path $root $Csproj

if (-not (Test-Path $csprojFull)) { throw "找不到项目文件：$csprojFull" }

$version = (Select-Xml -Path $csprojFull -XPath "/Project/PropertyGroup/Version/text()").Node.Value
if (-not $version) { throw "无法从 $Csproj 读取 <Version>" }
$tag = "v$version"

$package = [ordered]@{
    url = "https://github.com/chenmo02/CCToolbox/releases/download/$tag/CCToolbox.exe"
    rid = "win-x64"
}

# 第 2 期启用自动下载前，可传入已发布 exe 自动补齐 size / sha256
if ($ExePath) {
    $exeFull = if ([System.IO.Path]::IsPathRooted($ExePath)) { $ExePath } else { Join-Path $root $ExePath }
    if (-not (Test-Path $exeFull)) { throw "找不到发布产物：$exeFull" }
    $package["size"] = (Get-Item $exeFull).Length
    $package["sha256"] = (Get-FileHash -Path $exeFull -Algorithm SHA256).Hash.ToLowerInvariant()
}

$manifest = [ordered]@{
    schema           = 1
    product          = "CCToolbox"
    channel          = "stable"
    version          = $version
    releaseDate      = $ReleaseDate
    minimumVersion   = $MinimumVersion
    mandatory        = [bool]$Mandatory
    downloadUrl      = "https://github.com/chenmo02/CCToolbox/releases/latest"
    releaseNotesUrl  = "https://github.com/chenmo02/CCToolbox/releases/tag/$tag"
    changelog        = @($Changelog)
    package          = $package
}

$outFull = Join-Path $root $Output
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outFull) | Out-Null
$json = $manifest | ConvertTo-Json -Depth 6

# 无 BOM UTF-8：HTTP 客户端解析带 BOM 的 JSON 会失败
[System.IO.File]::WriteAllText($outFull, $json + [Environment]::NewLine, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "已生成 $outFull (v$version, $ReleaseDate)"
