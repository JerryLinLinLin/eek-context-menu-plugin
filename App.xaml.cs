using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;

namespace EekContextMenu;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow(GetScanTarget(args.Arguments));
        MainWindow.Activate();
    }

    private static string? GetScanTarget(string activationArguments)
    {
        return FindScanTarget(ParseActivationArguments(activationArguments))
            ?? FindScanTarget(Environment.GetCommandLineArgs().Skip(1));
    }

    private static string? FindScanTarget(IEnumerable<string> arguments)
    {
        var args = arguments.ToArray();
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--scan", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return string.Join(' ', args.Skip(i + 1));
            }

            const string scanPrefix = "--scan=";
            if (args[i].StartsWith(scanPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return args[i][scanPrefix.Length..];
            }
        }

        return null;
    }

    private static IEnumerable<string> ParseActivationArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return [];
        }

        var argv = CommandLineToArgvW($"EekContextMenu.exe {arguments}", out var count);
        if (argv == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            var parsed = new string[Math.Max(0, count - 1)];
            for (var i = 1; i < count; i++)
            {
                parsed[i - 1] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size)) ?? string.Empty;
            }

            return parsed;
        }
        finally
        {
            LocalFree(argv);
        }
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(
        [MarshalAs(UnmanagedType.LPWStr)] string commandLine,
        out int numberOfArgs);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);
}
