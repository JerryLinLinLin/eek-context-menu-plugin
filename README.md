# Emsisoft Emergency Kit Explorer Scan Integration

Unofficial Windows 11 Explorer integration for Emsisoft Emergency Kit. This project adds a packaged WinUI 3 settings app and a modern Explorer command so selected files or folders can be scanned with EEK's `a2cmd.exe`.

This project is not affiliated with, endorsed by, or supported by Emsisoft. Emsisoft Emergency Kit is not bundled.

## Install From GitHub Release

Download `EekContextMenu-msix-*.zip` from the release page and extract it.

The GitHub release package is signed with the included self-signed test certificate:

1. Right-click `EekContextMenu.Test.cer` and choose **Install Certificate**.
2. Select **Local Machine**.
3. Place the certificate in **Trusted People**.
4. Install the `EekContextMenu_*.msix` file from the extracted package folder.
5. Open the settings app and confirm the Emsisoft Emergency Kit folder. The default is `C:\EEK`.

Unzip Emsisoft Emergency Kit separately, or choose its folder in the app after installation.

The release zip intentionally contains only the app `.msix`, the public test certificate, and an install note. Visual Studio may also generate `Add-AppDevPackage.ps1`, `Add-AppDevPackage.resources`, and a `Dependencies` folder; those are helper/offline sideload files, not required app files.

If installation reports a missing `Microsoft.WindowsAppRuntime` dependency, install the Windows App Runtime from Microsoft and retry the `.msix`.

If the Explorer command does not appear immediately, restart Explorer or sign out and back in.

## Build And Install Yourself

Open `EekContextMenu.sln` in Visual Studio, choose `Release|x64`, then use **Package and Publish > Create App Packages** on the `EekContextMenu` project. Choose sideloading / non-Store distribution.

Use a package signing certificate whose subject matches the manifest publisher:

```text
CN=EekContextMenu
```

Install the generated certificate into **Trusted People**, then install the generated `.msix` package. The package includes both the WinUI settings app and the native Explorer command DLL; installing only `EekShellExtension.dll` is not enough for the Windows 11 Explorer integration.

## Release Pipeline

The GitHub Actions workflow at `.github/workflows/package-msix.yml` builds `Release|x64`, creates a self-signed test certificate whose publisher matches the package manifest, signs the MSIX package, and uploads a release zip.

Run it manually from **Actions > Package MSIX > Run workflow**. You can enter a package version such as `1.0.0`, or leave it blank to use the workflow run number.

To publish a GitHub Release automatically, push a tag like:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

For tag builds, the workflow updates the package manifest version from the tag, creates or updates the matching GitHub Release, and uploads `EekContextMenu-msix-<version>.zip`.

The private signing key exists only in the GitHub Actions runner certificate store. The release zip contains the public `.cer` that users install to trust the test-signed package.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).

## References

- [Integrate a packaged app with File Explorer](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer)
- [`desktop4:FileExplorerContextMenus`](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-desktop4-fileexplorercontextmenus)
- [Continuous integration for WinUI 3 projects](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/ci-for-winui3)
- [Sign an app package using SignTool](https://learn.microsoft.com/en-us/windows/msix/package/sign-app-package-using-signtool)
- [Create a certificate for package signing](https://learn.microsoft.com/en-us/windows/msix/package/create-certificate-package-signing)
- [Windows App SDK deployment overview](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview)
- [Windows App SDK downloads](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)
