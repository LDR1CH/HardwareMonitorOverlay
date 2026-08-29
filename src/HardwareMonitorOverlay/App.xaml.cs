using System;
using System.Threading;
using System.Windows;
using HardwareMonitorOverlay.Configuration;
using HardwareMonitorOverlay.Services;
using HardwareMonitorOverlay.Tray;
using HardwareMonitorOverlay.ViewModels;

namespace HardwareMonitorOverlay;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Global\\HardwareMonitorOverlay.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private OverlayConfig _config = new();
    private OverlayViewModel _viewModel = new();
    private SensorService? _sensors;
    private SensorPoller? _poller;
    private TrayIconManager? _tray;
    private OverlayWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool isFirstInstance);

        if (!isFirstInstance)
        {
            // A second HUD would just draw on top of the first one.
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        _config = OverlayConfig.Load();

        _sensors = new SensorService();
        _sensors.Start();

        var window = new OverlayWindow(_config, _viewModel);
        _window = window;
        window.Show();

        // Gives the tray context menu the modern themed look.
        System.Windows.Forms.Application.EnableVisualStyles();

        _tray = new TrayIconManager(
            _config,
            _sensors.SensorsAvailable,
            onAppearanceChanged: window.ApplyConfig,
            onIntervalChanged: interval => _poller?.SetInterval(interval),
            onExit: Shutdown);

        _poller = new SensorPoller(_sensors, Dispatcher, OnSample);
        _poller.Start(_config.PollIntervalMs);

        if (!_sensors.SensorsAvailable)
        {
            MessageBox.Show(
                "Temperature sensors could not be opened.\n\n" +
                "Run the overlay as administrator so it can load the hardware " +
                "monitoring driver. CPU usage will still be shown without it.",
                "Hardware Monitor Overlay",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Runs on the UI thread once per poll. The view model discards samples that
    /// format to the same text, so an idle machine causes no repaints at all.
    /// </summary>
    private void OnSample(HardwareReadings readings)
    {
        _viewModel.Apply(readings);
        _window?.KeepOnTop();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _poller?.Dispose();
        _tray?.Dispose();
        _sensors?.Dispose();

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }
}
