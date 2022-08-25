using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TopMostFriend {
    public static class Program {
        private static NotifyIcon SysIcon;
        private static readonly Process OwnProcess = Process.GetCurrentProcess();
        private static int InitialItems = 0;

        [STAThread]
        public static void Main() {
            if (Environment.OSVersion.Version.Major >= 6)
                SetProcessDPIAware();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            SysIcon = new NotifyIcon {
                Visible = true,
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
                Text = @"Top Most Application Manager",
            };
            SysIcon.MouseDown += SysIcon_MouseDown;
            SysIcon.ContextMenuStrip = new ContextMenuStrip();
            SysIcon.ContextMenuStrip.Items.AddRange(new ToolStripItem[] {
                new ToolStripSeparator(),
                new ToolStripMenuItem(@"&About", Properties.Resources.help, new EventHandler((s, e) => AboutWindow.Display())),
                new ToolStripMenuItem(@"&Quit", Properties.Resources.door_in, new EventHandler((s, e) => Application.Exit())),
            });
            InitialItems = SysIcon.ContextMenuStrip.Items.Count;

            Application.Run();

            SysIcon.Dispose();
        }

        private static void RefreshWindowList() {
            while (SysIcon.ContextMenuStrip.Items.Count > InitialItems)
                SysIcon.ContextMenuStrip.Items.RemoveAt(0);

            IEnumerable<WindowEntry> windows = GetWindowList();

            foreach(WindowEntry window in windows) {
                string title = GetWindowTextLazy(window.Window);

                // i think it's a fair assumption that any visible window worth a damn has a window title
                if (string.IsNullOrEmpty(title))
                    continue;

                // skip explorer things with specific titles, there's probably a much better way of doing this check
                // and this will also probably only work properly on english windows but Fuck It what do you want from me
                if (window.Process.ProcessName == @"explorer" && (title == @"Program Manager" || title == @"Start"))
                    continue;

                IntPtr flags = GetWindowLongPtr(window.Window, GWL_EXSTYLE);
                bool isTopMost = (flags.ToInt32() & WS_EX_TOPMOST) > 0;

                Image icon = GetWindowIcon(window.Window).ToBitmap();

                SysIcon.ContextMenuStrip.Items.Insert(0, new ToolStripMenuItem(
                    title, icon, new EventHandler((s, e) => {
                    SetWindowPos(
                        window.Window, new IntPtr(isTopMost ? HWND_NOTOPMOST : HWND_TOPMOST),
                        0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW
                    );

                    if (!isTopMost)
                        SwitchToThisWindow(window.Window, false);
                })) {
                    CheckOnClick = true,
                    Checked = isTopMost,
                });
            }
        }

        private static Icon GetWindowIcon(IntPtr hWnd) {
            IntPtr hIcon = SendMessage(hWnd, WM_GETICON, ICON_SMALL2, 0);

            if(hIcon == IntPtr.Zero) {
                hIcon = SendMessage(hWnd, WM_GETICON, ICON_SMALL, 0);

                if(hIcon == IntPtr.Zero) {
                    hIcon = SendMessage(hWnd, WM_GETICON, ICON_BIG, 0);

                    if(hIcon == IntPtr.Zero) {
                        hIcon = GetClassLongPtr(hWnd, GCL_HICON);

                        if (hIcon == IntPtr.Zero)
                            hIcon = GetClassLongPtr(hWnd, GCL_HICONSM);
                    }
                }
            }

            return hIcon == IntPtr.Zero ? null : Icon.FromHandle(hIcon);
        }

        private static IEnumerable<WindowEntry> GetWindowList() {
            Process[] procs = Process.GetProcesses();

            foreach (Process proc in procs) {
                if (proc.Id == OwnProcess.Id)
                    continue;

                IEnumerable<IntPtr> hwnds = proc.GetWindowHandles();

                foreach (IntPtr ptr in hwnds) {
                    if (!IsWindowVisible(ptr))
                        continue;

                    yield return new WindowEntry(proc, ptr);
                }
            }
        }

        private class WindowEntry {
            public Process Process;
            public IntPtr Window;

            public WindowEntry(Process proc, IntPtr win) {
                Process = proc;
                Window = win;
            }
        }

        private static void SysIcon_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button != MouseButtons.Right)
                return;

            RefreshWindowList();
        }

        public static IEnumerable<IntPtr> GetWindowHandles(this Process proc) {
            IntPtr hwndCurr = IntPtr.Zero;

            do {
                hwndCurr = FindWindowEx(IntPtr.Zero, hwndCurr, null, null);
                GetWindowThreadProcessId(hwndCurr, out uint procId);

                if(proc.Id == procId)
                    yield return hwndCurr;
            } while (hwndCurr != IntPtr.Zero);
        }

        private const int HWND_TOPMOST = -1;
        private const int HWND_NOTOPMOST = -2;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_SHOWWINDOW = 0x0040;
        private const int GWL_EXSTYLE = -20;
        private const int GCL_HICON = -14;
        private const int GCL_HICONSM = -34;
        private const int WS_EX_TOPMOST = 0x08;
        private const int WM_GETICON = 0x7F;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int ICON_SMALL2 = 2;

        [DllImport(@"user32")]
        private static extern bool SetProcessDPIAware();

        [DllImport(@"user32", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport(@"user32", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            return new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        [DllImport(@"user32", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport(@"user32", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport(@"user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport(@"user32", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport(@"user32", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        private static string GetWindowTextLazy(IntPtr hwnd) {
            int length = GetWindowTextLength(hwnd) + 1;
            StringBuilder sb = new StringBuilder(length);
            GetWindowText(hwnd, sb, length);
            return sb.ToString();
        }

        private static IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex) {
            if (IntPtr.Size > 4)
                return GetClassLongPtr64(hWnd, nIndex);
            return new IntPtr(GetClassLongPtr32(hWnd, nIndex));
        }

        [DllImport(@"user32", EntryPoint = "GetClassLong")]
        private static extern uint GetClassLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport(@"user32", EntryPoint = "GetClassLongPtr")]
        private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport(@"user32", SetLastError = true)]
        private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport(@"user32", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int uFlags);

        [DllImport(@"user32", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport(@"user32")]
        public static extern bool ReleaseCapture();
    }
}
