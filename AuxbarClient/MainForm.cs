using AuxbarClient.Services;
using AuxbarClient.Models;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AuxbarClient;

public partial class MainForm : Form
{
    private readonly ApiService _apiService;
    private readonly WebSocketService _webSocketService;
    private readonly MediaSessionService _mediaSessionService;
    private readonly DiscordRpcService _discordRpcService;

    private NotifyIcon _notifyIcon = null!;
    private Label _statusLabel = null!;
    private Label _trackLabel = null!;
    private Label _discordStatusLabel = null!;
    private TextBox _emailBox = null!;
    private TextBox _passwordBox = null!;
    private Button _loginButton = null!;
    private Button _logoutButton = null!;
    private Panel _loginPanel = null!;
    private Panel _connectedPanel = null!;
    private Panel _statusIndicator = null!;
    private Panel _discordIndicator = null!;
    private PictureBox _logoPictureBox = null!;
    private PictureBox _logoConnectedPictureBox = null!;

    // Custom font
    private static PrivateFontCollection? _fontCollection;
    private static FontFamily? _pixelFontFamily;

    // Retro pixel color scheme (matching web)
    private static readonly Color PixelPrimary = Color.FromArgb(255, 107, 157);     // #ff6b9d
    private static readonly Color PixelAccent = Color.FromArgb(69, 230, 184);       // #45e6b8
    private static readonly Color PixelBgDark = Color.FromArgb(26, 26, 46);         // #1a1a2e
    private static readonly Color PixelBgMid = Color.FromArgb(45, 45, 68);          // #2d2d44
    private static readonly Color PixelBgLight = Color.FromArgb(61, 61, 92);        // #3d3d5c
    private static readonly Color PixelBorder = Color.FromArgb(92, 92, 138);        // #5c5c8a
    private static readonly Color PixelText = Color.FromArgb(240, 230, 255);        // #f0e6ff
    private static readonly Color PixelTextDim = Color.FromArgb(168, 158, 201);     // #a89ec9
    private static readonly Color PixelShadow = Color.FromArgb(13, 13, 26);         // #0d0d1a

    public MainForm()
    {
        _apiService = new ApiService();
        _webSocketService = new WebSocketService(_apiService);
        _mediaSessionService = new MediaSessionService();
        _discordRpcService = new DiscordRpcService();

        LoadEmbeddedFont();
        InitializeComponent();
        SetupTrayIcon();
        SetupEvents();

        // Load Discord settings from config
        LoadDiscordSettings();
    }

    private void LoadDiscordSettings()
    {
        var config = ConfigService.Load();
        _discordRpcService.IsEnabled = config.Discord.Enabled;
        _discordRpcService.ShowAlbumName = config.Discord.ShowAlbumName;
        _discordRpcService.ShowPlaybackProgress = config.Discord.ShowPlaybackProgress;
        _discordRpcService.ShowButton = config.Discord.ShowButton;
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
        // Fallback to Consolas if font loading failed
        return new Font("Consolas", size, style);
    }

    private void InitializeComponent()
    {
        Text = "Auxbar";
        Size = new Size(450, 420);
        FormBorderStyle = FormBorderStyle.FixedSingle;

        // Set window icon
        Icon = LoadEmbeddedIcon();
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = PixelBgDark;
        DoubleBuffered = true;

        // Override paint to add scanlines effect
        Paint += MainForm_Paint;

        // Login Panel
        _loginPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(20)
        };

        // Logo PictureBox
        _logoPictureBox = CreateLogoPictureBox(new Point(85, 15), new Size(260, 55));

        var subtitleLabel = CreatePixelLabel("SIGN IN TO CONNECT", new Point(130, 75), GetPixelFont(6), PixelTextDim);

        // Decorative border panel with pixel art style
        var decorBorder = new Panel
        {
            Location = new Point(20, 110),
            Size = new Size(390, 3),
            BackColor = PixelBorder
        };

        var emailLabel = CreatePixelLabel("EMAIL:", new Point(30, 135), GetPixelFont(7), PixelText);

        var emailPanel = CreatePixelTextBoxPanel(new Point(30, 155), new Size(370, 36), out _emailBox);

        var passwordLabel = CreatePixelLabel("PASSWORD:", new Point(30, 197), GetPixelFont(7), PixelText);

        var passwordPanel = CreatePixelTextBoxPanel(new Point(30, 225), new Size(370, 36), out _passwordBox);
        _passwordBox.PasswordChar = '•';

        _loginButton = CreatePixelButton("SIGN IN", new Point(30, 285), new Size(370, 45), PixelAccent);
        _loginButton.Click += LoginButton_Click;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";
        var versionLabel = CreatePixelLabel(versionText, new Point(185, 350), GetPixelFont(5), PixelTextDim);

        _loginPanel.Controls.AddRange(new Control[]
        {
            _logoPictureBox, subtitleLabel, decorBorder, emailLabel, emailPanel,
            passwordLabel, passwordPanel, _loginButton, versionLabel
        });

        // Connected Panel
        _connectedPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(20),
            Visible = false
        };

        // Logo for connected panel
        _logoConnectedPictureBox = CreateLogoPictureBox(new Point(85, 15), new Size(260, 55));

        // Server status indicator (animated dot)
        _statusIndicator = new Panel
        {
            Location = new Point(30, 80),
            Size = new Size(10, 10),
            BackColor = PixelPrimary
        };

        _statusLabel = CreatePixelLabel("SERVER: CONNECTING...", new Point(48, 78), GetPixelFont(6), PixelTextDim);

        // Discord status indicator
        _discordIndicator = new Panel
        {
            Location = new Point(240, 80),
            Size = new Size(10, 10),
            BackColor = PixelTextDim
        };

        _discordStatusLabel = CreatePixelLabel("DISCORD: OFF", new Point(258, 78), GetPixelFont(6), PixelTextDim);

        // Track info panel with pixel border
        var trackPanel = new Panel
        {
            Location = new Point(30, 105),
            Size = new Size(370, 110),
            BackColor = PixelBgMid
        };
        trackPanel.Paint += (s, e) =>
        {
            // Pixel border with shadow
            using var shadowPen = new Pen(PixelShadow, 3);
            e.Graphics.DrawRectangle(shadowPen, 3, 3, trackPanel.Width - 4, trackPanel.Height - 4);
            using var borderPen = new Pen(PixelBorder, 3);
            e.Graphics.DrawRectangle(borderPen, 0, 0, trackPanel.Width - 1, trackPanel.Height - 1);
        };

        _trackLabel = new Label
        {
            Text = "NO MUSIC PLAYING",
            Font = GetPixelFont(8),
            ForeColor = PixelText,
            Location = new Point(15, 15),
            Size = new Size(340, 80),
            AutoEllipsis = true,
            BackColor = Color.Transparent
        };

        trackPanel.Controls.Add(_trackLabel);

        // Buttons with pixel art style
        _logoutButton = CreatePixelButton("SIGN OUT", new Point(30, 230), new Size(110, 38), PixelBgLight);
        _logoutButton.Click += LogoutButton_Click;

        var minimizeButton = CreatePixelButton("MINIMIZE", new Point(150, 230), new Size(110, 38), PixelBgLight);
        minimizeButton.Click += (s, e) => Hide();

        var settingsButton = CreatePixelButton("SETTINGS", new Point(270, 230), new Size(110, 38), PixelBgLight);
        settingsButton.Click += SettingsButton_Click;

        var footerLabel = CreatePixelLabel("KEEP OPEN WHILE STREAMING", new Point(95, 290), GetPixelFont(5), PixelTextDim);

        _connectedPanel.Controls.AddRange(new Control[]
        {
            _logoConnectedPictureBox, _statusIndicator, _statusLabel,
            _discordIndicator, _discordStatusLabel, trackPanel,
            _logoutButton, minimizeButton, settingsButton, footerLabel
        });

        Controls.Add(_loginPanel);
        Controls.Add(_connectedPanel);
    }

    private void MainForm_Paint(object? sender, PaintEventArgs e)
    {
        // Draw CRT scanlines effect
        using var brush = new SolidBrush(Color.FromArgb(25, 0, 0, 0));
        for (int y = 0; y < Height; y += 3)
        {
            e.Graphics.FillRectangle(brush, 0, y, Width, 1);
        }
    }

    private PictureBox CreateLogoPictureBox(Point location, Size size)
    {
        var pictureBox = new PictureBox
        {
            Location = location,
            Size = size,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };

        // Load the embedded PNG logo
        pictureBox.Image = LoadEmbeddedLogo();

        return pictureBox;
    }

    private static Image? _cachedLogo;

    private static Image LoadEmbeddedLogo()
    {
        if (_cachedLogo != null) return _cachedLogo;

        var assembly = Assembly.GetExecutingAssembly();

        // Try primary (high-DPI) first, then fallback
        var resourceNames = new[]
        {
            "AuxbarClient.Resources.primary.png",
            "AuxbarClient.Resources.fallback.png"
        };

        foreach (var resourceName in resourceNames)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    _cachedLogo = Image.FromStream(stream);
                    return _cachedLogo;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load {resourceName}: {ex.Message}");
            }
        }

        // If all else fails, return a placeholder
        var placeholder = new Bitmap(260, 55);
        using var g = Graphics.FromImage(placeholder);
        g.Clear(Color.Transparent);
        return placeholder;
    }

    private static Icon? _cachedIcon;

    private static Icon LoadEmbeddedIcon()
    {
        if (_cachedIcon != null) return _cachedIcon;

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("AuxbarClient.Resources.app.ico");
            if (stream != null)
            {
                _cachedIcon = new Icon(stream);
                return _cachedIcon;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load embedded icon: {ex.Message}");
        }

        return SystemIcons.Application;
    }

    private Panel CreatePixelTextBoxPanel(Point location, Size size, out TextBox textBox)
    {
        var wrapper = new Panel
        {
            Location = location,
            Size = size,
            BackColor = PixelBgMid
        };
        wrapper.Paint += (s, e) =>
        {
            using var pen = new Pen(PixelBorder, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, wrapper.Width - 1, wrapper.Height - 1);
        };

        // Use smaller font to prevent clipping
        textBox = new TextBox
        {
            Location = new Point(8, 12),
            Size = new Size(size.Width - 16, size.Height - 16),
            BackColor = PixelBgMid,
            ForeColor = PixelText,
            BorderStyle = BorderStyle.None,
            Font = GetPixelFont(6)
        };

        wrapper.Controls.Add(textBox);
        return wrapper;
    }

    private Label CreatePixelLabel(string text, Point location, Font font, Color foreColor)
    {
        // Offset Y position up by 2px to compensate for pixel font top clipping
        var label = new Label
        {
            Text = text,
            Font = font,
            ForeColor = foreColor,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(location.X, location.Y - 2)
        };

        return label;
    }

    private Button CreatePixelButton(string text, Point location, Size size, Color bgColor)
    {
        var button = new Button
        {
            Text = text,
            Location = location,
            Size = size,
            BackColor = bgColor,
            ForeColor = bgColor == PixelAccent ? PixelBgDark : PixelText,
            FlatStyle = FlatStyle.Flat,
            Font = GetPixelFont(7),
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderSize = 3;
        button.FlatAppearance.BorderColor = bgColor == PixelAccent
            ? Color.FromArgb(46, 184, 138)  // Darker teal #2eb88a
            : PixelBorder;

        // Add shadow effect
        button.Paint += (s, e) =>
        {
            using var shadowPen = new Pen(PixelShadow, 2);
            e.Graphics.DrawLine(shadowPen, 4, button.Height - 1, button.Width - 1, button.Height - 1);
            e.Graphics.DrawLine(shadowPen, button.Width - 1, 4, button.Width - 1, button.Height - 1);
        };

        // Hover effect
        button.MouseEnter += (s, e) =>
        {
            button.BackColor = bgColor == PixelAccent ? PixelPrimary : PixelBgMid;
        };
        button.MouseLeave += (s, e) =>
        {
            button.BackColor = bgColor;
        };

        return button;
    }

    private void SetupTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadEmbeddedIcon(),
            Text = "Auxbar",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.BackColor = PixelBgMid;
        contextMenu.ForeColor = PixelText;
        contextMenu.Renderer = new PixelMenuRenderer();

        contextMenu.Items.Add("Open", null, (s, e) => { Show(); WindowState = FormWindowState.Normal; });
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exit", null, (s, e) => Application.Exit());

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => { Show(); WindowState = FormWindowState.Normal; };
    }

    private void SetupEvents()
    {
        // API Service events
        _apiService.TokenRefreshed += async () =>
        {
            Console.WriteLine("Token refreshed successfully");
            // Reconnect WebSocket with new token
            if (_webSocketService.IsConnected)
            {
                await _webSocketService.ReconnectAsync();
            }
        };

        _apiService.TokenRefreshFailed += () =>
        {
            Invoke(() =>
            {
                MessageBox.Show(
                    "Your session has expired. Please sign in again.",
                    "SESSION EXPIRED",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                LogoutButton_Click(null, EventArgs.Empty);
            });
        };

        // WebSocket events
        _webSocketService.Connected += () =>
        {
            Invoke(() =>
            {
                _statusLabel.Text = "SERVER: CONNECTED";
                _statusLabel.ForeColor = PixelAccent;
                _statusIndicator.BackColor = PixelAccent;
            });
        };

        _webSocketService.Disconnected += () =>
        {
            Invoke(() =>
            {
                _statusLabel.Text = "SERVER: RECONNECTING...";
                _statusLabel.ForeColor = PixelTextDim;
                _statusIndicator.BackColor = Color.FromArgb(255, 193, 7); // Yellow
            });
        };

        // Handle widget slug received from WebSocket connection
        _webSocketService.WidgetSlugReceived += (widgetSlug) =>
        {
            Console.WriteLine($"Setting widget slug for Discord RPC: {widgetSlug}");
            _discordRpcService.WidgetSlug = widgetSlug;
            ConfigService.UpdateWidgetSlug(widgetSlug);
        };

        // Discord RPC events
        _discordRpcService.Connected += () =>
        {
            Invoke(() =>
            {
                _discordStatusLabel.Text = "DISCORD: ON";
                _discordStatusLabel.ForeColor = PixelAccent;
                _discordIndicator.BackColor = PixelAccent;
            });
        };

        _discordRpcService.Disconnected += () =>
        {
            Invoke(() =>
            {
                _discordStatusLabel.Text = "DISCORD: OFF";
                _discordStatusLabel.ForeColor = PixelTextDim;
                _discordIndicator.BackColor = PixelTextDim;
            });
        };

        _discordRpcService.Error += (error) =>
        {
            Console.WriteLine($"Discord RPC Error: {error}");
            Invoke(() =>
            {
                _discordStatusLabel.Text = "DISCORD: ERROR";
                _discordStatusLabel.ForeColor = PixelPrimary;
                _discordIndicator.BackColor = PixelPrimary;
            });
        };

        _mediaSessionService.TrackChanged += (track) =>
        {
            if (track != null)
            {
                _webSocketService.SendTrackUpdate(track);

                // Update Discord Rich Presence
                if (_discordRpcService.IsEnabled)
                {
                    _discordRpcService.UpdatePresence(track);
                }

                Invoke(() =>
                {
                    var playState = track.Playing ? "▶" : "⏸";
                    _trackLabel.Text = $"{playState} NOW PLAYING\n\n{track.Title.ToUpper()}\n{track.Artist.ToUpper()}";
                    _notifyIcon.Text = $"Auxbar - {track.Title}";
                });
            }
            else
            {
                _webSocketService.SendIdle();

                // Set Discord to idle state
                if (_discordRpcService.IsEnabled)
                {
                    _discordRpcService.SetIdlePresence();
                }

                Invoke(() =>
                {
                    _trackLabel.Text = "NO MUSIC PLAYING";
                    _notifyIcon.Text = "Auxbar";
                });
            }
        };

        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        // Try to load saved tokens
        _apiService.LoadTokens();

        // Load widget slug for Discord RPC
        var config = ConfigService.Load();
        if (!string.IsNullOrEmpty(config.WidgetSlug))
        {
            _discordRpcService.WidgetSlug = config.WidgetSlug;
            Console.WriteLine($"Widget slug loaded for Discord RPC: {config.WidgetSlug}");
        }

        if (_apiService.IsAuthenticated)
        {
            // Verify token is still valid and start auto-refresh
            if (await _apiService.RefreshTokenAsync())
            {
                await ConnectAndStart();
            }
            else
            {
                // Token refresh failed, clear saved tokens
                _apiService.ClearTokens();
            }
        }
    }

    private async void LoginButton_Click(object? sender, EventArgs e)
    {
        await AttemptLogin(forceLogin: false);
    }

    private async Task AttemptLogin(bool forceLogin)
    {
        _loginButton.Enabled = false;
        _loginButton.Text = "SIGNING IN...";

        var result = await _apiService.LoginAsync(_emailBox.Text, _passwordBox.Text, forceLogin);

        if (result.Error == "ACTIVE_SESSION_EXISTS")
        {
            // Show confirmation dialog for session conflict
            var dialogResult = MessageBox.Show(
                "You are already logged in on another device or instance.\n\n" +
                "Do you want to log out from the other session and log in here instead?",
                "Active Session Detected",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (dialogResult == DialogResult.Yes)
            {
                // Retry with force login
                await AttemptLogin(forceLogin: true);
            }
            else
            {
                // User cancelled, reset button
                _loginButton.Enabled = true;
                _loginButton.Text = "SIGN IN";
            }
            return;
        }

        if (result.Error != null)
        {
            MessageBox.Show(result.Error, "LOGIN FAILED", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _loginButton.Enabled = true;
            _loginButton.Text = "SIGN IN";
            return;
        }

        // Save and set widget slug for Discord RPC album art
        if (result.User?.WidgetSlug != null)
        {
            ConfigService.UpdateWidgetSlug(result.User.WidgetSlug);
            _discordRpcService.WidgetSlug = result.User.WidgetSlug;
            Console.WriteLine($"Widget slug saved for Discord RPC: {result.User.WidgetSlug}");
        }

        await ConnectAndStart();
    }

    private async Task ConnectAndStart()
    {
        _loginPanel.Visible = false;
        _connectedPanel.Visible = true;

        await _mediaSessionService.InitializeAsync();
        await _webSocketService.ConnectAsync();

        // Initialize Discord RPC if enabled
        if (_discordRpcService.IsEnabled)
        {
            _discordRpcService.Initialize();

            // Sync current track to Discord after a short delay to ensure connection
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000); // Wait for Discord to connect
                SyncCurrentTrackToDiscord();
            });
        }
    }

    private void SyncCurrentTrackToDiscord()
    {
        if (!_discordRpcService.IsEnabled) return;

        var currentTrack = _mediaSessionService.CurrentTrack;
        if (currentTrack != null)
        {
            _discordRpcService.UpdatePresence(currentTrack);
        }
        else
        {
            _discordRpcService.SetIdlePresence();
        }
    }

    private void LogoutButton_Click(object? sender, EventArgs e)
    {
        _webSocketService.Disconnect();
        _discordRpcService.ClearPresence();
        _discordRpcService.WidgetSlug = null; // Clear widget slug for next login
        _apiService.ClearTokens();

        _connectedPanel.Visible = false;
        _loginPanel.Visible = true;
        _emailBox.Text = "";
        _passwordBox.Text = "";
        _loginButton.Enabled = true;
        _loginButton.Text = "SIGN IN";
    }

    private void SettingsButton_Click(object? sender, EventArgs e)
    {
        using var settingsForm = new SettingsForm(_discordRpcService, UpdateDiscordStatusDisplay);
        settingsForm.ShowDialog(this);
    }

    private void UpdateDiscordStatusDisplay()
    {
        // Update the Discord status indicator based on current state
        if (_discordRpcService.IsEnabled)
        {
            if (_discordRpcService.IsConnected)
            {
                _discordStatusLabel.Text = "DISCORD: ON";
                _discordStatusLabel.ForeColor = PixelAccent;
                _discordIndicator.BackColor = PixelAccent;
            }
            else
            {
                _discordStatusLabel.Text = "DISCORD: CONNECTING";
                _discordStatusLabel.ForeColor = PixelTextDim;
                _discordIndicator.BackColor = Color.FromArgb(255, 193, 7); // Yellow
            }
        }
        else
        {
            _discordStatusLabel.Text = "DISCORD: OFF";
            _discordStatusLabel.ForeColor = PixelTextDim;
            _discordIndicator.BackColor = PixelTextDim;
        }

        // Sync current track to Discord when settings change
        if (_discordRpcService.IsEnabled && _discordRpcService.IsConnected)
        {
            SyncCurrentTrackToDiscord();
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            _notifyIcon.Dispose();
            _discordRpcService.Dispose();
            _webSocketService.Dispose();
            _mediaSessionService.Dispose();
            _apiService.Dispose();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
        }
    }
}

// Custom renderer for pixel-style context menu
public class PixelMenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color BgColor = Color.FromArgb(45, 45, 68);
    private static readonly Color HighlightColor = Color.FromArgb(69, 230, 184);
    private static readonly Color TextColor = Color.FromArgb(240, 230, 255);
    private static readonly Color BorderColor = Color.FromArgb(92, 92, 138);

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var rc = new Rectangle(Point.Empty, e.Item.Size);
        var bgColor = e.Item.Selected ? HighlightColor : BgColor;
        using var brush = new SolidBrush(bgColor);
        e.Graphics.FillRectangle(brush, rc);
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(BgColor);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
        using var pen = new Pen(BorderColor, 2);
        e.Graphics.DrawRectangle(pen, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Selected ? Color.FromArgb(26, 26, 46) : TextColor;
        base.OnRenderItemText(e);
    }
}
