using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Windows.Storage.Pickers;
using SymlinkGUI.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;

namespace SymlinkGUI;

public sealed partial class MainWindow : Window
{
    private LinkType _selectedType = LinkType.SymbolicLink;

    private readonly LinkService _service = LinkService.Default;
    private readonly ContextMenuRegistrar _registrar = ContextMenuRegistrar.CreateDefault();

    private bool _initialized;
    private bool _nameEditedByUser;
    private bool _settingNameProgrammatically;
    private bool _updatingToggle;
    private bool _busy;
    private string? _lastCreatedLink;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        SetAppIcon();
        SizeAndCenter(640, 650);

        StatusBar.CloseButtonClick += (_, _) => StatusBar.IsOpen = false;
        Activated += (_, e) =>
        {
            // Re-check when the user returns.
            if (e.WindowActivationState != WindowActivationState.Deactivated)
                RefreshEnvironment();
        };

        LinkTypeCombo.SelectedIndex = 0;
        _initialized = true;

        UpdateLinkTypeControls();
        RefreshEnvironment();
        Validate();
    }

    /// <summary>Pre-fills the source (used by "Create Link To..." from Explorer).</summary>
    public void SetSource(string path)
    {
        SourceBox.Text = path;
        DestinationBox.Focus(FocusState.Programmatic);
    }

    #region Window sizing

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    private void SizeAndCenter(int widthDip, int heightDip)
    {
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        double scale = GetDpiForWindow(hwnd) / 96.0;
        var size = new SizeInt32((int)(widthDip * scale), (int)(heightDip * scale));

        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        size.Height = Math.Min(size.Height, area.Height);
        AppWindow.MoveAndResize(new RectInt32(
            area.X + (area.Width - size.Width) / 2,
            area.Y + (area.Height - size.Height) / 2,
            size.Width, size.Height));
    }

    private void SetAppIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }
    }

    #endregion

    #region Environment (context menu + permissions)

    private void LinkTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;

        _selectedType = LinkTypeCombo.SelectedIndex switch
        {
            1 => LinkType.Junction,
            2 => LinkType.HardLink,
            _ => LinkType.SymbolicLink,
        };

        UpdateLinkTypeControls();
        RefreshEnvironment();
        Validate();
    }

    private void UpdateLinkTypeControls()
    {
        if (LinkTypeDescriptionText is not null)
        {
            LinkTypeDescriptionText.Text = _selectedType switch
            {
                LinkType.SymbolicLink => "Points to a file or folder across any drives. Requires administrator permission if not already elevated.",
                LinkType.Junction => "Points to a local folder. Works without administrator permission.",
                LinkType.HardLink => "Direct alias to an existing file on the same drive. Works without administrator permission.",
                _ => "",
            };
        }

        if (BrowseSourceFileButton is not null && BrowseSourceFolderButton is not null)
        {
            BrowseSourceFileButton.IsEnabled = _selectedType != LinkType.Junction;
            ToolTipService.SetToolTip(BrowseSourceFileButton, _selectedType == LinkType.Junction ? "Junctions only support folders" : "Choose a file");

            BrowseSourceFolderButton.IsEnabled = _selectedType != LinkType.HardLink;
            ToolTipService.SetToolTip(BrowseSourceFolderButton, _selectedType == LinkType.HardLink ? "Hard links only support files" : "Choose a folder");
        }

        if (CreateButtonText is not null)
            CreateButtonText.Text = $"Create {_selectedType.DisplayName()}";
    }

    private void RefreshEnvironment()
    {
        if (!_initialized) return;

        _updatingToggle = true;
        ContextMenuToggle.IsOn = _registrar.IsInstalled;
        _updatingToggle = false;
        StaleMenuBar.IsOpen = _registrar.IsStale;

        if (StaleMenuBadge is not null)
            StaleMenuBadge.Visibility = _registrar.IsStale ? Visibility.Visible : Visibility.Collapsed;

        if (ExplorerIntegrationDropDown is not null)
        {
            ToolTipService.SetToolTip(ExplorerIntegrationDropDown,
                _registrar.IsStale
                    ? "Context menu points to another location (repair needed)"
                    : "File Explorer integration settings");
        }

        bool requiresElevation = _service.RequiresElevation(_selectedType);
        ShieldIcon.Visibility = (!_busy && requiresElevation) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ContextMenuToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingToggle) return;
        try
        {
            if (ContextMenuToggle.IsOn) _registrar.Install();
            else _registrar.Uninstall();
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, "Couldn't update the context menu", ex.Message);
        }
        RefreshEnvironment();
    }

    private void RepairMenu_Click(object sender, RoutedEventArgs e)
    {
        _registrar.Install();
        RefreshEnvironment();
    }

    #endregion

    #region Inputs

    private void SourceBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        string source = SourceBox.Text.Trim().Trim('"');
        UpdateSourceInfo(source);

        if (!_nameEditedByUser)
        {
            _settingNameProgrammatically = true;
            NameBox.Text = DefaultLinkName(source);
            _settingNameProgrammatically = false;
        }
        Validate();
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        if (!_settingNameProgrammatically)
            _nameEditedByUser = NameBox.Text.Length > 0;
        Validate();
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        Validate();
    }

    private static string DefaultLinkName(string source)
    {
        if (source.Length == 0) return "";
        try
        {
            string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
            return string.IsNullOrEmpty(name) ? source.TrimEnd('\\', ':') : name;
        }
        catch
        {
            return "";
        }
    }

    private void UpdateSourceInfo(string source)
    {
        if (source.Length == 0)
        {
            SourceInfoPanel.Visibility = Visibility.Collapsed;
            return;
        }

        SourceInfoPanel.Visibility = Visibility.Visible;
        if (Directory.Exists(source))
        {
            SourceInfoIcon.Glyph = "\uE8B7";
            SourceInfoText.Text = "Folder";
        }
        else if (File.Exists(source))
        {
            SourceInfoIcon.Glyph = "\uE8A5";
            SourceInfoText.Text = $"File · {FormatSize(new FileInfo(source).Length)}";
        }
        else
        {
            SourceInfoIcon.Glyph = "\uE783";
            SourceInfoText.Text = "Not found";
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["bytes", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return unit == 0 ? $"{bytes:N0} bytes" : $"{size:0.#} {units[unit]}";
    }

    private string Source => SourceBox.Text.Trim().Trim('"');
    private string Destination => DestinationBox.Text.Trim().Trim('"');
    private string LinkName => NameBox.Text.Trim();

    /// <summary>Validates inputs; returns the full link path when everything is valid.</summary>
    private string? Validate()
    {
        if (!_initialized) return null;

        string? error = null;
        string? linkPath = null;

        string source = Source;
        bool sourceExists = LinkService.PathExists(source);
        bool isDir = sourceExists && Directory.Exists(source);
        bool isFile = sourceExists && File.Exists(source);

        if (source.Length == 0) error = "Choose a source file or folder.";
        else if (!sourceExists) error = "The source doesn't exist.";
        else if (_selectedType == LinkType.Junction && isFile) error = "Junctions can only be created for folders.";
        else if (_selectedType == LinkType.Junction && source.StartsWith(@"\\") && !source.StartsWith(@"\\?\") && !source.StartsWith(@"\\.\"))
            error = "Junctions cannot point to network shares.";
        else if (_selectedType == LinkType.HardLink && isDir) error = "Hard links can only be created for files.";
        else if (Destination.Length == 0) error = "Choose a destination folder.";
        else if (!Directory.Exists(Destination)) error = "The destination folder doesn't exist.";
        else if (_selectedType == LinkType.HardLink && !string.Equals(Path.GetPathRoot(Path.GetFullPath(source)), Path.GetPathRoot(Path.GetFullPath(Destination)), StringComparison.OrdinalIgnoreCase))
            error = "Hard links must be created on the same drive as the source file.";
        else if (LinkService.ValidateName(LinkName) is { } nameError) error = nameError;
        else
        {
            try
            {
                linkPath = Path.GetFullPath(Path.Combine(Destination, LinkName));
                if (LinkService.PathExists(linkPath))
                    error = $"\"{LinkName}\" already exists in the destination folder. Choose another name.";
                else if (string.Equals(linkPath, Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
                    error = "The link can't replace its own source.";
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        if (linkPath is not null && error is null)
        {
            PreviewText.Text = $"{linkPath}  →  {Path.GetFullPath(source)}";
            PreviewText.Visibility = Visibility.Visible;
        }
        else
        {
            PreviewText.Visibility = Visibility.Collapsed;
        }

        ValidationText.Text = error ?? "";
        CreateButton.IsEnabled = error is null && !_busy;
        return error is null ? linkPath : null;
    }

    #endregion

    #region Pickers

    private async void BrowseSourceFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker(AppWindow.Id) { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add("*");
        var result = await picker.PickSingleFileAsync();
        if (result is not null) SourceBox.Text = result.Path;
    }

    private async void BrowseSourceFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker(AppWindow.Id) { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        var result = await picker.PickSingleFolderAsync();
        if (result is not null) SourceBox.Text = result.Path;
    }

    private async void BrowseDestination_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker(AppWindow.Id) { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        var result = await picker.PickSingleFolderAsync();
        if (result is not null) DestinationBox.Text = result.Path;
    }

    #endregion

    #region Drag and drop

    private void SourceCard_DragOver(object sender, DragEventArgs e) => AcceptStorageItems(e, "Use as source");
    private void DestinationCard_DragOver(object sender, DragEventArgs e) => AcceptStorageItems(e, "Use as destination");

    private static void AcceptStorageItems(DragEventArgs e, string caption)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        e.AcceptedOperation = DataPackageOperation.Link;
        e.DragUIOverride.Caption = caption;
    }

    private async void SourceCard_Drop(object sender, DragEventArgs e)
    {
        if (await GetFirstDroppedItem(e) is { } item) SourceBox.Text = item.Path;
    }

    private async void DestinationCard_Drop(object sender, DragEventArgs e)
    {
        if (await GetFirstDroppedItem(e) is not { } item) return;
        // Dropping a file onto the destination uses its containing folder.
        DestinationBox.Text = item is StorageFolder ? item.Path : Path.GetDirectoryName(item.Path) ?? item.Path;
    }

    private static async Task<IStorageItem?> GetFirstDroppedItem(DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return null;
        var items = await e.DataView.GetStorageItemsAsync();
        return items.FirstOrDefault();
    }

    #endregion

    #region Create

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (Validate() is not { } linkPath) return;
        string source = Path.GetFullPath(Source);

        SetBusy(true);
        StatusBar.IsOpen = false;
        try
        {
            LinkResult result;
            if (_service.RequiresElevation(_selectedType))
            {
                int code = await Elevation.RunElevatedAndWaitAsync(["--create", _selectedType.ToToken(), source, linkPath]);
                result = CommandRouter.DecodeExitCode(code, source, linkPath);
            }
            else
            {
                result = await Task.Run(() => _service.CreateLink(_selectedType, source, linkPath));
            }

            ShowResult(result);
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, "Couldn't create the link", ex.Message);
        }
        finally
        {
            SetBusy(false);
            Validate();
        }
    }

    private void ShowResult(LinkResult result)
    {
        if (result.Success)
        {
            _lastCreatedLink = result.LinkPath;
            var open = new Button { Content = "Show in Explorer" };
            open.Click += (_, _) => ShowInExplorer(_lastCreatedLink);
            ShowStatus(InfoBarSeverity.Success, $"{_selectedType.DisplayName()} created", result.LinkPath, open);

            // Ready for the next link: keep the destination, clear the source.
            _nameEditedByUser = false;
            SourceBox.Text = "";
            return;
        }

        if (result.Error == LinkError.Cancelled)
        {
            ShowStatus(InfoBarSeverity.Informational, "Cancelled", "Administrator permission was not granted, so no link was created.");
            return;
        }

        ShowStatus(InfoBarSeverity.Error, "Couldn't create the link", result.Message);
    }

    private static void ShowInExplorer(string? path)
    {
        if (path is null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        CreateButton.IsEnabled = !busy;
        CreateProgress.IsActive = busy;
        CreateProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ShieldIcon.Visibility = !busy && _service.RequiresElevation(_selectedType) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message, ButtonBase? action = null)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.ActionButton = action;
        StatusBar.IsOpen = true;
    }

    #endregion
}
