# Installer

Two installers are built from the same Inno Setup script — pick based on whether you have
admin rights on the target machine.

| File | Installs to | Admin needed |
|---|---|---|
| `dist\SteelCoatingTakeoffSetup.exe` | `C:\Program Files\Steel Coating Takeoff\` | **Yes** (UAC prompt) |
| `dist\SteelCoatingTakeoffSetup-NoAdmin.exe` | `%LOCALAPPDATA%\Programs\Steel Coating Takeoff\` | **No** |

Both bundle the Sage SDK, register an uninstaller in Add/Remove Programs (per-machine vs
per-user hive), and create Start Menu + optional Desktop shortcuts. They carry different
AppIds, so they can coexist without fighting over one uninstall entry.

**Use the `-NoAdmin` build when you cannot elevate on the server.** It is a normal
per-user install — no UAC prompt, nothing written outside the user's profile. (It replaces
the old IExpress self-extractor, which had no uninstaller and whose extractor process kept
the setup .exe locked against deletion.)

## What it does

- Installs to Program Files (admin build) or `%LOCALAPPDATA%\Programs` (no-admin build)
- Ships the WPF app plus the **bundled Sage Estimating SDK 25.2** under `.\Sdk\`, which the
  app resolves at runtime (`App.xaml.cs` probes `SAGE_SDK_DIR`, then `.\Sdk`, then the
  build-time path)
- Creates a **Start Menu** group (app + uninstall) and an optional **Desktop** shortcut
- Registers in **Add/Remove Programs** ("Steel Coating Takeoff", publisher Asnicar &
  Associates) with a working uninstaller
- Silent install supported: `SteelCoatingTakeoffSetup.exe /VERYSILENT /NORESTART`
  (same switches work for the `-NoAdmin` build)

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
