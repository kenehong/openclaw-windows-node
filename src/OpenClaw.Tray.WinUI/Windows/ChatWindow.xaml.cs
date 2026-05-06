using Microsoft.UI.Xaml;
using OpenClaw.Shared;
using OpenClawTray.Helpers;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WinUIEx;

namespace OpenClawTray.Windows;

public sealed partial class ChatWindow : WindowEx
{
    private readonly string _gatewayUrl;
    private readonly string _token;
    public bool IsClosed { get; private set; }

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO mi);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attr, ref int val, int size);

    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT2 { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT2 rcMonitor;
        public RECT2 rcWork;
        public int dwFlags;
    }

    public ChatWindow(string gatewayUrl, string token)
    {
        _gatewayUrl = gatewayUrl;
        _token = token;
        InitializeComponent();

        // No system title bar for popup panel — our custom header replaces it
        this.SetWindowSize(480, 640);
        this.SetIcon(IconHelper.GetStatusIconPath(ConnectionStatus.Connected));

        // Set as tool window (hidden from taskbar) + remove system caption
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        var wStyle = GetWindowLong(hwnd, GWL_STYLE);
        SetWindowLong(hwnd, GWL_STYLE, wStyle & ~WS_CAPTION & ~WS_THICKFRAME);

        // Rounded corners (Windows 11)
        var cornerPref = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

        // Auto-hide when clicking outside the panel
        Activated += OnWindowActivated;

        // Hide instead of close — preserves WebView2 session for instant reopen
        Closed += OnWindowClosing;

        // Hand off to the active surface (native if flag is on, else WebView2-backed).
        var app = Application.Current as App;
        var settings = app?.Settings;
        _nativeActive = NativeChatFeature.IsEnabled(settings);
        OpenClawTray.Services.Logger.Info($"[NativeChat] ChatWindow ctor: nativeActive={_nativeActive}, gatewayClient={(app?.GatewayClient != null ? "ready" : "NULL")}, url={_gatewayUrl}");
        if (_nativeActive)
        {
            Surface.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            NativeSurface.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            if (app?.GatewayClient != null)
            {
                OpenClawTray.Services.Logger.Info("[NativeChat] ChatWindow: calling NativeSurface.Initialize with live GatewayClient");
                NativeSurface.Initialize(_gatewayUrl, _token, app.GatewayClient);
            }
            else
            {
                OpenClawTray.Services.Logger.Info("[NativeChat] ChatWindow: GatewayClient is NULL — deferring NativeSurface.Initialize");
            }
        }
        else
        {
            Surface.Initialize(_gatewayUrl, _token);
        }
    }

    /// <summary>
    /// Re-bind the native surface to a (possibly new) GatewayClient. Called from App
    /// after ReinitializeGatewayClient so a stale/null client doesn't leave the surface
    /// dead.
    /// </summary>
    public void RebindNativeSurface(OpenClawGatewayClient client)
    {
        if (!_nativeActive) return;
        OpenClawTray.Services.Logger.Info("[NativeChat] ChatWindow.RebindNativeSurface: re-initializing NativeSurface");
        NativeSurface.Initialize(_gatewayUrl, _token, client);
    }

    private readonly bool _nativeActive;

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            this.Hide();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        this.Hide();
    }

    /// <summary>Position near the system tray and show with animation.</summary>
    public void ShowNearTray()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Get cursor position (near tray icon click)
        GetCursorPos(out POINT pt);

        // Get work area of the monitor containing the cursor
        var hMon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(hMon, ref mi);
        var work = mi.rcWork;

        // Get DPI scale for this monitor
        uint dpi = GetDpiForWindow(hwnd);
        double scale = dpi / 96.0;

        // Panel size in physical pixels
        int panelWPx = (int)(480 * scale);
        int panelHPx = (int)(640 * scale);

        // Position: bottom-right of work area, 8px margin from edges
        int margin = 8;
        int x = work.Right - panelWPx - margin;
        int y = work.Bottom - panelHPx - margin;

        this.Move(x, y);
        this.SetWindowSize(480, 640);

        this.Show();
        SetForegroundWindow(hwnd);
    }

    /// <summary>Show near tray. No animation — WebView2 doesn't participate in composition animations.</summary>
    public void ShowNearTrayAnimated()
    {
        ShowNearTray();
    }

    private void OnWindowClosing(object sender, WindowEventArgs args)
    {
        // Intercept close → hide instead (keeps WebView2 warm)
        args.Handled = true;
        this.Hide();
    }

    /// <summary>Actually close and dispose (called on app shutdown).</summary>
    public void ForceClose()
    {
        Closed -= OnWindowClosing;
        IsClosed = true;
        Close();
    }

    private void OnHome(object sender, RoutedEventArgs e)
    {
        if (_nativeActive) NativeSurface.NavigateHome(); else Surface.NavigateHome();
    }
    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        if (_nativeActive) NativeSurface.Reload(); else Surface.Reload();
    }
    private void OnPopout(object sender, RoutedEventArgs e)
    {
        if (_nativeActive) NativeSurface.OpenInBrowser(); else Surface.OpenInBrowser();
    }
}
