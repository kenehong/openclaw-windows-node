using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using OpenClawTray.Controls.ChatExplorations;
using WinUIEx;

namespace OpenClawTray.Windows;

/// <summary>
/// Component-library-only preview window. Mirrors the tray ChatWindow chrome
/// (480×640, frameless, rounded corners, custom header) so designers can see
/// what each chat-UI variation looks like under a real Mica/Acrylic backdrop —
/// not a brush look-alike.
/// </summary>
public sealed partial class ChatExplorationPreviewWindow : WindowEx
{
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attr, ref int val, int size);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const uint DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;
    private const uint WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 2;

    private IntPtr _hwnd;

    public ChatExplorationPreviewWindow()
    {
        InitializeComponent();
        // Caller sets size via SetWindowSize before Activate(); we just seed a sane default.
        this.SetWindowSize(480, 640);

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        var wStyle = GetWindowLong(_hwnd, GWL_STYLE);
        SetWindowLong(_hwnd, GWL_STYLE, wStyle & ~WS_CAPTION & ~WS_THICKFRAME);

        var cornerPref = DWMWCP_ROUND;
        DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

        // Crisp 16px header icon — DecodePixelWidth/Height forces the bitmap to decode
        // at the exact target size instead of falling back to the ICO's smallest frame.
        var bmp = new BitmapImage(new Uri("ms-appx:///Assets/Icons/StatusConnected.ico"));
        bmp.DecodePixelType = DecodePixelType.Logical;
        bmp.DecodePixelWidth = 16;
        bmp.DecodePixelHeight = 16;
        HeaderIcon.Source = bmp;
    }

    /// <summary>
    /// Frameless window has no system caption, so make the header pull double duty
    /// as the drag handle: ReleaseCapture() + WM_NCLBUTTONDOWN/HTCAPTION hands the
    /// drag off to the OS so it behaves like a real title bar.
    /// </summary>
    private void OnTitleBarPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse) return;
        var props = e.GetCurrentPoint((UIElement)sender).Properties;
        if (!props.IsLeftButtonPressed) return;
        ReleaseCapture();
        SendMessage(_hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
    }

    /// <summary>Apply props captured from the inline preview, then activate.</summary>
    public void ApplyProps(
        ChatVariation variation,
        ChatBackdropMode backdrop,
        double bubbleCornerRadius,
        double gutter,
        double messageGap,
        ChatPaddingDensity paddingDensity,
        ChatAvatarMode avatarMode,
        bool showTimestamps,
        ChatPreviewTheme theme,
        double composerCornerRadius,
        ChatComposerLayout composerLayout,
        double composerIconSize,
        double sendButtonSize)
    {
        // Real system backdrop driven by BackdropMode.
        SystemBackdrop = backdrop switch
        {
            ChatBackdropMode.Mica => new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base },
            ChatBackdropMode.MicaAlt => new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt },
            ChatBackdropMode.Acrylic => new DesktopAcrylicBackdrop(),
            _ => null,
        };

        Preview.Variation = variation;
        Preview.BackdropMode = backdrop;
        Preview.BubbleCornerRadius = bubbleCornerRadius;
        Preview.Gutter = gutter;
        Preview.MessageGap = messageGap;
        Preview.PaddingDensity = paddingDensity;
        Preview.AvatarMode = avatarMode;
        Preview.ShowTimestamps = showTimestamps;
        Preview.PreviewTheme = theme;
        Preview.ComposerCornerRadius = composerCornerRadius;
        Preview.ComposerLayout = composerLayout;
        Preview.ComposerIconSize = composerIconSize;
        Preview.SendButtonSize = sendButtonSize;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>Designer-controlled header: title text and online indicator visibility.</summary>
    public void SetHeader(string title, bool showOnlineIndicator)
    {
        HeaderTitle.Text = string.IsNullOrWhiteSpace(title) ? "Chat" : title;
        OnlineDot.Visibility = showOnlineIndicator ? Visibility.Visible : Visibility.Collapsed;
    }
}

