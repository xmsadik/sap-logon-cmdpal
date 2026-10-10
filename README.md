# SAP Logon for Command Palette

A [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) extension
that lists the SAP GUI systems from SAP Logon. Type **sap**, pick **SAP Logon**, search, press **Enter**, and the
system opens in SAP GUI's normal logon screen, just like a double-click in SAP Logon (default client included).

> Unofficial community project. Not affiliated with or endorsed by SAP SE or Microsoft.

## Features
- Lists every SAP GUI entry in `%APPDATA%\SAP\Common\SAPUILandscape.xml`, including local (`file:`) includes.
- Search by description, SID, server address or SAP Logon folder.
- **Enter** opens the connection through SAP GUI Scripting (`OpenConnection`), starting SAP Logon minimized if it
  is not running. If scripting is unavailable, it falls back to `sapshcut.exe` (a small password dialog without a
  client field).
- **Ctrl+K** copies the server address, SID or description.
- Picks up changes to SAP Logon automatically the next time the list opens.

Privacy: the landscape file is only read, never written. Entry notes (memos) are never read or shown, because
people often keep credentials there. Nothing leaves your machine.

## Requirements
- Windows 10 19041+ or Windows 11 with PowerToys Command Palette.
- SAP GUI for Windows 7.70 or later, with SAP Logon (landscape XML format).
- SAP GUI Scripting enabled on the client (SAP GUI Options > Accessibility & Scripting > Scripting) for the normal
  logon screen; otherwise the sapshcut fallback is used. VBScript must be available (`cscript.exe`).

## Install
1. From the [latest release](https://github.com/xmsadik/sap-logon-cmdpal/releases/latest) download
   `SapLogonDev.cer` and `SapLogon_<version>_x64.msix` (Intel/AMD) or `SapLogon_<version>_arm64.msix` (Arm).
2. Trust the certificate once, in **PowerShell as Administrator**:
   ```powershell
   Import-Certificate .\SapLogonDev.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```
3. Install: `Add-AppxPackage .\SapLogon_0.1.0.0_x64.msix`
4. In Command Palette run **Reload**, then type **sap**.

Tip: give the command an alias in Command Palette settings to jump straight into the list.

## Build from source
```powershell
dotnet test tests\SapLogon.Tests
.\scripts\dev-deploy.ps1          # build Debug x64 and register the package (needs Developer Mode)
.\scripts\dev-deploy.ps1 -Remove  # unregister
.\scripts\pack.ps1 -Platform x64 -Sign   # signed Release MSIX in dist\
```

## Troubleshooting
- **Enter shows a small user/password dialog instead of the logon screen:** SAP GUI Scripting is disabled or
  unavailable, so the sapshcut fallback was used. Enable scripting in SAP GUI options.
- Errors are logged to `diag.log` under `%LOCALAPPDATA%\Packages\SapLogon_*\LocalCache\Local\SapLogon\`.

## License
MIT. See [LICENSE](LICENSE) and [NOTICE](NOTICE).
