<#
  Builds dist\SteelCoatingTakeoffSetup.exe — a single self-extracting installer that
  bundles the app + the Sage Estimating SDK and installs to the user's LocalAppData
  (no admin required). Uses only built-in Windows tooling (IExpress).

  Usage (from a Developer PowerShell, after building Release):
    .\installer\build-installer.ps1
    .\installer\build-installer.ps1 -SdkDir "D:\path\to\Sage.Estimating.Sdk.<ver>\Binaries"
#>
param(
  [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
  [string]$SdkDir   = "C:\00 - Program Installs\SDK\SDK - 25.2\Sage.Estimating.Sdk.25.2.2510.091\Binaries",
  [string]$MsBuild  = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
)
$ErrorActionPreference = "Stop"

$rel   = Join-Path $RepoRoot "src\SteelCoatingTakeoff.App\bin\Release\net48"
$work  = Join-Path $env:TEMP ("sct_installer_" + [Guid]::NewGuid().ToString("N"))
$app   = Join-Path $work "SteelCoatingTakeoff"
$dist  = Join-Path $RepoRoot "dist"
$target= Join-Path $dist "SteelCoatingTakeoffSetup.exe"

Write-Host "1/5  Building Release..."
& $MsBuild (Join-Path $RepoRoot "SteelCoatingTakeoff.sln") /t:Build /p:Configuration=Release /v:minimal /nologo | Out-Null
if (-not (Test-Path (Join-Path $rel "SteelCoatingTakeoff.exe"))) { throw "Release build missing." }

Write-Host "2/5  Staging app + bundled SDK..."
New-Item -ItemType Directory -Path (Join-Path $app "Sdk") -Force | Out-Null
foreach ($f in "SteelCoatingTakeoff.exe","SteelCoatingTakeoff.exe.config","SteelCoatingTakeoff.Core.dll","appsettings.json") {
  Copy-Item (Join-Path $rel $f) $app
}
Copy-Item (Join-Path $SdkDir "*") (Join-Path $app "Sdk") -Recurse -Force

Write-Host "3/5  Zipping payload..."
Compress-Archive -Path $app -DestinationPath (Join-Path $work "SteelCoatingTakeoff.zip") -CompressionLevel Optimal -Force
Copy-Item (Join-Path $PSScriptRoot "install.cmd") $work

Write-Host "4/5  Writing IExpress directive..."
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$sed = Join-Path $work "pkg.sed"
@"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=0
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=%PostInstallCmd%
AdminQuietInstCmd=%AdminQuietInstCmd%
UserQuietInstCmd=%UserQuietInstCmd%
SourceFiles=SourceFiles
[Strings]
InstallPrompt=Install Steel Coating Takeoff (bundled Sage SDK)? Installs to your user profile; no admin required.
DisplayLicense=
FinishMessage=
TargetName=$target
FriendlyName=Steel Coating Takeoff Setup
AppLaunched=cmd.exe /c install.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
FILE0="install.cmd"
FILE1="SteelCoatingTakeoff.zip"
[SourceFiles]
SourceFiles0=$work
[SourceFiles0]
%FILE0%=
%FILE1%=
"@ | Set-Content -Path $sed -Encoding ASCII

Write-Host "5/5  Building setup.exe with IExpress..."
Start-Process -FilePath "$env:WINDIR\System32\iexpress.exe" -ArgumentList "/N","/Q",$sed -Wait
Remove-Item $work -Recurse -Force

if (Test-Path $target) {
  $mb = [math]::Round((Get-Item $target).Length/1MB,1)
  Write-Host "DONE -> $target  ($mb MB)"
} else {
  throw "IExpress did not produce the installer."
}
