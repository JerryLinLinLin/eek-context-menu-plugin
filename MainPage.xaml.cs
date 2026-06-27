using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace EekContextMenu;

public sealed partial class MainPage : Page
{
    private bool _loading;

    public MainPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        _loading = true;
        EekPathBox.Text = EekIntegration.GetEekRoot();
        IntegrationToggle.IsOn = EekIntegration.IsEnabled();
        UpdateBeforeScanToggle.IsOn = EekIntegration.CheckForUpdatesBeforeScan();
        QuarantineToggle.IsOn = EekIntegration.QuarantineDetections();
        _loading = false;
    }

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        try
        {
            SaveSettings();
        }
        catch (Exception ex)
        {
            EekPathBox.PlaceholderText = ex.Message;
        }
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        if (App.MainWindow is not null)
        {
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        }

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            EekPathBox.Text = folder.Path;
            Setting_Changed(sender, e);
        }
    }

    private void SaveSettings()
    {
        EekIntegration.Save(
            EekPathBox.Text,
            IntegrationToggle.IsOn,
            UpdateBeforeScanToggle.IsOn,
            QuarantineToggle.IsOn);
    }
}
