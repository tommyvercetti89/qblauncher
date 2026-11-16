using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using QBLauncherPro.Models;
using QBLauncherPro.Services;
using Path = System.IO.Path;

namespace QBLauncherPro;

public partial class MainWindow : Window
{
    private sealed record PresetEntry(string Name, string Subtitle, string Target, string Category, string Icon, bool RunAsAdmin);
    private sealed record ProfileDef(string Id, string Label);

    private static readonly ProfileDef[] Profiles =
    {
        new("all", "Tümü (Ana Panel)"),
        new("app", "Uygulamalar"),
        new("system", "PowerShell ve Sistem"),
        new("file", "Dosya ve Klasörler"),
        new("web", "Web Siteleri")
    };

    private readonly HotkeyService _hotkeyService = new();
    private AppConfig _config = new();
    private string _activeProfile = "all";
    private string _activeTab = "picker";
    private string _presetCat = "all";
    private string? _selectedItemId;
    private string? _editingModalId;
    private QuickLauncherWindow? _quickWindow;
    private bool _recordingHotkey;
    private bool _isMaximized;
    private Rect _restoreBounds;
    private Point _dragStartPoint;
    private int _draggingIndex = -1;
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(2.4) };

    public MainWindow()
    {
        InitializeComponent();
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            ToastBorder.Visibility = Visibility.Collapsed;
        };

        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _config = ConfigStorage.LoadOrCreate();
        if (_config.Items.Count > 0) _selectedItemId = _config.Items[0].Id;

        FitMainWindowToWorkArea();

        _hotkeyService.Attach(this, OnGlobalHotkeyTriggered);
        ApplyTheme(_config.Theme);

        if (_config.ListenerConsent)
        {
            _hotkeyService.UpdateRegistration(true, _config.HotkeyMod, _config.HotkeyKey);
        }

        StartupService.SetStartup(_config.RunOnStartup);
        RefreshUI();
    }

    public void WakeFromBackground()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    private void FitMainWindowToWorkArea()
    {
        var area = SystemParameters.WorkArea;
        MinWidth = 760;
        MinHeight = 500;
        Width = Math.Round(Math.Min(1240, Math.Max(760, area.Width - 40)));
        Height = Math.Round(Math.Min(690, Math.Max(500, area.Height - 36)));
        OuterMarginGrid.Margin = new Thickness(10);
        ShadowLayer.Visibility = Visibility.Visible;
        ShadowLayer.CornerRadius = new CornerRadius(18);
        RootShell.CornerRadius = new CornerRadius(18);
        RootShell.BorderThickness = new Thickness(1);
        CenterWindowOnScreen();
        _restoreBounds = new Rect(Left, Top, Width, Height);
        UpdateShellClip();
    }

    private void ApplyMaximizedBounds()
    {
        var area = SystemParameters.WorkArea;
        OuterMarginGrid.Margin = new Thickness(0);
        ShadowLayer.Visibility = Visibility.Collapsed;
        Left = area.Left;
        Top = area.Top;
        Width = area.Width;
        Height = area.Height;
        RootShell.CornerRadius = new CornerRadius(0);
        RootShell.BorderThickness = new Thickness(0);
        UpdateShellClip();
    }

    private void InnerClipGrid_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateShellClip();

    private void UpdateShellClip()
    {
        if (InnerClipGrid.ActualWidth <= 0 || InnerClipGrid.ActualHeight <= 0) return;
        if (_isMaximized)
        {
            InnerClipGrid.Clip = null;
            return;
        }
        InnerClipGrid.Clip = new RectangleGeometry(
            new Rect(0, 0, InnerClipGrid.ActualWidth, InnerClipGrid.ActualHeight),
            17.0,
            17.0);
    }

    private static Image MakeIconImage(string? iconKey, double size)
    {
        var img = new Image
        {
            Source = IconHelper.GetIconImageSource(iconKey),
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform
        };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        return img;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SwitchTab("picker");
        if (App.StartInBackground && _config.ListenerConsent)
        {
            HideToBackground();
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _quickWindow?.ForceClose();
        _hotkeyService.Dispose();
    }

    private void OnGlobalHotkeyTriggered()
    {
        Dispatcher.Invoke(() =>
        {
            if (_quickWindow != null && _quickWindow.IsVisible)
                _quickWindow.HideOverlay();
            else
                OpenQuickLauncher();
        });
    }

    private void ApplyTheme(string theme)
    {
        ThemeService.ApplyTheme(Resources, theme);
        _quickWindow?.ApplyCurrentTheme(theme);
        HighlightThemeCards();
    }

    private void HighlightThemeCards()
    {
        var blueFill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
        blueFill.Freeze();
        var blueStroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#60A5FA"));
        blueStroke.Freeze();

        (Button Card, Border Dot)[] entries =
        {
            (ThemeCardDark, ThemeDotDark),
            (ThemeCardLight, ThemeDotLight),
            (ThemeCardLavender, ThemeDotLavender),
            (ThemeCardMint, ThemeDotMint),
            (ThemeCardSky, ThemeDotSky),
            (ThemeCardAmber, ThemeDotAmber)
        };

        foreach (var (card, dot) in entries)
        {
            bool active = string.Equals(card.Tag as string, _config.Theme, StringComparison.OrdinalIgnoreCase);
            card.SetResourceReference(BorderBrushProperty, active ? "AccentYellow" : "BorderStrong");
            card.BorderThickness = new Thickness(active ? 2 : 1);
            card.SetResourceReference(BackgroundProperty, active ? "BgCard" : "BgPill");

            if (active)
            {
                dot.Background = blueFill;
                dot.BorderBrush = blueStroke;
                dot.BorderThickness = new Thickness(2);
            }
            else
            {
                dot.Background = Brushes.Transparent;
                dot.SetResourceReference(Border.BorderBrushProperty, "TextMuted");
                dot.BorderThickness = new Thickness(2);
            }
        }
    }

    private void HighlightLauncherStyleControls()
    {
        bool isSquare = !string.Equals(_config.LauncherStyle, "classic", StringComparison.OrdinalIgnoreCase);

        SegSquareModeBtn.SetResourceReference(BackgroundProperty, isSquare ? "AccentYellow" : "Transparent");
        SegSquareModeBtn.SetResourceReference(ForegroundProperty, isSquare ? "AccentYellowFg" : "TextSecondary");
        SegSquareModeBtn.FontWeight = isSquare ? FontWeights.Bold : FontWeights.SemiBold;

        SegClassicModeBtn.SetResourceReference(BackgroundProperty, isSquare ? "Transparent" : "AccentYellow");
        SegClassicModeBtn.SetResourceReference(ForegroundProperty, isSquare ? "TextSecondary" : "AccentYellowFg");
        SegClassicModeBtn.FontWeight = isSquare ? FontWeights.SemiBold : FontWeights.Bold;

        GenStyleSquareBtn.SetResourceReference(BackgroundProperty, isSquare ? "NavActiveBg" : "BgPill");
        GenStyleSquareBtn.SetResourceReference(BorderBrushProperty, isSquare ? "AccentYellow" : "BorderStrong");
        GenStyleSquareBtn.SetResourceReference(ForegroundProperty, isSquare ? "NavActiveFg" : "TextPrimary");

        GenStyleClassicBtn.SetResourceReference(BackgroundProperty, isSquare ? "BgPill" : "NavActiveBg");
        GenStyleClassicBtn.SetResourceReference(BorderBrushProperty, isSquare ? "BorderStrong" : "AccentYellow");
        GenStyleClassicBtn.SetResourceReference(ForegroundProperty, isSquare ? "TextPrimary" : "NavActiveFg");
    }

    private void RefreshUI()
    {
        SidebarHotkeyLabel.Text = _config.HotkeyLabel;
        HotkeyDisplayText.Text = _config.HotkeyLabel;
        ConfigPathText.Text = "./qblauncher-config.json (Uygulama Klasörü)";
        HomeItemCount.Text = _config.Items.Count.ToString();
        HomeHotkeyLabel.Text = _config.HotkeyLabel;
        PresetsTotalCountText.Text = _config.Items.Count.ToString();
        PresetsQuickSlotsText.Text = $"{Math.Min(9, _config.Items.Count)} / 9 Aktif";
        PresetsModeText.Text = string.Equals(_config.LauncherStyle, "classic", StringComparison.OrdinalIgnoreCase)
            ? "Klasik Mini"
            : "Kare Izgara";
        PresetsHotkeyText.Text = _config.HotkeyLabel;
        ConsentBannerDesc.Text = $"Uygulama siz kapatana kadar \"{_config.HotkeyLabel}\" tuşunu süresiz dinlesin ve Mini Seçme Ekranını açsın mı?";

        ChkListenerConsent.IsChecked = _config.ListenerConsent;
        ChkAutoHide.IsChecked = _config.AutoHideOnLaunch;
        ChkQuickNumbers.IsChecked = _config.QuickNumberKeys;
        ChkAnimations.IsChecked = _config.EnableAnimations;
        ChkRunOnStartup.IsChecked = _config.RunOnStartup;

        UpdateConsentBannerVisibility();

        if (_config.ListenerConsent)
        {
            if (_hotkeyService.IsRunning)
            {
                SidebarStatusDot.Fill = (Brush)Resources["SuccessColor"];
                HealthDot.Fill = (Brush)Resources["SuccessColor"];
                SidebarStatusTitle.Text = "Dinleyici Aktif";
                TopHotkeyPill.Text = $"{_config.HotkeyLabel} • Dinleniyor";
                HealthText.Text = $"Aktif: \"{_config.HotkeyLabel}\" kısayolu arka planda süresiz dinleniyor.";
                HomeListenerState.Text = "Aktif";
            }
            else
            {
                SidebarStatusDot.Fill = (Brush)Resources["DangerColor"];
                HealthDot.Fill = (Brush)Resources["DangerColor"];
                SidebarStatusTitle.Text = "Kısayol Hatası";
                TopHotkeyPill.Text = $"{_config.HotkeyLabel} • Çakışma";
                HealthText.Text = string.IsNullOrEmpty(_hotkeyService.LastError) ? "Kısayol kaydedilemedi." : _hotkeyService.LastError;
                HomeListenerState.Text = "Çakışma";
            }
        }
        else
        {
            SidebarStatusDot.Fill = (Brush)Resources["WarningColor"];
            HealthDot.Fill = (Brush)Resources["WarningColor"];
            SidebarStatusTitle.Text = "Onay Bekleniyor";
            TopHotkeyPill.Text = $"{_config.HotkeyLabel} • Onay Bekliyor";
            HealthText.Text = "Dinleyici kapalı. Başlatmak için onay vermeniz gerekir.";
            HomeListenerState.Text = "Beklemede";
        }

        HighlightThemeCards();
        HighlightLauncherStyleControls();
        RenderProfiles();
        RenderStagePreview();
        RenderInlineEditor();
        RenderPresetsPool();
        RenderFullItemsList();
    }

    private void UpdateConsentBannerVisibility()
    {
        bool show = !_config.ListenerConsent && (_activeTab == "home" || _activeTab == "hotkeys");
        ConsentBanner.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderProfiles()
    {
        ProfileListStack.Children.Clear();
        var q = ProfileSearchBox.Text.Trim().ToLowerInvariant();
        foreach (var p in Profiles)
        {
            if (!string.IsNullOrEmpty(q) && !p.Label.ToLowerInvariant().Contains(q)) continue;
            int count = p.Id == "all" ? _config.Items.Count : _config.Items.Count(x => x.Category == p.Id);
            bool active = _activeProfile == p.Id;

            var btn = new Button
            {
                Style = (Style)FindResource("NavBtn"),
                Height = 44,
                Padding = new Thickness(13, 0, 13, 0),
                Margin = new Thickness(0, 0, 0, 4),
                FontSize = 15
            };
            RenderOptions.SetClearTypeHint(btn, ClearTypeHint.Enabled);
            if (active)
            {
                btn.SetResourceReference(BackgroundProperty, "NavActiveBg");
                btn.SetResourceReference(ForegroundProperty, "NavActiveFg");
                btn.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                btn.SetResourceReference(ForegroundProperty, "TextPrimary");
            }

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text = p.Label,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(lbl, 0);
            rowGrid.Children.Add(lbl);

            var badge = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(9, 2, 9, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.SetResourceReference(Border.BackgroundProperty, "BadgeBg");
            var badgeTxt = new TextBlock
            {
                Text = count.ToString(),
                FontFamily = (FontFamily)FindResource("MonoFont"),
                FontSize = 13
            };
            badgeTxt.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            badge.Child = badgeTxt;
            Grid.SetColumn(badge, 1);
            rowGrid.Children.Add(badge);

            btn.Content = rowGrid;
            btn.Click += (_, _) =>
            {
                _activeProfile = p.Id;
                RenderProfiles();
                RenderStagePreview();
                RenderInlineEditor();
            };
            ProfileListStack.Children.Add(btn);
        }
    }

    private List<LauncherItem> GetProfileItems() =>
        _activeProfile == "all" ? _config.Items : _config.Items.Where(x => x.Category == _activeProfile).ToList();

    private void RenderStagePreview()
    {
        StagePreviewGrid.Children.Clear();
        var items = GetProfileItems();
        bool isSquare = !string.Equals(_config.LauncherStyle, "classic", StringComparison.OrdinalIgnoreCase);

        StageCaptionText.Text = isSquare
            ? $"{items.Count} öğe • Buğulu cam üzerinde kare ızgara — düzenlemek için kutuya tıklayın"
            : $"{items.Count} öğe • Klasik mini kart penceresi — düzenlemek için kutuya tıklayın";

        if (items.Count == 0)
        {
            StagePreviewGrid.Columns = 1;
            var empty = new TextBlock
            {
                Text = "Bu profilde öğe yok. Sağdan tek tıkla ekleyin.",
                FontSize = 15,
                Margin = new Thickness(0, 24, 0, 24),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            StagePreviewGrid.Children.Add(empty);
            return;
        }

        if (!items.Any(x => x.Id == _selectedItemId)) _selectedItemId = items[0].Id;

        if (isSquare)
        {
            StagePreviewGrid.Columns = items.Count <= 4 && items.Count > 1 ? 2 : 3;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                bool selected = item.Id == _selectedItemId;

                var sq = new Border
                {
                    Height = 112,
                    Margin = new Thickness(6),
                    Padding = new Thickness(10),
                    CornerRadius = new CornerRadius(16),
                    BorderThickness = new Thickness(selected ? 2 : 1.25),
                    Cursor = Cursors.Hand
                };
                RenderOptions.SetClearTypeHint(sq, ClearTypeHint.Enabled);
                sq.SetResourceReference(Border.BackgroundProperty, selected ? "NavActiveBg" : "BgCard");
                sq.SetResourceReference(Border.BorderBrushProperty, selected ? "AccentYellow" : "BorderSubtle");

                var g = new Grid();
                if (i < 9)
                {
                    var b = new Border
                    {
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(5, 1, 5, 1)
                    };
                    b.SetResourceReference(Border.BackgroundProperty, selected ? "AccentYellow" : "BadgeBg");
                    var n = new TextBlock
                    {
                        Text = (i + 1).ToString(),
                        FontFamily = (FontFamily)FindResource("MonoFont"),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold
                    };
                    n.SetResourceReference(TextBlock.ForegroundProperty, selected ? "AccentYellowFg" : "TextMuted");
                    b.Child = n;
                    g.Children.Add(b);
                }

                var sp = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var iconBox = new Border
                {
                    Width = 42,
                    Height = 42,
                    CornerRadius = new CornerRadius(11),
                    Padding = new Thickness(5),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 7)
                };
                iconBox.SetResourceReference(Border.BackgroundProperty, "BgPill");
                iconBox.Child = MakeIconImage(item.Icon, 30);
                sp.Children.Add(iconBox);

                var nameTxt = new TextBlock
                {
                    Text = item.Name,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                nameTxt.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
                sp.Children.Add(nameTxt);

                var subTxt = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(item.Subtitle) ? IconHelper.CategoryLabel(item.Category) : item.Subtitle,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                subTxt.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
                sp.Children.Add(subTxt);

                g.Children.Add(sp);
                sq.Child = g;

                sq.MouseLeftButtonDown += (_, e) =>
                {
                    _selectedItemId = item.Id;
                    RenderStagePreview();
                    RenderInlineEditor();
                    if (e.ClickCount == 2) LaunchItem(item);
                };
                StagePreviewGrid.Children.Add(sq);
            }
            return;
        }

        StagePreviewGrid.Columns = 2;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            bool selected = item.Id == _selectedItemId;

            var tile = new Button
            {
                Style = (Style)FindResource("PillBtn"),
                Margin = new Thickness(5),
                Padding = new Thickness(12),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            RenderOptions.SetClearTypeHint(tile, ClearTypeHint.Enabled);
            if (selected)
            {
                tile.SetResourceReference(BackgroundProperty, "NavActiveBg");
                tile.SetResourceReference(BorderBrushProperty, "AccentYellow");
            }
            else
            {
                tile.SetResourceReference(BackgroundProperty, "BgCard");
                tile.SetResourceReference(BorderBrushProperty, "BorderSubtle");
            }

            var grid = new Grid();
            var dock = new DockPanel();

            var iconBox = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Padding = new Thickness(5), Margin = new Thickness(0, 0, 11, 0) };
            iconBox.SetResourceReference(Border.BackgroundProperty, "BgPill");
            iconBox.Child = MakeIconImage(item.Icon, 28);
            DockPanel.SetDock(iconBox, Dock.Left);
            dock.Children.Add(iconBox);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var t1 = new TextBlock { Text = item.Name, FontSize = 15, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
            t1.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
            var t2 = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.Subtitle) ? IconHelper.CategoryLabel(item.Category) : item.Subtitle,
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            t2.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            stack.Children.Add(t1);
            stack.Children.Add(t2);
            dock.Children.Add(stack);

            grid.Children.Add(dock);
            if (i < 9)
            {
                var num = new TextBlock
                {
                    Text = (i + 1).ToString(),
                    FontFamily = (FontFamily)FindResource("MonoFont"),
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -3, -2, 0)
                };
                num.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
                grid.Children.Add(num);
            }

            tile.Content = grid;
            tile.Click += (_, _) =>
            {
                _selectedItemId = item.Id;
                RenderStagePreview();
                RenderInlineEditor();
            };
            tile.MouseDoubleClick += (_, _) => LaunchItem(item);
            StagePreviewGrid.Children.Add(tile);
        }
    }

    private void RenderInlineEditor()
    {
        var item = _config.Items.FirstOrDefault(x => x.Id == _selectedItemId);
        if (item == null)
        {
            InlineEditorCard.Visibility = Visibility.Collapsed;
            return;
        }
        InlineEditorCard.Visibility = Visibility.Visible;
        InlineEditorTitle.Text = item.Name;
        EdName.Text = item.Name;
        EdSubtitle.Text = item.Subtitle;
        EdTarget.Text = item.Target;
    }

    private static List<PresetEntry> GetPresetCatalog()
    {
        return new List<PresetEntry>
        {
            new("Antigravity", "AI Kodlama Ortamı", "antigravity", "app", "antigravity", false),
            new("PowerShell", "Windows Kabuğu", "powershell.exe", "system", "powershell", false),
            new("PowerShell (Admin)", "Yönetici Kabuğu", "powershell.exe", "system", "powershell", true),
            new("Komut İstemi", "CMD Terminal", "cmd.exe", "system", "terminal", false),
            new("Görev Yöneticisi", "Performans", "taskmgr.exe", "system", "cpu", false),
            new("Denetim Masası", "Sistem Ayarları", "control.exe", "system", "app", false),
            new("Hesap Makinesi", "Araç", "calc.exe", "system", "calculator", false),
            new("Ekran Alıntısı", "Snipping Tool", "snippingtool.exe", "app", "app", false),
            new("Kayıt Defteri", "Regedit", "regedit.exe", "system", "bolt", false),
            new("Masaüstü", "Hızlı Klasör", @"%USERPROFILE%\Desktop", "file", "folder", false),
            new("İndirilenler", "Hızlı Klasör", @"%USERPROFILE%\Downloads", "file", "folder", false),
            new("GitHub", "github.com", "https://github.com", "web", "globe", false),
            new("Google", "google.com", "https://google.com", "web", "globe", false)
        };
    }

    private void RenderPresetsPool()
    {
        PresetsPoolPanel.Children.Clear();
        var q = PresetSearchBox.Text.Trim().ToLowerInvariant();
        var catalog = GetPresetCatalog().Where(p =>
            (_presetCat == "all" || p.Category == _presetCat) &&
            (string.IsNullOrEmpty(q) || p.Name.ToLowerInvariant().Contains(q) || p.Subtitle.ToLowerInvariant().Contains(q)));

        foreach (var preset in catalog)
        {
            var btn = new Button
            {
                Style = (Style)FindResource("PillBtn"),
                Padding = new Thickness(10, 6, 10, 6),
                Height = 50,
                Margin = new Thickness(0, 0, 0, 7),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            btn.SetResourceReference(BackgroundProperty, "BgStageInner");
            btn.SetResourceReference(BorderBrushProperty, "BorderSubtle");
            RenderOptions.SetClearTypeHint(btn, ClearTypeHint.Enabled);

            var row = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBox = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(4),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBox.SetResourceReference(Border.BackgroundProperty, "BgPill");
            iconBox.Child = MakeIconImage(preset.Icon, 24);
            Grid.SetColumn(iconBox, 0);
            row.Children.Add(iconBox);

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var t1 = new TextBlock { Text = preset.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
            t1.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
            var t2 = new TextBlock { Text = preset.Subtitle, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
            t2.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            textStack.Children.Add(t1);
            textStack.Children.Add(t2);
            Grid.SetColumn(textStack, 1);
            row.Children.Add(textStack);

            var addBadge = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            addBadge.SetResourceReference(Border.BackgroundProperty, "BgPill");
            var addTxt = new TextBlock { Text = "+ Ekle", FontSize = 12, FontWeight = FontWeights.SemiBold };
            addTxt.SetResourceReference(TextBlock.ForegroundProperty, "AccentYellow");
            addBadge.Child = addTxt;
            Grid.SetColumn(addBadge, 2);
            row.Children.Add(addBadge);

            btn.Content = row;

            btn.Click += (_, _) =>
            {
                var newItem = new LauncherItem
                {
                    Id = $"item-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Name = preset.Name,
                    Subtitle = preset.Subtitle,
                    Target = preset.Target,
                    Category = preset.Category,
                    Icon = preset.Icon,
                    RunAsAdmin = preset.RunAsAdmin
                };
                _config.Items.Add(newItem);
                _selectedItemId = newItem.Id;
                ConfigStorage.Save(_config);
                RefreshUI();
                ShowToast($"{preset.Name} eklendi");
            };
            PresetsPoolPanel.Children.Add(btn);
        }
    }

    private void RenderFullItemsList()
    {
        FullItemsStack.Children.Clear();
        for (int i = 0; i < _config.Items.Count; i++)
        {
            var item = _config.Items[i];
            int currentIdx = i;
            var card = new Border
            {
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            RenderOptions.SetClearTypeHint(card, ClearTypeHint.Enabled);
            card.SetResourceReference(Border.BackgroundProperty, "BgCard");
            card.SetResourceReference(Border.BorderBrushProperty, "BorderSubtle");

            var dock = new DockPanel();
            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(btns, Dock.Right);

            var bUp = new Button
            {
                Style = (Style)FindResource("PillBtn"),
                Padding = new Thickness(9, 5, 9, 5),
                FontSize = 13,
                Content = "↑",
                Margin = new Thickness(0, 0, 5, 0),
                IsEnabled = currentIdx > 0,
                Opacity = currentIdx > 0 ? 1.0 : 0.4
            };
            bUp.Click += (_, _) => ReorderItem(currentIdx, -1);

            var bDown = new Button
            {
                Style = (Style)FindResource("PillBtn"),
                Padding = new Thickness(9, 5, 9, 5),
                FontSize = 13,
                Content = "↓",
                Margin = new Thickness(0, 0, 8, 0),
                IsEnabled = currentIdx < _config.Items.Count - 1,
                Opacity = currentIdx < _config.Items.Count - 1 ? 1.0 : 0.4
            };
            bDown.Click += (_, _) => ReorderItem(currentIdx, 1);

            var bOpen = new Button { Style = (Style)FindResource("PillBtn"), Padding = new Thickness(12, 5, 12, 5), FontSize = 13, Content = "Aç", Margin = new Thickness(0, 0, 6, 0) };
            bOpen.Click += (_, _) => LaunchItem(item);
            var bEdit = new Button { Style = (Style)FindResource("PillBtn"), Padding = new Thickness(12, 5, 12, 5), FontSize = 13, Content = "Düzenle", Margin = new Thickness(0, 0, 6, 0) };
            bEdit.Click += (_, _) => OpenItemModal(item);
            var bDel = new Button { Style = (Style)FindResource("PillBtn"), Padding = new Thickness(12, 5, 12, 5), FontSize = 13, Content = "Sil", Foreground = (Brush)Resources["DangerColor"] };
            bDel.Click += (_, _) =>
            {
                _config.Items.RemoveAll(x => x.Id == item.Id);
                ConfigStorage.Save(_config);
                RefreshUI();
                ShowToast("Öğe silindi");
            };
            btns.Children.Add(bUp);
            btns.Children.Add(bDown);
            btns.Children.Add(bOpen);
            btns.Children.Add(bEdit);
            btns.Children.Add(bDel);
            dock.Children.Add(btns);

            var iconBox = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(10), Padding = new Thickness(5), Margin = new Thickness(0, 0, 14, 0) };
            iconBox.SetResourceReference(Border.BackgroundProperty, "BgPill");
            iconBox.Child = MakeIconImage(item.Icon, 30);
            DockPanel.SetDock(iconBox, Dock.Left);
            dock.Children.Add(iconBox);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var t1 = new TextBlock { Text = $"{i + 1}. {item.Name}", FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            t1.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
            titleRow.Children.Add(t1);

            if (i < 9)
            {
                var kbdBadge = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(7, 2, 7, 2),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                kbdBadge.SetResourceReference(Border.BackgroundProperty, "NavActiveBg");
                var kbdTxt = new TextBlock { Text = $"Kısayol #{i + 1}", FontSize = 11, FontWeight = FontWeights.SemiBold };
                kbdTxt.SetResourceReference(TextBlock.ForegroundProperty, "NavActiveFg");
                kbdBadge.Child = kbdTxt;
                titleRow.Children.Add(kbdBadge);
            }

            var catBadge = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            catBadge.SetResourceReference(Border.BackgroundProperty, "BgPill");
            var catTxt = new TextBlock { Text = IconHelper.CategoryLabel(item.Category), FontSize = 11 };
            catTxt.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            catBadge.Child = catTxt;
            titleRow.Children.Add(catBadge);

            info.Children.Add(titleRow);
            var t2 = new TextBlock { Text = item.Target, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 0, 0) };
            t2.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            info.Children.Add(t2);
            dock.Children.Add(info);

            card.Child = dock;

            var dragGrip = new TextBlock
            {
                Text = "⋮⋮",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Cursor = Cursors.SizeAll,
                ToolTip = "Sırayı değiştirmek için sürükleyin"
            };
            dragGrip.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
            DockPanel.SetDock(dragGrip, Dock.Left);
            dock.Children.Insert(1, dragGrip);

            card.AllowDrop = true;
            card.Cursor = Cursors.Hand;
            card.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (FindAncestorButton(e.OriginalSource as DependencyObject) != null)
                {
                    _draggingIndex = -1;
                    return;
                }
                _dragStartPoint = e.GetPosition(null);
                _draggingIndex = currentIdx;
            };
            card.PreviewMouseMove += (_, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && _draggingIndex == currentIdx)
                {
                    var diff = _dragStartPoint - e.GetPosition(null);
                    if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        var sourceIndex = _draggingIndex;
                        _draggingIndex = -1;
                        DragDrop.DoDragDrop(card, sourceIndex, DragDropEffects.Move);
                    }
                }
            };
            card.DragEnter += (_, e) =>
            {
                if (e.Data.GetDataPresent(typeof(int)) && (int)e.Data.GetData(typeof(int)) != currentIdx)
                {
                    e.Effects = DragDropEffects.Move;
                    card.SetResourceReference(Border.BorderBrushProperty, "AccentYellow");
                    card.BorderThickness = new Thickness(2);
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
                e.Handled = true;
            };
            card.DragOver += (_, e) =>
            {
                e.Effects = e.Data.GetDataPresent(typeof(int)) && (int)e.Data.GetData(typeof(int)) != currentIdx
                    ? DragDropEffects.Move
                    : DragDropEffects.None;
                e.Handled = true;
            };
            card.DragLeave += (_, _) =>
            {
                card.SetResourceReference(Border.BorderBrushProperty, "BorderSubtle");
                card.BorderThickness = new Thickness(1);
            };
            card.Drop += (_, e) =>
            {
                card.SetResourceReference(Border.BorderBrushProperty, "BorderSubtle");
                card.BorderThickness = new Thickness(1);
                if (e.Data.GetDataPresent(typeof(int)))
                {
                    int srcIdx = (int)e.Data.GetData(typeof(int));
                    if (srcIdx != currentIdx && srcIdx >= 0 && srcIdx < _config.Items.Count)
                    {
                        var moved = _config.Items[srcIdx];
                        _config.Items.RemoveAt(srcIdx);
                        _config.Items.Insert(currentIdx, moved);
                        _selectedItemId = moved.Id;
                        ConfigStorage.Save(_config);
                        RefreshUI();
                        ShowToast($"{moved.Name} {currentIdx + 1}. sıraya taşındı");
                    }
                }
                e.Handled = true;
            };

            FullItemsStack.Children.Add(card);
        }
    }

    private static Button? FindAncestorButton(DependencyObject? d)
    {
        while (d != null)
        {
            if (d is Button btn) return btn;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    private void ReorderItem(int idx, int delta)
    {
        int target = idx + delta;
        if (idx < 0 || target < 0 || target >= _config.Items.Count) return;
        (_config.Items[idx], _config.Items[target]) = (_config.Items[target], _config.Items[idx]);
        _selectedItemId = _config.Items[target].Id;
        ConfigStorage.Save(_config);
        RefreshUI();
    }

    private void QuickPresetAdd_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string rawTag) return;
        var parts = rawTag.Split('|');
        if (parts.Length < 6) return;

        var newItem = new LauncherItem
        {
            Id = $"item-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Name = parts[0],
            Subtitle = parts[1],
            Target = parts[2],
            Category = parts[3],
            Icon = parts[4],
            RunAsAdmin = bool.TryParse(parts[5], out var admin) && admin
        };

        _config.Items.Add(newItem);
        _selectedItemId = newItem.Id;
        ConfigStorage.Save(_config);
        RefreshUI();
        ShowToast($"{newItem.Name} eklendi");
    }

    private void OpenQuickLauncher()
    {
        _quickWindow ??= new QuickLauncherWindow(
            onEditRequested: WakeFromBackground,
            onLaunchItem: LaunchItem);

        _quickWindow.ShowLauncher(_config, _hotkeyService.IsRunning, this);
    }

    private void HideToBackground() => Hide();

    private void CenterWindowOnScreen()
    {
        var area = SystemParameters.WorkArea;
        Left = Math.Round(area.Left + (area.Width - Width) / 2);
        Top = Math.Round(area.Top + (area.Height - Height) / 2);
    }

    private void LaunchItem(LauncherItem item, bool? overrideRunAsAdmin = null)
    {
        try
        {
            LauncherService.Launch(item, overrideRunAsAdmin);
        }
        catch (Exception ex)
        {
            ShowToast("Başlatılamadı: " + ex.Message);
        }
    }

    private void SwitchTab(string tab)
    {
        _activeTab = tab;
        UpdateConsentBannerVisibility();

        Button[] btns = { NavHome, NavGeneral, NavHotkeys, NavPicker, NavPresets, NavTheme, NavAbout };
        foreach (var b in btns)
        {
            bool active = (string)b.Tag == tab;
            if (active)
            {
                b.SetResourceReference(BackgroundProperty, "NavActiveBg");
                b.SetResourceReference(ForegroundProperty, "NavActiveFg");
                b.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                b.Background = Brushes.Transparent;
                b.SetResourceReference(ForegroundProperty, "TextSecondary");
                b.FontWeight = FontWeights.Medium;
            }
        }

        UIElement activePanel = tab switch
        {
            "home" => TabHome,
            "general" => TabGeneral,
            "hotkeys" => TabHotkeys,
            "presets" => TabPresets,
            "theme" => TabTheme,
            "about" => TabAbout,
            _ => TabPicker
        };

        TabHome.Visibility = tab == "home" ? Visibility.Visible : Visibility.Collapsed;
        TabGeneral.Visibility = tab == "general" ? Visibility.Visible : Visibility.Collapsed;
        TabHotkeys.Visibility = tab == "hotkeys" ? Visibility.Visible : Visibility.Collapsed;
        TabPicker.Visibility = tab == "picker" ? Visibility.Visible : Visibility.Collapsed;
        TabPresets.Visibility = tab == "presets" ? Visibility.Visible : Visibility.Collapsed;
        TabTheme.Visibility = tab == "theme" ? Visibility.Visible : Visibility.Collapsed;
        TabAbout.Visibility = tab == "about" ? Visibility.Visible : Visibility.Collapsed;

        if (_config.EnableAnimations && IsLoaded)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var tt = new TranslateTransform(0, 8);
            activePanel.RenderTransform = tt;
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
            activePanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0.25, 1.0, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
        }
    }

    private void ShowToast(string msg)
    {
        ToastText.Text = msg;
        ToastBorder.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    // Event Handlers
    private void DragWindow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && !_isMaximized) DragMove();
    }

    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2)
        {
            ToggleMaximize_Click(sender, e);
        }
        else if (!_isMaximized)
        {
            DragMove();
        }
    }

    private void ToggleMaximize_Click(object sender, RoutedEventArgs e)
    {
        _isMaximized = !_isMaximized;
        if (_isMaximized)
        {
            _restoreBounds = new Rect(Left, Top, Width, Height);
            ApplyMaximizedBounds();
        }
        else
        {
            OuterMarginGrid.Margin = new Thickness(10);
            ShadowLayer.Visibility = Visibility.Visible;
            ShadowLayer.CornerRadius = new CornerRadius(18);
            RootShell.CornerRadius = new CornerRadius(18);
            RootShell.BorderThickness = new Thickness(1);
            if (_restoreBounds.Width >= 760 && _restoreBounds.Height >= 500)
            {
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;
            }
            else
            {
                FitMainWindowToWorkArea();
            }
            UpdateShellClip();
        }
    }

    private void NavTab_Click(object sender, RoutedEventArgs e) => SwitchTab((string)((Button)sender).Tag);
    private void GoToPicker_Click(object sender, RoutedEventArgs e) => SwitchTab("picker");
    private void SwitchToMini_Click(object sender, RoutedEventArgs e) => OpenQuickLauncher();
    private void HideToBackground_Click(object sender, RoutedEventArgs e) => HideToBackground();
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Quit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void SelectLauncherStyle_Click(object sender, RoutedEventArgs e)
    {
        var style = (string)((Button)sender).Tag == "classic" ? "classic" : "square";
        if (_config.LauncherStyle == style) return;
        _config.LauncherStyle = style;
        ConfigStorage.Save(_config);
        RefreshUI();
        ShowToast(style == "square"
            ? "Görünüm: Kare Izgara (Buğulu Cam)"
            : "Görünüm: Klasik Mini Pencere");
    }

    private void QuickTheme_Click(object sender, RoutedEventArgs e)
    {
        string[] order = { "dark", "light", "lavender", "mint", "sky", "amber" };
        int idx = Array.FindIndex(order, x => string.Equals(x, _config.Theme, StringComparison.OrdinalIgnoreCase));
        _config.Theme = order[(idx + 1) % order.Length];
        ApplyTheme(_config.Theme);
        ConfigStorage.Save(_config);
        RefreshUI();
        ShowToast($"Tema: {ThemeService.GetTheme(_config.Theme).Name}");
    }

    private void SelectTheme_Click(object sender, RoutedEventArgs e)
    {
        _config.Theme = (string)((Button)sender).Tag;
        ApplyTheme(_config.Theme);
        ConfigStorage.Save(_config);
        RefreshUI();
        ShowToast($"Tema: {ThemeService.GetTheme(_config.Theme).Name}");
    }

    private void ApproveBanner_Click(object sender, RoutedEventArgs e)
    {
        _config.ListenerConsent = true;
        ConfigStorage.Save(_config);
        _hotkeyService.UpdateRegistration(true, _config.HotkeyMod, _config.HotkeyKey);
        RefreshUI();
        ShowToast($"Dinleyici başlatıldı ({_config.HotkeyLabel})");
    }

    private void ListenerConsent_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        bool consent = ChkListenerConsent.IsChecked == true;
        if (_config.ListenerConsent == consent) return;
        _config.ListenerConsent = consent;
        ConfigStorage.Save(_config);
        _hotkeyService.UpdateRegistration(consent, _config.HotkeyMod, _config.HotkeyKey);
        RefreshUI();
        ShowToast(consent ? $"Dinleyici başlatıldı ({_config.HotkeyLabel})" : "Dinleyici duraklatıldı");
    }

    private void BehaviorToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        bool startupChanged = _config.RunOnStartup != (ChkRunOnStartup.IsChecked == true);
        _config.AutoHideOnLaunch = ChkAutoHide.IsChecked == true;
        _config.QuickNumberKeys = ChkQuickNumbers.IsChecked == true;
        _config.EnableAnimations = ChkAnimations.IsChecked == true;
        _config.RunOnStartup = ChkRunOnStartup.IsChecked == true;
        ConfigStorage.Save(_config);

        if (startupChanged)
        {
            StartupService.SetStartup(_config.RunOnStartup);
            ShowToast(_config.RunOnStartup ? "Otomatik başlatma etkinleştirildi" : "Otomatik başlatma kapatıldı");
        }
    }

    private void ProfileSearch_TextChanged(object sender, TextChangedEventArgs e) => RenderProfiles();
    private void PresetSearch_TextChanged(object sender, TextChangedEventArgs e) => RenderPresetsPool();

    private void PresetChip_Click(object sender, RoutedEventArgs e)
    {
        _presetCat = (string)((Button)sender).Tag;
        foreach (var child in PresetChipsPanel.Children.OfType<Button>())
        {
            child.Style = (Style)FindResource((string)child.Tag == _presetCat ? "YellowPillBtn" : "PillBtn");
            child.Padding = new Thickness(7, 4, 7, 4);
            child.FontSize = 12;
        }
        RenderPresetsPool();
    }

    private void InlineSave_Click(object sender, RoutedEventArgs e)
    {
        var item = _config.Items.FirstOrDefault(x => x.Id == _selectedItemId);
        if (item == null) return;
        var name = EdName.Text.Trim();
        var target = EdTarget.Text.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(target))
        {
            ShowToast("Ad ve hedef yol boş olamaz");
            return;
        }
        item.Name = name;
        item.Subtitle = EdSubtitle.Text.Trim();
        item.Target = target;
        ConfigStorage.Save(_config);
        RefreshUI();
        ShowToast("Değişiklikler kaydedildi");
    }

    private void InlinePickFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Uygulama veya Dosya Seçin" };
        if (dlg.ShowDialog() == true) EdTarget.Text = dlg.FileName;
    }

    private void InlinePickFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Klasör Seçin" };
        if (dlg.ShowDialog() == true) EdTarget.Text = dlg.FolderName;
    }

    private void InlineTest_Click(object sender, RoutedEventArgs e)
    {
        var item = _config.Items.FirstOrDefault(x => x.Id == _selectedItemId);
        if (item != null) LaunchItem(item);
    }

    private void InlineUp_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void InlineDown_Click(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int delta)
    {
        if (_selectedItemId == null) return;
        int idx = _config.Items.FindIndex(x => x.Id == _selectedItemId);
        int target = idx + delta;
        if (idx < 0 || target < 0 || target >= _config.Items.Count) return;
        (_config.Items[idx], _config.Items[target]) = (_config.Items[target], _config.Items[idx]);
        ConfigStorage.Save(_config);
        RefreshUI();
    }

    private void InlineDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItemId == null) return;
        _config.Items.RemoveAll(x => x.Id == _selectedItemId);
        _selectedItemId = _config.Items.FirstOrDefault()?.Id;
        ConfigStorage.Save(_config);
        RefreshUI();
        ShowToast("Öğe kaldırıldı");
    }

    private void QuickAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Hızlı Erişim Klasörü Seçin" };
        if (dlg.ShowDialog() != true) return;
        var name = Path.GetFileName(dlg.FolderName);
        if (string.IsNullOrEmpty(name)) name = dlg.FolderName;
        var item = new LauncherItem
        {
            Id = $"item-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Name = name,
            Subtitle = "Hızlı Klasör",
            Target = dlg.FolderName,
            Category = "file",
            Icon = "folder"
        };
        _config.Items.Add(item);
        _selectedItemId = item.Id;
        ConfigStorage.Save(_config);
        RefreshUI();
        ShowToast($"{name} klasörü eklendi");
    }

    private void QuickAddFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Uygulama veya Dosya Seçin" };
        if (dlg.ShowDialog() != true) return;
        var name = Path.GetFileNameWithoutExtension(dlg.FileName);
        var item = new LauncherItem
        {
            Id = $"item-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Name = name,
            Subtitle = "Uygulama / Dosya",
            Target = dlg.FileName,
            Category = "app",
            Icon = "app"
        };
        _config.Items.Add(item);
        _selectedItemId = item.Id;
        ConfigStorage.Save(_config);
        RefreshUI();
        ShowToast($"{name} eklendi");
    }

    private void PresetHotkey_Click(object sender, RoutedEventArgs e)
    {
        var parts = ((string)((Button)sender).Tag).Split('|');
        UpdateHotkey(parts[0], parts[1], parts[2]);
    }

    private void RecordHotkey_Click(object sender, RoutedEventArgs e)
    {
        _recordingHotkey = !_recordingHotkey;
        HotkeyDisplayText.Text = _recordingHotkey ? "Tuşlara basın..." : _config.HotkeyLabel;
        BtnRecordHotkey.Content = _recordingHotkey ? "İptal" : "Yeni Kısayol Kaydet";
        HotkeyDisplayBorder.BorderBrush = (Brush)Resources[_recordingHotkey ? "AccentYellow" : "BorderStrong"];
    }

    private void UpdateHotkey(string mod, string key, string label)
    {
        _config.HotkeyMod = mod;
        _config.HotkeyKey = key;
        _config.HotkeyLabel = label;
        ConfigStorage.Save(_config);
        _hotkeyService.UpdateRegistration(_config.ListenerConsent, mod, key);
        RefreshUI();
        ShowToast(string.IsNullOrEmpty(_hotkeyService.LastError) ? $"Kısayol güncellendi: {label}" : _hotkeyService.LastError);
    }

    private void OpenAddModal_Click(object sender, RoutedEventArgs e) => OpenItemModal(null);

    private void OpenItemModal(LauncherItem? item)
    {
        _editingModalId = item?.Id;
        ModalTitleText.Text = item == null ? "Yeni Öğe Ekle" : "Öğeyi Düzenle";
        InpName.Text = item?.Name ?? string.Empty;
        InpSubtitle.Text = item?.Subtitle ?? string.Empty;
        InpTarget.Text = item?.Target ?? string.Empty;
        InpArgs.Text = item?.Args ?? string.Empty;
        InpAdmin.IsChecked = item?.RunAsAdmin ?? false;
        SelectComboByTag(InpCategory, item?.Category ?? "app");
        SelectComboByTag(InpIcon, item?.Icon ?? "app");
        ItemModalOverlay.Visibility = Visibility.Visible;
    }

    private static void SelectComboByTag(ComboBox box, string tag)
    {
        foreach (ComboBoxItem ci in box.Items)
        {
            if ((string)ci.Tag == tag)
            {
                box.SelectedItem = ci;
                return;
            }
        }
        box.SelectedIndex = 0;
    }

    private void CloseModal_Click(object sender, RoutedEventArgs e)
    {
        ItemModalOverlay.Visibility = Visibility.Collapsed;
        _editingModalId = null;
    }

    private void PickFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Uygulama veya Dosya Seçin" };
        if (dlg.ShowDialog() == true)
        {
            InpTarget.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(InpName.Text)) InpName.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
        }
    }

    private void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Klasör Seçin" };
        if (dlg.ShowDialog() == true)
        {
            InpTarget.Text = dlg.FolderName;
            SelectComboByTag(InpCategory, "file");
            SelectComboByTag(InpIcon, "folder");
            if (string.IsNullOrWhiteSpace(InpName.Text)) InpName.Text = Path.GetFileName(dlg.FolderName);
        }
    }

    private void SaveModal_Click(object sender, RoutedEventArgs e)
    {
        var name = InpName.Text.Trim();
        var target = InpTarget.Text.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(target))
        {
            ShowToast("Ad ve hedef yol zorunludur");
            return;
        }

        var item = new LauncherItem
        {
            Id = _editingModalId ?? $"item-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Name = name,
            Subtitle = InpSubtitle.Text.Trim(),
            Target = target,
            Args = InpArgs.Text.Trim(),
            Category = (InpCategory.SelectedItem as ComboBoxItem)?.Tag as string ?? "app",
            Icon = (InpIcon.SelectedItem as ComboBoxItem)?.Tag as string ?? "app",
            RunAsAdmin = InpAdmin.IsChecked == true
        };

        if (_editingModalId != null)
        {
            int idx = _config.Items.FindIndex(x => x.Id == _editingModalId);
            if (idx >= 0) _config.Items[idx] = item;
        }
        else
        {
            _config.Items.Add(item);
        }

        _selectedItemId = item.Id;
        ConfigStorage.Save(_config);
        ItemModalOverlay.Visibility = Visibility.Collapsed;
        _editingModalId = null;
        RefreshUI();
        ShowToast("Öğe kaydedildi");
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_recordingHotkey)
        {
            e.Handled = true;
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            if (actualKey == Key.Escape)
            {
                _recordingHotkey = false;
                HotkeyDisplayText.Text = _config.HotkeyLabel;
                BtnRecordHotkey.Content = "Yeni Kısayol Kaydet";
                HotkeyDisplayBorder.BorderBrush = (Brush)Resources["BorderStrong"];
                return;
            }
            if (actualKey is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;

            var mods = new List<string>();
            var prettyMods = new List<string>();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { mods.Add("CTRL"); prettyMods.Add("Ctrl"); }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) { mods.Add("ALT"); prettyMods.Add("Alt"); }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { mods.Add("SHIFT"); prettyMods.Add("Shift"); }
            if (mods.Count == 0)
            {
                ShowToast("Alt, Ctrl veya Shift ile birlikte basın (iptal: Esc).");
                return;
            }

            string keyToken = actualKey switch
            {
                Key.Space => "SPACE",
                >= Key.D0 and <= Key.D9 => ((char)('0' + (actualKey - Key.D0))).ToString(),
                >= Key.NumPad0 and <= Key.NumPad9 => $"NUMPAD{actualKey - Key.NumPad0}",
                _ => actualKey.ToString().ToUpperInvariant()
            };
            string prettyKey = actualKey switch
            {
                Key.Space => "Space",
                >= Key.D0 and <= Key.D9 => ((char)('0' + (actualKey - Key.D0))).ToString(),
                >= Key.NumPad0 and <= Key.NumPad9 => $"Num{actualKey - Key.NumPad0}",
                Key.Prior => "PageUp",
                Key.Next => "PageDown",
                _ => actualKey.ToString()
            };
            string label = $"{string.Join(" + ", prettyMods)} + {prettyKey}";
            _recordingHotkey = false;
            BtnRecordHotkey.Content = "Yeni Kısayol Kaydet";
            HotkeyDisplayBorder.BorderBrush = (Brush)Resources["BorderStrong"];
            UpdateHotkey(string.Join("+", mods), keyToken, label);
            return;
        }

        if (e.Key == Key.Escape && ItemModalOverlay.Visibility == Visibility.Visible)
        {
            e.Handled = true;
            ItemModalOverlay.Visibility = Visibility.Collapsed;
            _editingModalId = null;
        }
    }
}
