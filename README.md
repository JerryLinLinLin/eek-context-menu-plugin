# EEK Context Menu

WinUI 3 settings app plus a packaged `IExplorerCommand` shell extension for the Windows 11 Explorer context menu.

## Build

Open `EekContextMenu.sln` in Visual Studio and build `Debug|x64` or `Release|x64`.

The solution builds:

- `ShellExtension\EekShellExtension.vcxproj`: native COM DLL loaded by Explorer.
- `EekContextMenu.csproj`: packaged WinUI 3 settings app.

## Test Load

In Visual Studio, set `EekContextMenu` as the startup project, choose the `EekContextMenu (Package)` launch profile, and run.

CLI equivalent:

```powershell
msbuild .\EekContextMenu.sln /restore /m /p:Configuration=Debug /p:Platform=x64
winapp run .\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64 --exe EekContextMenu.exe
```

## How It Works

The package manifest registers `EekShellExtension.dll` as a COM surrogate server and exposes it through `desktop4:FileExplorerContextMenus` for `Directory`.

The settings app writes:

```text
HKCU\Software\EekContextMenu\EekRoot = C:\EEK
HKCU\Software\EekContextMenu\Enabled = 1 or 0
```

The Explorer command reads those values. When disabled, `GetState` returns hidden. When invoked, it launches:

```powershell
C:\EEK\bin64\a2cmd.exe /f="<selected folder>" /a /q="C:\EEK\Quarantine" /l="C:\EEK\Reports\context-menu-scan-*.log"
```

Microsoft docs for this path:

- [Integrate a packaged app with File Explorer](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer)
- [`desktop4:FileExplorerContextMenus`](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-desktop4-fileexplorercontextmenus)
