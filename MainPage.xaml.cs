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
        _loading = false;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveSettings(IntegrationToggle.IsOn);
            ShowStatus("Saved.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, InfoBarSeverity.Error);
        }
    }

    private void IntegrationToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        try
        {
            SaveSettings(IntegrationToggle.IsOn);
            ShowStatus(
                IntegrationToggle.IsOn ? "Explorer integration enabled." : "Explorer integration disabled.",
                IntegrationToggle.IsOn ? InfoBarSeverity.Success : InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            _loading = true;
            IntegrationToggle.IsOn = EekIntegration.IsEnabled();
            _loading = false;
            ShowStatus(ex.Message, InfoBarSeverity.Error);
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
        }
    }

    private void SaveSettings(bool enabled)
    {
        EekIntegration.Save(EekPathBox.Text, enabled);
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }
}
