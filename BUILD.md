# Building HASS.Agent (WinUI 3)

The project supports two build modes via a single csproj. Choose with the
`BuildMode` MSBuild property.

## 1. Unpackaged (default — dev / sideload / zip)

```
cd src/HASS.Agent.UI
dotnet build HASS.Agent.UI.csproj
```

Produces:

- `bin/Debug/net8.0-windows10.0.19041.0/win-x64/HASS.Agent.exe`
- Bundled Windows App Runtime (~100 MB output dir — `WindowsAppSDKSelfContained=true`)
- `resources.pri` synthesised via `makepri.exe` (Directory.Build.targets fallback)

End users can run the .exe directly. No installer, no admin rights, no MSIX
signing certificate needed. This is the default and the path most users will
care about while we're pre-Store.

## 2. MSIX (Store / signed sideload)

```
cd src/HASS.Agent.UI
dotnet build HASS.Agent.UI.csproj -p:BuildMode=Msix -p:AppxPackageSigningEnabled=false
```

Produces an unsigned package:

- `bin/Debug/net8.0-windows10.0.19041.0/win-x64/AppPackages/HASS.Agent.UI_<version>_Debug_Test/HASS.Agent.UI_<version>_x64_Debug.msix`
- WindowsAppRuntime dependency MSIX packages under `Dependencies/`
- `Add-AppDevPackage.ps1` — Microsoft's sideload helper

To install the unsigned package locally:

1. Enable Developer Mode (Settings → Privacy & security → For developers → on)
2. Run `Add-AppDevPackage.ps1` as administrator — it will install the dependency
   packages and the app together

For Store submission you need a real signing cert. Pass it via:

```
dotnet build HASS.Agent.UI.csproj -p:BuildMode=Msix `
    -p:PackageCertificateThumbprint=<thumbprint> `
    -p:PackageCertificateKeyFile=<path-to-pfx>
```

Or for a self-signed dev cert that you've already imported as
`Trusted People → Local Machine`:

```
dotnet build HASS.Agent.UI.csproj -p:BuildMode=Msix `
    -p:PackageCertificateThumbprint=<your-cert-thumbprint>
```

To generate a self-signed cert:

```powershell
New-SelfSignedCertificate -Type Custom `
    -Subject "CN=LAB02Research" `
    -KeyUsage DigitalSignature `
    -FriendlyName "HASS.Agent Self-Signed" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
```

Then export to .pfx and import into `Trusted People → Local Machine` so
Windows accepts your installed app.

## Switching between modes

Each mode touches `obj/` differently. If you're flipping back and forth do a
`dotnet clean` first to avoid PRI conflicts:

```
dotnet clean HASS.Agent.UI.csproj
```

## Notes

- The MSIX manifest is `src/HASS.Agent.UI/Package.appxmanifest`. Update its
  `<Identity Publisher="...">` to match your signing cert's subject CN.
- Visual assets live in `src/HASS.Agent.UI/Assets/`. Run `Assets/make-assets`
  (small console app described in `BUILD.md`) to regenerate them from the
  `hassagent.ico` source.
- `Directory.Build.targets` contains a workaround that synthesises a minimal
  `resources.pri` for the unpackaged build path. It's skipped automatically
  for MSIX builds (the SDK handles PRI properly there).
