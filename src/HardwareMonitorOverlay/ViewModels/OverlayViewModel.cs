using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using HardwareMonitorOverlay.Services;

namespace HardwareMonitorOverlay.ViewModels;

/// <summary>
/// Formats each sample into display strings and raises change notifications only
/// when the rendered text actually differs. Sub-degree sensor jitter therefore
/// costs nothing: no PropertyChanged, no layout, no repaint.
/// </summary>
public sealed class OverlayViewModel : INotifyPropertyChanged
{
    private const string NoValue = "--";
    private const string Degree = "°C";

    // Frozen brushes are shared, thread-safe and skip per-render allocation.
    private static readonly Brush CoolBrush = Freeze(Color.FromRgb(0x8B, 0xE9, 0xA8));
    private static readonly Brush WarmBrush = Freeze(Color.FromRgb(0xF5, 0xC8, 0x6B));
    private static readonly Brush HotBrush = Freeze(Color.FromRgb(0xF2, 0x76, 0x76));
    private static readonly Brush UnknownBrush = Freeze(Color.FromRgb(0x9A, 0x9A, 0x9A));

    private string _cpuLoadText = NoValue;
    private string _cpuTemperatureText = NoValue;
    private string _gpuLoadText = NoValue;
    private string _gpuTemperatureText = NoValue;
    private Brush _cpuTemperatureBrush = UnknownBrush;
    private Brush _gpuTemperatureBrush = UnknownBrush;
    private double _fontSize = 13;
    private bool _showCpu = true;
    private bool _showGpu = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CpuLoadText
    {
        get => _cpuLoadText;
        private set => Set(ref _cpuLoadText, value);
    }

    public string CpuTemperatureText
    {
        get => _cpuTemperatureText;
        private set => Set(ref _cpuTemperatureText, value);
    }

    public string GpuLoadText
    {
        get => _gpuLoadText;
        private set => Set(ref _gpuLoadText, value);
    }

    public string GpuTemperatureText
    {
        get => _gpuTemperatureText;
        private set => Set(ref _gpuTemperatureText, value);
    }

    public Brush CpuTemperatureBrush
    {
        get => _cpuTemperatureBrush;
        private set => Set(ref _cpuTemperatureBrush, value);
    }

    public Brush GpuTemperatureBrush
    {
        get => _gpuTemperatureBrush;
        private set => Set(ref _gpuTemperatureBrush, value);
    }

    public double FontSize
    {
        get => _fontSize;
        set => Set(ref _fontSize, value);
    }

    public bool ShowCpu
    {
        get => _showCpu;
        set => Set(ref _showCpu, value);
    }

    public bool ShowGpu
    {
        get => _showGpu;
        set => Set(ref _showGpu, value);
    }

    /// <summary>Applies a sample. Must be called on the UI thread.</summary>
    public void Apply(HardwareReadings readings)
    {
        CpuLoadText = FormatPercentage(readings.CpuLoad);
        CpuTemperatureText = FormatTemperature(readings.CpuTemperature);
        CpuTemperatureBrush = BrushForTemperature(readings.CpuTemperature);

        GpuLoadText = FormatPercentage(readings.GpuLoad);
        GpuTemperatureText = FormatTemperature(readings.GpuTemperature);
        GpuTemperatureBrush = BrushForTemperature(readings.GpuTemperature);
    }

    private static string FormatPercentage(float? value) => value.HasValue
        ? string.Concat(Math.Clamp((int)Math.Round(value.Value), 0, 100).ToString(CultureInfo.InvariantCulture), "%")
        : NoValue;

    private static string FormatTemperature(float? value) => value.HasValue
        ? string.Concat(((int)Math.Round(value.Value)).ToString(CultureInfo.InvariantCulture), Degree)
        : NoValue;

    private static Brush BrushForTemperature(float? value) => value switch
    {
        null => UnknownBrush,
        < 60f => CoolBrush,
        < 80f => WarmBrush,
        _ => HotBrush
    };

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
