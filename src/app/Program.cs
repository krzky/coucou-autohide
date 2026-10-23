using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CoucouAutoHide
{
    internal static class Program
    {
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int L, T, R, B; }
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public int dwFlags; }

        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT p, uint f);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr m, ref MONITORINFO mi);
        [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr v);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint p);

        private delegate bool EnumProc(IntPtr h, IntPtr l);

        private const uint SWP_NOSIZE = 1, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10, SWP_NOOWNERZORDER = 0x200;
        private const int PEEK = 3;            // px left visible when hidden
        private const int ANIM_MS = 220;       // slide duration
        private const int HIDE_DELAY_MS = 700; // wait after the pointer leaves
        private const int TRIGGER_PX = 2;      // top-edge hot zone height

        private static IntPtr FindCoucou()
        {
            var pids = new HashSet<uint>();
            foreach (var p in Process.GetProcessesByName("coucou")) pids.Add((uint)p.Id);
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, l) =>
            {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (!pids.Contains(pid)) return true;
                var sb = new StringBuilder(64);
                GetWindowText(h, sb, 64);
                if (sb.ToString() == "Coucou") { found = h; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static RECT MonitorOf(int x, int y)
        {
            var p = new POINT { X = x, Y = y };
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            GetMonitorInfo(MonitorFromPoint(p, 2), ref mi);
            return mi.rcMonitor;
        }

        private static void MoveY(IntPtr h, int x, int y)
        {
            SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
        }

        private static void Animate(IntPtr h, int x, int from, int to)
        {
            var sw = Stopwatch.StartNew();
            while (true)
            {
                double t = Math.Min(1.0, sw.ElapsedMilliseconds / (double)ANIM_MS);
                double e = 1 - Math.Pow(1 - t, 3); // ease-out cubic
                MoveY(h, x, (int)Math.Round(from + (to - from) * e));
                if (t >= 1) break;
                Thread.Sleep(8);
            }
        }

        [STAThread]
        private static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, @"Global\CoucouAutoHide", out created))
            {
                if (!created) return;   // already running

                try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
                timeBeginPeriod(1);

                IntPtr h = IntPtr.Zero;
                bool hidden = false;
                int lastY = int.MinValue;
                long leftAt = -1;
                var clock = Stopwatch.StartNew();

                while (true)
                {
                    Thread.Sleep(30);
                    if (h == IntPtr.Zero || !IsWindow(h))
                    {
                        h = FindCoucou(); hidden = false; lastY = int.MinValue;
                        if (h == IntPtr.Zero) { Thread.Sleep(1000); continue; }
                    }
                    if (!IsWindowVisible(h) || IsIconic(h)) { hidden = false; lastY = int.MinValue; continue; }

                    RECT r;
                    GetWindowRect(h, out r);
                    int height = r.B - r.T;
                    var mon = MonitorOf((r.L + r.R) / 2, Math.Max(r.T, r.B - PEEK - 1));
                    int homeY = mon.T, hideY = mon.T - height + PEEK;

                    // Coucou moved itself (new message / re-shown): treat as shown.
                    if (hidden && r.T != hideY) hidden = false;
                    if (!hidden && lastY != int.MinValue && r.T != lastY) leftAt = -1;
                    // Only manage the overlay while it is docked at the top edge.
                    if (!hidden && r.T != homeY) { lastY = r.T; continue; }

                    POINT c;
                    GetCursorPos(out c);
                    bool overWin = c.X >= r.L && c.X < r.R && c.Y >= r.T && c.Y < r.B;
                    bool atEdge = c.X >= r.L && c.X < r.R && c.Y <= mon.T + TRIGGER_PX && c.Y >= mon.T;
                    bool focused = GetForegroundWindow() == h;

                    if (hidden)
                    {
                        if (atEdge || overWin || focused)
                        {
                            Animate(h, r.L, r.T, homeY);
                            hidden = false; leftAt = -1; lastY = homeY;
                        }
                    }
                    else
                    {
                        if (overWin || focused) { leftAt = -1; }
                        else
                        {
                            if (leftAt < 0) leftAt = clock.ElapsedMilliseconds;
                            else if (clock.ElapsedMilliseconds - leftAt >= HIDE_DELAY_MS)
                            {
                                Animate(h, r.L, r.T, hideY);
                                hidden = true; lastY = hideY; leftAt = -1;
                            }
                        }
                        if (!hidden) lastY = r.T;
                    }
                }
            }
        }
    }
}
