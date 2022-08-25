using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TopMostFriend {
    public static class Program {
        private static NotifyIcon SysIcon;
        private static HotKeyWindow HotKeys;
        private static Icon OriginalIcon;
        private static int InitialItems = 0;

        private const string GUID =
#if DEBUG
            @"{1A22D9CA-2AA9-48F2-B007-3A48CF205CDD}";
#else
            @"{5BE25191-E1E2-48A7-B038-E986CD989E91}";
#endif
        private static readonly Mutex GlobalMutex = new Mutex(true, GUID);

        public const string FOREGROUND_HOTKEY_ATOM = @"{86795D64-770D-4BD6-AA26-FA638FBAABCF}";
#if DEBUG
        public const string FOREGROUND_HOTKEY_SETTING = @"ForegroundHotKey_DEBUG";
#else
        public const string FOREGROUND_HOTKEY_SETTING = @"ForegroundHotKey";
#endif

        public const string PROCESS_SEPARATOR_SETTING = @"InsertProcessSeparator";
        public const string LIST_SELF_SETTING = @"ListSelf";
        public const string SHOW_EMPTY_WINDOW_SETTING = @"ShowEmptyWindowTitles";
        public const string LIST_BACKGROUND_PATH_SETTING = @"ListBackgroundPath";
        public const string LIST_BACKGROUND_LAYOUT_SETTING = @"ListBackgroundLayout";
        public const string ALWAYS_ADMIN_SETTING = @"RunAsAdministrator";
        public const string TOGGLE_BALLOON_SETTING = @"ShowNotificationOnHotKey";
        public static readonly bool ToggleBalloonDefault = Environment.OSVersion.Version.Major < 10;
        public const string SHIFT_CLICK_BLACKLIST = @"ShiftClickToBlacklist";
        public const string TITLE_BLACKLIST = @"TitleBlacklist";
        public const string SHOW_HOTKEY_ICON = @"ShowHotkeyIcon";

        private static readonly List<string> TitleBlacklist = new List<string>();

        [STAThread]
        public static void Main(string[] args) {
            if (Environment.OSVersion.Version.Major >= 6)
                Win32.SetProcessDPIAware();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Contains(@"--reset-admin"))
                Settings.Remove(ALWAYS_ADMIN_SETTING);

            string cliToggle = args.FirstOrDefault(x => x.StartsWith(@"--hwnd="));
            if (!string.IsNullOrEmpty(cliToggle) && int.TryParse(cliToggle.Substring(7), out int cliToggleHWnd))
                ToggleWindow(new IntPtr(cliToggleHWnd));

            if (args.Contains(@"--stop"))
                return;

            if (!GlobalMutex.WaitOne(0, true)) {
                MessageBox.Show(@"An instance of Top Most Friend is already running.", @"Top Most Friend");
                return;
            }

            Settings.SetDefault(FOREGROUND_HOTKEY_SETTING, 0);
            Settings.SetDefault(ALWAYS_ADMIN_SETTING, false);
            Settings.SetDefault(SHIFT_CLICK_BLACKLIST, true);
            Settings.SetDefault(SHOW_HOTKEY_ICON, true);
            // Defaulting to false on Windows 10 because it uses the stupid, annoying and intrusive new Android style notification system
            // This would fucking piledrive the notification history and also just be annoying in general because intrusive
            Settings.SetDefault(TOGGLE_BALLOON_SETTING, ToggleBalloonDefault);

            if(!Settings.Has(TITLE_BLACKLIST)) {
                List<string> titles = new List<string> { @"Program Manager" };

                if(Environment.OSVersion.Version.Major >= 10)
                    titles.Add(@"Windows Shell Experience Host");

                if (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 2)
                    titles.Add(@"Start menu");
                else if(Environment.OSVersion.Version.Major > 6 || (Environment.OSVersion.Version.Major <= 6 && Environment.OSVersion.Version.Minor < 2))
                    titles.Add(@"Start");

                Settings.Set(TITLE_BLACKLIST, titles.ToArray());
            }

            if (Settings.Get<bool>(ALWAYS_ADMIN_SETTING) && !IsElevated()) {
                Elevate();
                return;
            }

            TitleBlacklist.Clear();
            string[] titleBlacklist = Settings.Get(TITLE_BLACKLIST);

            if (titleBlacklist != null)
                ApplyBlacklistedTitles(titleBlacklist);

            string backgroundPath = Settings.Get(LIST_BACKGROUND_PATH_SETTING, string.Empty);
            Image backgroundImage = null;
            ImageLayout backgroundLayout = 0;

            if(File.Exists(backgroundPath)) {
                try {
                    backgroundImage = Image.FromFile(backgroundPath);
                    backgroundLayout = (ImageLayout)Settings.Get(LIST_BACKGROUND_LAYOUT_SETTING, 0);
                } catch {}
            }

            OriginalIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            SysIcon = new NotifyIcon {
                Visible = true,
                Icon = OriginalIcon,
                Text = @"Top Most Application Manager",
            };
            SysIcon.MouseDown += SysIcon_MouseDown;
            SysIcon.ContextMenuStrip = new ContextMenuStrip {
                BackgroundImage = backgroundImage,
                BackgroundImageLayout = backgroundLayout,
            };
            SysIcon.ContextMenuStrip.Items.AddRange(new ToolStripItem[] {
                new ToolStripSeparator(),
                new ToolStripMenuItem(@"&Settings", Properties.Resources.cog, new EventHandler((s, e) => SettingsWindow.Display())),
                new ToolStripMenuItem(@"&About", Properties.Resources.help, new EventHandler((s, e) => AboutWindow.Display())),
                new ToolStripMenuItem(@"&Quit", Properties.Resources.door_in, new EventHandler((s, e) => Application.Exit())),
            });
            InitialItems = SysIcon.ContextMenuStrip.Items.Count;

            HotKeys = new HotKeyWindow();

            try {
                SetForegroundHotKey(Settings.Get<int>(FOREGROUND_HOTKEY_SETTING));
            } catch(Win32Exception ex) {
                Console.WriteLine(@"Hotkey registration failed:");
                Console.WriteLine(ex);
            }

            Application.Run();

            Shutdown();
        }

        public static void AddBlacklistedTitle(string title) {
            lock (TitleBlacklist)
                TitleBlacklist.Add(title);
        }
        public static void RemoveBlacklistedTitle(string title) {
            lock (TitleBlacklist)
                TitleBlacklist.RemoveAll(x => x == title);
        }
        public static void ApplyBlacklistedTitles(string[] arr) {
            lock (TitleBlacklist) {
                TitleBlacklist.Clear();
                TitleBlacklist.AddRange(arr);
            }
        }
        public static bool CheckBlacklistedTitles(string title) {
            lock (TitleBlacklist)
                return TitleBlacklist.Contains(title);
        }
        public static string[] GetBlacklistedTitles() {
            lock (TitleBlacklist)
                return TitleBlacklist.ToArray();
        }
        public static void SaveBlacklistedTitles() {
            lock (TitleBlacklist)
                Settings.Set(TITLE_BLACKLIST, TitleBlacklist.ToArray());
        }

        public static void Shutdown() {
            HotKeys?.Dispose();
            SysIcon?.Dispose();
            GlobalMutex.ReleaseMutex();
        }

        private static bool? IsElevatedValue;

        public static bool IsElevated() {
            if (!IsElevatedValue.HasValue) {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                    IsElevatedValue = identity != null && new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }

            return IsElevatedValue.Value;
        }

        public static void Elevate(string args = null) {
            if (IsElevated())
                return;

            Shutdown();

            Process.Start(new ProcessStartInfo {
                UseShellExecute = true,
                FileName = Application.ExecutablePath,
                WorkingDirectory = Environment.CurrentDirectory,
                Arguments = args ?? string.Empty,
                Verb = @"runas",
            });
            Application.Exit();
        }

        public static void SetForegroundHotKey(int keyCode) {
            SetForegroundHotKey((Win32ModKeys)(keyCode & 0xFFFF), (Keys)((keyCode & 0xFFFF0000) >> 16));
        }

        public static void SetForegroundHotKey(Win32ModKeys mods, Keys key) {
            try {
                Settings.Set(FOREGROUND_HOTKEY_SETTING, ((int)key << 16) | (int)mods);
                HotKeys.Unregister(FOREGROUND_HOTKEY_ATOM);

                if (mods != 0 && key != 0)
                    HotKeys.Register(FOREGROUND_HOTKEY_ATOM, mods, key, ToggleForegroundWindow);
            } catch (Win32Exception ex) {
                Debug.WriteLine(@"Hotkey registration failed:");
                Debug.WriteLine(ex);
            }
        }

        private static void RefreshWindowList() {
            while (SysIcon.ContextMenuStrip.Items.Count > InitialItems)
                SysIcon.ContextMenuStrip.Items.RemoveAt(0);

            IEnumerable<WindowEntry> windows = GetWindowList();
            Process lastProc = null;
            bool procSeparator = Settings.Get(PROCESS_SEPARATOR_SETTING, false);
            bool showEmptyTitles = Settings.Get(SHOW_EMPTY_WINDOW_SETTING, false);
            bool shiftClickBlacklist = Settings.Get(SHIFT_CLICK_BLACKLIST, true);

            foreach(WindowEntry window in windows) {
                if(procSeparator && lastProc != window.Process) {
                    if (lastProc != null)
                        SysIcon.ContextMenuStrip.Items.Insert(0, new ToolStripSeparator());
                    lastProc = window.Process;
                }

                string title = Win32.GetWindowTextString(window.Window);
                
                // i think it's a fair assumption that any visible window worth a damn has a window title
                if (!showEmptyTitles && string.IsNullOrEmpty(title))
                    continue;

                // Skip items in the blacklist
                if (CheckBlacklistedTitles(title))
                    continue;

                Image icon = GetWindowIcon(window.Window)?.ToBitmap() ?? null;
                bool isTopMost = IsTopMost(window.Window);

                SysIcon.ContextMenuStrip.Items.Insert(0, new ToolStripMenuItem(
                    title, icon,
                    new EventHandler((s, e) => {
                        if (shiftClickBlacklist && Control.ModifierKeys.HasFlag(Keys.Shift)) {
                            AddBlacklistedTitle(title);
                            SaveBlacklistedTitles();
                        } else
                            SetTopMost(window.Window, !isTopMost);
                    })
                ) {
                    CheckOnClick = true,
                    Checked = isTopMost,
                });
            }
        }

        public static bool IsTopMost(IntPtr hWnd) {
            IntPtr flags = Win32.GetWindowLongPtr(hWnd, Win32.GWL_EXSTYLE);
            return (flags.ToInt32() & Win32.WS_EX_TOPMOST) > 0;
        }

        private class ActionTimeout {
            private readonly Action Action;
            private bool Continue = true;
            private int Remaining = 0;
            private const int STEP = 500;

            public ActionTimeout(Action action, int timeout) {
                Action = action ?? throw new ArgumentNullException(nameof(action));
                if (timeout < 1)
                    throw new ArgumentException(@"Timeout must be a positive integer.", nameof(timeout));
                Remaining = timeout;
                new Thread(ThreadBody) { IsBackground = true }.Start();
            }

            private void ThreadBody() {
                do {
                    Thread.Sleep(STEP);
                    Remaining -= STEP;

                    if (!Continue)
                        return;
                } while (Remaining > 0);

                Action.Invoke();
            }

            public void Cancel() {
                Continue = false;
            }
        }

        public static bool SetTopMost(IntPtr hWnd, bool state) {
            Win32.SetWindowPos(
                hWnd, new IntPtr(state ? Win32.HWND_TOPMOST : Win32.HWND_NOTOPMOST),
                0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_SHOWWINDOW
            );

            if(IsTopMost(hWnd) != state) {
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"Wasn't able to change topmost status on this window.");

                if (!IsElevated()) {
                    sb.AppendLine(@"Do you want to restart Top Most Friend as administrator and try again?");
                    buttons = MessageBoxButtons.YesNo;
                }

                DialogResult result = MessageBox.Show(sb.ToString(), @"Top Most Friend", buttons, MessageBoxIcon.Error);

                if (result == DialogResult.Yes)
                    Elevate($@"--hwnd={hWnd}");
                return false;
            }

            if (state)
                Win32.SwitchToThisWindow(hWnd, false);

            return true;
        }

        private static ActionTimeout IconTimeout;

        public static void ToggleForegroundWindow() {
            IntPtr hWnd = Win32.GetForegroundWindow();

            if (ToggleWindow(hWnd)) {
                if (Settings.Get(TOGGLE_BALLOON_SETTING, false)) {
                    string title = Win32.GetWindowTextString(hWnd);

                    SysIcon?.ShowBalloonTip(
                        2000, IsTopMost(hWnd) ? @"Always on top" : @"No longer always on top",
                        string.IsNullOrEmpty(title) ? @"Window has no title." : title,
                        ToolTipIcon.Info
                    );
                }

                if (SysIcon != null && Settings.Get(SHOW_HOTKEY_ICON, true)) {
                    Icon icon = GetWindowIcon(hWnd);

                    if (icon != null) {
                        IconTimeout?.Cancel();
                        SysIcon.Icon = icon;
                        IconTimeout = new ActionTimeout(() => SysIcon.Icon = OriginalIcon, 2000);
                    }
                }
            }
        }

        public static bool ToggleWindow(IntPtr hWnd) {
            return SetTopMost(hWnd, !IsTopMost(hWnd));
        }

        private static Icon GetWindowIcon(IntPtr hWnd) {
            IntPtr hIcon = Win32.SendMessage(hWnd, Win32.WM_GETICON, Win32.ICON_SMALL2, 0);

            if(hIcon == IntPtr.Zero) {
                hIcon = Win32.SendMessage(hWnd, Win32.WM_GETICON, Win32.ICON_SMALL, 0);

                if(hIcon == IntPtr.Zero) {
                    hIcon = Win32.SendMessage(hWnd, Win32.WM_GETICON, Win32.ICON_BIG, 0);

                    if(hIcon == IntPtr.Zero) {
                        hIcon = Win32.GetClassLongPtr(hWnd, Win32.GCL_HICON);

                        if (hIcon == IntPtr.Zero)
                            hIcon = Win32.GetClassLongPtr(hWnd, Win32.GCL_HICONSM);
                    }
                }
            }

            return hIcon == IntPtr.Zero ? null : Icon.FromHandle(hIcon);
        }

        private static IEnumerable<WindowEntry> GetWindowList() {
            Process[] procs = Process.GetProcesses();
            Process self = Process.GetCurrentProcess();

            foreach (Process proc in procs) {
                if (!Settings.Get(LIST_SELF_SETTING, Debugger.IsAttached) && proc == self)
                    continue;

                IEnumerable<IntPtr> hwnds = proc.GetWindowHandles();

                foreach (IntPtr ptr in hwnds) {
                    if (!Win32.IsWindowVisible(ptr))
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
            if (e.Button.HasFlag(MouseButtons.Right))
                RefreshWindowList();
        }

        public static IEnumerable<IntPtr> GetWindowHandles(this Process proc) {
            IntPtr hwndCurr = IntPtr.Zero;

            do {
                hwndCurr = Win32.FindWindowEx(IntPtr.Zero, hwndCurr, null, null);
                Win32.GetWindowThreadProcessId(hwndCurr, out uint procId);
                
                if(proc.Id == procId)
                    yield return hwndCurr;
            } while (hwndCurr != IntPtr.Zero);
        }
    }
}
