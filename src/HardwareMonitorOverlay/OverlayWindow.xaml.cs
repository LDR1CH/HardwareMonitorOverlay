using System;
using System.Windows;
using System.Windows.Interop;
using HardwareMonitorOverlay.Configuration;
using HardwareMonitorOverlay.ViewModels;
using Microsoft.Win32;

namespace HardwareMonitorOverlay;

public partial class OverlayWindow : Window
{
    private readonly OverlayConfig _config;
    private readonly OverlayViewModel _viewModel;
    private IntPtr _handle = IntPtr.Zero;

    public OverlayWindow(OverlayConfig config, OverlayViewModel viewModel)
    {
        _config = config;
        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;
        Opacity = config.Opacity;
        viewModel.FontSize = config.FontSize;
        viewModel.ShowCpu = config.ShowCpu;
        viewModel.ShowGpu = config.ShowGpu;

        // The panel resizes when rows are toggled or the font changes; re-anchor it.
        SizeChanged += (_, _) => ApplyPosition();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;
        Interop.NativeMethods.MakeClickThrough(_handle);
        ApplyPosition();
    }

    /// <summary>
    /// Called once per poll tick. Full-screen apps often take over the topmost
    /// band, so the HUD asks for it back rather than assuming Topmost sticks.
    /// </summary>
    public void KeepOnTop() => Interop.NativeMethods.ReassertTopmost(_handle);

    /// <summary>Re-reads the config and applies corner, opacity, font and visibility.</summary>
    public void ApplyConfig()
    {
        Opacity = _config.Opacity;
        _viewModel.FontSize = _config.FontSize;
        _viewModel.ShowCpu = _config.ShowCpu;
        _viewModel.ShowGpu = _config.ShowGpu;
        ApplyPosition();
    }

    /// <summary>
    /// Anchors the HUD to the configured corner of the primary work area, which
    /// excludes the taskbar.
    /// </summary>
    private void ApplyPosition()
    {
        Rect workArea = SystemParameters.WorkArea;

        double width = ActualWidth > 0 ? ActualWidth : Width;
        double height = ActualHeight > 0 ? ActualHeight : Height;

        if (double.IsNaN(width) || double.IsNaN(height))
        {
            return;
        }

        bool isLeft = _config.Corner is OverlayCorner.TopLeft or OverlayCorner.BottomLeft;
        bool isTop = _config.Corner is OverlayCorner.TopLeft or OverlayCorner.TopRight;

        Left = isLeft
            ? workArea.Left + _config.MarginX
            : workArea.Right - width - _config.MarginX;

        Top = isTop
            ? workArea.Top + _config.MarginY
            : workArea.Bottom - height - _config.MarginY;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => ApplyPosition();

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        base.OnClosed(e);
    }
}
