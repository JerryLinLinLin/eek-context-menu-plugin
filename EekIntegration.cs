using Microsoft.Win32;

namespace EekContextMenu;

public static class EekIntegration
{
    public const string DefaultEekRoot = @"C:\EEK";

    private const string SettingsKeyPath = @"Software\EekContextMenu";
    private const string EekRootValue = "EekRoot";
    private const string EnabledValue = "Enabled";

    public static string GetEekRoot()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath);
        var value = key?.GetValue(EekRootValue) as string;
        return string.IsNullOrWhiteSpace(value) ? DefaultEekRoot : value;
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath);
        return key?.GetValue(EnabledValue) is int value && value != 0;
    }

    public static void Save(string eekRoot, bool enabled)
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
