using Microsoft.Win32;

namespace EekContextMenu;

public static class EekIntegration
{
    public const string DefaultEekRoot = @"C:\EEK";

    private const string SettingsKeyPath = @"Software\EekContextMenu";
    private const string EekRootValue = "EekRoot";
    private const string EnabledValue = "Enabled";
    private const string CheckForUpdatesBeforeScanValue = "CheckForUpdatesBeforeScan";
    private const string QuarantineDetectionsValue = "QuarantineDetections";

    public sealed record ScanOptions(
        string EekRoot,
        bool CheckForUpdatesBeforeScan,
        bool QuarantineDetections);

    public static string GetEekRoot()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath);
        var value = key?.GetValue(EekRootValue) as string;
        return string.IsNullOrWhiteSpace(value) ? DefaultEekRoot : value;
    }

    public static bool IsEnabled()
    {
        return GetBoolSetting(EnabledValue, false);
    }

    public static bool CheckForUpdatesBeforeScan()
    {
        return GetBoolSetting(CheckForUpdatesBeforeScanValue, true);
    }

    public static bool QuarantineDetections()
    {
        return GetBoolSetting(QuarantineDetectionsValue, true);
    }

    public static ScanOptions GetScanOptions()
    {
        return new ScanOptions(GetEekRoot(), CheckForUpdatesBeforeScan(), QuarantineDetections());
    }

    public static void Save(string eekRoot, bool enabled, bool checkForUpdatesBeforeScan, bool quarantineDetections)
    {
        var root = NormalizeEekRoot(eekRoot);
        if (enabled)
        {
            var error = GetValidationError(root);
            if (error is not null)
            {
                throw new InvalidOperationException(error);
            }
        }

        using var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath, true)
            ?? throw new InvalidOperationException("Could not open the settings registry key.");
        key.SetValue(EekRootValue, root, RegistryValueKind.String);
        key.SetValue(EnabledValue, enabled ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue(CheckForUpdatesBeforeScanValue, checkForUpdatesBeforeScan ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue(QuarantineDetectionsValue, quarantineDetections ? 1 : 0, RegistryValueKind.DWord);
    }

    public static string? GetValidationError(string eekRoot)
    {
        string root;
        try
        {
            root = NormalizeEekRoot(eekRoot);
        }
        catch
        {
            return "Choose a valid EEK folder.";
        }

        if (!Directory.Exists(root))
        {
            return "EEK folder was not found.";
        }

        if (!File.Exists(Path.Combine(root, "bin64", "a2cmd.exe")))
        {
            return @"bin64\a2cmd.exe was not found.";
        }

        return null;
    }

    public static string GetScannerPath(string eekRoot)
    {
        return Path.Combine(NormalizeEekRoot(eekRoot), "bin64", "a2cmd.exe");
    }

    public static string GetReportsFolder(string eekRoot)
    {
        return Path.Combine(NormalizeEekRoot(eekRoot), "Reports");
    }

    public static string GetQuarantineFolder(string eekRoot)
    {
        return Path.Combine(NormalizeEekRoot(eekRoot), "Quarantine");
    }

    private static bool GetBoolSetting(string name, bool fallback)
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath);
        return key?.GetValue(name) is int value ? value != 0 : fallback;
    }

    private static string NormalizeEekRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is empty.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path.Trim().Trim('"'));
        var root = Path.GetPathRoot(fullPath);
        return root is not null && fullPath.Length > root.Length
            ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : fullPath;
    }
}
