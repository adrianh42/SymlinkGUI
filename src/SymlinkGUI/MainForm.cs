using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using SymlinkGUI.Core;

namespace SymlinkGUI;

public sealed partial class MainForm : Form
{
    private const LinkType CurrentType = LinkType.SymbolicLink;

    private readonly LinkService _service = LinkService.Default;
    private readonly ContextMenuRegistrar _registrar = ContextMenuRegistrar.CreateDefault();

    private bool _nameEditedByUser;
    private bool _settingNameProgrammatically;
    private bool _updatingToggle;
    private bool _busy;
    private string? _lastCreatedLink;

    // Theme colors
    private readonly bool _isDark;
    private readonly Color _bgForm;
    private readonly Color _bgCard;
    private readonly Color _borderCard;
    private readonly Color _textPrimary;
    private readonly Color _textSecondary;
    private readonly Color _accentColor;
    private readonly Color _accentText;

    // UI Controls
    private TextBox _sourceBox = null!;
    private TextBox _destinationBox = null!;
    private TextBox _nameBox = null!;
    private Label _sourceInfoLabel = null!;
    private Label _previewLabel = null!;
    private Label _validationLabel = null!;
    private Button _createButton = null!;
    private CheckBox _contextMenuCheckBox = null!;
    private Label _elevationTitle = null!;
    private Label _elevationDesc = null!;
    private Panel _statusBanner = null!;
    private Label _statusBannerText = null!;
    private Button _statusBannerAction = null!;
    private Button _statusBannerClose = null!;
    private Panel _staleBanner = null!;

    // Win32 APIs for shield & dark mode
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);
    private const uint BCM_SETSHIELD = 0x160C;

    public MainForm(string? initialSource = null)
    {
        _isDark = DetectDarkMode();

        if (_isDark)
        {
            _bgForm = Color.FromArgb(32, 32, 32);
            _bgCard = Color.FromArgb(43, 43, 43);
            _borderCard = Color.FromArgb(60, 60, 60);
            _textPrimary = Color.FromArgb(245, 245, 245);
            _textSecondary = Color.FromArgb(170, 170, 170);
            _accentColor = Color.FromArgb(0, 120, 215);
            _accentText = Color.White;
        }
        else
        {
            _bgForm = Color.FromArgb(243, 243, 243);
            _bgCard = Color.FromArgb(255, 255, 255);
            _borderCard = Color.FromArgb(229, 229, 229);
            _textPrimary = Color.FromArgb(27, 27, 27);
            _textSecondary = Color.FromArgb(92, 92, 92);
            _accentColor = Color.FromArgb(0, 103, 192);
            _accentText = Color.White;
        }

        InitializeComponents();

        if (!string.IsNullOrEmpty(initialSource))
        {
            SetSource(initialSource);
        }

        RefreshEnvironment();
        ValidateInputs();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_isDark)
        {
            int darkMode = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        }
        UpdateShieldState();
    }

    public void SetSource(string path)
    {
        _sourceBox.Text = path;
        _destinationBox.Focus();
    }

    private static bool DetectDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            return val is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    private void InitializeComponents()
    {
        Text = "Symlink GUI";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 780);
        MinimumSize = new Size(520, 680);
        BackColor = _bgForm;
        ForeColor = _textPrimary;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        AutoScroll = true;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 9,
            Padding = new Padding(24, 20, 24, 24),
            BackColor = Color.Transparent
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        // Header
        var titleLabel = new Label
        {
            Text = "Create a symbolic link",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = _textPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 14)
        };
        mainLayout.Controls.Add(titleLabel);

        // Status Banner (Initially hidden)
        _statusBanner = CreateStatusBanner();
        mainLayout.Controls.Add(_statusBanner);

        // Stale Menu Banner (Initially hidden)
        _staleBanner = CreateStaleBanner();
        mainLayout.Controls.Add(_staleBanner);

        // Card 1: Source
        var sourceCard = CreateCardPanel("Source", "The existing file or folder the link will point to. You can also drag it here.");
        sourceCard.AllowDrop = true;
        sourceCard.DragEnter += SourceCard_DragEnter;
        sourceCard.DragDrop += SourceCard_DragDrop;

        var sourceInputLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 6, 0, 4)
        };
        sourceInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        sourceInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sourceInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _sourceBox = CreateStyledTextBox(@"C:\Path\To\Original");
        _sourceBox.AllowDrop = true;
        _sourceBox.DragEnter += SourceCard_DragEnter;
        _sourceBox.DragDrop += SourceCard_DragDrop;
        _sourceBox.TextChanged += SourceBox_TextChanged;

        var browseFileBtn = CreateSecondaryButton("📄 File");
        browseFileBtn.Click += BrowseSourceFile_Click;
        var browseFolderBtn = CreateSecondaryButton("📁 Folder");
        browseFolderBtn.Click += BrowseSourceFolder_Click;

        sourceInputLayout.Controls.Add(_sourceBox, 0, 0);
        sourceInputLayout.Controls.Add(browseFileBtn, 1, 0);
        sourceInputLayout.Controls.Add(browseFolderBtn, 2, 0);
        sourceCard.Controls.Add(sourceInputLayout);

        _sourceInfoLabel = new Label
        {
            AutoSize = true,
            ForeColor = _textSecondary,
            Font = new Font("Segoe UI", 8.5f),
            Margin = new Padding(0, 4, 0, 0),
            Visible = false
        };
        sourceCard.Controls.Add(_sourceInfoLabel);
        mainLayout.Controls.Add(sourceCard);

        // Card 2: Destination
        var destCard = CreateCardPanel("Destination folder", "Where the new link will be created.");
        destCard.AllowDrop = true;
        destCard.DragEnter += DestinationCard_DragEnter;
        destCard.DragDrop += DestinationCard_DragDrop;

        var destInputLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 6, 0, 0)
        };
        destInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        destInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _destinationBox = CreateStyledTextBox(@"C:\Path\To\Folder");
        _destinationBox.AllowDrop = true;
        _destinationBox.DragEnter += DestinationCard_DragEnter;
        _destinationBox.DragDrop += DestinationCard_DragDrop;
        _destinationBox.TextChanged += (s, e) => ValidateInputs();

        var browseDestBtn = CreateSecondaryButton("Browse...");
        browseDestBtn.Click += BrowseDestination_Click;

        destInputLayout.Controls.Add(_destinationBox, 0, 0);
        destInputLayout.Controls.Add(browseDestBtn, 1, 0);
        destCard.Controls.Add(destInputLayout);
        mainLayout.Controls.Add(destCard);

        // Card 3: Link Name & Preview
        var nameCard = CreateCardPanel("Link name", null);
        _nameBox = CreateStyledTextBox("Name of the link");
        _nameBox.TextChanged += NameBox_TextChanged;
        nameCard.Controls.Add(_nameBox);

        _previewLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Cascadia Mono, Consolas", 8.5f),
            ForeColor = _textSecondary,
            Margin = new Padding(0, 6, 0, 0),
            Visible = false
        };
        nameCard.Controls.Add(_previewLabel);
        mainLayout.Controls.Add(nameCard);

        // Action Panel (Validation message + Create button)
        var actionPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 14)
        };
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _validationLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = _textSecondary,
            Font = new Font("Segoe UI", 9f),
            AutoSize = true
        };

        _createButton = new Button
        {
            Text = "Create Symlink",
            Width = 160,
            Height = 36,
            FlatStyle = FlatStyle.System,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Enabled = false
        };
        _createButton.Click += CreateButton_Click;

        actionPanel.Controls.Add(_validationLabel, 0, 0);
        actionPanel.Controls.Add(_createButton, 1, 0);
        mainLayout.Controls.Add(actionPanel);

        // Explorer Integration Card
        var explorerCard = CreateCardPanel("Explorer integration", null);

        var explorerInner = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1
        };
        explorerInner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        explorerInner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var explorerTextPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        var explorerTitle = new Label
        {
            Text = "Show in File Explorer context menu",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = _textPrimary,
            AutoSize = true
        };
        var explorerSub = new Label
        {
            Text = "Adds \"Pick as Symlink Source\", \"Drop Symbolic Link Here\" and \"Create Symlink To...\". On Windows 11, find them under \"Show more options\".",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = _textSecondary,
            AutoSize = true,
            MaximumSize = new Size(460, 0),
            Margin = new Padding(0, 2, 0, 0)
        };
        explorerTextPanel.Controls.Add(explorerTitle);
        explorerTextPanel.Controls.Add(explorerSub);

        _contextMenuCheckBox = new CheckBox
        {
            Text = "Enabled",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Font = new Font("Segoe UI", 9.5f)
        };
        _contextMenuCheckBox.CheckedChanged += ContextMenuCheckBox_CheckedChanged;

        explorerInner.Controls.Add(explorerTextPanel, 0, 0);
        explorerInner.Controls.Add(_contextMenuCheckBox, 1, 0);
        explorerCard.Controls.Add(explorerInner);
        mainLayout.Controls.Add(explorerCard);

        // Elevation Card
        var elevationCard = CreateCardPanel("Permission status", null);
        var elevationPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        _elevationTitle = new Label
        {
            Text = "Administrator permission",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = _textPrimary,
            AutoSize = true
        };
        _elevationDesc = new Label
        {
            Text = "Checking permissions...",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = _textSecondary,
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Margin = new Padding(0, 2, 0, 0)
        };
        elevationPanel.Controls.Add(_elevationTitle);
        elevationPanel.Controls.Add(_elevationDesc);
        elevationCard.Controls.Add(elevationPanel);
        mainLayout.Controls.Add(elevationCard);

        Controls.Add(mainLayout);

        Activated += (_, _) => RefreshEnvironment();
    }

    #region Helpers for Modern Controls

    private Panel CreateCardPanel(string title, string? description)
    {
        var card = new CardPanel(_bgCard, _borderCard)
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 14, 16, 14),
            Margin = new Padding(0, 0, 0, 12)
        };

        var titleLabel = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = _textPrimary,
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, description != null ? 2 : 6)
        };
        card.Controls.Add(titleLabel);

        if (!string.IsNullOrEmpty(description))
        {
            var descLabel = new Label
            {
                Text = description,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = _textSecondary,
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 8)
            };
            card.Controls.Add(descLabel);
        }

        return card;
    }

    private TextBox CreateStyledTextBox(string placeholder)
    {
        var box = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 32,
            Font = new Font("Segoe UI", 9.5f),
            BackColor = _bgCard,
            ForeColor = _textPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = placeholder
        };
        return box;
    }

    private Button CreateSecondaryButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            Height = 30,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = _isDark ? Color.FromArgb(55, 55, 55) : Color.FromArgb(240, 240, 240),
            ForeColor = _textPrimary,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(6, 0, 0, 0)
        };
        btn.FlatAppearance.BorderColor = _borderCard;
        btn.FlatAppearance.BorderSize = 1;
        return btn;
    }

    private Panel CreateStatusBanner()
    {
        var banner = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 12),
            Visible = false
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _statusBannerText = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true,
            Font = new Font("Segoe UI", 9f)
        };

        _statusBannerAction = CreateSecondaryButton("Show in Explorer");
        _statusBannerAction.Visible = false;

        _statusBannerClose = new Button
        {
            Text = "✕",
            Width = 26,
            Height = 26,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            Margin = new Padding(6, 0, 0, 0)
        };
        _statusBannerClose.FlatAppearance.BorderSize = 0;
        _statusBannerClose.Click += (_, _) => _statusBanner.Visible = false;

        layout.Controls.Add(_statusBannerText, 0, 0);
        layout.Controls.Add(_statusBannerAction, 1, 0);
        layout.Controls.Add(_statusBannerClose, 2, 0);
        banner.Controls.Add(layout);

        return banner;
    }

    private Panel CreateStaleBanner()
    {
        var banner = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = _isDark ? Color.FromArgb(60, 50, 20) : Color.FromArgb(255, 248, 225),
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 12),
            Visible = false
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var text = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true,
            Text = "Context menu points to another location: The menu entries were registered from a different copy of this app.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = _isDark ? Color.FromArgb(255, 215, 100) : Color.FromArgb(140, 90, 0)
        };

        var repairBtn = CreateSecondaryButton("Repair");
        repairBtn.Click += (_, _) =>
        {
            _registrar.Install();
            RefreshEnvironment();
        };

        layout.Controls.Add(text, 0, 0);
        layout.Controls.Add(repairBtn, 1, 0);
        banner.Controls.Add(layout);
        return banner;
    }

    #endregion

    #region Environment & Status

    private void RefreshEnvironment()
    {
        _updatingToggle = true;
        _contextMenuCheckBox.Checked = _registrar.IsInstalled;
        _updatingToggle = false;
        _staleBanner.Visible = _registrar.IsStale;

        bool requiresElevation = _service.RequiresElevation(CurrentType);
        bool admin = Elevation.IsAdministrator;

        _elevationTitle.Text = admin ? "Running as administrator" : "Administrator permission";
        _elevationDesc.Text = requiresElevation
            ? "Creating symbolic links requires administrator permission. Windows will show a UAC prompt when creating links."
            : admin
                ? "Running as administrator. Links can be created without additional UAC prompts."
                : "Developer mode is active or user rights allow links without elevation prompts.";

        UpdateShieldState();
    }

    private void UpdateShieldState()
    {
        if (_createButton.IsHandleCreated)
        {
            bool requiresElevation = _service.RequiresElevation(CurrentType);
            SendMessage(_createButton.Handle, BCM_SETSHIELD, 0, requiresElevation ? 1 : 0);
        }
    }

    private void ContextMenuCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_updatingToggle) return;
        try
        {
            if (_contextMenuCheckBox.Checked) _registrar.Install();
            else _registrar.Uninstall();
        }
        catch (Exception ex)
        {
            ShowStatus(StatusSeverity.Error, $"Couldn't update the context menu: {ex.Message}");
        }
        RefreshEnvironment();
    }

    private enum StatusSeverity { Success, Error, Info }

    private void ShowStatus(StatusSeverity severity, string message, Action? action = null, string actionText = "Show in Explorer")
    {
        _statusBannerText.Text = message;
        _statusBanner.BackColor = severity switch
        {
            StatusSeverity.Success => _isDark ? Color.FromArgb(20, 55, 30) : Color.FromArgb(235, 247, 238),
            StatusSeverity.Error => _isDark ? Color.FromArgb(60, 25, 25) : Color.FromArgb(253, 237, 237),
            _ => _isDark ? Color.FromArgb(25, 45, 65) : Color.FromArgb(235, 243, 252)
        };
        _statusBannerText.ForeColor = severity switch
        {
            StatusSeverity.Success => _isDark ? Color.FromArgb(140, 240, 160) : Color.FromArgb(20, 100, 40),
            StatusSeverity.Error => _isDark ? Color.FromArgb(255, 130, 130) : Color.FromArgb(180, 40, 40),
            _ => _isDark ? Color.FromArgb(130, 190, 255) : Color.FromArgb(20, 80, 160)
        };

        if (action != null)
        {
            _statusBannerAction.Text = actionText;
            _statusBannerAction.Visible = true;
            _statusBannerAction.Click -= StatusActionHandler;
            _statusBannerAction.Click += StatusActionHandler;
            void StatusActionHandler(object? s, EventArgs e)
            {
                _statusBannerAction.Click -= StatusActionHandler;
                action();
            }
        }
        else
        {
            _statusBannerAction.Visible = false;
        }

        _statusBanner.Visible = true;
    }

    #endregion

    #region Inputs & Validation

    private string Source => _sourceBox.Text.Trim().Trim('"');
    private string Destination => _destinationBox.Text.Trim().Trim('"');
    private string LinkName => _nameBox.Text.Trim();

    private void SourceBox_TextChanged(object? sender, EventArgs e)
    {
        string source = Source;
        UpdateSourceInfo(source);

        if (!_nameEditedByUser)
        {
            _settingNameProgrammatically = true;
            _nameBox.Text = DefaultLinkName(source);
            _settingNameProgrammatically = false;
        }
        ValidateInputs();
    }

    private void NameBox_TextChanged(object? sender, EventArgs e)
    {
        if (!_settingNameProgrammatically)
            _nameEditedByUser = _nameBox.Text.Length > 0;
        ValidateInputs();
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
            _sourceInfoLabel.Visible = false;
            return;
        }

        _sourceInfoLabel.Visible = true;
        if (Directory.Exists(source))
        {
            _sourceInfoLabel.Text = "📁 Folder";
            _sourceInfoLabel.ForeColor = _textSecondary;
        }
        else if (File.Exists(source))
        {
            _sourceInfoLabel.Text = $"📄 File · {FormatSize(new FileInfo(source).Length)}";
            _sourceInfoLabel.ForeColor = _textSecondary;
        }
        else
        {
            _sourceInfoLabel.Text = "⚠️ Not found";
            _sourceInfoLabel.ForeColor = Color.OrangeRed;
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

    private string? ValidateInputs()
    {
        string? error = null;
        string? linkPath = null;

        if (Source.Length == 0) error = "Choose a source file or folder.";
        else if (!LinkService.PathExists(Source)) error = "The source doesn't exist.";
        else if (Destination.Length == 0) error = "Choose a destination folder.";
        else if (!Directory.Exists(Destination)) error = "The destination folder doesn't exist.";
        else if (LinkService.ValidateName(LinkName) is { } nameError) error = nameError;
        else
        {
            try
            {
                linkPath = Path.GetFullPath(Path.Combine(Destination, LinkName));
                if (LinkService.PathExists(linkPath))
                    error = $"\"{LinkName}\" already exists in the destination folder. Choose another name.";
                else if (string.Equals(linkPath, Path.GetFullPath(Source), StringComparison.OrdinalIgnoreCase))
                    error = "The link can't replace its own source.";
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        if (linkPath != null && error == null)
        {
            _previewLabel.Text = $"{linkPath}  →  {Path.GetFullPath(Source)}";
            _previewLabel.Visible = true;
        }
        else
        {
            _previewLabel.Visible = false;
        }

        _validationLabel.Text = error ?? "";
        _validationLabel.ForeColor = error != null ? (_isDark ? Color.FromArgb(255, 140, 140) : Color.DarkRed) : _textSecondary;
        _createButton.Enabled = error == null && !_busy;
        return error == null ? linkPath : null;
    }

    #endregion

    #region Drag and Drop & Pickers

    private void SourceCard_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Link;
    }

    private void SourceCard_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            _sourceBox.Text = files[0];
        }
    }

    private void DestinationCard_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Link;
    }

    private void DestinationCard_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            string item = files[0];
            _destinationBox.Text = Directory.Exists(item) ? item : Path.GetDirectoryName(item) ?? item;
        }
    }

    private void BrowseSourceFile_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose Source File",
            Filter = "All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _sourceBox.Text = dialog.FileName;
        }
    }

    private void BrowseSourceFolder_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose Source Folder",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _sourceBox.Text = dialog.SelectedPath;
        }
    }

    private void BrowseDestination_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose Destination Folder",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _destinationBox.Text = dialog.SelectedPath;
        }
    }

    #endregion

    #region Creation

    private async void CreateButton_Click(object? sender, EventArgs e)
    {
        if (ValidateInputs() is not { } linkPath) return;
        string source = Path.GetFullPath(Source);

        SetBusy(true);
        _statusBanner.Visible = false;

        try
        {
            LinkResult result;
            if (_service.RequiresElevation(CurrentType))
            {
                int code = await Elevation.RunElevatedAndWaitAsync(["--create", CurrentType.ToToken(), source, linkPath]);
                result = CommandRouter.DecodeExitCode(code, source, linkPath);
            }
            else
            {
                result = await Task.Run(() => _service.CreateLink(CurrentType, source, linkPath));
            }

            ShowResult(result);
        }
        catch (Exception ex)
        {
            ShowStatus(StatusSeverity.Error, $"Couldn't create the link: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            ValidateInputs();
        }
    }

    private void ShowResult(LinkResult result)
    {
        if (result.Success)
        {
            _lastCreatedLink = result.LinkPath;
            ShowStatus(
                StatusSeverity.Success,
                $"Symbolic link created: {result.LinkPath}",
                () => ShowInExplorer(_lastCreatedLink),
                "Show in Explorer"
            );

            // Ready for next link: keep destination, clear source
            _nameEditedByUser = false;
            _sourceBox.Text = "";
            return;
        }

        if (result.Error == LinkError.Cancelled)
        {
            ShowStatus(StatusSeverity.Info, "Cancelled: Administrator permission was not granted, so no link was created.");
            return;
        }

        ShowStatus(StatusSeverity.Error, $"Couldn't create the link: {result.Message}");
    }

    private static void ShowInExplorer(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _createButton.Enabled = !busy;
        _createButton.Text = busy ? "Creating..." : "Create Symlink";
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        UpdateShieldState();
    }

    #endregion

    private sealed class CardPanel : Panel
    {
        private readonly Color _borderColor;

        public CardPanel(Color bgColor, Color borderColor)
        {
            BackColor = bgColor;
            _borderColor = borderColor;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(_borderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}
