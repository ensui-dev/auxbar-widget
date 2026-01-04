using AuxbarClient.Services;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AuxbarClient;

public class SettingsForm : Form
{
    private readonly DiscordRpcService _discordRpcService;
    private readonly Action _onSettingsChanged;

    // Retro pixel color scheme (matching main form)
    private static readonly Color PixelPrimary = Color.FromArgb(255, 107, 157);
    private static readonly Color PixelAccent = Color.FromArgb(69, 230, 184);
    private static readonly Color PixelBgDark = Color.FromArgb(26, 26, 46);
    private static readonly Color PixelBgMid = Color.FromArgb(45, 45, 68);
    private static readonly Color PixelBgLight = Color.FromArgb(61, 61, 92);
    private static readonly Color PixelBorder = Color.FromArgb(92, 92, 138);
    private static readonly Color PixelText = Color.FromArgb(240, 230, 255);
    private static readonly Color PixelTextDim = Color.FromArgb(168, 158, 201);
    private static readonly Color PixelShadow = Color.FromArgb(13, 13, 26);

    // Custom font
    private static PrivateFontCollection? _fontCollection;
    private static FontFamily? _pixelFontFamily;

    // Controls
    private PixelCheckBox _discordEnabledToggle = null!;
    private PixelCheckBox _showAlbumNameToggle = null!;
    private PixelCheckBox _showProgressToggle = null!;
    private PixelCheckBox _showButtonToggle = null!;

    public SettingsForm(DiscordRpcService discordRpcService, Action onSettingsChanged)
    {
        _discordRpcService = discordRpcService;
        _onSettingsChanged = onSettingsChanged;

        LoadEmbeddedFont();
        InitializeComponent();
        LoadSettings();
    }

    private static void LoadEmbeddedFont()
    {
        if (_fontCollection != null) return;

        try
        {
            _fontCollection = new PrivateFontCollection();
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "AuxbarClient.Resources.PressStart2P-Regular.ttf";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                var fontData = new byte[stream.Length];
                stream.Read(fontData, 0, fontData.Length);

                var fontPtr = Marshal.AllocCoTaskMem(fontData.Length);
                Marshal.Copy(fontData, 0, fontPtr, fontData.Length);
                _fontCollection.AddMemoryFont(fontPtr, fontData.Length);
                Marshal.FreeCoTaskMem(fontPtr);

                _pixelFontFamily = _fontCollection.Families[0];
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load embedded font: {ex.Message}");
        }
    }

    private Font GetPixelFont(float size, FontStyle style = FontStyle.Regular)
    {
        if (_pixelFontFamily != null)
        {
            return new Font(_pixelFontFamily, size, style, GraphicsUnit.Point);
        }
        return new Font("Consolas", size, style);
    }

    private void InitializeComponent()
    {
        Text = "Settings";
        // Use AutoScaleMode and proper sizing for DPI awareness
        AutoScaleMode = AutoScaleMode.Dpi;
        // Height: Title bar (~30) + content (~410) + padding = 480
        ClientSize = new Size(400, 450);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = PixelBgDark;
        DoubleBuffered = true;

        // Load icon from main form
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("AuxbarClient.Resources.app.ico");
            if (stream != null)
            {
                Icon = new Icon(stream);
            }
        }
        catch { }

        Paint += SettingsForm_Paint;

        // Title - more top padding
        var titleLabel = CreatePixelLabel("SETTINGS", new Point(150, 30), GetPixelFont(10), PixelText);
        Controls.Add(titleLabel);

        // Discord section header - more spacing
        var discordHeader = CreatePixelLabel("DISCORD RICH PRESENCE", new Point(30, 75), GetPixelFont(7), PixelAccent);
        Controls.Add(discordHeader);

        // Separator line
        var separator = new Panel
        {
            Location = new Point(30, 105),
            Size = new Size(340, 2),
            BackColor = PixelBorder
        };
        Controls.Add(separator);

        // Discord enabled toggle - increased vertical spacing
        _discordEnabledToggle = CreateToggle("Enable Discord Presence", new Point(30, 125));
        _discordEnabledToggle.CheckedChanged += DiscordEnabledToggle_CheckedChanged;
        Controls.Add(_discordEnabledToggle);

        var enabledDesc = CreatePixelLabel("Show your music on Discord profile", new Point(60, 155), GetPixelFont(5), PixelTextDim);
        Controls.Add(enabledDesc);

        // Show album name toggle
        _showAlbumNameToggle = CreateToggle("Show Album Name", new Point(30, 190));
        _showAlbumNameToggle.CheckedChanged += SettingChanged;
        Controls.Add(_showAlbumNameToggle);

        var albumDesc = CreatePixelLabel("Display album name as hover text", new Point(60, 220), GetPixelFont(5), PixelTextDim);
        Controls.Add(albumDesc);

        // Show progress toggle
        _showProgressToggle = CreateToggle("Show Playback Progress", new Point(30, 255));
        _showProgressToggle.CheckedChanged += SettingChanged;
        Controls.Add(_showProgressToggle);

        var progressDesc = CreatePixelLabel("Display elapsed/remaining time", new Point(60, 285), GetPixelFont(5), PixelTextDim);
        Controls.Add(progressDesc);

        // Show button toggle
        _showButtonToggle = CreateToggle("Show 'Get Auxbar' Button", new Point(30, 320));
        _showButtonToggle.CheckedChanged += SettingChanged;
        Controls.Add(_showButtonToggle);

        var buttonDesc = CreatePixelLabel("Add link button to presence", new Point(60, 350), GetPixelFont(5), PixelTextDim);
        Controls.Add(buttonDesc);

        // Close button - positioned with proper padding from bottom
        var closeButton = CreatePixelButton("CLOSE", new Point(140, 400), new Size(120, 35), PixelBgLight);
        closeButton.Click += (s, e) => Close();
        Controls.Add(closeButton);
    }

    private void SettingsForm_Paint(object? sender, PaintEventArgs e)
    {
        // Draw CRT scanlines effect
        using var brush = new SolidBrush(Color.FromArgb(25, 0, 0, 0));
        for (int y = 0; y < Height; y += 3)
        {
            e.Graphics.FillRectangle(brush, 0, y, Width, 1);
        }
    }

    private PixelCheckBox CreateToggle(string text, Point location)
    {
        var toggle = new PixelCheckBox
        {
            Text = text,
            Font = GetPixelFont(6),
            ForeColor = PixelText,
            Location = location,
            Size = new Size(340, 25),
            Cursor = Cursors.Hand,
            AccentColor = PixelAccent,
            BoxBackColor = PixelBgMid,
            BoxBorderColor = PixelBorder,
            DisabledBackColor = PixelBgDark,
            DisabledBorderColor = PixelTextDim
        };

        return toggle;
    }

    private Label CreatePixelLabel(string text, Point location, Font font, Color foreColor)
    {
        return new Label
        {
            Text = text,
            Font = font,
            ForeColor = foreColor,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(location.X, location.Y - 2)
        };
    }

    private Button CreatePixelButton(string text, Point location, Size size, Color bgColor)
    {
        var button = new Button
        {
            Text = text,
            Location = location,
            Size = size,
            BackColor = bgColor,
            ForeColor = PixelText,
            FlatStyle = FlatStyle.Flat,
            Font = GetPixelFont(7),
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderSize = 3;
        button.FlatAppearance.BorderColor = PixelBorder;

        button.Paint += (s, e) =>
        {
            using var shadowPen = new Pen(PixelShadow, 2);
            e.Graphics.DrawLine(shadowPen, 4, button.Height - 1, button.Width - 1, button.Height - 1);
            e.Graphics.DrawLine(shadowPen, button.Width - 1, 4, button.Width - 1, button.Height - 1);
        };

        button.MouseEnter += (s, e) => button.BackColor = PixelBgMid;
        button.MouseLeave += (s, e) => button.BackColor = bgColor;

        return button;
    }

    private void LoadSettings()
    {
        var config = ConfigService.Load();
        _discordEnabledToggle.Checked = config.Discord.Enabled;
        _showAlbumNameToggle.Checked = config.Discord.ShowAlbumName;
        _showProgressToggle.Checked = config.Discord.ShowPlaybackProgress;
        _showButtonToggle.Checked = config.Discord.ShowButton;

        UpdateSubTogglesState();
    }

    private void UpdateSubTogglesState()
    {
        var enabled = _discordEnabledToggle.Checked;
        _showAlbumNameToggle.Enabled = enabled;
        _showProgressToggle.Enabled = enabled;
        _showButtonToggle.Enabled = enabled;

        var dimColor = enabled ? PixelText : PixelTextDim;
        _showAlbumNameToggle.ForeColor = dimColor;
        _showProgressToggle.ForeColor = dimColor;
        _showButtonToggle.ForeColor = dimColor;
    }

    private void DiscordEnabledToggle_CheckedChanged(object? sender, EventArgs e)
    {
        UpdateSubTogglesState();
        SaveAndApplySettings();
    }

    private void SettingChanged(object? sender, EventArgs e)
    {
        SaveAndApplySettings();
    }

    private void SaveAndApplySettings()
    {
        var settings = new DiscordSettings
        {
            Enabled = _discordEnabledToggle.Checked,
            ShowAlbumName = _showAlbumNameToggle.Checked,
            ShowPlaybackProgress = _showProgressToggle.Checked,
            ShowButton = _showButtonToggle.Checked
        };

        ConfigService.UpdateDiscordSettings(settings);

        // Apply to Discord RPC service
        _discordRpcService.IsEnabled = settings.Enabled;
        _discordRpcService.ShowAlbumName = settings.ShowAlbumName;
        _discordRpcService.ShowPlaybackProgress = settings.ShowPlaybackProgress;
        _discordRpcService.ShowButton = settings.ShowButton;

        if (settings.Enabled)
        {
            if (!_discordRpcService.IsConnected)
            {
                _discordRpcService.Initialize();
            }
        }
        else
        {
            _discordRpcService.ClearPresence();
        }

        _onSettingsChanged?.Invoke();
    }
}

// Custom owner-drawn checkbox with pixel art style
public class PixelCheckBox : Control
{
    private bool _checked;
    private bool _hovering;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked != value)
            {
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public Color AccentColor { get; set; } = Color.FromArgb(69, 230, 184);
    public Color BoxBackColor { get; set; } = Color.FromArgb(45, 45, 68);
    public Color BoxBorderColor { get; set; } = Color.FromArgb(92, 92, 138);
    public Color DisabledBackColor { get; set; } = Color.FromArgb(26, 26, 46);
    public Color DisabledBorderColor { get; set; } = Color.FromArgb(168, 158, 201);

    public event EventHandler? CheckedChanged;

    public PixelCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(200, 25);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

        var boxSize = 14;
        var boxY = (Height - boxSize) / 2;

        // Draw checkbox background
        using var bgBrush = new SolidBrush(Enabled ? (_hovering ? BoxBorderColor : BoxBackColor) : DisabledBackColor);
        g.FillRectangle(bgBrush, 0, boxY, boxSize, boxSize);

        // Draw checkbox border
        using var borderPen = new Pen(Enabled ? BoxBorderColor : DisabledBorderColor, 2);
        g.DrawRectangle(borderPen, 0, boxY, boxSize, boxSize);

        // Draw checkmark if checked
        if (Checked)
        {
            using var checkPen = new Pen(AccentColor, 2);
            g.DrawLine(checkPen, 3, boxY + 7, 6, boxY + 10);
            g.DrawLine(checkPen, 6, boxY + 10, 11, boxY + 4);
        }

        // Draw text
        var textX = boxSize + 8;
        using var textBrush = new SolidBrush(ForeColor);
        g.DrawString(Text, Font, textBrush, textX, (Height - Font.Height) / 2f);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovering = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovering = false;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (Enabled)
        {
            Checked = !Checked;
        }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }
}
