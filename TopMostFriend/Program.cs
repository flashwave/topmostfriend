using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TopMostFriend {
    public static class Program {
        private static NotifyIcon SysIcon;
        private static ContextMenuStrip CtxMenu;
        private static HotKeyWindow HotKeys;
        private static Icon OriginalIcon;

        public const string TITLE = @"Top Most Friend";

        private const string GUID =
#if DEBUG
            @"{1A22D9CA-2AA9-48F2-B007-3A48CF205CDD}";
#else
            @"{5BE25191-E1E2-48A7-B038-E986CD989E91}";
#endif
        private static readonly Mutex GlobalMutex = new Mutex(true, GUID);

        private const string CUSTOM_LANGUAGE = @"TopMostFriendLanguage.xml";

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
        public const string LAST_VERSION = @"LastVersion";
        public const string ALWAYS_RETRY_ELEVATED = @"AlwaysRetryElevated";
        public const string REVERT_ON_EXIT = @"RevertOnExit";
        public const string LANGUAGE = @"Language";

        private static ToolStripItem RefreshButton;
        private static ToolStripItem LastSelectedItem = null;

        private static readonly List<string> TitleBlacklist = new List<string>();

        private static ToolStripItem[] ListActionItems;
        private static ToolStripItem[] AppActionItems;

        private static readonly Dictionary<IntPtr, bool> OriginalStates = new Dictionary<IntPtr, bool>();

        [STAThread]
        public static int Main(string[] args) {
            Settings.Set(LAST_VERSION, Application.ProductVersion);

            IEnumerable<string> cliToggleNew = args.Where(a => a.StartsWith(@"--toggle=")).Select(a => a.Substring(9));
            if(cliToggleNew.Any()) {
                bool is32bit = IntPtr.Size == 4;

                foreach(string hwndStr in cliToggleNew) {
                    IntPtr hwnd;
                    if(is32bit) {
                        if(int.TryParse(hwndStr, out int hwnd32))
                            hwnd = new IntPtr(hwnd32);
                        else
                            return 1;
                    } else {
                        if(long.TryParse(hwndStr, out long hwnd64))
                            hwnd = new IntPtr(hwnd64);
                        else
                            return 1;
                    }

                    // pass 0 to skip implicit FindOwnerId call
                    WindowInfo wi = new WindowInfo(hwnd, 0);
                    if(!wi.ToggleTopMost(!args.Contains($@"--background={hwndStr}")))
                        return 2;
                }

                return 0;
            }

            if(Environment.OSVersion.Version.Major >= 6)
                Win32.SetProcessDPIAware();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if(args.Contains(@"--reset-admin"))
                Settings.Remove(ALWAYS_ADMIN_SETTING);

            string cliToggleOld = args.FirstOrDefault(a => a.StartsWith(@"--hwnd="))?.Substring(7);
            if(!string.IsNullOrEmpty(cliToggleOld)) {
                IntPtr cliToggleHWnd = IntPtr.Zero;
                if(IntPtr.Size == 4) {
                    if(int.TryParse(cliToggleOld, out int hwnd32))
                        cliToggleHWnd = new IntPtr(hwnd32);
                } else {
                    if(long.TryParse(cliToggleOld, out long hwnd64))
                        cliToggleHWnd = new IntPtr(hwnd64);
                }

                if(cliToggleHWnd != IntPtr.Zero) {
                    WindowInfo cliWindow = new WindowInfo(cliToggleHWnd);
                    if(!cliWindow.ToggleTopMost())
                        TopMostFailed(cliWindow);
                }
            }

            if(args.Contains(@"--stop"))
                return 0;

            if(File.Exists(CUSTOM_LANGUAGE)) {
                string customLanguage;
                using(Stream s = File.OpenRead(CUSTOM_LANGUAGE))
                    try {
                        customLanguage = Locale.LoadLanguage(s);
                    } catch {
                        customLanguage = Locale.DEFAULT;
                    }
                Locale.SetLanguage(customLanguage);
            } else
                Locale.SetLanguage(Locale.GetPreferredLanguage());

            if(!GlobalMutex.WaitOne(0, true)) {
                MessageBox.Show(Locale.String(@"AlreadyRunning"), TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return -1;
            }

            bool isFirstRun = !Settings.Has(FOREGROUND_HOTKEY_SETTING);

            Settings.SetDefault(FOREGROUND_HOTKEY_SETTING, 0);
            Settings.SetDefault(ALWAYS_ADMIN_SETTING, false);
            Settings.SetDefault(SHIFT_CLICK_BLACKLIST, true);
            Settings.SetDefault(SHOW_HOTKEY_ICON, true);
            Settings.SetDefault(SHOW_WINDOW_LIST, true);
            Settings.SetDefault(ALWAYS_RETRY_ELEVATED, false);
            Settings.SetDefault(REVERT_ON_EXIT, false);
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

            if(Settings.Get<bool>(ALWAYS_ADMIN_SETTING) && !UAC.IsElevated) {
                UAC.RestartElevated();
                return -2;
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
                RefreshButton = new ToolStripMenuItem(Locale.String(@"TrayRefresh"), Properties.Resources.arrow_refresh, new EventHandler((s, e) => RefreshWindowList())),
            };
            AppActionItems = new ToolStripItem[] {
                new ToolStripMenuItem(Locale.String(@"TraySettings"), Properties.Resources.cog, new EventHandler((s, e) => SettingsWindow.Display())),
                new ToolStripMenuItem(Locale.String(@"TrayAbout"), Properties.Resources.help, new EventHandler((s, e) => AboutWindow.Display())),
                new ToolStripMenuItem(Locale.String(@"TrayQuit"), Properties.Resources.door_in, new EventHandler((s, e) => Application.Exit())),
            };
            CtxMenu.Items.AddRange(AppActionItems);

            SysIcon = new NotifyIcon {
                Visible = true,
                Icon = OriginalIcon,
                Text = TITLE,
            };
            SysIcon.ContextMenuStrip = CtxMenu;
            SysIcon.MouseDown += SysIcon_MouseDown;

            HotKeys = new HotKeyWindow();
            SetForegroundHotKey(Settings.Get<int>(FOREGROUND_HOTKEY_SETTING));

            if(isFirstRun)
                FirstRunWindow.Display();

            Application.Run();

            if(Settings.Get(REVERT_ON_EXIT, false))
                RevertTopMostStatus();

            Shutdown();

            return 0;
        }

        public static void RevertTopMostStatus() {
            foreach(KeyValuePair<IntPtr, bool> originalState in OriginalStates) {
                WindowInfo wi = new WindowInfo(originalState.Key);
                if(wi.IsTopMost != originalState.Value)
                    if(!wi.ToggleTopMost(false))
                        TopMostFailed(wi, false);
            }
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
                        } else {
                            if(window.ToggleTopMost()) {
                                if(!OriginalStates.ContainsKey(window.Handle))
                                    OriginalStates[window.Handle] = !window.IsTopMost;
                            } else
                                TopMostFailed(window);
                        }
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

        private static void TopMostFailed(WindowInfo window, bool switchWindow = true) {
            bool retryElevated = Settings.Get(ALWAYS_RETRY_ELEVATED, false),
                isElevated = UAC.IsElevated;

            if(!retryElevated) {
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(Locale.String(@"ErrUnableAlterStatus1"));

                if(!isElevated) {
                    sb.AppendLine(Locale.String(@"ErrUnableAlterStatus2"));
                    buttons = MessageBoxButtons.YesNo;
                }

                retryElevated = MessageBox.Show(sb.ToString(), TITLE, buttons, MessageBoxIcon.Error) == DialogResult.Yes;
            }

            if(retryElevated) {
                if(window.ToggleTopMostElevated(switchWindow)) {
                    if(!OriginalStates.ContainsKey(window.Handle))
                        OriginalStates[window.Handle] = !window.IsTopMost;
                } else
                    MessageBox.Show(Locale.String(@"ErrUnableAlterStatusProtected"), TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static ActionTimeout IconTimeout;

        public static void ToggleForegroundWindow() {
            WindowInfo window = WindowInfo.GetForegroundWindow();

            if(window.ToggleTopMost()) {
                if(Settings.Get(TOGGLE_BALLOON_SETTING, false)) {
                    string title = window.Title;
                    SysIcon?.ShowBalloonTip(
                        2000, Locale.String(window.IsTopMost ? @"NotifyOnTop" : @"NotifyNoLonger"),
                        string.IsNullOrEmpty(title) ? Locale.String(@"NotifyNoTitle") : title,
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
