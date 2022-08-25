using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

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

        public SettingsWindow() {
            Text = @"Top Most Friend Settings";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(410, 113);
            MinimizeBox = MaximizeBox = false;
            MinimumSize = MaximumSize = Size;

            KeyCode = Settings.Get(Program.FOREGROUND_HOTKEY_SETTING, 0);

            Button applyButton = new Button {
                Text = @"Apply",
                Size = new Size(75, 23),
                Location = new Point(ClientSize.Width - 81, ClientSize.Height - 30),
                TabIndex = 10003,
            };
            applyButton.Click += ApplyButton_Click;
            Button cancelButton = new Button {
                Text = @"Cancel",
                Size = applyButton.Size,
                Location = new Point(ClientSize.Width - 162, applyButton.Location.Y),
                TabIndex = 10002,
            };
            cancelButton.Click += CancelButton_Click;
            Button okButton = new Button {
                Text = @"OK",
                Size = applyButton.Size,
                Location = new Point(ClientSize.Width - 243, applyButton.Location.Y),
                TabIndex = 10001,
            };
            okButton.Click += OkButton_Click;

            GroupBox hotKeyGroup = new GroupBox {
                Text = @"Hotkeys",
                Location = new Point(6, 6),
                Size = new Size(Width - 18, 70),
            };

            Controls.AddRange(new Control[] {
                applyButton, cancelButton, okButton, hotKeyGroup,
            });

            Label toggleForegroundLabel = new Label {
                AutoSize = true,
                Text = @"Toggle always on top status on active window",
                Location = new Point(8, 17),
            };

            const int mod_x = 120;
            const int mod_y = 34;

            Button fgReset = new Button {
                Text = @"Reset",
                Location = new Point(hotKeyGroup.Width - 85, mod_y),
            };
            fgReset.Click += FgReset_Click;

            FgKey = new TextBox {
                Text = ((Keys)(KeyCode >> 16)).ToString(),
                Location = new Point(12, mod_y + 2),
            };
            FgKey.KeyDown += FgKey_KeyDown;

            FgModCtrl = new CheckBox {
                Text = @"CTRL",
                Location = new Point(mod_x, mod_y),
                Checked = (KeyCode & (int)Win32ModKeys.MOD_CONTROL) > 0,
                Appearance = Appearance.Button,
                Size = new Size(50, 23),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            FgModCtrl.Click += FgModCtrl_Click;

            FgModAlt = new CheckBox {
                Text = @"ALT",
                Location = new Point(mod_x + 50, mod_y),
                Checked = (KeyCode & (int)Win32ModKeys.MOD_ALT) > 0,
                Appearance = FgModCtrl.Appearance,
                Size = FgModCtrl.Size,
                TextAlign = FgModCtrl.TextAlign,
            };
            FgModAlt.Click += FgModAlt_Click;

            FgModShift = new CheckBox {
                Text = @"SHIFT",
                Location = new Point(mod_x + 100, mod_y),
                Checked = (KeyCode & (int)Win32ModKeys.MOD_SHIFT) > 0,
                Appearance = FgModCtrl.Appearance,
                Size = FgModCtrl.Size,
                TextAlign = FgModCtrl.TextAlign,
            };
            FgModShift.Click += FgModShift_Click;

            hotKeyGroup.Controls.AddRange(new Control[] {
                toggleForegroundLabel, FgModCtrl, FgModAlt, FgModShift, fgReset, FgKey,
            });
        }

        private void FgReset_Click(object sender, EventArgs e) {
            FgModCtrl.Checked = FgModAlt.Checked = FgModShift.Checked = false;
            FgKey.Text = string.Empty;
            KeyCode = 0;
        }

        public void Apply() {
            Settings.Set(Program.FOREGROUND_HOTKEY_SETTING, KeyCode);
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

        private void FgModCtrl_Click(object sender, EventArgs e) {
            if(sender is CheckBox cb) {
                if (cb.Checked)
                    KeyCode |= (int)Win32ModKeys.MOD_CONTROL;
                else
                    KeyCode &= ~(int)Win32ModKeys.MOD_CONTROL;
            }
        }
        private void FgModAlt_Click(object sender, EventArgs e) {
            if (sender is CheckBox cb) {
                if (cb.Checked)
                    KeyCode |= (int)Win32ModKeys.MOD_ALT;
                else
                    KeyCode &= ~(int)Win32ModKeys.MOD_ALT;
            }
        }
        private void FgModShift_Click(object sender, EventArgs e) {
            if (sender is CheckBox cb) {
                if (cb.Checked)
                    KeyCode |= (int)Win32ModKeys.MOD_SHIFT;
                else
                    KeyCode &= ~(int)Win32ModKeys.MOD_SHIFT;
            }
        }

        private void FgKey_KeyDown(object sender, KeyEventArgs e) {
            if (!(sender is TextBox textBox))
                return;
            e.Handled = e.SuppressKeyPress = true;
            textBox.Text = e.KeyCode.ToString();
            KeyCode &= 0xFFFF;
            KeyCode |= (int)e.KeyCode << 16;
        }

        protected override void OnFormClosed(FormClosedEventArgs e) {
            base.OnFormClosed(e);
            Instance.Dispose();
            Instance = null;
        }
    }
}
