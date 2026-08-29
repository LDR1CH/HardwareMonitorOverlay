using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HardwareMonitorOverlay.Configuration;

public enum OverlayCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

/// <summary>
/// User settings, persisted next to the user's profile so the elevated process
/// still writes somewhere the signed-in user owns.
/// </summary>
public sealed class OverlayConfig
{
    public OverlayCorner Corner { get; set; } = OverlayCorner.TopRight;

    /// <summary>Gap between the overlay and the work-area edge, in DIPs.</summary>
    public double MarginX { get; set; } = 12;

    public double MarginY { get; set; } = 12;

    /// <summary>Sensor poll interval. Clamped to 1000-10000 ms on load.</summary>
    public int PollIntervalMs { get; set; } = 1500;

    public double Opacity { get; set; } = 0.85;

    public double FontSize { get; set; } = 13;

    public bool ShowCpu { get; set; } = true;

    public bool ShowGpu { get; set; } = true;

    [JsonIgnore]
    public static string ConfigPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HardwareMonitorOverlay",
        "config.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static OverlayConfig Load()
    {
        OverlayConfig config;
        try
        {
            config = File.Exists(ConfigPath)
                ? JsonSerializer.Deserialize<OverlayConfig>(File.ReadAllText(ConfigPath), SerializerOptions) ?? new OverlayConfig()
                : new OverlayConfig();
        }
        catch (Exception)
        {
            // A corrupt or unreadable config should never stop the overlay from starting.
            config = new OverlayConfig();
        }

        config.Clamp();
        return config;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, SerializerOptions));
        }
        catch (Exception)
        {
            // Persisting preferences is best-effort.
        }
    }

    private void Clamp()
    {
        PollIntervalMs = Math.Clamp(PollIntervalMs, 1000, 10000);
        Opacity = Math.Clamp(Opacity, 0.2, 1.0);
        FontSize = Math.Clamp(FontSize, 8, 48);
        MarginX = Math.Clamp(MarginX, 0, 500);
        MarginY = Math.Clamp(MarginY, 0, 500);

        if (!ShowCpu && !ShowGpu)
        {
            ShowCpu = true;
        }
    }
}
