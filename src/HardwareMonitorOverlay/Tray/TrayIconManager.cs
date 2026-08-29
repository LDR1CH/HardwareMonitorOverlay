using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using HardwareMonitorOverlay.Configuration;

namespace HardwareMonitorOverlay.Tray;

/// <summary>
/// The overlay is click-through, so it can never be right-clicked. The tray icon
/// is the entire user interface for changing settings and quitting.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private const string OpacityMenuText = "Opacity";

    private readonly OverlayConfig _config;
    private readonly Action _onAppearanceChanged;
    private readonly Action<int> _onIntervalChanged;
    private readonly Action _onExit;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private IntPtr _iconHandle = IntPtr.Zero;
    private bool _disposed;

    public TrayIconManager(
        OverlayConfig config,
        bool sensorsAvailable,
        Action onAppearanceChanged,
        Action<int> onIntervalChanged,
        Action onExit)
    {
        _config = config;
        _onAppearanceChanged = onAppearanceChanged;
        _onIntervalChanged = onIntervalChanged;
        _onExit = onExit;

        _menu = BuildMenu();

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = sensorsAvailable
                ? "Hardware Monitor Overlay"
                : "Hardware Monitor Overlay (no sensor access)",
            Visible = true,
            ContextMenuStrip = _menu
        };
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var corner = new ToolStripMenuItem("Corner");
        corner.DropDownItems.AddRange(new ToolStripItem[]
        {
            CornerItem("Top left", OverlayCorner.TopLeft),
            CornerItem("Top right", OverlayCorner.TopRight),
            CornerItem("Bottom left", OverlayCorner.BottomLeft),
            CornerItem("Bottom right", OverlayCorner.BottomRight)
        });

        var opacity = new ToolStripMenuItem(OpacityMenuText);
        opacity.DropDownItems.AddRange(new ToolStripItem[]
        {
            OpacityItem("40%", 0.4),
            OpacityItem("60%", 0.6),
            OpacityItem("85%", 0.85),
            OpacityItem("100%", 1.0)
        });

        var size = new ToolStripMenuItem("Text size");
        size.DropDownItems.AddRange(new ToolStripItem[]
        {
            FontSizeItem("Small", 11),
            FontSizeItem("Medium", 13),
            FontSizeItem("Large", 16),
            FontSizeItem("Extra large", 20)
        });

        var rate = new ToolStripMenuItem("Refresh rate");
        rate.DropDownItems.AddRange(new ToolStripItem[]
        {
            IntervalItem("1 second", 1000),
            IntervalItem("1.5 seconds", 1500),
            IntervalItem("2 seconds", 2000),
            IntervalItem("5 seconds", 5000)
        });

        var rows = new ToolStripMenuItem("Show");
        rows.DropDownItems.AddRange(new ToolStripItem[]
        {
            RowItem("CPU", isCpu: true),
            RowItem("GPU", isCpu: false)
        });

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => _onExit();

        menu.Items.AddRange(new ToolStripItem[]
        {
            corner, opacity, size, rate, rows, new ToolStripSeparator(), exit
        });

        // Check marks are recomputed when the menu opens rather than tracked per click.
        menu.Opening += (_, _) => RefreshCheckStates(menu);

        return menu;
    }

    private ToolStripMenuItem CornerItem(string text, OverlayCorner value)
    {
        var item = new ToolStripMenuItem(text) { Tag = value };
        item.Click += (_, _) =>
        {
            _config.Corner = value;
            CommitAppearance();
        };
        return item;
    }

    private ToolStripMenuItem OpacityItem(string text, double value)
    {
        var item = new ToolStripMenuItem(text) { Tag = value };
        item.Click += (_, _) =>
        {
            _config.Opacity = value;
            CommitAppearance();
        };
        return item;
    }

    private ToolStripMenuItem FontSizeItem(string text, double value)
    {
        var item = new ToolStripMenuItem(text) { Tag = value };
        item.Click += (_, _) =>
        {
            _config.FontSize = value;
            CommitAppearance();
        };
        return item;
    }

    private ToolStripMenuItem IntervalItem(string text, int milliseconds)
    {
        var item = new ToolStripMenuItem(text) { Tag = milliseconds };
        item.Click += (_, _) =>
        {
            _config.PollIntervalMs = milliseconds;
            _config.Save();
            _onIntervalChanged(milliseconds);
        };
        return item;
    }

    private ToolStripMenuItem RowItem(string text, bool isCpu)
    {
        var item = new ToolStripMenuItem(text) { Tag = isCpu ? "cpu" : "gpu" };
        item.Click += (_, _) =>
        {
            bool turningOff = isCpu ? _config.ShowCpu : _config.ShowGpu;
            bool otherIsOff = isCpu ? !_config.ShowGpu : !_config.ShowCpu;

            // Never let both rows be hidden, which would leave an empty box on screen.
            if (turningOff && otherIsOff)
            {
                return;
            }

            if (isCpu)
            {
                _config.ShowCpu = !_config.ShowCpu;
            }
            else
            {
                _config.ShowGpu = !_config.ShowGpu;
            }

            CommitAppearance();
        };
        return item;
    }

    private void CommitAppearance()
    {
        _config.Save();
        _onAppearanceChanged();
    }

    private void RefreshCheckStates(ContextMenuStrip menu)
    {
        foreach (ToolStripItem item in menu.Items)
        {
            if (item is not ToolStripMenuItem parent)
            {
                continue;
            }

            bool isOpacityMenu = parent.Text == OpacityMenuText;

            foreach (ToolStripItem child in parent.DropDownItems)
            {
                if (child is not ToolStripMenuItem entry)
                {
                    continue;
                }

                entry.Checked = entry.Tag switch
                {
                    OverlayCorner corner => corner == _config.Corner,
                    double value when isOpacityMenu => Math.Abs(value - _config.Opacity) < 0.001,
                    double value => Math.Abs(value - _config.FontSize) < 0.001,
                    int interval => interval == _config.PollIntervalMs,
                    "cpu" => _config.ShowCpu,
                    "gpu" => _config.ShowGpu,
                    _ => entry.Checked
                };
            }
        }
    }

    /// <summary>
    /// Draws the tray icon at runtime so the project ships without binary assets.
    /// </summary>
    private Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // A thermometer: pale stem and bulb with a red column inside.
            using var casing = new SolidBrush(Color.FromArgb(230, 235, 235, 240));
            using var mercury = new SolidBrush(Color.FromArgb(255, 242, 118, 118));

            graphics.FillRectangle(casing, 12, 3, 8, 18);
            graphics.FillEllipse(casing, 8, 16, 16, 16);
            graphics.FillRectangle(mercury, 14, 9, 4, 13);
            graphics.FillEllipse(mercury, 11, 19, 10, 10);
        }

        _iconHandle = bitmap.GetHicon();
        return Icon.FromHandle(_iconHandle);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();

        if (_iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }
    }
}
