using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace EekContextMenu;

public sealed partial class MainWindow : Window
{
    public MainWindow(string? scanTarget = null)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var isScanWindow = !string.IsNullOrWhiteSpace(scanTarget);
        Title = isScanWindow ? "EEK Scan" : "EEK Context Menu";
        AppTitleBar.Title = Title;

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(isScanWindow ? new SizeInt32(1400, 920) : new SizeInt32(800, 680));

        RootFrame.Navigate(isScanWindow ? typeof(ScanPage) : typeof(MainPage), scanTarget);
    }
}
