# Installer

`dist\SteelCoatingTakeoffSetup.exe` is a real Windows installer built with **Inno Setup**.
It is UAC-elevated, installs to **Program Files**, and registers a proper uninstaller.

## What it does

- Installs to `C:\Program Files\Steel Coating Takeoff\` (requires admin — the setup exe
  carries the UAC shield)
- Ships the WPF app plus the **bundled Sage Estimating SDK 25.2** under `.\Sdk\`, which the
  app resolves at runtime (`App.xaml.cs` probes `SAGE_SDK_DIR`, then `.\Sdk`, then the
  build-time path)
- Creates a **Start Menu** group (app + uninstall) and an optional **Desktop** shortcut
- Registers in **Add/Remove Programs** ("Steel Coating Takeoff", publisher Asnicar &
  Associates) with a working uninstaller
- Silent install supported: `SteelCoatingTakeoffSetup.exe /VERYSILENT /NORESTART`

## Where settings live

Program Files is read-only for standard users, so the app writes settings **per user** to:

```
%APPDATA%\SteelCoatingTakeoff\appsettings.json
```

The `appsettings.json` installed beside the exe is only the **seed defaults**, read on first
run. This is why "Save settings" works for a non-admin user.

## Server requirements

- **Windows x64** with **.NET Framework 4.8** (present on any machine running Sage Estimating)
- A **Sage Estimating 25.x** SQL environment. The bundled SDK is 25.2 and opens
  `25.01.00.*` databases only — it will refuse a 26.x database.
- On first run open **Connection settings…**, set the SQL Server, Estimating database,
  Standard database and Estimate name, then **Save**. Leave **Dry run** on until
  **Test connection** succeeds.

## Rebuild

```powershell
.\installer\build-installer.ps1
# or with a different SDK drop / version stamp:
.\installer\build-installer.ps1 -SdkDir "D:\...\Sage.Estimating.Sdk.<ver>\Binaries" -AppVersion 2.6.0
```

Requires Inno Setup 6: `winget install JRSoftware.InnoSetup`.
The script builds Release, stages the app + SDK, compiles `SteelCoatingTakeoff.iss`, and
cleans up staging.

## Licensing note

The installer **redistributes the Sage Estimating SDK binaries**. That is fine for testing
on machines you control. Before shipping to a third party, confirm it is permitted under
your Sage Development Partner agreement — otherwise build a variant that omits `.\Sdk` and
set `SAGE_SDK_DIR` (or install the SDK) on the target instead.

## Note on unsigned executables

The setup exe is **not code-signed**, so SmartScreen may warn on first run ("Windows
protected your PC" → More info → Run anyway), and Defender may briefly quarantine freshly
built binaries. For real distribution, sign both the app exe and the installer with a code
signing certificate.
