using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OpenClaw.Shared;
using OpenClawTray.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using WinUIEx;

namespace OpenClawTray.Windows;

/// <summary>
/// A popup window that displays the tray menu at the cursor position.
/// Uses Win32 to remove title bar (workaround for Bug 57667927).
/// </summary>
public sealed partial class TrayMenuWindow : WindowEx
{
    private const int MenuWidthViewUnits = 320;

    #region Win32 Imports
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    
    // SetWindowPos flags
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private enum MonitorDpiType
    {
        MDT_EFFECTIVE_DPI = 0
    }
    #endregion

    public event EventHandler<string>? MenuItemClicked;

    private int _menuHeight = 400;
    private int _itemCount = 0;
    private int _separatorCount = 0;
    private int _headerCount = 0;
    private bool _styleApplied = false;
    private readonly TrayMenuWindow? _ownerMenu;
    private TrayMenuWindow? _activeFlyoutWindow;
    private Button? _activeFlyoutOwner;
    private string? _activeFlyoutKey;
    private bool _isShown;
    private global::Windows.Graphics.RectInt32? _lastMoveAndResizeRect;
    private uint _lastMeasureDpi;
    private double _lastMeasureRasterizationScale;

    public TrayMenuWindow() : this(ownerMenu: null)
    {
    }

    private TrayMenuWindow(TrayMenuWindow? ownerMenu)
    {
        _ownerMenu = ownerMenu;

        InitializeComponent();

        // Configure as popup-style window
        this.IsMaximizable = false;
        this.IsMinimizable = false;
        this.IsResizable = false;
        this.IsAlwaysOnTop = true;
        
        // Apply acrylic backdrop for system-consistent transparency
        BackdropHelper.TrySetAcrylicBackdrop(this);
        
        // NOTE: Do NOT set IsTitleBarVisible = false!
        // Bug 57667927: causes fail-fast in WndProc during dictionary enumeration.
        // We remove the caption via Win32 SetWindowLong instead.
        
        // Hide when focus lost
        Activated += OnActivated;

        // Keyboard support: Esc dismisses, ↑/↓ navigates between menu items.
        // Tab navigation works automatically via UseSystemFocusVisuals on each button.
        RootGrid.KeyDown += OnRootKeyDown;
    }

    private void OnRootKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Escape)
        {
            HideCascade();
            _ownerMenu?.HideCascade();
            e.Handled = true;
            return;
        }

        if (e.Key == global::Windows.System.VirtualKey.Down || e.Key == global::Windows.System.VirtualKey.Up)
        {
            var direction = e.Key == global::Windows.System.VirtualKey.Down
                ? Microsoft.UI.Xaml.Input.FocusNavigationDirection.Down
                : Microsoft.UI.Xaml.Input.FocusNavigationDirection.Up;
            var options = new Microsoft.UI.Xaml.Input.FindNextElementOptions
            {
                SearchRoot = RootGrid,
                XYFocusNavigationStrategyOverride = Microsoft.UI.Xaml.Input.XYFocusNavigationStrategyOverride.Projection,
            };
            var next = Microsoft.UI.Xaml.Input.FocusManager.FindNextElement(direction, options);
            if (next is Control control)
            {
                control.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (Environment.GetEnvironmentVariable("OPENCLAW_UI_AUTOMATION") == "1")
            return;

        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            // Always use delayed check — immediate dismiss races with flyout transitions
            var timer = DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(150);
            timer.IsRepeating = false;
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (!_isShown) return; // already hidden

                var foreground = GetForegroundWindow();
                var thisHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var flyoutHwnd = _activeFlyoutWindow != null
                    ? WinRT.Interop.WindowNative.GetWindowHandle(_activeFlyoutWindow)
                    : IntPtr.Zero;
                var ownerHwnd = _ownerMenu != null
                    ? WinRT.Interop.WindowNative.GetWindowHandle(_ownerMenu)
                    : IntPtr.Zero;

                // Stay open if focus is on this window, its flyout, or its parent
                if (foreground == thisHwnd || foreground == flyoutHwnd ||
                    (ownerHwnd != IntPtr.Zero && foreground == ownerHwnd))
                    return;

                // Focus went elsewhere — dismiss everything
                HideCascade();
                _ownerMenu?.HideCascade();
            };
            timer.Start();
        }
    }

    public void ShowAtCursor()
    {
        ApplyPopupStyle();

        if (GetCursorPos(out POINT pt))
        {
            // Get work area of monitor where cursor is
            var hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(hMonitor, ref monitorInfo);
            var workArea = monitorInfo.rcWork;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var dpi = GetEffectiveMonitorDpi(hMonitor, hwnd);
            SizeToContent(workArea.Bottom - workArea.Top, dpi);
            var menuWidthPx = ConvertViewUnitsToPixels(MenuWidthViewUnits, dpi);
            var menuHeightPx = ConvertViewUnitsToPixels(_menuHeight, dpi);

            const int margin = 8;

            var (x, y) = OpenClaw.Shared.MenuPositioner.CalculatePosition(
                pt.X, pt.Y,
                menuWidthPx, menuHeightPx,
                workArea.Left, workArea.Top, workArea.Right, workArea.Bottom,
                margin);

            var targetRect = new global::Windows.Graphics.RectInt32(x, y, menuWidthPx, menuHeightPx);
            if (!RectEquals(_lastMoveAndResizeRect, targetRect))
            {
                AppWindow.MoveAndResize(targetRect);
                _lastMoveAndResizeRect = targetRect;
            }
        }
        else
        {
            SizeToContent();
        }

        ApplyRoundedWindowRegion();
        _isShown = true;
        Activate();
        SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
        _ = VisualTestCapture.CaptureAsync(RootGrid, "TrayMenu");
    }

    private void ShowAdjacentTo(FrameworkElement parentElement)
    {
        ApplyPopupStyle();

        if (!TryGetElementScreenRect(parentElement, out var parentRect))
        {
            ShowAtCursor();
            return;
        }

        var center = new POINT
        {
            X = parentRect.Left + ((parentRect.Right - parentRect.Left) / 2),
            Y = parentRect.Top + ((parentRect.Bottom - parentRect.Top) / 2)
        };
        var hMonitor = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(hMonitor, ref monitorInfo);
        var workArea = monitorInfo.rcWork;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var dpi = GetEffectiveMonitorDpi(hMonitor, hwnd);
        SizeToContent(monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top, dpi);
        var submenuWidthPx = ConvertViewUnitsToPixels(280, dpi);
        var submenuHeightPx = ConvertViewUnitsToPixels(_menuHeight, dpi);

        const int overlap = 2;
        const int margin = 8;
        var roomRight = workArea.Right - parentRect.Right;
        var roomLeft = parentRect.Left - workArea.Left;
        var openRight = roomRight >= submenuWidthPx + margin || roomRight >= roomLeft;
        var x = openRight
            ? parentRect.Right - overlap
            : parentRect.Left - submenuWidthPx + overlap;
        var y = parentRect.Top;

        x = Math.Clamp(x, workArea.Left + margin, Math.Max(workArea.Left + margin, workArea.Right - submenuWidthPx - margin));
        y = Math.Clamp(y, workArea.Top + margin, Math.Max(workArea.Top + margin, workArea.Bottom - submenuHeightPx - margin));

        var targetRect = new global::Windows.Graphics.RectInt32(x, y, submenuWidthPx, submenuHeightPx);
        if (!RectEquals(_lastMoveAndResizeRect, targetRect))
        {
            AppWindow.MoveAndResize(targetRect);
            _lastMoveAndResizeRect = targetRect;
        }

        ApplyRoundedWindowRegion();
        if (!_isShown)
        {
            AppWindow.Show();
            _isShown = true;
        }
    }

    /// <summary>
    /// Builds an icon element for a menu row. Recognizes single Private-Use-Area
    /// codepoints (Segoe Fluent Icons range \uE000–\uF8FF) and renders them as a
    /// FontIcon; everything else (emoji, ASCII) renders as a TextBlock. Returns
    /// null for empty input so callers can leave the slot empty without padding.
    /// </summary>
    private static FrameworkElement? BuildIconElement(string? icon)
    {
        if (string.IsNullOrEmpty(icon)) return null;

        // Detect Segoe Fluent Icons glyph: single char in PUA range, or a high-surrogate pair
        var isFluentGlyph = icon.Length == 1 && icon[0] >= '\uE000' && icon[0] <= '\uF8FF';
        if (isFluentGlyph)
        {
            return new FontIcon
            {
                Glyph = icon,
                FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsTextScaleFactorEnabled = false
            };
        }

        return new TextBlock
        {
            Text = icon,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsTextSelectionEnabled = false,
            IsTextScaleFactorEnabled = false
        };
    }

    /// <summary>
    /// Adds a checkmark menu item — Windows menu idiom for toggleable state.
    /// Renders a 16px ✓ slot on the left (visible only when checked), then icon + label.
    /// Click invokes <paramref name="onToggle"/> with the new state and does NOT dismiss the menu.
    /// </summary>
    public void AddCheckMenuItem(string text, string? icon, bool isChecked, Action<bool> onToggle, bool isEnabled = true, string? tooltip = null)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); // ✓
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); // icon
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var check = new FontIcon
        {
            Glyph = isChecked ? "\uE73E" : string.Empty, // CheckMark
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
            FontSize = 14,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(check, 0);
        grid.Children.Add(check);

        var iconEl = BuildIconElement(icon);
        if (iconEl != null)
        {
            Grid.SetColumn(iconEl, 1);
            grid.Children.Add(iconEl);
        }

        var label = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            IsTextSelectionEnabled = false
        };
        if (!isEnabled)
            label.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorDisabledBrush"];
        Grid.SetColumn(label, 2);
        grid.Children.Add(label);

        var button = new Button
        {
            Content = grid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 8, 12, 8),
            Background = null,
            BorderThickness = new Thickness(0),
            IsEnabled = isEnabled,
            CornerRadius = (CornerRadius)Application.Current.Resources["ControlCornerRadius"],
            UseSystemFocusVisuals = true
        };
        AutomationProperties.SetAutomationId(button, BuildMenuItemAutomationId("toggle:" + text, text));
        AutomationProperties.SetName(button, isChecked ? $"{text} on" : $"{text} off");
        if (!string.IsNullOrEmpty(tooltip))
        {
            ToolTipService.SetToolTip(button, tooltip);
            AutomationProperties.SetHelpText(button, tooltip);
        }

        button.Click += (s, e) =>
        {
            var newState = !isChecked;
            isChecked = newState;
            check.Glyph = newState ? "\uE73E" : string.Empty;
            AutomationProperties.SetName(button, newState ? $"{text} on" : $"{text} off");
            onToggle(newState);
            // Do NOT dismiss — toggles stay open so users can flip multiple capabilities
        };

        button.PointerEntered += (s, e) =>
        {
            HideActiveFlyout();
            if (button.IsEnabled)
                button.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        };
        button.PointerExited += (s, e) =>
        {
            button.Background = null;
        };

        MenuPanel.Children.Add(button);
        _itemCount++;
    }

    public void AddMenuItem(string text, string? icon, string action, bool isEnabled = true, bool indent = false, string? accessKey = null, string? accelerator = null)
    {
        var rowGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(16) },             // icon slot
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, // label
                new ColumnDefinition { Width = GridLength.Auto }                  // accelerator
            }
        };

        var iconEl = BuildIconElement(icon);
        if (iconEl != null)
        {
            Grid.SetColumn(iconEl, 0);
            rowGrid.Children.Add(iconEl);
        }

        var content = new TextBlock
        {
            Text = text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsTextSelectionEnabled = false,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (!isEnabled)
            content.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorDisabledBrush"];
        Grid.SetColumn(content, 1);
        rowGrid.Children.Add(content);

        if (!string.IsNullOrEmpty(accelerator))
        {
            var accel = new TextBlock
            {
                Text = accelerator,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                IsTextSelectionEnabled = false
            };
            Grid.SetColumn(accel, 2);
            rowGrid.Children.Add(accel);
        }

        var leftPadding = indent ? 24 : 12;
        var button = new Button
        {
            Content = rowGrid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(leftPadding, 8, 12, 8),
            Background = null,
            BorderThickness = new Thickness(0),
            IsEnabled = isEnabled,
            Tag = action,
            CornerRadius = (CornerRadius)Application.Current.Resources["ControlCornerRadius"],
            UseSystemFocusVisuals = true
        };
        AutomationProperties.SetAutomationId(button, BuildMenuItemAutomationId(action, text));
        AutomationProperties.SetName(button, text);

        // AccessKey + KeyboardAccelerator wiring intentionally disabled — when set on
        // Buttons inside the custom WindowEx tray popup, they triggered a native
        // WinUI fail-fast (0xc000027b) the moment the menu was shown. Deferred until
        // we can validate AccessKeyManager scope behavior on a non-Page XamlRoot.
        _ = accessKey; _ = accelerator;

        button.Click += (s, e) =>
        {
            MenuItemClicked?.Invoke(this, action);
            HideCascade(); // Hide instead of close - window is reused
        };

        // Hover effect
        button.PointerEntered += (s, e) =>
        {
            HideActiveFlyout();
            if (button.IsEnabled)
                button.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        };
        button.PointerExited += (s, e) =>
        {
            button.Background = null;
        };

        MenuPanel.Children.Add(button);
        _itemCount++;
    }

    private static string BuildMenuItemAutomationId(string action, string text)
    {
        var source = string.IsNullOrWhiteSpace(action) ? text : action;
        var chars = source
            .Where(char.IsLetterOrDigit)
            .Take(48)
            .ToArray();
        return chars.Length == 0
            ? "TrayMenuItem"
            : "TrayMenuItem" + new string(chars);
    }

    /// <summary>
    /// Parses a display accelerator string (e.g., "Ctrl+,", "Ctrl+Shift+Q") into a
    /// Windows.System.VirtualKey + modifier set so the menu row registers a real
    /// KeyboardAccelerator. Returns false for empty/unparseable input.
    /// </summary>
    private static bool TryParseAccelerator(string? accelerator, out global::Windows.System.VirtualKey key, out global::Windows.System.VirtualKeyModifiers mods)
    {
        key = default;
        mods = global::Windows.System.VirtualKeyModifiers.None;
        if (string.IsNullOrWhiteSpace(accelerator)) return false;

        var parts = accelerator.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl":
                case "control": mods |= global::Windows.System.VirtualKeyModifiers.Control; break;
                case "shift": mods |= global::Windows.System.VirtualKeyModifiers.Shift; break;
                case "alt": mods |= global::Windows.System.VirtualKeyModifiers.Menu; break;
                case "win":
                case "windows": mods |= global::Windows.System.VirtualKeyModifiers.Windows; break;
                default: return false;
            }
        }

        var keyText = parts[^1];
        if (keyText.Length == 1)
        {
            var ch = char.ToUpperInvariant(keyText[0]);
            if (ch >= 'A' && ch <= 'Z') { key = (global::Windows.System.VirtualKey)ch; return true; }
            if (ch >= '0' && ch <= '9') { key = (global::Windows.System.VirtualKey)ch; return true; }
            if (ch == ',') { key = (global::Windows.System.VirtualKey)188; return true; } // OEM Comma
            if (ch == '.') { key = (global::Windows.System.VirtualKey)190; return true; } // OEM Period
            if (ch == '/') { key = (global::Windows.System.VirtualKey)191; return true; }
        }
        return Enum.TryParse<global::Windows.System.VirtualKey>(keyText, ignoreCase: true, out key);
    }

    public void AddFlyoutMenuItem(string text, string? icon, IEnumerable<TrayMenuFlyoutItem> items, bool indent = false, string? action = null)
    {
        var rowGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(16) },                  // icon
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },// label
                new ColumnDefinition { Width = GridLength.Auto }                       // chevron
            }
        };

        var iconEl = BuildIconElement(icon);
        if (iconEl != null)
        {
            Grid.SetColumn(iconEl, 0);
            rowGrid.Children.Add(iconEl);
        }

        var content = new TextBlock
        {
            Text = text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsTextSelectionEnabled = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(content, 1);
        rowGrid.Children.Add(content);

        var chevron = new FontIcon
        {
            Glyph = "\uE76C", // ChevronRight
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            VerticalAlignment = VerticalAlignment.Center,
            IsTextScaleFactorEnabled = false
        };
        AutomationProperties.SetAccessibilityView(chevron, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        Grid.SetColumn(chevron, 2);
        rowGrid.Children.Add(chevron);

        var flyoutItems = items.ToArray();

        var leftPadding = indent ? 24 : 12;
        var button = new Button
        {
            Content = rowGrid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(leftPadding, 8, 12, 8),
            Background = null,
            BorderThickness = new Thickness(0),
            CornerRadius = (CornerRadius)Application.Current.Resources["ControlCornerRadius"],
            UseSystemFocusVisuals = true
        };
        AutomationProperties.SetName(button, text);

        button.PointerEntered += (s, e) =>
        {
            button.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
            ShowCascadingFlyout(button, flyoutItems);
        };
        button.PointerExited += (s, e) =>
        {
            button.Background = null;
        };
        button.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(action))
            {
                HideActiveFlyout();
                MenuItemClicked?.Invoke(this, action);
            }
            else
            {
                ShowCascadingFlyout(button, flyoutItems);
            }
        };

        MenuPanel.Children.Add(button);
        _itemCount++;
    }

    public void AddSeparator()
    {
        var sep = new Border
        {
            Height = 1,
            Margin = new Thickness(8, 4, 8, 4),
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"]
        };
        sep.PointerEntered += (s, e) => HideActiveFlyout();
        MenuPanel.Children.Add(sep);
        _separatorCount++;
    }

    public void AddBrandHeader(string emoji, string text)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Padding = new Thickness(12, 12, 12, 8),
            Spacing = 8
        };

        panel.Children.Add(new TextBlock
        {
            Text = emoji,
            Style = (Style)Application.Current.Resources["TitleTextBlockStyle"]
        });

        panel.Children.Add(new TextBlock
        {
            Text = text,
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            VerticalAlignment = VerticalAlignment.Center
        });

        MenuPanel.Children.Add(panel);
        _headerCount += 2; // Counts as larger
    }

    public void AddHeader(string text)
    {
        var tb = new TextBlock
        {
            Text = text,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Padding = new Thickness(12, 8, 12, 4)
        };
        tb.PointerEntered += (s, e) => HideActiveFlyout();
        MenuPanel.Children.Add(tb);
        _headerCount++;
    }

    public void AddCustomElement(UIElement element)
    {
        if (element is FrameworkElement fe)
            fe.PointerEntered += (s, e) => HideActiveFlyout();
        MenuPanel.Children.Add(element);
    }

    /// <summary>
    /// Adds a custom UIElement as a flyout-enabled menu item with hover/click behavior.
    /// Same behavior as AddFlyoutMenuItem but accepts any UIElement instead of text.
    /// </summary>
    public void AddFlyoutCustomItem(UIElement content, IEnumerable<TrayMenuFlyoutItem> items, string? action = null)
    {
        var flyoutItems = items.ToArray();

        var button = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0),
            Background = null,
            BorderThickness = new Thickness(0),
            CornerRadius = (CornerRadius)Application.Current.Resources["ControlCornerRadius"],
            UseSystemFocusVisuals = true
        };

        button.PointerEntered += (s, e) =>
        {
            button.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
            ShowCascadingFlyout(button, flyoutItems);
        };
        button.PointerExited += (s, e) =>
        {
            button.Background = null;
        };
        button.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(action))
            {
                HideActiveFlyout();
                MenuItemClicked?.Invoke(this, action);
                HideCascade();
            }
            else
            {
                ShowCascadingFlyout(button, flyoutItems);
            }
        };

        MenuPanel.Children.Add(button);
        _itemCount++;
    }

    /// <summary>
    /// Adds a custom UIElement as a flyout-enabled menu item whose flyout content is a raw UIElement.
    /// </summary>
    public void ClearItems()
    {
        HideActiveFlyout();
        MenuPanel.Children.Clear();
        _itemCount = 0;
        _separatorCount = 0;
        _headerCount = 0;
    }

    public void HideCascade()
    {
        HideActiveFlyout();
        this.Hide();
        _isShown = false;
    }

    /// <summary>
    /// Adjusts the window height to fit content and stores it for positioning
    /// </summary>
    public void SizeToContent()
    {
        if (TryGetCurrentMonitorMetrics(out var workAreaHeightPx, out var dpi))
        {
            SizeToContent(workAreaHeightPx, dpi);
            return;
        }

        SizeToContent(0, 96);
    }

    private void SizeToContent(int workAreaHeightPx, uint dpi)
    {
        PrepareLayoutForMeasurement(dpi);

        // Measure the actual content size instead of estimating
        MenuPanel.Measure(new global::Windows.Foundation.Size(MenuWidthViewUnits, double.PositiveInfinity));
        var desiredHeight = MenuPanel.DesiredSize.Height;
        
        // Add border chrome (1px border top+bottom = 2px, plus small rounding buffer)
        var contentHeight = (int)Math.Ceiling(desiredHeight) + 4;
        _menuHeight = Math.Max(contentHeight, 100);

        if (workAreaHeightPx > 0)
        {
            var workAreaHeight = MenuSizingHelper.ConvertPixelsToViewUnits(workAreaHeightPx, dpi);
            _menuHeight = MenuSizingHelper.CalculateWindowHeight(_menuHeight, workAreaHeight);
        }

        this.SetWindowSize(MenuWidthViewUnits, _menuHeight);
        ApplyRoundedWindowRegion();
    }

    private void PrepareLayoutForMeasurement(uint dpi)
    {
        dpi = dpi == 0 ? 96 : dpi;
        var rasterizationScale = RootGrid.XamlRoot?.RasterizationScale ?? dpi / 96.0;
        var dpiChanged = _lastMeasureDpi != 0
            && MenuSizingHelper.HasDpiOrScaleChanged(_lastMeasureDpi, _lastMeasureRasterizationScale, dpi, rasterizationScale);

        _lastMeasureDpi = dpi;
        _lastMeasureRasterizationScale = rasterizationScale;

        if (dpiChanged)
        {
            _lastMoveAndResizeRect = null;
            HideActiveFlyout();
        }

        RootGrid.InvalidateMeasure();
        RootGrid.InvalidateArrange();
        MenuPanel.InvalidateMeasure();
        MenuPanel.InvalidateArrange();
        RootGrid.UpdateLayout();
    }

    private bool TryGetCurrentMonitorMetrics(out int workAreaHeight, out uint dpi)
    {
        workAreaHeight = 0;
        dpi = 96;

        if (!GetCursorPos(out POINT pt))
            return false;

        var hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
        if (hMonitor == IntPtr.Zero)
            return false;

        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref monitorInfo))
            return false;

        workAreaHeight = monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        dpi = GetEffectiveMonitorDpi(hMonitor, hwnd);
        return workAreaHeight > 0;
    }

    private static uint GetEffectiveMonitorDpi(IntPtr hMonitor, IntPtr hwnd)
    {
        if (hMonitor != IntPtr.Zero)
        {
            try
            {
                var hr = GetDpiForMonitor(hMonitor, MonitorDpiType.MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY);
                if (hr == 0)
                {
                    if (dpiY != 0)
                        return dpiY;

                    if (dpiX != 0)
                        return dpiX;
                }
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        var dpi = hwnd != IntPtr.Zero ? GetDpiForWindow(hwnd) : 0;
        return dpi == 0 ? 96u : dpi;
    }

    private void ApplyPopupStyle()
    {
        if (_styleApplied)
            return;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        int style = GetWindowLong(hwnd, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU);
        SetWindowLong(hwnd, GWL_STYLE, style);

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW;
        if (_ownerMenu != null)
            exStyle |= WS_EX_NOACTIVATE;

        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

        // Must call SetWindowPos with SWP_FRAMECHANGED to apply the style change.
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

        _styleApplied = true;
        ApplyRoundedWindowRegion();
    }

    private void ApplyRoundedWindowRegion()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero)
            return;

        if (!GetWindowRect(hwnd, out var rect))
            return;

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
            return;

        var dpi = GetDpiForWindow(hwnd);
        var cornerDiameter = ConvertViewUnitsToPixels(16, dpi);
        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, cornerDiameter, cornerDiameter);
        if (region == IntPtr.Zero)
            return;

        if (SetWindowRgn(hwnd, region, false) == 0)
        {
            DeleteObject(region);
        }
    }

    private void ShowCascadingFlyout(Button ownerButton, IReadOnlyList<TrayMenuFlyoutItem> items)
    {
        var flyoutKey = CreateFlyoutKey(items);
        var flyoutWindow = _activeFlyoutWindow;
        if (flyoutWindow == null)
        {
            flyoutWindow = new TrayMenuWindow(this);
            flyoutWindow.MenuItemClicked += (_, action) =>
            {
                MenuItemClicked?.Invoke(this, action);
                HideCascade();
            };

            _activeFlyoutWindow = flyoutWindow;
        }

        if (!ReferenceEquals(_activeFlyoutOwner, ownerButton) || !string.Equals(_activeFlyoutKey, flyoutKey, StringComparison.Ordinal))
        {
            flyoutWindow.ClearItems();
            foreach (var item in items)
            {
                if (item.IsHeader)
                {
                    flyoutWindow.AddHeader(item.Text);
                }
                else if (item.IsCheck && item.OnToggle != null)
                {
                    var toggleCallback = item.OnToggle;
                    flyoutWindow.AddCheckMenuItem(item.Text, item.Icon, item.IsChecked, newState =>
                    {
                        item.IsChecked = newState;
                        toggleCallback(newState);
                    }, tooltip: item.Tooltip);
                }
                else if (string.IsNullOrEmpty(item.Action))
                {
                    // Non-interactive detail line — compact padding
                    flyoutWindow.AddCustomElement(new TextBlock
                    {
                        Text = item.Text,
                        Padding = new Thickness(12, 2, 12, 2),
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        TextWrapping = TextWrapping.Wrap
                    });
                }
                else
                {
                    flyoutWindow.AddMenuItem(item.Text, item.Icon, item.Action);
                }
            }

            _activeFlyoutOwner = ownerButton;
            _activeFlyoutKey = flyoutKey;
        }

        flyoutWindow.ShowAdjacentTo(ownerButton);
    }

    private void HideActiveFlyout()
    {
        _activeFlyoutWindow?.HideCascade();
        _activeFlyoutWindow = null;
        _activeFlyoutOwner = null;
        _activeFlyoutKey = null;
    }

    private static string CreateFlyoutKey(IEnumerable<TrayMenuFlyoutItem> items)
    {
        return string.Join('\u001f', items.Select(item => $"{item.Text}\u001e{item.Icon}\u001e{item.Action}"));
    }

    private static bool RectEquals(global::Windows.Graphics.RectInt32? current, global::Windows.Graphics.RectInt32 next)
    {
        return current.HasValue &&
            current.Value.X == next.X &&
            current.Value.Y == next.Y &&
            current.Value.Width == next.Width &&
            current.Value.Height == next.Height;
    }

    private bool TryGetElementScreenRect(FrameworkElement element, out RECT rect)
    {
        rect = default;

        try
        {
            var transform = element.TransformToVisual(null);
            var bounds = transform.TransformBounds(new global::Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
            var scale = element.XamlRoot?.RasterizationScale ?? 1.0;
            var sourceWindow = _ownerMenu ?? this;
            var windowPosition = sourceWindow.AppWindow.Position;

            rect = new RECT
            {
                Left = windowPosition.X + (int)Math.Round(bounds.Left * scale),
                Top = windowPosition.Y + (int)Math.Round(bounds.Top * scale),
                Right = windowPosition.X + (int)Math.Round(bounds.Right * scale),
                Bottom = windowPosition.Y + (int)Math.Round(bounds.Bottom * scale)
            };

            return rect.Right > rect.Left && rect.Bottom > rect.Top;
        }
        catch
        {
            return false;
        }
    }

    private static int ConvertViewUnitsToPixels(int viewUnits, uint dpi)
    {
        if (viewUnits <= 0)
            return 1;

        if (dpi == 0)
            dpi = 96;

        return Math.Max(1, (int)Math.Ceiling(viewUnits * dpi / 96.0));
    }

    private void HideCascadeIfFocusLeavesMenu()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(150);
        timer.IsRepeating = false;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var foreground = GetForegroundWindow();
            var thisHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var flyoutHwnd = _activeFlyoutWindow == null
                ? IntPtr.Zero
                : WinRT.Interop.WindowNative.GetWindowHandle(_activeFlyoutWindow);

            if (foreground != thisHwnd && foreground != flyoutHwnd)
            {
                HideCascade();
            }
        };
        timer.Start();
    }
}

public sealed class TrayMenuFlyoutItem
{
    public TrayMenuFlyoutItem() { }

    public TrayMenuFlyoutItem(string text, string? icon = null, string? action = null)
    {
        Text = text;
        Icon = icon;
        Action = action ?? "";
    }

    public string Text { get; set; } = "";
    public string? Icon { get; set; }
    public string Action { get; set; } = "";
    public bool IsHeader { get; set; }

    // Optional interactive checkbox row. When IsCheck=true and OnToggle is set,
    // the flyout renders this as a check-menu row (✓ slot + label) that toggles
    // without dismissing the flyout. Used for per-device capability permissions.
    public bool IsCheck { get; set; }
    public bool IsChecked { get; set; }
    public Action<bool>? OnToggle { get; set; }
    public string? Tooltip { get; set; }
}
