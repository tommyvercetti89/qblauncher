using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using QBLauncherPro.Models;
using QBLauncherPro.Services;

namespace QBLauncherPro;

public partial class QuickLauncherWindow : Window
{
    private readonly Action _onEditRequested;
    private readonly Action<LauncherItem, bool?> _onLaunchItem;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private AppConfig _config = new();
    private bool _listenerRunning;
    private List<LauncherItem> _filteredItems = new();
    private int _selectedIndex;
    private int _activeColumns = 3;
    private bool _allowClose;
    private bool _isClosing;
    private string _currentMode = "square";

    private LauncherItem? _flyoutItem;
    private FrameworkElement? _flyoutAnchorTile;
    private int _flyoutItemIndex = -1;
    private int _flyoutAnimToken;
    private readonly DispatcherTimer _flyoutDismissTimer = new() { Interval = TimeSpan.FromMilliseconds(160) };

    public QuickLauncherWindow(Action onEditRequested, Action<LauncherItem, bool?> onLaunchItem)
    {
        InitializeComponent();
        _onEditRequested = onEditRequested;
        _onLaunchItem = onLaunchItem;

        _flyoutDismissTimer.Tick += (_, _) =>
        {
            _flyoutDismissTimer.Stop();
            if (!DualFlyoutRoot.IsMouseOver && _flyoutAnchorTile?.IsMouseOver != true)
            {
                HideDualFlyout(animated: true);
            }
        };

        InitializeFlyoutHandlers();
    }

    private void InitializeFlyoutHandlers()
    {
        FlyoutNormalBox.MouseEnter += (_, _) =>
        {
            FlyoutNormalBox.Background = (Brush)FindResource("NavActiveBg");
            FlyoutNormalBox.BorderBrush = (Brush)FindResource("AccentYellow");
            FlyoutNormalBox.BorderThickness = new Thickness(2);
        };
        FlyoutNormalBox.MouseLeave += (_, _) =>
        {
            FlyoutNormalBox.Background = (Brush)FindResource("GlassTileBg");
            FlyoutNormalBox.BorderBrush = (Brush)FindResource("BorderStrong");
            FlyoutNormalBox.BorderThickness = new Thickness(1.5);
        };
        FlyoutNormalBox.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            if (_flyoutItem == null) return;
            var targetItem = _flyoutItem;
            var targetTile = _flyoutAnchorTile;
            if (_flyoutItemIndex >= 0) _selectedIndex = _flyoutItemIndex;
            HideDualFlyout(animated: false);
            TriggerLaunch(targetItem, false, targetTile);
        };

        FlyoutAdminBox.MouseEnter += (_, _) =>
        {
            FlyoutAdminBox.Background = (Brush)FindResource("NavActiveBg");
            FlyoutAdminBox.BorderBrush = (Brush)FindResource("AccentYellow");
            FlyoutAdminBox.BorderThickness = new Thickness(2);
        };
        FlyoutAdminBox.MouseLeave += (_, _) =>
        {
            FlyoutAdminBox.Background = (Brush)FindResource("GlassTileHoverBg");
            FlyoutAdminBox.BorderBrush = (Brush)FindResource("AccentYellow");
            FlyoutAdminBox.BorderThickness = new Thickness(1.5);
        };
        FlyoutAdminBox.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            if (_flyoutItem == null) return;
            var targetItem = _flyoutItem;
            var targetTile = _flyoutAnchorTile;
            if (_flyoutItemIndex >= 0) _selectedIndex = _flyoutItemIndex;
            HideDualFlyout(animated: false);
            TriggerLaunch(targetItem, true, targetTile);
        };
    }

    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    public void ApplyCurrentTheme(string? themeId)
    {
        ThemeService.ApplyTheme(Resources, themeId);
        if (IsVisible)
        {
            RenderItems();
        }
    }

    public void HideOverlay()
    {
        AnimateAndHide();
    }

    private void HideOverlayImmediate()
    {
        HideDualFlyout(animated: false);
        if (IsVisible)
        {
            Hide();
        }
        BackdropShotImage.Source = null;
    }

    public void ShowLauncher(AppConfig config, bool listenerRunning, Window? ownerMain)
    {
        _config = config;
        _listenerRunning = listenerRunning;
        _currentMode = string.Equals(config.LauncherStyle, "classic", StringComparison.OrdinalIgnoreCase)
            ? "classic"
            : "square";
        _selectedIndex = 0;
        _isClosing = false;

        HideDualFlyout(animated: false);
        SquareInfoPopup.Visibility = Visibility.Collapsed;
        ResetAnimationStates();
        ThemeService.ApplyTheme(Resources, config.Theme);

        // Capture screen before showing overlay so this window is never in the frosted screenshot
        if (IsVisible)
        {
            Hide();
        }

        var snapshot = ScreenCaptureService.CaptureActiveMonitor(ownerMain);

        if (_currentMode == "square")
        {
            ConfigureSquareMode(snapshot);
        }
        else
        {
            ConfigureClassicMode(snapshot);
        }

        UpdateListenerIndicators();
        RenderItems();

        Show();
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            SetForegroundWindow(hwnd);
        }
        Activate();
        Focus();

        if (_currentMode == "classic")
        {
            ClassicSearchBox.Focus();
            Keyboard.Focus(ClassicSearchBox);
            if (config.EnableAnimations)
            {
                PlayClassicEntranceAnimation();
            }
        }
        else
        {
            Keyboard.Focus(this);
            if (config.EnableAnimations)
            {
                PlaySquareEntranceAnimation();
            }
        }
    }

    private void ResetAnimationStates()
    {
        RootViewport.BeginAnimation(OpacityProperty, null);
        RootViewport.Opacity = 1.0;

        SquareCenterContainer.BeginAnimation(OpacityProperty, null);
        SquareCenterContainer.Opacity = 1.0;
        SquareEntranceScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        SquareEntranceScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        SquareEntranceScale.ScaleX = 1.0;
        SquareEntranceScale.ScaleY = 1.0;

        ClassicModeRoot.BeginAnimation(OpacityProperty, null);
        ClassicModeRoot.Opacity = 1.0;
        ClassicEntranceScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ClassicEntranceScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        ClassicEntranceScale.ScaleX = 1.0;
        ClassicEntranceScale.ScaleY = 1.0;
    }

    private void ConfigureSquareMode(MonitorSnapshot snapshot)
    {
        ClassicModeRoot.Visibility = Visibility.Collapsed;
        SquareModeRoot.Visibility = Visibility.Visible;

        Left = snapshot.DipBounds.Left;
        Top = snapshot.DipBounds.Top;
        Width = snapshot.DipBounds.Width;
        Height = snapshot.DipBounds.Height;

        BackdropShotImage.Source = snapshot.Screenshot;
        SquareScrollViewer.MaxHeight = Math.Round(Math.Clamp(snapshot.DipBounds.Height * 0.68, 300, 540));
    }

    private void ConfigureClassicMode(MonitorSnapshot snapshot)
    {
        SquareModeRoot.Visibility = Visibility.Collapsed;
        BackdropShotImage.Source = null;
        ClassicModeRoot.Visibility = Visibility.Visible;

        const double winW = 652;
        const double winH = 452;
        Width = winW;
        Height = winH;
        Left = Math.Round(snapshot.DipBounds.Left + (snapshot.DipBounds.Width - winW) / 2.0);
        Top = Math.Round(snapshot.DipBounds.Top + (snapshot.DipBounds.Height - winH) / 2.0);

        ClassicSearchBox.Text = string.Empty;
        ClassicSearchHint.Visibility = Visibility.Visible;
    }

    private void PlaySquareEntranceAnimation()
    {
        SquareEntranceScale.ScaleX = 0.94;
        SquareEntranceScale.ScaleY = 0.94;
        SquareCenterContainer.Opacity = 0;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };
        var sx = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(175)) { EasingFunction = ease };
        var sy = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(175)) { EasingFunction = ease };

        SquareCenterContainer.BeginAnimation(OpacityProperty, fade);
        SquareEntranceScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        SquareEntranceScale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
    }

    private void PlayClassicEntranceAnimation()
    {
        ClassicEntranceScale.ScaleX = 0.94;
        ClassicEntranceScale.ScaleY = 0.94;
        RootViewport.Opacity = 0;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease };
        var sx = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(165)) { EasingFunction = ease };
        var sy = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(165)) { EasingFunction = ease };

        RootViewport.BeginAnimation(OpacityProperty, fade);
        ClassicEntranceScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        ClassicEntranceScale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
    }

    private void AnimateAndHide(Action? onCompleted = null, FrameworkElement? launchedVisual = null)
    {
        if (!IsVisible)
        {
            onCompleted?.Invoke();
            return;
        }

        if (_isClosing) return;

        if (!_config.EnableAnimations)
        {
            HideOverlayImmediate();
            onCompleted?.Invoke();
            return;
        }

        _isClosing = true;
        IsHitTestVisible = false;
        HideDualFlyout(animated: false);

        if (launchedVisual?.RenderTransform is ScaleTransform tileScale)
        {
            var pulseEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            tileScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1.09, TimeSpan.FromMilliseconds(140)) { EasingFunction = pulseEase });
            tileScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1.09, TimeSpan.FromMilliseconds(140)) { EasingFunction = pulseEase });
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var exitScale = _currentMode == "square" ? SquareEntranceScale : ClassicEntranceScale;
        var sxOut = new DoubleAnimation(1.0, 0.93, TimeSpan.FromMilliseconds(175)) { EasingFunction = ease };
        var syOut = new DoubleAnimation(1.0, 0.93, TimeSpan.FromMilliseconds(175)) { EasingFunction = ease };
        var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(175)) { EasingFunction = ease };

        fadeOut.Completed += (_, _) =>
        {
            IsHitTestVisible = true;
            _isClosing = false;
            ResetAnimationStates();
            HideOverlayImmediate();
            onCompleted?.Invoke();
        };

        exitScale.BeginAnimation(ScaleTransform.ScaleXProperty, sxOut);
        exitScale.BeginAnimation(ScaleTransform.ScaleYProperty, syOut);
        RootViewport.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void UpdateListenerIndicators()
    {
        string text = _listenerRunning ? $"{_config.HotkeyLabel} • Dinleniyor" : "Dinleyici Kapalı";
        string brushKey = _listenerRunning ? "SuccessColor" : "WarningColor";
        var brush = (Brush)FindResource(brushKey);

        SquareListenerText.Text = text;
        SquareListenerText.Foreground = brush;
        ClassicStatusText.Text = text;
        ClassicStatusText.Foreground = brush;
    }

    private void RenderItems()
    {
        string query = _currentMode == "classic"
            ? (ClassicSearchBox.Text ?? string.Empty).Trim().ToLowerInvariant()
            : string.Empty;

        _filteredItems = _config.Items
            .Where(item => string.IsNullOrEmpty(query)
                           || item.Name.ToLowerInvariant().Contains(query)
                           || item.Subtitle.ToLowerInvariant().Contains(query)
                           || item.Target.ToLowerInvariant().Contains(query))
            .ToList();

        if (_filteredItems.Count == 0)
        {
            _selectedIndex = -1;
        }
        else if (_selectedIndex < 0 || _selectedIndex >= _filteredItems.Count)
        {
            _selectedIndex = 0;
        }

        if (_currentMode == "square")
        {
            RenderSquareGrid();
        }
        else
        {
            RenderClassicGrid();
        }
    }

    private void RenderSquareGrid()
    {
        SquareTilesGrid.Children.Clear();

        if (_filteredItems.Count == 0)
        {
            _activeColumns = 1;
            SquareTilesGrid.Columns = 1;
            SquareTilesGrid.Children.Add(BuildEmptySquareCard());
            return;
        }

        int cols = _filteredItems.Count switch
        {
            1 => 1,
            2 => 2,
            4 => 2,
            > 9 when Width >= 920 => 4,
            _ => 3
        };
        _activeColumns = cols;
        SquareTilesGrid.Columns = cols;

        for (int i = 0; i < _filteredItems.Count; i++)
        {
            SquareTilesGrid.Children.Add(BuildSquareTile(_filteredItems[i], i));
        }
    }

    private UIElement BuildEmptySquareCard()
    {
        var bd = new Border
        {
            Width = 280,
            Height = 160,
            CornerRadius = new CornerRadius(20),
            Background = (Brush)FindResource("GlassTileBg"),
            BorderBrush = (Brush)FindResource("BorderStrong"),
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(20)
        };
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new TextBlock
        {
            Text = "Henüz kısayol eklenmedi",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextPrimary"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        });
        sp.Children.Add(new TextBlock
        {
            Text = "Aşağıdan Düzenle (F1) ile uygulama ekleyebilirsiniz.",
            FontSize = 12,
            Foreground = (Brush)FindResource("TextMuted"),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });
        bd.Child = sp;
        return bd;
    }

    private Border BuildDualModeButton(string label, bool isAdmin, Action onClick)
    {
        var btn = new Border
        {
            Height = 28,
            CornerRadius = new CornerRadius(8),
            Background = (Brush)FindResource(isAdmin ? "NavActiveBg" : "BgPill"),
            BorderBrush = (Brush)FindResource(isAdmin ? "AccentYellow" : "BorderStrong"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 0, 8, 0),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, isAdmin ? 5 : 0, 0, 0)
        };

        var txt = new TextBlock
        {
            Text = label,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource(isAdmin ? "NavActiveFg" : "TextPrimary"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        btn.Child = txt;

        btn.MouseEnter += (_, _) =>
        {
            btn.Background = (Brush)FindResource(isAdmin ? "AccentYellow" : "BgPillHover");
            btn.BorderBrush = (Brush)FindResource("AccentYellow");
            txt.Foreground = (Brush)FindResource(isAdmin ? "AccentYellowFg" : "TextPrimary");
        };
        btn.MouseLeave += (_, _) =>
        {
            btn.Background = (Brush)FindResource(isAdmin ? "NavActiveBg" : "BgPill");
            btn.BorderBrush = (Brush)FindResource(isAdmin ? "AccentYellow" : "BorderStrong");
            txt.Foreground = (Brush)FindResource(isAdmin ? "NavActiveFg" : "TextPrimary");
        };
        btn.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };

        return btn;
    }

    private UIElement BuildSquareTile(LauncherItem item, int idx)
    {
        bool isSelected = idx == _selectedIndex;
        bool hasDualMode = LauncherService.SupportsAdminOption(item);

        var tile = new Border
        {
            Width = 148,
            Height = 148,
            Margin = new Thickness(9),
            CornerRadius = new CornerRadius(20),
            Background = (Brush)FindResource(isSelected ? "GlassTileHoverBg" : "GlassTileBg"),
            BorderBrush = (Brush)FindResource(isSelected ? "AccentYellow" : "BorderStrong"),
            BorderThickness = new Thickness(isSelected ? 2 : 1.25),
            Padding = new Thickness(11),
            Cursor = Cursors.Hand,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(isSelected ? 1.03 : 1.0, isSelected ? 1.03 : 1.0),
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = isSelected ? 20 : 14,
                ShadowDepth = 4,
                Opacity = isSelected ? 0.34 : 0.22,
                RenderingBias = RenderingBias.Performance
            }
        };

        var grid = new Grid();

        // Top-right 1-9 number badge
        if (_config.QuickNumberKeys && idx < 9)
        {
            var badge = new Border
            {
                MinWidth = 24,
                Height = 22,
                Padding = new Thickness(6, 0, 6, 0),
                CornerRadius = new CornerRadius(7),
                Background = (Brush)FindResource(isSelected ? "AccentYellow" : "BadgeBg"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = (idx + 1).ToString(),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource(isSelected ? "AccentYellowFg" : "TextSecondary"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            grid.Children.Add(badge);
        }

        // Center 3D Icon + Bottom Title/Subtitle
        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var iconContainer = new Border
        {
            Width = 58,
            Height = 58,
            CornerRadius = new CornerRadius(14),
            Background = (Brush)FindResource("BgPill"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 4),
            Child = new Image
            {
                Source = IconHelper.GetIconImageSource(item.Icon),
                Width = 42,
                Height = 42,
                Stretch = Stretch.Uniform
            }
        };
        Grid.SetRow(iconContainer, 0);
        content.Children.Add(iconContainer);

        var textStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(2, 0, 2, 0)
        };
        textStack.Children.Add(new TextBlock
        {
            Text = item.Name,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextPrimary"),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        textStack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(item.Subtitle) ? IconHelper.CategoryLabel(item.Category) : item.Subtitle,
            FontSize = 11,
            Foreground = (Brush)FindResource("TextMuted"),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 1, 0, 0)
        });
        Grid.SetRow(textStack, 1);
        content.Children.Add(textStack);
        grid.Children.Add(content);

        tile.Child = grid;

        tile.MouseEnter += (_, _) =>
        {
            tile.Background = (Brush)FindResource("GlassTileHoverBg");
            tile.BorderBrush = (Brush)FindResource("AccentYellow");
            tile.BorderThickness = new Thickness(2);
            tile.RenderTransform = new ScaleTransform(1.04, 1.04);

            if (hasDualMode)
            {
                _flyoutDismissTimer.Stop();
                ShowDualFlyoutForTile(tile, item, idx);
            }
            else
            {
                HideDualFlyout(animated: true);
            }
        };
        tile.MouseLeave += (_, _) =>
        {
            if (_isClosing) return;
            bool sel = idx == _selectedIndex;
            tile.Background = (Brush)FindResource(sel ? "GlassTileHoverBg" : "GlassTileBg");
            tile.BorderBrush = (Brush)FindResource(sel ? "AccentYellow" : "BorderStrong");
            tile.BorderThickness = new Thickness(sel ? 2 : 1.25);
            tile.RenderTransform = new ScaleTransform(sel ? 1.03 : 1.0, sel ? 1.03 : 1.0);

            if (hasDualMode && _flyoutAnchorTile == tile)
            {
                _flyoutDismissTimer.Stop();
                _flyoutDismissTimer.Start();
            }
        };
        tile.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            _selectedIndex = idx;
            TriggerLaunch(item, null, tile);
        };

        return tile;
    }

    private void ShowDualFlyoutForTile(FrameworkElement tile, LauncherItem item, int idx)
    {
        if (_isClosing || _currentMode != "square" || tile.ActualWidth <= 0) return;

        if (DualFlyoutRoot.Visibility == Visibility.Visible &&
            _flyoutAnchorTile == tile &&
            ReferenceEquals(_flyoutItem, item))
        {
            return;
        }

        _flyoutAnimToken++;
        _flyoutItem = item;
        _flyoutAnchorTile = tile;
        _flyoutItemIndex = idx;

        var anchor = tile.TranslatePoint(new Point(tile.ActualWidth, tile.ActualHeight / 2.0), SquareModeRoot);
        double left = Math.Round(anchor.X - 3);
        double top = Math.Round(anchor.Y - DualFlyoutRoot.Height / 2.0);
        if (left + DualFlyoutRoot.Width > SquareModeRoot.ActualWidth - 16)
        {
            left = Math.Max(16, SquareModeRoot.ActualWidth - DualFlyoutRoot.Width - 16);
        }
        top = Math.Clamp(top, 16, Math.Max(16, SquareModeRoot.ActualHeight - DualFlyoutRoot.Height - 16));

        Canvas.SetLeft(DualFlyoutRoot, left);
        Canvas.SetTop(DualFlyoutRoot, top);

        // Clear any pending exit animation clocks before animating in
        DualFlyoutRoot.BeginAnimation(OpacityProperty, null);
        DualFlyoutRoot.Opacity = 1.0;
        DualFlyoutRoot.Visibility = Visibility.Visible;

        FlyoutNormalBox.Background = (Brush)FindResource("GlassTileBg");
        FlyoutNormalBox.BorderBrush = (Brush)FindResource("BorderStrong");
        FlyoutNormalBox.BorderThickness = new Thickness(1.5);
        FlyoutAdminBox.Background = (Brush)FindResource("GlassTileHoverBg");
        FlyoutAdminBox.BorderBrush = (Brush)FindResource("AccentYellow");
        FlyoutAdminBox.BorderThickness = new Thickness(1.5);

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        FlyoutArrowsScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        FlyoutArrowsScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        FlyoutArrowsScale.ScaleX = 0.05;
        FlyoutArrowsScale.ScaleY = 0.35;
        FlyoutArrowsScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.05, 1.0, TimeSpan.FromMilliseconds(350)) { EasingFunction = ease });
        FlyoutArrowsScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.35, 1.0, TimeSpan.FromMilliseconds(370)) { EasingFunction = ease });

        FlyoutNormalBox.BeginAnimation(OpacityProperty, null);
        FlyoutNormalScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        FlyoutNormalScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        FlyoutNormalTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        FlyoutNormalTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        FlyoutNormalBox.Opacity = 0;
        FlyoutNormalScale.ScaleX = 0.85;
        FlyoutNormalScale.ScaleY = 0.85;
        FlyoutNormalTranslate.X = -22;
        FlyoutNormalTranslate.Y = 14;
        FlyoutNormalBox.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(360)) { EasingFunction = ease });
        FlyoutNormalScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease });
        FlyoutNormalScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease });
        FlyoutNormalTranslate.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(-22, 0, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease });
        FlyoutNormalTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease });

        var stagger = TimeSpan.FromMilliseconds(55);
        FlyoutAdminBox.BeginAnimation(OpacityProperty, null);
        FlyoutAdminScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        FlyoutAdminScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        FlyoutAdminTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        FlyoutAdminTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        FlyoutAdminBox.Opacity = 0;
        FlyoutAdminScale.ScaleX = 0.85;
        FlyoutAdminScale.ScaleY = 0.85;
        FlyoutAdminTranslate.X = -22;
        FlyoutAdminTranslate.Y = -14;
        FlyoutAdminBox.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(380)) { BeginTime = stagger, EasingFunction = ease });
        FlyoutAdminScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(430)) { BeginTime = stagger, EasingFunction = ease });
        FlyoutAdminScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(430)) { BeginTime = stagger, EasingFunction = ease });
        FlyoutAdminTranslate.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(-22, 0, TimeSpan.FromMilliseconds(430)) { BeginTime = stagger, EasingFunction = ease });
        FlyoutAdminTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-14, 0, TimeSpan.FromMilliseconds(430)) { BeginTime = stagger, EasingFunction = ease });
    }

    private void HideDualFlyout(bool animated)
    {
        _flyoutDismissTimer.Stop();
        _flyoutAnimToken++;
        int token = _flyoutAnimToken;

        _flyoutItem = null;
        _flyoutAnchorTile = null;
        _flyoutItemIndex = -1;

        if (DualFlyoutRoot.Visibility != Visibility.Visible)
        {
            return;
        }

        if (!animated)
        {
            DualFlyoutRoot.BeginAnimation(OpacityProperty, null);
            DualFlyoutRoot.Opacity = 1.0;
            DualFlyoutRoot.Visibility = Visibility.Collapsed;
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fadeOut = new DoubleAnimation(DualFlyoutRoot.Opacity, 0.0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease };
        fadeOut.Completed += (_, _) =>
        {
            if (token != _flyoutAnimToken) return;
            DualFlyoutRoot.BeginAnimation(OpacityProperty, null);
            DualFlyoutRoot.Opacity = 1.0;
            DualFlyoutRoot.Visibility = Visibility.Collapsed;
        };

        FlyoutArrowsScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.5, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
        DualFlyoutRoot.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void DualFlyoutRoot_MouseEnter(object sender, MouseEventArgs e)
    {
        _flyoutDismissTimer.Stop();
    }

    private void DualFlyoutRoot_MouseLeave(object sender, MouseEventArgs e)
    {
        _flyoutDismissTimer.Stop();
        _flyoutDismissTimer.Start();
    }

    private void SquareScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (Math.Abs(e.VerticalChange) > 0.1)
        {
            HideDualFlyout(animated: false);
        }
    }

    private void RenderClassicGrid()
    {
        ClassicCardsGrid.Children.Clear();
        _activeColumns = 2;

        if (_filteredItems.Count == 0)
        {
            ClassicCardsGrid.Columns = 1;
            ClassicCardsGrid.Children.Add(new TextBlock
            {
                Text = "Eşleşen uygulama bulunamadı.",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 14,
                Margin = new Thickness(12, 28, 12, 12),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            return;
        }

        ClassicCardsGrid.Columns = 2;
        for (int i = 0; i < _filteredItems.Count; i++)
        {
            ClassicCardsGrid.Children.Add(BuildClassicCard(_filteredItems[i], i));
        }
    }

    private UIElement BuildClassicCard(LauncherItem item, int idx)
    {
        bool isSelected = idx == _selectedIndex;
        bool hasDualMode = LauncherService.SupportsAdminOption(item);

        var card = new Border
        {
            Margin = new Thickness(5),
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(12),
            Background = (Brush)FindResource(isSelected ? "NavActiveBg" : "BgCard"),
            BorderBrush = (Brush)FindResource(isSelected ? "AccentYellow" : "BorderSubtle"),
            BorderThickness = new Thickness(1.25),
            Cursor = Cursors.Hand,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBox = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(10),
            Background = (Brush)FindResource("BgPill"),
            Margin = new Thickness(0, 0, 11, 0),
            Child = new Image
            {
                Source = IconHelper.GetIconImageSource(item.Icon),
                Width = 24,
                Height = 24,
                Stretch = Stretch.Uniform
            }
        };
        Grid.SetColumn(iconBox, 0);
        grid.Children.Add(iconBox);

        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new TextBlock
        {
            Text = item.Name,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextPrimary"),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        sp.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(item.Subtitle) ? IconHelper.CategoryLabel(item.Category) : item.Subtitle,
            FontSize = 12,
            Foreground = (Brush)FindResource("TextMuted"),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(sp, 1);
        grid.Children.Add(sp);

        Border? badge = null;
        if (_config.QuickNumberKeys && idx < 9)
        {
            badge = new Border
            {
                MinWidth = 24,
                Height = 22,
                Padding = new Thickness(6, 0, 6, 0),
                CornerRadius = new CornerRadius(7),
                Background = (Brush)FindResource(isSelected ? "AccentYellow" : "BadgeBg"),
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = (idx + 1).ToString(),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource(isSelected ? "AccentYellowFg" : "TextSecondary"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);
        }

        StackPanel? dualRow = null;
        if (hasDualMode)
        {
            dualRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            var normalBtn = BuildDualModeButton("Normal", false, () =>
            {
                _selectedIndex = idx;
                TriggerLaunch(item, false, card);
            });
            normalBtn.Margin = new Thickness(0, 0, 4, 0);
            var adminBtn = BuildDualModeButton("Yönetici", true, () =>
            {
                _selectedIndex = idx;
                TriggerLaunch(item, true, card);
            });
            adminBtn.Margin = new Thickness(0);
            dualRow.Children.Add(normalBtn);
            dualRow.Children.Add(adminBtn);
            Grid.SetColumn(dualRow, 2);
            grid.Children.Add(dualRow);
        }

        card.Child = grid;

        card.MouseEnter += (_, _) =>
        {
            card.Background = (Brush)FindResource("BgPillHover");
            card.BorderBrush = (Brush)FindResource("AccentYellow");
            if (dualRow != null)
            {
                if (badge != null) badge.Visibility = Visibility.Collapsed;
                dualRow.Visibility = Visibility.Visible;
            }
        };
        card.MouseLeave += (_, _) =>
        {
            if (_isClosing) return;
            bool sel = idx == _selectedIndex;
            card.Background = (Brush)FindResource(sel ? "NavActiveBg" : "BgCard");
            card.BorderBrush = (Brush)FindResource(sel ? "AccentYellow" : "BorderSubtle");
            if (dualRow != null)
            {
                dualRow.Visibility = Visibility.Collapsed;
                if (badge != null) badge.Visibility = Visibility.Visible;
            }
        };
        card.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            _selectedIndex = idx;
            TriggerLaunch(item, null, card);
        };

        return card;
    }

    private void TriggerLaunch(LauncherItem item, bool? forceAdmin = null, FrameworkElement? tileVisual = null)
    {
        if (_config.AutoHideOnLaunch)
        {
            AnimateAndHide(() => _onLaunchItem(item, forceAdmin), tileVisual);
        }
        else
        {
            _onLaunchItem(item, forceAdmin);
        }
    }

    private void MoveSelection(int delta)
    {
        if (_filteredItems.Count == 0) return;
        _selectedIndex = Math.Clamp(_selectedIndex + delta, 0, _filteredItems.Count - 1);
        RenderItems();

        var grid = _currentMode == "square" ? SquareTilesGrid : ClassicCardsGrid;
        if (_selectedIndex >= 0 && _selectedIndex < grid.Children.Count && grid.Children[_selectedIndex] is FrameworkElement fe)
        {
            fe.BringIntoView();
        }
    }

    private void GlassBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        AnimateAndHide();
    }

    private void ClassicHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private void CloseOverlay_Click(object sender, RoutedEventArgs e)
    {
        AnimateAndHide();
    }

    private void OpenEditor_Click(object sender, RoutedEventArgs e)
    {
        AnimateAndHide(() => _onEditRequested());
    }

    private void SquareInfoBtn_MouseEnter(object sender, MouseEventArgs e)
    {
        SquareInfoPopup.Visibility = Visibility.Visible;
    }

    private void SquareInfoBtn_MouseLeave(object sender, MouseEventArgs e)
    {
        SquareInfoPopup.Visibility = Visibility.Collapsed;
    }

    private void SquareInfoBtn_Click(object sender, RoutedEventArgs e)
    {
        SquareInfoPopup.Visibility = SquareInfoPopup.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        Keyboard.Focus(this);
    }

    private void ClassicSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ClassicSearchHint.Visibility = string.IsNullOrEmpty(ClassicSearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        _selectedIndex = 0;
        RenderItems();
    }

    private void ClassicClipGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ClassicClipGrid.Clip = new RectangleGeometry(
            new Rect(0, 0, Math.Round(e.NewSize.Width), Math.Round(e.NewSize.Height)),
            19, 19);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isClosing)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            AnimateAndHide();
            return;
        }

        if (e.Key == Key.F1)
        {
            e.Handled = true;
            AnimateAndHide(() => _onEditRequested());
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (_selectedIndex >= 0 && _selectedIndex < _filteredItems.Count)
            {
                var grid = _currentMode == "square" ? SquareTilesGrid : ClassicCardsGrid;
                var visual = _selectedIndex < grid.Children.Count ? grid.Children[_selectedIndex] as FrameworkElement : null;
                TriggerLaunch(_filteredItems[_selectedIndex], null, visual);
            }
            return;
        }

        if (e.Key == Key.Left)
        {
            e.Handled = true;
            MoveSelection(-1);
            return;
        }

        if (e.Key == Key.Right)
        {
            e.Handled = true;
            MoveSelection(1);
            return;
        }

        if (e.Key == Key.Up)
        {
            e.Handled = true;
            MoveSelection(-Math.Max(1, _activeColumns));
            return;
        }

        if (e.Key == Key.Down)
        {
            e.Handled = true;
            MoveSelection(Math.Max(1, _activeColumns));
            return;
        }

        if (_config.QuickNumberKeys && Keyboard.Modifiers == ModifierKeys.None)
        {
            int num = -1;
            if (e.Key >= Key.D1 && e.Key <= Key.D9) num = e.Key - Key.D1;
            else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9) num = e.Key - Key.NumPad1;

            if (num >= 0 && num < _filteredItems.Count)
            {
                e.Handled = true;
                var grid = _currentMode == "square" ? SquareTilesGrid : ClassicCardsGrid;
                var visual = num < grid.Children.Count ? grid.Children[num] as FrameworkElement : null;
                TriggerLaunch(_filteredItems[num], null, visual);
            }
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideOverlayImmediate();
        }
    }
}
