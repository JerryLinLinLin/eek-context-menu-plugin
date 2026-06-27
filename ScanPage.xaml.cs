using System.Diagnostics;
using System.Security.Principal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace EekContextMenu;

public sealed partial class ScanPage : Page
{
    private static readonly string[] ScannerLineMarkers =
    [
        "Emsisoft Commandline Scanner -",
        "Last update:",
        "(C) 2003-",
        "Update start:",
        "Update end:",
        "Update time:",
        "Scan settings:",
        "Scan type:",
        "Objects:",
        "Detect Potentially Unwanted Programs:",
        "Scan archives:",
        "Scan mail archives:",
        "ADS Scan:",
        "Scan start:",
        "Scan end:",
        "Scanned",
        "Found",
        "Scanning Files"
    ];

    private Process? _currentProcess;
    private readonly object _outputLock = new();
    private readonly System.Text.StringBuilder _outputText = new();
    private bool _pendingCarriageReturn;
    private int _outputVersion;
    private int _appliedOutputVersion;

    public ScanPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var target = e.Parameter as string;
        TargetPathText.Text = target ?? string.Empty;
        _ = RunAsync(target);
    }

    private async Task RunAsync(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            SetStatus("Missing scan target.", InfoBarSeverity.Error);
            return;
        }

        target = Path.GetFullPath(target.Trim().Trim('"'));
        TargetPathText.Text = target;
        if (!Directory.Exists(target) && !File.Exists(target))
        {
            SetStatus("Scan target was not found.", InfoBarSeverity.Error);
            AppendLine($"Target: {target}");
            return;
        }

        var options = EekIntegration.GetScanOptions();
        var scanner = EekIntegration.GetScannerPath(options.EekRoot);
        if (!File.Exists(scanner))
        {
            SetStatus("EEK scanner was not found.", InfoBarSeverity.Error);
            AppendLine(scanner);
            return;
        }

        Directory.CreateDirectory(EekIntegration.GetReportsFolder(options.EekRoot));
        if (options.QuarantineDetections)
        {
            Directory.CreateDirectory(EekIntegration.GetQuarantineFolder(options.EekRoot));
        }

        var logPath = Path.Combine(
            EekIntegration.GetReportsFolder(options.EekRoot),
            $"context-menu-scan-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        LogPathText.Text = logPath;

        AppendLine(IsAdministrator() ? "Running elevated." : "Running without administrator rights.");

        if (options.CheckForUpdatesBeforeScan)
        {
            SetStatus("Checking for EEK updates.", InfoBarSeverity.Informational);
            var updateCode = await RunA2CmdAsync(scanner, ["/u"]);
            if (updateCode != 0)
            {
                AppendLine($"Update exited with code {updateCode}. Continuing with scan.");
            }
        }

        SetStatus("Scanning.", InfoBarSeverity.Informational);
        var scanArgs = new List<string>
        {
            target,
            "/a",
            $"/l={logPath}"
        };

        if (options.QuarantineDetections)
        {
            scanArgs.Add($"/q={EekIntegration.GetQuarantineFolder(options.EekRoot)}");
        }

        var scanCode = await RunA2CmdAsync(scanner, scanArgs);
        SetStatus(ScanResultText(scanCode), ScanResultSeverity(scanCode));
        CancelButton.IsEnabled = false;
    }

    private async Task<int> RunA2CmdAsync(string scanner, IEnumerable<string> arguments)
    {
        var argumentList = arguments.ToArray();

        AppendLine("");
        AppendLine($"> {FormatCommandLineArgument(scanner)} {string.Join(' ', argumentList.Select(FormatCommandLineArgument))}");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = scanner,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(scanner) ?? string.Empty
            },
            EnableRaisingEvents = true
        };

        foreach (var argument in argumentList)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        _currentProcess = process;
        process.Start();
        var outputTask = PumpOutputAsync(process.StandardOutput);
        var errorTask = PumpOutputAsync(process.StandardError);

        await process.WaitForExitAsync();
        await Task.WhenAll(outputTask, errorTask);
        FlushPendingCarriageReturn();

        _currentProcess = null;
        return process.ExitCode;
    }

    private async Task PumpOutputAsync(StreamReader reader)
    {
        var buffer = new char[4096];
        int count;

        while ((count = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            AppendScannerOutput(new string(buffer, 0, count));
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProcess is { HasExited: false } process)
        {
            process.Kill(entireProcessTree: true);
            AppendLine("Stopped.");
        }
    }

    private void AppendLine(string? line)
    {
        if (line is null)
        {
            return;
        }

        AppendLogText(line + Environment.NewLine);
    }

    private void AppendScannerOutput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var normalized = NormalizeScannerOutput(text);
        if (normalized.Length == 0)
        {
            return;
        }

        AppendLogText(normalized, isScannerOutput: true);
    }

    private void AppendLogText(string text, bool isScannerOutput = false)
    {
        string snapshot;
        int version;

        lock (_outputLock)
        {
            if (isScannerOutput && StartsWithScannerLineMarker(text) && !OutputEndsWithNewLine())
            {
                text = Environment.NewLine + text;
            }

            _outputText.Append(text);
            snapshot = _outputText.ToString();
            version = ++_outputVersion;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (version < _appliedOutputVersion)
            {
                return;
            }

            _appliedOutputVersion = version;
            OutputBox.Text = snapshot;
            OutputBox.SelectionStart = OutputBox.Text.Length;
        });
    }

    private string NormalizeScannerOutput(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);

        lock (_outputLock)
        {
            foreach (var character in text)
            {
                if (_pendingCarriageReturn)
                {
                    _pendingCarriageReturn = false;

                    if (character == '\n')
                    {
                        builder.Append(Environment.NewLine);
                        continue;
                    }

                    builder.Append(Environment.NewLine);
                }

                switch (character)
                {
                    case '\r':
                        _pendingCarriageReturn = true;
                        break;
                    case '\n':
                        builder.Append(Environment.NewLine);
                        break;
                    case '\b':
                    case '\0':
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }
        }

        return FixMergedScannerLines(builder.ToString());
    }

    private void FlushPendingCarriageReturn()
    {
        var shouldAppend = false;

        lock (_outputLock)
        {
            if (_pendingCarriageReturn)
            {
                _pendingCarriageReturn = false;
                shouldAppend = true;
            }
        }

        if (shouldAppend)
        {
            AppendLogText(Environment.NewLine);
        }
    }

    private static string FixMergedScannerLines(string text)
    {
        foreach (var marker in ScannerLineMarkers)
        {
            text = InsertNewLineBeforeMergedMarker(text, marker);
        }

        return text;
    }

    private bool OutputEndsWithNewLine()
    {
        return _outputText.Length == 0 || _outputText[^1] is '\r' or '\n';
    }

    private static bool StartsWithScannerLineMarker(string text)
    {
        return ScannerLineMarkers.Any(marker => text.StartsWith(marker, StringComparison.Ordinal));
    }

    private static string InsertNewLineBeforeMergedMarker(string text, string marker)
    {
        var searchStart = 0;

        while (searchStart < text.Length)
        {
            var index = text.IndexOf(marker, searchStart, StringComparison.Ordinal);
            if (index <= 0)
            {
                break;
            }

            if (text[index - 1] is '\n' or '\r')
            {
                searchStart = index + marker.Length;
                continue;
            }

            text = text.Insert(index, Environment.NewLine);
            searchStart = index + Environment.NewLine.Length + marker.Length;
        }

        return text;
    }

    private void SetStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Title = message;
        StatusInfoBar.Severity = severity;
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string ScanResultText(int exitCode)
    {
        return exitCode switch
        {
            0 => "Scan complete. No detections found.",
            1 => "Scan complete. Detections found.",
            3 or 9 => "Scan needs administrator rights.",
            _ => $"Scan failed. Exit code {exitCode}."
        };
    }

    private static InfoBarSeverity ScanResultSeverity(int exitCode)
    {
        return exitCode switch
        {
            0 => InfoBarSeverity.Success,
            1 => InfoBarSeverity.Warning,
            _ => InfoBarSeverity.Error
        };
    }

    private static string FormatCommandLineArgument(string value)
    {
        var equalsIndex = value.IndexOf('=');
        if (value.StartsWith('/') && equalsIndex > 1 && equalsIndex < value.Length - 1)
        {
            var name = value[..(equalsIndex + 1)];
            var optionValue = value[(equalsIndex + 1)..];
            return name + FormatCommandLineArgument(optionValue);
        }

        if (value.Length > 0 && value.IndexOfAny([' ', '\t', '"']) < 0)
        {
            return value;
        }

        var quoted = new System.Text.StringBuilder("\"");
        var backslashes = 0;

        foreach (var character in value)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                quoted.Append('\\', backslashes * 2 + 1);
                quoted.Append(character);
                backslashes = 0;
                continue;
            }

            quoted.Append('\\', backslashes);
            backslashes = 0;
            quoted.Append(character);
        }

        quoted.Append('\\', backslashes * 2);
        quoted.Append('"');
        return quoted.ToString();
    }
}
