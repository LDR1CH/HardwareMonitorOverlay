using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace HardwareMonitorOverlay.Services;

/// <summary>
/// Reads CPU/GPU load and temperature.
///
/// CPU load comes from a performance counter (cheap, no driver needed); everything
/// else comes from LibreHardwareMonitor, which needs the elevated kernel driver.
/// Only the CPU and GPU hardware nodes are enabled and updated, so a poll touches
/// as few sensors as possible.
/// </summary>
public sealed class SensorService : IDisposable
{
    // Preferred sensor names, most specific first. Intel reports "CPU Package";
    // AMD reports "Core (Tctl/Tdie)". If neither exists we fall back to the
    // hottest per-core sensor.
    private static readonly string[] CpuTemperatureNames =
    {
        "CPU Package", "Core (Tctl/Tdie)", "Core (Tctl)", "CPU Cores", "Core Average", "CPU Die (average)"
    };

    private static readonly string[] GpuTemperatureNames = { "GPU Core", "GPU Temperature", "GPU Hot Spot" };

    private static readonly string[] GpuLoadNames = { "GPU Core", "GPU Utilization", "D3D 3D" };

    private readonly Computer _computer;
    private readonly List<IHardware> _cpuHardware = new();
    private readonly List<IHardware> _gpuHardware = new();
    private PerformanceCounter? _cpuLoadCounter;
    private bool _disposed;

    public SensorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = false,
            IsMotherboardEnabled = false,
            IsStorageEnabled = false,
            IsNetworkEnabled = false,
            IsControllerEnabled = false,
            IsPsuEnabled = false,
            IsBatteryEnabled = false
        };
    }

    /// <summary>True when the hardware layer opened successfully (i.e. we are elevated).</summary>
    public bool SensorsAvailable { get; private set; }

    public void Start()
    {
        try
        {
            _computer.Open();
            SensorsAvailable = true;
        }
        catch (Exception)
        {
            // Without elevation (or the driver) LibreHardwareMonitor throws here.
            // The overlay still runs and shows CPU load from the perf counter.
            SensorsAvailable = false;
        }

        if (SensorsAvailable)
        {
            foreach (IHardware hardware in _computer.Hardware)
            {
                switch (hardware.HardwareType)
                {
                    case HardwareType.Cpu:
                        _cpuHardware.Add(hardware);
                        break;
                    case HardwareType.GpuNvidia:
                        // Only the NVIDIA GPU is monitored. On systems with an
                        // integrated GPU alongside it (e.g. a laptop or desktop APU),
                        // including both mixed the idle iGPU's sensors in with the
                        // NVIDIA card's and could win the "first match" pick even
                        // while a game was rendering entirely on the NVIDIA GPU.
                        _gpuHardware.Add(hardware);
                        break;
                }
            }
        }

        _cpuLoadCounter = TryCreateCpuLoadCounter();
    }

    /// <summary>
    /// Takes one sample. Safe to call from a background thread; never throws.
    /// </summary>
    public HardwareReadings Read()
    {
        if (_disposed)
        {
            return HardwareReadings.Empty;
        }

        float? cpuLoad = ReadCpuLoadFromCounter();
        float? cpuTemperature = null;
        float? gpuLoad = null;
        float? gpuTemperature = null;

        if (SensorsAvailable)
        {
            try
            {
                foreach (IHardware hardware in _cpuHardware)
                {
                    hardware.Update();
                }

                foreach (IHardware hardware in _gpuHardware)
                {
                    hardware.Update();
                }

                cpuTemperature = PickSensor(_cpuHardware, SensorType.Temperature, CpuTemperatureNames, fallbackToMax: true);
                cpuLoad ??= PickSensor(_cpuHardware, SensorType.Load, new[] { "CPU Total" }, fallbackToMax: false);
                gpuTemperature = PickSensor(_gpuHardware, SensorType.Temperature, GpuTemperatureNames, fallbackToMax: true);
                gpuLoad = PickSensor(_gpuHardware, SensorType.Load, GpuLoadNames, fallbackToMax: false);
            }
            catch (Exception)
            {
                // A transient sensor read failure shouldn't kill the poll loop;
                // whatever we managed to gather this tick is reported as-is.
            }
        }

        return new HardwareReadings(cpuLoad, cpuTemperature, gpuLoad, gpuTemperature);
    }

    /// <summary>
    /// Finds the best sensor of a given type: the first match by preferred name,
    /// otherwise (for temperatures) the hottest sensor reported by the device.
    /// </summary>
    private static float? PickSensor(
        List<IHardware> hardwareList,
        SensorType type,
        IReadOnlyList<string> preferredNames,
        bool fallbackToMax)
    {
        List<ISensor> candidates = hardwareList
            .SelectMany(hardware => hardware.Sensors)
            .Where(sensor => sensor.SensorType == type && sensor.Value.HasValue)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        foreach (string name in preferredNames)
        {
            ISensor? match = candidates.FirstOrDefault(
                sensor => string.Equals(sensor.Name, name, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match.Value;
            }
        }

        return fallbackToMax ? candidates.Max(sensor => sensor.Value!.Value) : null;
    }

    private float? ReadCpuLoadFromCounter()
    {
        if (_cpuLoadCounter is null)
        {
            return null;
        }

        try
        {
            return Math.Clamp(_cpuLoadCounter.NextValue(), 0f, 100f);
        }
        catch (Exception)
        {
            // Corrupt perf-counter registries are common enough to be worth
            // surviving: drop the counter and let LibreHardwareMonitor cover it.
            _cpuLoadCounter.Dispose();
            _cpuLoadCounter = null;
            return null;
        }
    }

    private static PerformanceCounter? TryCreateCpuLoadCounter()
    {
        try
        {
            var counter = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);

            // The first sample of a rate counter is always 0; burn it at startup.
            counter.NextValue();
            return counter;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cpuLoadCounter?.Dispose();
        _cpuLoadCounter = null;

        try
        {
            if (SensorsAvailable)
            {
                _computer.Close();
            }
        }
        catch (Exception)
        {
            // Closing the driver handle can fail during shutdown; nothing to do.
        }
    }
}
