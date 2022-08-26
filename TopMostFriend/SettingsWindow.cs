using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using TopMostFriend.Languages;

namespace TopMostFriend {
    public class SettingsWindow : Form {
        public static SettingsWindow Instance;

        public static void Display() {
            if(Instance != null) {
                Instance.Show();
                return;
            }

            Instance = new SettingsWindow();
            Instance.Show();
        }

        public int KeyCode { get; set; }

        public readonly TextBox FgKey;
        public readonly CheckBox FgModCtrl;
        public readonly CheckBox FgModAlt;
        public readonly CheckBox FgModShift;
        public readonly CheckBox FgModWindows;

        public readonly CheckBox FlAlwaysAdmin;
        public readonly CheckBox FlToggleNotification;
        public readonly CheckBox FlShiftClickBlacklist;
        public readonly CheckBox FlShowHotkeyIcon;
        public readonly CheckBox FlShowWindowList;
        public readonly CheckBox FlAlwaysRetryAsAdmin;
        public readonly CheckBox FlRevertOnExit;

        public readonly ComboBox LangSelect;

        public SettingsWindow() {
            Text = Locale.String(@"SettingsTitle");
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(430, 407);
            MinimizeBox = MaximizeBox = false;
            TopMost = true;

            KeyCode = Settings.Get(Program.FOREGROUND_HOTKEY_SETTING, 0);

            Button applyButton = new Button {
                Text = Locale.String(@"SettingsApply"),
                Size = new Size(75, 23),
                Location = new Point(ClientSize.Width - 81, ClientSize.Height - 30),
                TabIndex = 10003,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };
            applyButton.Click += ApplyButton_Click;
            Button cancelButton = new Button {
                Text = Locale.String(@"SettingsCancel"),
                Size = applyButton.Size,
                Location = new Point(ClientSize.Width - 162, applyButton.Location.Y),
                TabIndex = 10002,
                Anchor = applyButton.Anchor,
            };
            cancelButton.Click += CancelButton_Click;
            Button okButton = new Button {
                Text = Locale.String(@"SettingsOk"),
                Size = applyButton.Size,
                Location = new Point(ClientSize.Width - 243, applyButton.Location.Y),
                TabIndex = 10001,
                Anchor = applyButton.Anchor,
            };
            okButton.Click += OkButton_Click;

            GroupBox hotKeyGroup = new GroupBox {
                Text = Locale.String(@"SettingsHotKeysTitle"),
                Location = new Point(6, 6),
                Size = new Size(Width - 18, 70),
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
            };
            GroupBox flagsGroup = new GroupBox {
                Text = Locale.String(@"SettingsOptionsTitle"),
                Location = new Point(6, 80),
                Size = new Size(Width - 18, 170),
                Anchor = hotKeyGroup.Anchor,
            };
            GroupBox langGroup = new GroupBox {
                Text = Locale.String(@"SettingsLanguageTitle"),
                Location = new Point(6, 254),
                Size = new Size(Width - 18, 55),
                Anchor = hotKeyGroup.Anchor,
            };
            GroupBox otherGroup = new GroupBox {
                Text = Locale.String(@"SettingsOtherTitle"),
                Location = new Point(6, 313),
                Size = new Size(Width - 18, 55),
                Anchor = hotKeyGroup.Anchor,
            };

            Controls.AddRange(new Control[] {
                applyButton, cancelButton, okButton,
                hotKeyGroup, flagsGroup, langGroup, otherGroup,
            });

            hotKeyGroup.Controls.Add(new Label {
                AutoSize = true,
                Text = Locale.String(@"SettingsHotKeysToggle"),
                Location = new Point(8, 17),
            });

            CreateHotKeyInput(
                hotKeyGroup,
                () => KeyCode,
                keyCode => KeyCode = keyCode
            );

            FlToggleNotification = new CheckBox {
                Text = Locale.String(@"SettingsOptionsToggleNotify"),
                Location = new Point(10, 20),
                Checked = Settings.Get(Program.TOGGLE_BALLOON_SETTING, Program.ToggleBalloonDefault),
                AutoSize = true,
                TabIndex = 201,
            };
            FlShowHotkeyIcon = new CheckBox {
                Text = Locale.String(@"SettingsOptionsToggleNotifyIcon"),
                Location = new Point(10, 40),
                Checked = Settings.Get(Program.SHOW_HOTKEY_ICON, true),
                AutoSize = true,
                TabIndex = 202,
            };
            FlAlwaysRetryAsAdmin = new CheckBox {
                Text = Locale.String(@"SettingsOptionsElevatedRetry"),
                Location = new Point(10, 60),
                Checked = Settings.Get(Program.ALWAYS_RETRY_ELEVATED, false),
                AutoSize = true,
                TabIndex = 203,
            };
            FlShiftClickBlacklist = new CheckBox {
                Text = Locale.String(@"SettingsOptionsShiftBlacklist"),
                Location = new Point(10, 80),
                Checked = Settings.Get(Program.SHIFT_CLICK_BLACKLIST, true),
                AutoSize = true,
                TabIndex = 204,
            };
            FlRevertOnExit = new CheckBox {
                Text = Locale.String(@"SettingsOptionsRevertOnExit"),
                Location = new Point(10, 100),
                Checked = Settings.Get(Program.REVERT_ON_EXIT, false),
                AutoSize = true,
                TabIndex = 205,
            };
            FlShowWindowList = new CheckBox {
                Text = Locale.String(@"SettingsOptionsShowTrayList"),
                Location = new Point(10, 120),
                Checked = Settings.Get(Program.SHOW_WINDOW_LIST, true),
                AutoSize = true,
                TabIndex = 206,
            };
            FlAlwaysAdmin = new CheckBox {
                Text = Locale.String(@"SettingsOptionsAlwaysAdmin"),
                Location = new Point(10, 140),
                Checked = Settings.Get(Program.ALWAYS_ADMIN_SETTING, false),
                AutoSize = true,
                TabIndex = 207,
            };

            CheckBox[] options = new[] {
                FlAlwaysAdmin, FlToggleNotification, FlShiftClickBlacklist,
                FlShowHotkeyIcon, FlShowWindowList, FlAlwaysRetryAsAdmin,
                FlRevertOnExit,
            };

            flagsGroup.Controls.AddRange(options);

            foreach(CheckBox option in options)
                if((option.Width + (option.Left * 2)) > flagsGroup.ClientSize.Width)
                    ClientSize = new Size(option.Width + 30, ClientSize.Height);

            LangSelect = new ComboBox {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Size = new Size(langGroup.Width - 24, 21),
                Location = new Point(12, 22),
            };
            LangSelect.Items.AddRange(Locale.GetAvailableLanguages());
            LangSelect.SelectedItem = Locale.GetCurrentLanguage();
            langGroup.Controls.Add(LangSelect);

            Button titleBlacklist = new Button {
                Size = new Size(120, 23),
                Location = new Point(10, 20),
                Text = Locale.String(@"SettingsOtherBlacklistButton"),
                TabIndex = 301,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
            };
            titleBlacklist.Click += (s, e) => {
                string[] newList = BlacklistWindow.Display(Locale.String(@"SettingsOtherBlacklistWindowTitle"), Program.GetBlacklistedTitles());

                if(newList != null) {
                    Program.ApplyBlacklistedTitles(newList);
                    Program.SaveBlacklistedTitles();
                }
            };

            Button startWithWindows = new Button {
                Size = new Size(120, 23),
                Location = new Point(134, 20),
                Text = Locale.String(@"SettingsOtherStartupButton"),
                TabIndex = 302,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
            };
            startWithWindows.Click += (s, e) => {
                DialogResult dr = MessageBox.Show(Locale.String(@"SettingsOtherStartupConfirm"), Program.TITLE, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), @"TopMostFriend.lnk");

                if(File.Exists(path))
                    File.Delete(path);

                if(dr == DialogResult.Yes) {
                    IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
                    IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(path);
                    shortcut.TargetPath = Application.ExecutablePath;
                    shortcut.Save();
                }
            };

            Button resetSettings = new Button {
                Size = new Size(120, 23),
                Location = new Point(258, 20),
                Text = Locale.String(@"SettingsOtherResetButton"),
                TabIndex = 303,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
            };
            resetSettings.Click += (s, e) => {
                DialogResult dr = MessageBox.Show(Locale.String(@"SettingsOtherResetConfirm"), Program.TITLE, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                if(dr == DialogResult.Yes) {
                    Settings.Remove(Program.FOREGROUND_HOTKEY_SETTING);
                    Settings.Remove(Program.PROCESS_SEPARATOR_SETTING);
                    Settings.Remove(Program.LIST_SELF_SETTING);
                    Settings.Remove(Program.LIST_BACKGROUND_PATH_SETTING);
                    Settings.Remove(Program.LIST_BACKGROUND_LAYOUT_SETTING);
                    Settings.Remove(Program.ALWAYS_ADMIN_SETTING);
                    Settings.Remove(Program.TOGGLE_BALLOON_SETTING);
                    Settings.Remove(Program.SHIFT_CLICK_BLACKLIST);
                    Settings.Remove(Program.TITLE_BLACKLIST);
                    Settings.Remove(Program.SHOW_HOTKEY_ICON);
                    Settings.Remove(Program.SHOW_WINDOW_LIST);
                    Settings.Remove(Program.LAST_VERSION);
                    Settings.Remove(Program.ALWAYS_RETRY_ELEVATED);
                    Settings.Remove(Program.REVERT_ON_EXIT);
                    Program.Shutdown();
                    Process.Start(Application.ExecutablePath);
                    Application.Exit();
                }
            };

            Button[] otherButtons = new[] { titleBlacklist, startWithWindows, resetSettings };

            otherGroup.Controls.AddRange(otherButtons);
        }

        public static void CreateHotKeyInput(
            Control target,
            Func<int> getKeyCode,
            Action<int> setKeyCode,
            int offsetX = 0,
            int offsetY = 0
        ) {
            int modX = 120 + offsetX;
            int modY = 34 + offsetY;
            int keyCode = getKeyCode();

            Button fgReset = new Button {
                Text = Locale.String(@"SettingsHotKeysReset"),
                Location = new Point(target.Width - 85 + offsetX, modY),
                TabIndex = 105,
            };

            TextBox fgKey = new TextBox {
                Text = ((Keys)(keyCode >> 16)).ToString(),
                Location = new Point(12 + offsetX, modY + 2),
                TabIndex = 101,
            };

            CheckBox fgModCtrl = new CheckBox {
                Text = @"CTRL",
                Location = new Point(modX, modY),
                Checked = (keyCode & (int)Win32ModKeys.MOD_CONTROL) > 0,
                Appearance = Appearance.Button,
                Size = new Size(50, 23),
                TextAlign = ContentAlignment.MiddleCenter,
                TabIndex = 102,
            };

            CheckBox fgModWindows = new CheckBox {
                Text = @"WIN",
                Location = new Point(modX + 50, modY),
                Checked = (keyCode & (int)Win32ModKeys.MOD_WIN) > 0,
                Appearance = fgModCtrl.Appearance,
                Size = fgModCtrl.Size,
                TextAlign = fgModCtrl.TextAlign,
                TabIndex = 103,
            };

            CheckBox fgModAlt = new CheckBox {
                Text = @"ALT",
                Location = new Point(modX + 100, modY),
                Checked = (keyCode & (int)Win32ModKeys.MOD_ALT) > 0,
                Appearance = fgModCtrl.Appearance,
                Size = fgModCtrl.Size,
                TextAlign = fgModCtrl.TextAlign,
                TabIndex = 104,
            };

            CheckBox fgModShift = new CheckBox {
                Text = @"SHIFT",
                Location = new Point(modX + 150, modY),
                Checked = (keyCode & (int)Win32ModKeys.MOD_SHIFT) > 0,
                Appearance = fgModCtrl.Appearance,
                Size = fgModCtrl.Size,
                TextAlign = fgModCtrl.TextAlign,
                TabIndex = 105,
            };

            fgReset.Click += (s, e) => {
                fgModCtrl.Checked = fgModAlt.Checked = fgModShift.Checked = false;
                fgKey.Text = ((Keys)0).ToString();
                setKeyCode(0);
            };
            fgModCtrl.Click += (s, e) => {
                if(s is CheckBox cb) {
                    if(cb.Checked)
                        keyCode |= (int)Win32ModKeys.MOD_CONTROL;
                    else
                        keyCode &= ~(int)Win32ModKeys.MOD_CONTROL;
                    setKeyCode(keyCode);
                }
            };
            fgModAlt.Click += (s, e) => {
                if(s is CheckBox cb) {
                    if(cb.Checked)
                        keyCode |= (int)Win32ModKeys.MOD_ALT;
                    else
                        keyCode &= ~(int)Win32ModKeys.MOD_ALT;
                    setKeyCode(keyCode);
                }
            };
            fgModShift.Click += (s, e) => {
                if(s is CheckBox cb) {
                    if(cb.Checked)
                        keyCode |= (int)Win32ModKeys.MOD_SHIFT;
                    else
                        keyCode &= ~(int)Win32ModKeys.MOD_SHIFT;
                    setKeyCode(keyCode);
                }
            };
            fgModWindows.Click += (s, e) => {
                if(s is CheckBox cb) {
                    if(cb.Checked)
                        keyCode |= (int)Win32ModKeys.MOD_WIN;
                    else
                        keyCode &= ~(int)Win32ModKeys.MOD_WIN;
                    setKeyCode(keyCode);
                }
            };
            fgKey.KeyDown += (s, e) => {
                if(!(s is TextBox textBox))
                    return;
                e.Handled = e.SuppressKeyPress = true;
                textBox.Text = e.KeyCode.ToString();
                keyCode &= 0xFFFF;
                keyCode |= (int)e.KeyCode << 16;
                setKeyCode(keyCode);
            };

            target.Controls.AddRange(new Control[] {
                fgModCtrl, fgModAlt, fgModShift, fgModWindows, fgReset, fgKey,
            });
        }

        public void Apply() {
            if(LangSelect.SelectedItem is LanguageInfo li) {
                if(li != Locale.GetCurrentLanguage()) {
                    if(Locale.GetPreferredLanguage() == li.Id)
                        Settings.Remove(Program.LANGUAGE);
                    else
                        Settings.Set(Program.LANGUAGE, li.Id);

                    Locale.SetLanguage(li);
                    MessageBox.Show(Locale.String(@"SettingsLanguageChangedInfo"), Program.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            Settings.Set(Program.FOREGROUND_HOTKEY_SETTING, KeyCode);
            Settings.Set(Program.ALWAYS_ADMIN_SETTING, FlAlwaysAdmin.Checked);
            Settings.Set(Program.TOGGLE_BALLOON_SETTING, FlToggleNotification.Checked);
            Settings.Set(Program.SHIFT_CLICK_BLACKLIST, FlShiftClickBlacklist.Checked);
            Settings.Set(Program.SHOW_HOTKEY_ICON, FlShowHotkeyIcon.Checked);
            Settings.Set(Program.SHOW_WINDOW_LIST, FlShowWindowList.Checked);
            Settings.Set(Program.ALWAYS_RETRY_ELEVATED, FlAlwaysRetryAsAdmin.Checked);
            Settings.Set(Program.REVERT_ON_EXIT, FlRevertOnExit.Checked);
            Program.SetForegroundHotKey(KeyCode);
        }

        private void OkButton_Click(object sender, EventArgs e) {
            Apply();
            Close();
        }
        private void CancelButton_Click(object sender, EventArgs e) {
            Close();
        }
        private void ApplyButton_Click(object sender, EventArgs e) {
            Apply();
        }

        protected override void OnFormClosed(FormClosedEventArgs e) {
            base.OnFormClosed(e);
            Instance.Dispose();
            Instance = null;
        }
    }
}
