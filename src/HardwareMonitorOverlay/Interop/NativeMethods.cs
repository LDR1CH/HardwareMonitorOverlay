using System;
using System.Runtime.InteropServices;

namespace HardwareMonitorOverlay.Interop;

/// <summary>
/// The Win32 bits needed to make a WPF window behave like a HUD:
/// input-transparent, never activated, and reliably above other windows.
/// </summary>
internal static class NativeMethods
{
    internal const int GWL_EXSTYLE = -20;

    internal const int WS_EX_TRANSPARENT = 0x00000020;  // clicks fall through to whatever is underneath
    internal const int WS_EX_LAYERED = 0x00080000;      // required for per-window alpha + hit-test bypass
    internal const int WS_EX_TOOLWINDOW = 0x00000080;   // keeps the overlay out of Alt+Tab
    internal const int WS_EX_NOACTIVATE = 0x08000000;   // never steals focus

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    private static readonly IntPtr HWND_TOPMOST = new(-1);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    /// <summary>
    /// Adds the extended styles that make the window a click-through HUD.
    /// </summary>
    internal static void MakeClickThrough(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>
    /// Re-asserts topmost z-order without activating the window. Games and other
    /// full-screen apps routinely push themselves above existing topmost windows,
    /// so this is called on every poll tick to keep the HUD visible.
    /// </summary>
    internal static void ReassertTopmost(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }
}
