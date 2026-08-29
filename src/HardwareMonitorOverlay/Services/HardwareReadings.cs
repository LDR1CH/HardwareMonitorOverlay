namespace HardwareMonitorOverlay.Services;

/// <summary>
/// One sample of everything the overlay shows. Null means "sensor not available".
/// </summary>
public readonly record struct HardwareReadings(
    float? CpuLoad,
    float? CpuTemperature,
    float? GpuLoad,
    float? GpuTemperature)
{
    public static readonly HardwareReadings Empty = new(null, null, null, null);
}
