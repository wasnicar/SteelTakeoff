<#
  Builds dist\SteelCoatingTakeoffSetup.exe — a real Windows installer (Inno Setup):
  UAC-elevated, installs to Program Files, registers an uninstaller in Add/Remove
  Programs, and creates Start Menu + optional Desktop shortcuts. The Sage Estimating
  SDK is bundled so the app runs without a separate SDK install.

  Usage (after any code change):
    .\installer\build-installer.ps1
    .\installer\build-installer.ps1 -SdkDir "D:\path\to\Sage.Estimating.Sdk.<ver>\Binaries"

  Requires Inno Setup 6 (winget install JRSoftware.InnoSetup).
#>
param(
  [string]$RepoRoot   = (Split-Path $PSScriptRoot -Parent),
  [string]$SdkDir     = "C:\00 - Program Installs\SDK\SDK - 25.2\Sage.Estimating.Sdk.25.2.2510.091\Binaries",
  [string]$MsBuild    = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
  [string]$AppVersion = "2.5.0"
)
$ErrorActionPreference = "Stop"

$rel   = Join-Path $RepoRoot "src\SteelCoatingTakeoff.App\bin\Release\net48"
$dist  = Join-Path $RepoRoot "dist"
$stage = Join-Path $dist "stage\SteelCoatingTakeoff"
$iss   = Join-Path $PSScriptRoot "SteelCoatingTakeoff.iss"
$target= Join-Path $dist "SteelCoatingTakeoffSetup.exe"

# Locate the Inno Setup compiler (winget installs per-user by default).
$iscc = @(
  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
  "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
  "C:\Program Files\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup not found. Install it: winget install JRSoftware.InnoSetup" }

Write-Host "1/4  Building Release..."
& $MsBuild (Join-Path $RepoRoot "SteelCoatingTakeoff.sln") /t:Build /p:Configuration=Release /v:minimal /nologo | Out-Null
if (-not (Test-Path (Join-Path $rel "SteelCoatingTakeoff.exe"))) { throw "Release build missing." }

Write-Host "2/4  Staging app + bundled SDK..."
if (Test-Path (Join-Path $dist "stage")) { Remove-Item (Join-Path $dist "stage") -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $stage "Sdk") -Force | Out-Null
foreach ($f in "SteelCoatingTakeoff.exe","SteelCoatingTakeoff.exe.config","SteelCoatingTakeoff.Core.dll","appsettings.json") {
  Copy-Item (Join-Path $rel $f) $stage
}
Copy-Item (Join-Path $SdkDir "*") (Join-Path $stage "Sdk") -Recurse -Force

Write-Host "3/4  Compiling installer (Inno Setup)..."
if (Test-Path $target) { Remove-Item $target -Force }
& $iscc "/DAppVersion=$AppVersion" "/DStageDir=$stage" "/DOutDir=$dist" $iss | Out-Null
if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE" }

Write-Host "4/4  Cleaning staging..."
Remove-Item (Join-Path $dist "stage") -Recurse -Force

if (Test-Path $target) {
  $mb = [math]::Round((Get-Item $target).Length/1MB,1)
  Write-Host "DONE -> $target  ($mb MB)"
} else {
  throw "Installer was not produced."
}
