# Installer

`dist\SteelCoatingTakeoffSetup.exe` is a single self-extracting installer (built with
Windows' built-in IExpress) that bundles the app **and** the Sage Estimating SDK, and
installs to the user's profile — **no admin rights required**.

## What it installs

- To `%LOCALAPPDATA%\Programs\SteelCoatingTakeoff\`
- The WPF app (`SteelCoatingTakeoff.exe`, `SteelCoatingTakeoff.Core.dll`, `appsettings.json`)
- The bundled Sage Estimating **SDK 25.2** under `.\Sdk\` (the app resolves it from there
  at runtime — see `App.xaml.cs`, which probes `SAGE_SDK_DIR`, then `.\Sdk`, then the
  build-time path)
- Start Menu + Desktop shortcuts

## Server requirements

- **Windows x64** with **.NET Framework 4.8** (present on any machine running Sage
  Estimating).
- A **Sage Estimating 25.x** SQL environment. The bundled SDK is 25.2, which opens
  `25.01.00.*` databases only — it will refuse a 26.x database. Match the server's
  estimates DB version.
- On first run, open **Connection settings…** and set the SQL Server, Estimating
  database, Standard database and Estimate name for the server, then **Save**. (The
  shipped `appsettings.json` carries dev defaults.)
- Leave **Dry run** on until **Test connection** succeeds.

## Rebuild

```powershell
# after committing code changes:
.\installer\build-installer.ps1
# or with a different SDK drop:
.\installer\build-installer.ps1 -SdkDir "D:\...\Sage.Estimating.Sdk.<ver>\Binaries"
```

## Licensing note

The installer **redistributes the Sage Estimating SDK binaries**. That is fine for your
own testing on machines you control. Before shipping to a third party, confirm it is
permitted under your Sage Development Partner agreement — otherwise build a variant that
omits `.\Sdk` and set `SAGE_SDK_DIR` (or install the SDK) on the target instead.
