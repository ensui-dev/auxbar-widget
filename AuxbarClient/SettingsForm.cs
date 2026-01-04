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
    private CheckBox _discordEnabledToggle = null!;
    private CheckBox _showAlbumNameToggle = null!;
    private CheckBox _showProgressToggle = null!;
    private CheckBox _showButtonToggle = null!;

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
        Size = new Size(400, 380);
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

        // Title
        var titleLabel = CreatePixelLabel("SETTINGS", new Point(140, 20), GetPixelFont(10), PixelText);
        Controls.Add(titleLabel);

        // Discord section header
        var discordHeader = CreatePixelLabel("DISCORD RICH PRESENCE", new Point(20, 60), GetPixelFont(7), PixelAccent);
        Controls.Add(discordHeader);

        // Separator line
        var separator = new Panel
        {
            Location = new Point(20, 85),
            Size = new Size(340, 2),
            BackColor = PixelBorder
        };
        Controls.Add(separator);

        // Discord enabled toggle
        _discordEnabledToggle = CreateToggle("Enable Discord Presence", new Point(20, 100));
        _discordEnabledToggle.CheckedChanged += DiscordEnabledToggle_CheckedChanged;
        Controls.Add(_discordEnabledToggle);

        var enabledDesc = CreatePixelLabel("Show your music on Discord profile", new Point(50, 125), GetPixelFont(5), PixelTextDim);
        Controls.Add(enabledDesc);

        // Show album name toggle
        _showAlbumNameToggle = CreateToggle("Show Album Name", new Point(20, 155));
        _showAlbumNameToggle.CheckedChanged += SettingChanged;
        Controls.Add(_showAlbumNameToggle);

        var albumDesc = CreatePixelLabel("Display album name as hover text", new Point(50, 180), GetPixelFont(5), PixelTextDim);
        Controls.Add(albumDesc);

        // Show progress toggle
        _showProgressToggle = CreateToggle("Show Playback Progress", new Point(20, 210));
        _showProgressToggle.CheckedChanged += SettingChanged;
        Controls.Add(_showProgressToggle);

        var progressDesc = CreatePixelLabel("Display elapsed/remaining time", new Point(50, 235), GetPixelFont(5), PixelTextDim);
        Controls.Add(progressDesc);

        // Show button toggle
        _showButtonToggle = CreateToggle("Show 'Get Auxbar' Button", new Point(20, 265));
        _showButtonToggle.CheckedChanged += SettingChanged;
        Controls.Add(_showButtonToggle);

        var buttonDesc = CreatePixelLabel("Add link button to presence", new Point(50, 290), GetPixelFont(5), PixelTextDim);
        Controls.Add(buttonDesc);

        // Close button
        var closeButton = CreatePixelButton("CLOSE", new Point(130, 320), new Size(120, 35), PixelBgLight);
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

    private CheckBox CreateToggle(string text, Point location)
    {
        var toggle = new CheckBox
        {
            Text = text,
            Font = GetPixelFont(6),
            ForeColor = PixelText,
            BackColor = Color.Transparent,
            Location = location,
            Size = new Size(340, 25),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
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
