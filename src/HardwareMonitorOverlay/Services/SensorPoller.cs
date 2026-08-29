using System;
using System.Threading;
using System.Windows.Threading;

namespace HardwareMonitorOverlay.Services;

/// <summary>
/// Drives <see cref="SensorService"/> from a timer on the thread pool and hands each
/// sample to the UI thread. There is no render loop: the UI only changes when a
/// displayed value actually changes.
/// </summary>
public sealed class SensorPoller : IDisposable
{
    private readonly SensorService _sensors;
    private readonly Dispatcher _dispatcher;
    private readonly Action<HardwareReadings> _onSample;
    private readonly Timer _timer;
    private int _polling;
    private bool _disposed;

    public SensorPoller(
        SensorService sensors,
        Dispatcher dispatcher,
        Action<HardwareReadings> onSample)
    {
        _sensors = sensors;
        _dispatcher = dispatcher;
        _onSample = onSample;
        _timer = new Timer(Tick, state: null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start(int intervalMs)
    {
        // Fire once immediately so the HUD isn't blank for the first interval.
        _timer.Change(0, intervalMs);
    }

    public void SetInterval(int intervalMs)
    {
        if (!_disposed)
        {
            _timer.Change(intervalMs, intervalMs);
        }
    }

    private void Tick(object? state)
    {
        // A slow sensor read must not queue up overlapping polls behind it.
        if (Interlocked.Exchange(ref _polling, 1) == 1)
        {
            return;
        }

        try
        {
            HardwareReadings readings = _sensors.Read();

            if (!_disposed)
            {
                _dispatcher.InvokeAsync(() => _onSample(readings), DispatcherPriority.Background);
            }
        }
        catch (Exception)
        {
            // Never let a background exception tear down the process.
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Dispose();
    }
}
