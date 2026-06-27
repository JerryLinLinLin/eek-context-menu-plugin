# <img src="Assets/Square44x44Logo.scale-200.png" width="32" height="32" alt="EEK icon"> Emsisoft Emergency Kit Explorer Scan Integration

Unofficial Windows 11 Explorer integration for Emsisoft Emergency Kit. This project adds a packaged WinUI 3 settings app and a modern Explorer command so selected files or folders can be scanned with EEK's `a2cmd.exe`.

This project is not affiliated with, endorsed by, or supported by Emsisoft. Emsisoft Emergency Kit is not bundled.

Use this integration only with a properly licensed copy of [Emsisoft Emergency Kit](https://www.emsisoft.com/en/emergency-kit/) and comply with Emsisoft's terms. Emsisoft's [Getting Started guide](https://www.emsisoft.com/en/help/1702/getting-started/) describes Emergency Kit as free for private use, while business and other for-profit use requires the appropriate paid/business license.

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

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
