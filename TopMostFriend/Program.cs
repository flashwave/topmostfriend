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
        private static ContextMenuStrip CtxMenu;
        private static HotKeyWindow HotKeys;
        private static Icon OriginalIcon;

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
        public const string SHOW_WINDOW_LIST = @"ShowWindowList";

        private static ToolStripItem RefreshButton;
        private static ToolStripItem LastSelectedItem = null;

        private static readonly List<string> TitleBlacklist = new List<string>();

        private static ToolStripItem[] ListActionItems;
        private static ToolStripItem[] AppActionItems;

        [STAThread]
        public static void Main(string[] args) {
            if(Environment.OSVersion.Version.Major >= 6)
                Win32.SetProcessDPIAware();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if(args.Contains(@"--reset-admin"))
                Settings.Remove(ALWAYS_ADMIN_SETTING);

            string cliToggle = args.FirstOrDefault(x => x.StartsWith(@"--hwnd="));
            if(!string.IsNullOrEmpty(cliToggle) && int.TryParse(cliToggle.Substring(7), out int cliToggleHWnd)) {
                WindowInfo cliWindow = new WindowInfo(cliToggleHWnd);
                if(!cliWindow.ToggleTopMost())
                    TopMostFailed(cliWindow);
            }

            if(args.Contains(@"--stop"))
                return;

            if(!GlobalMutex.WaitOne(0, true)) {
                MessageBox.Show(@"An instance of Top Most Friend is already running.", @"Top Most Friend");
                return;
            }

            Settings.SetDefault(FOREGROUND_HOTKEY_SETTING, 0);
            Settings.SetDefault(ALWAYS_ADMIN_SETTING, false);
            Settings.SetDefault(SHIFT_CLICK_BLACKLIST, true);
            Settings.SetDefault(SHOW_HOTKEY_ICON, true);
            Settings.SetDefault(SHOW_WINDOW_LIST, true);
            // Defaulting to false on Windows 10 because it uses the stupid, annoying and intrusive new Android style notification system
            // This would fucking piledrive the notification history and also just be annoying in general because intrusive
            Settings.SetDefault(TOGGLE_BALLOON_SETTING, ToggleBalloonDefault);

            if(!Settings.Has(TITLE_BLACKLIST)) {
                List<string> titles = new List<string> { @"Program Manager" };

                if(Environment.OSVersion.Version.Major >= 10)
                    titles.Add(@"Windows Shell Experience Host");

                if(Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 2)
                    titles.Add(@"Start menu");
                else if(Environment.OSVersion.Version.Major > 6 || (Environment.OSVersion.Version.Major <= 6 && Environment.OSVersion.Version.Minor < 2))
                    titles.Add(@"Start");

                Settings.Set(TITLE_BLACKLIST, titles.ToArray());
            }

            if(Settings.Get<bool>(ALWAYS_ADMIN_SETTING) && !IsElevated()) {
                Elevate();
                return;
            }

            TitleBlacklist.Clear();
            string[] titleBlacklist = Settings.Get(TITLE_BLACKLIST);

            if(titleBlacklist != null)
                ApplyBlacklistedTitles(titleBlacklist);

            string backgroundPath = Settings.Get(LIST_BACKGROUND_PATH_SETTING, string.Empty);
            Image backgroundImage = null;
            ImageLayout backgroundLayout = 0;

            if(File.Exists(backgroundPath)) {
                try {
                    backgroundImage = Image.FromFile(backgroundPath);
                    backgroundLayout = (ImageLayout)Settings.Get(LIST_BACKGROUND_LAYOUT_SETTING, 0);
                } catch { }
            }

            OriginalIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            CtxMenu = new ContextMenuStrip {
                BackgroundImage = backgroundImage,
                BackgroundImageLayout = backgroundLayout,
            };
            CtxMenu.Closing += CtxMenu_Closing;
            CtxMenu.ItemClicked += CtxMenu_ItemClicked;
            ListActionItems = new ToolStripItem[] {
                new ToolStripSeparator(),
                RefreshButton = new ToolStripMenuItem(@"&Refresh", Properties.Resources.arrow_refresh, new EventHandler((s, e) => RefreshWindowList())),
            };
            AppActionItems = new ToolStripItem[] {
                new ToolStripMenuItem(@"&Settings", Properties.Resources.cog, new EventHandler((s, e) => SettingsWindow.Display())),
                new ToolStripMenuItem(@"&About", Properties.Resources.help, new EventHandler((s, e) => AboutWindow.Display())),
                new ToolStripMenuItem(@"&Quit", Properties.Resources.door_in, new EventHandler((s, e) => Application.Exit())),
            };
            CtxMenu.Items.AddRange(AppActionItems);

            SysIcon = new NotifyIcon {
                Visible = true,
                Icon = OriginalIcon,
                Text = @"Top Most Application Manager",
            };
            SysIcon.ContextMenuStrip = CtxMenu;
            SysIcon.MouseDown += SysIcon_MouseDown;

            HotKeys = new HotKeyWindow();
            SetForegroundHotKey(Settings.Get<int>(FOREGROUND_HOTKEY_SETTING));

            Application.Run();

            Shutdown();
        }

        public static void AddBlacklistedTitle(string title) {
            lock(TitleBlacklist)
                TitleBlacklist.Add(title);
        }
        public static void RemoveBlacklistedTitle(string title) {
            lock(TitleBlacklist)
                TitleBlacklist.RemoveAll(x => x == title);
        }
        public static void ApplyBlacklistedTitles(string[] arr) {
            lock(TitleBlacklist) {
                TitleBlacklist.Clear();
                TitleBlacklist.AddRange(arr);
            }
        }
        public static bool CheckBlacklistedTitles(string title) {
            lock(TitleBlacklist)
                return TitleBlacklist.Contains(title);
        }
        public static string[] GetBlacklistedTitles() {
            lock(TitleBlacklist)
                return TitleBlacklist.ToArray();
        }
        public static void SaveBlacklistedTitles() {
            lock(TitleBlacklist)
                Settings.Set(TITLE_BLACKLIST, TitleBlacklist.ToArray());
        }

        public static void Shutdown() {
            HotKeys?.Dispose();
            SysIcon?.Dispose();
            GlobalMutex.ReleaseMutex();
        }

        private static bool? IsElevatedValue;

        public static bool IsElevated() {
            if(!IsElevatedValue.HasValue) {
                using(WindowsIdentity identity = WindowsIdentity.GetCurrent())
                    IsElevatedValue = identity != null && new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }

            return IsElevatedValue.Value;
        }

        public static void Elevate(string args = null) {
            if(IsElevated())
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

                if(mods != 0 && key != 0)
                    HotKeys.Register(FOREGROUND_HOTKEY_ATOM, mods, key, ToggleForegroundWindow);
            } catch(Win32Exception ex) {
                Debug.WriteLine(@"Hotkey registration failed:");
                Debug.WriteLine(ex);
            }
        }

        private static void RefreshWindowList() {
            List<ToolStripItem> items = new List<ToolStripItem>();

            if(Settings.Get(SHOW_WINDOW_LIST, true)) {
                IEnumerable<WindowInfo> windows = WindowInfo.GetAllWindows();
                Process lastProc = null;
                bool procSeparator = Settings.Get(PROCESS_SEPARATOR_SETTING, false);
                bool showEmptyTitles = Settings.Get(SHOW_EMPTY_WINDOW_SETTING, false);
                bool listSelf = Settings.Get(LIST_SELF_SETTING, Debugger.IsAttached);

                foreach(WindowInfo window in windows) {
                    if(!listSelf && window.IsOwnWindow)
                        continue;

                    if(procSeparator && lastProc != window.Owner) {
                        if(lastProc != null)
                            items.Add(new ToolStripSeparator());
                        lastProc = window.Owner;
                    }

                    string title = window.Title;

                    // i think it's a fair assumption that any visible window worth a damn has a window title
                    if(!showEmptyTitles && string.IsNullOrEmpty(title))
                        continue;

                    // Skip items in the blacklist
                    if(CheckBlacklistedTitles(title))
                        continue;

                    items.Add(new ToolStripMenuItem(title, window.IconBitmap, new EventHandler((s, e) => {
                        if(Settings.Get(SHIFT_CLICK_BLACKLIST, true) && Control.ModifierKeys.HasFlag(Keys.Shift)) {
                            AddBlacklistedTitle(title);
                            SaveBlacklistedTitles();
                        } else if(!window.ToggleTopMost())
                            TopMostFailed(window);
                    })) {
                        CheckOnClick = true,
                        Checked = window.IsTopMost,
                    });
                }

                items.AddRange(ListActionItems);
            }

            items.AddRange(AppActionItems);

            CtxMenu.Items.Clear();
            CtxMenu.Items.AddRange(items.ToArray());
        }

        private static void TopMostFailed(WindowInfo window) {
            MessageBoxButtons buttons = MessageBoxButtons.OK;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@"Wasn't able to change topmost status on this window.");

            if(!IsElevated()) {
                sb.AppendLine(@"Do you want to restart Top Most Friend as administrator and try again?");
                buttons = MessageBoxButtons.YesNo;
            }

            DialogResult result = MessageBox.Show(sb.ToString(), @"Top Most Friend", buttons, MessageBoxIcon.Error);

            if(result == DialogResult.Yes)
                Elevate($@"--hwnd={window.Handle}");
        }

        private class ActionTimeout {
            private readonly Action Action;
            private bool Continue = true;
            private int Remaining = 0;
            private const int STEP = 500;

            public ActionTimeout(Action action, int timeout) {
                Action = action ?? throw new ArgumentNullException(nameof(action));
                if(timeout < 1)
                    throw new ArgumentException(@"Timeout must be a positive integer.", nameof(timeout));
                Remaining = timeout;
                new Thread(ThreadBody) { IsBackground = true }.Start();
            }

            private void ThreadBody() {
                do {
                    Thread.Sleep(STEP);
                    Remaining -= STEP;

                    if(!Continue)
                        return;
                } while(Remaining > 0);

                Action.Invoke();
            }

            public void Cancel() {
                Continue = false;
            }
        }

        private static ActionTimeout IconTimeout;

        public static void ToggleForegroundWindow() {
            WindowInfo window = WindowInfo.GetForegroundWindow();

            if(window.ToggleTopMost()) {
                if(Settings.Get(TOGGLE_BALLOON_SETTING, false)) {
                    string title = window.Title;
                    SysIcon?.ShowBalloonTip(
                        2000, window.IsTopMost ? @"Always on top" : @"No longer always on top",
                        string.IsNullOrEmpty(title) ? @"Window has no title." : title,
                        ToolTipIcon.Info
                    );
                }

                if(SysIcon != null && Settings.Get(SHOW_HOTKEY_ICON, true)) {
                    Icon icon = window.Icon;

                    if(icon != null) {
                        IconTimeout?.Cancel();
                        SysIcon.Icon = icon;
                        IconTimeout = new ActionTimeout(() => SysIcon.Icon = OriginalIcon, 2000);
                    }
                }
            } else
                TopMostFailed(window);
        }

        private static void CtxMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e) {
            LastSelectedItem = e.ClickedItem;
        }

        private static void CtxMenu_Closing(object sender, ToolStripDropDownClosingEventArgs e) {
            if(e.CloseReason == ToolStripDropDownCloseReason.ItemClicked && LastSelectedItem == RefreshButton)
                e.Cancel = true;
        }

        private static void SysIcon_MouseDown(object sender, MouseEventArgs e) {
            if((e.Button & MouseButtons.Right) > 0)
                RefreshWindowList();
        }
    }
}
