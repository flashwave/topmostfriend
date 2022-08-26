using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace TopMostFriend {
    public class FirstRunWindow : Form {
        public static void Display() {
            using(FirstRunWindow firstRun = new FirstRunWindow())
                firstRun.ShowDialog();
        }

        private bool CanClose = false;
        private bool IsClosing = false;
        private bool IsSizing = false;

        private Button NextBtn { get; }
        private Button PrevBtn { get; }

        private Action NextAct = null;
        private Action PrevAct = null;

        private bool NextVisible { get => NextBtn.Visible; set => NextBtn.Visible = value; }
        private bool PrevVisible { get => PrevBtn.Visible; set => PrevBtn.Visible = value; }

        private Panel WorkArea { get; }

        public FirstRunWindow() {
            Text = Program.TITLE + @" v" + Application.ProductVersion.Substring(0, Application.ProductVersion.Length - 2);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            AutoScaleMode = AutoScaleMode.Dpi;
            MaximizeBox = MinimizeBox = false;
            TopMost = true;
            ClientSize = new Size(410, 80);

            Controls.Add(new PictureBox {
                Image = Properties.Resources.firstrun,
                Size = Properties.Resources.firstrun.Size,
                Location = new Point(0, 0),
            });

            NextBtn = new Button {
                Text = Locale.String(@"FirstRunNext"),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Visible = false,
                Location = new Point(ClientSize.Width - 81, ClientSize.Height - 29),
            };
            NextBtn.Click += NextBtn_Click;
            Controls.Add(NextBtn);

            PrevBtn = new Button {
                Text = Locale.String(@"FirstRunPrev"),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
                Visible = false,
                Location = new Point(6, ClientSize.Height - 29),
            };
            PrevBtn.Click += PrevBtn_Click;
            Controls.Add(PrevBtn);

            WorkArea = new Panel {
                Dock = DockStyle.Fill,
            };
            Controls.Add(WorkArea);
        }

        private void PrevBtn_Click(object sender, EventArgs e) {
            if(!PrevVisible)
                return;
            WorkArea.Controls.Clear();
            if(PrevAct == null)
                Close();
            else
                Invoke(PrevAct);
        }

        private void NextBtn_Click(object sender, EventArgs e) {
            if(!NextVisible)
                return;
            WorkArea.Controls.Clear();
            if(NextAct == null)
                Close();
            else
                Invoke(NextAct);
        }

        protected override void OnShown(EventArgs e) {
            base.OnShown(e);
            Update();
            Thread.Sleep(500);
            ShowPageIntro();
        }

        protected override void OnFormClosing(FormClosingEventArgs e) {
            if(e.CloseReason == CloseReason.UserClosing && !CanClose) {
                e.Cancel = true;

                if(!IsClosing) {
                    IsClosing = true;

                    SetHeight(80, new Action(() => {
                        CanClose = true;
                        Thread.Sleep(100);
                        Close();
                    }));
                }
                return;
            }

            base.OnFormClosing(e);
        }

        public void SetHeight(int height, Action onFinish = null) {
            if(height < 80)
                throw new ArgumentException(@"target height must be more than or equal to 80.", nameof(height));

            if(IsSizing)
                return;
            IsSizing = true;

            const int timeout = 1000 / 60;
            double time = 0;
            double period = timeout / 400d;

            int currentHeight = ClientSize.Height;
            int currentY = Location.Y;
            int diffHeight = height - currentHeight;
            int diffY = diffHeight / 2;

            Action setHeight = new Action(() => {
                int newHeight = currentHeight + (int)Math.Ceiling(time * diffHeight);
                int newY = currentY - (int)Math.Ceiling(time * diffY);
                ClientSize = new Size(ClientSize.Width, newHeight);
                Location = new Point(Location.X, newY);
            });

            new Thread(() => {
                Stopwatch sw = new Stopwatch();

                try {
                    do {
                        sw.Restart();

                        Invoke(setHeight);
                        time += period;

                        int delay = timeout - (int)sw.ElapsedMilliseconds;
                        if(delay > 1)
                            Thread.Sleep(delay);
                    } while(time < 1d + period);
                } finally {
                    sw.Stop();
                    if(onFinish != null)
                        Invoke(onFinish);
                    IsSizing = false;
                }
            }) {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
            }.Start();
        }

        public void ShowPageIntro() {
            Text = Locale.String(@"FirstRunWelcomeTitle");
            PrevVisible = false;
            NextVisible = true;
            NextAct = ShowPageHotKey;

            SetHeight(190, new Action(() => {
                WorkArea.Controls.Add(new Label {
                    Text = Locale.String(@"FirstRunWelcomeIntro"),
                    Location = new Point(10, 90),
                    Size = new Size(ClientSize.Width - 20, 200),
                });
            }));
        }

        public void ShowPageHotKey() {
            Text = Locale.String(@"FirstRunHotKeyTitle");
            PrevVisible = NextVisible = true;
            PrevAct = ShowPageIntro;
            NextAct = () => {
                Program.SetForegroundHotKey(Settings.Get(Program.FOREGROUND_HOTKEY_SETTING, 0));
                ShowPageElevation();
            };

            SetHeight(230, new Action(() => {
                WorkArea.Controls.Add(new Label {
                    Text = Locale.String(@"FirstRunHotKeyExplain"),
                    Location = new Point(10, 90),
                    Size = new Size(ClientSize.Width - 20, 40),
                });

                SettingsWindow.CreateHotKeyInput(
                    WorkArea,
                    () => Settings.Get(Program.FOREGROUND_HOTKEY_SETTING, 0),
                    keyCode => Settings.Set(Program.FOREGROUND_HOTKEY_SETTING, keyCode),
                    0,
                    110
                );

                CheckBox flShowNotification = new CheckBox {
                    Text = Locale.String(@"FirstRunHotKeyNotify"),
                    Location = new Point(12, 170),
                    Checked = Settings.Get(Program.TOGGLE_BALLOON_SETTING, Program.ToggleBalloonDefault),
                    AutoSize = true,
                    TabIndex = 201,
                };
                flShowNotification.CheckedChanged += (s, e) => {
                    Settings.Set(Program.TOGGLE_BALLOON_SETTING, flShowNotification.Checked);
                };
                WorkArea.Controls.Add(flShowNotification);
            }));
        }

        public void ShowPageElevation() {
            Text = Locale.String(@"FirstRunAdminTitle");
            PrevVisible = NextVisible = true;
            PrevAct = ShowPageHotKey;
            NextAct = ShowPageThanks;

            SetHeight(280, () => {
                WorkArea.Controls.Add(new Label {
                    Text = Locale.String(@"FirstRunAdminExplain"),
                    Location = new Point(10, 90),
                    Size = new Size(ClientSize.Width - 20, 40),
                });

                bool alwaysAdmin = Settings.Get(Program.ALWAYS_ADMIN_SETTING, false);
                bool implicitAdmin = Settings.Get(Program.ALWAYS_RETRY_ELEVATED, false);

                RadioButton rdAsk = new RadioButton {
                    Text = Locale.String(@"FirstRunAdminOptionAsk"),
                    Location = new Point(10, 140),
                    Size = new Size(ClientSize.Width - 20, 30),
                    Appearance = Appearance.Button,
                    Checked = !alwaysAdmin && !implicitAdmin,
                };
                rdAsk.CheckedChanged += (s, e) => {
                    if(rdAsk.Checked) {
                        Settings.Set(Program.ALWAYS_ADMIN_SETTING, false);
                        Settings.Set(Program.ALWAYS_RETRY_ELEVATED, false);
                    }
                };
                WorkArea.Controls.Add(rdAsk);

                RadioButton rdImplicit = new RadioButton {
                    Text = Locale.String(@"FirstRunAdminOptionImplicit"),
                    Location = new Point(rdAsk.Location.X, rdAsk.Location.Y + 36),
                    Size = rdAsk.Size,
                    Appearance = rdAsk.Appearance,
                    Checked = implicitAdmin && !alwaysAdmin,
                };
                rdImplicit.CheckedChanged += (s, e) => {
                    if(rdImplicit.Checked) {
                        Settings.Set(Program.ALWAYS_ADMIN_SETTING, false);
                        Settings.Set(Program.ALWAYS_RETRY_ELEVATED, true);
                    }
                };
                WorkArea.Controls.Add(rdImplicit);

                RadioButton rdAlways = new RadioButton {
                    Text = Locale.String(@"FirstRunAdminOptionAlways"),
                    Location = new Point(rdAsk.Location.X, rdImplicit.Location.Y + 36),
                    Size = rdAsk.Size,
                    Appearance = rdAsk.Appearance,
                    Checked = alwaysAdmin,
                };
                rdAlways.CheckedChanged += (s, e) => {
                    if(rdAlways.Checked) {
                        Settings.Set(Program.ALWAYS_ADMIN_SETTING, true);
                        Settings.Set(Program.ALWAYS_RETRY_ELEVATED, false);
                    }
                };
                WorkArea.Controls.Add(rdAlways);
            });
        }

        public void ShowPageThanks() {
            Text = Locale.String(@"FirstRunThanksTitle");
            PrevVisible = NextVisible = true;
            PrevAct = ShowPageElevation;
            NextAct = CheckRestartNeeded;

            SetHeight(270, () => {
                Label thankYou = new Label {
                    Text = Locale.String(@"FirstRunThanksThank"),
                    Location = new Point(10, 90),
                    Size = new Size(ClientSize.Width - 20, 20),
                };
                WorkArea.Controls.Add(thankYou);

                string updateLinkString = Locale.String(@"FirstRunThanksUpdate");

                int websiteStart = updateLinkString.IndexOf(@"[WEB]");
                updateLinkString = updateLinkString.Substring(0, websiteStart) + updateLinkString.Substring(websiteStart + 5);
                int websiteEnd = updateLinkString.IndexOf(@"[/WEB]");
                updateLinkString = updateLinkString.Substring(0, websiteEnd) + updateLinkString.Substring(websiteEnd + 6);

                int changelogStart = updateLinkString.IndexOf(@"[CHANGELOG]");
                updateLinkString = updateLinkString.Substring(0, changelogStart) + updateLinkString.Substring(changelogStart + 11);
                int changelogEnd = updateLinkString.IndexOf(@"[/CHANGELOG]");
                updateLinkString = updateLinkString.Substring(0, changelogEnd) + updateLinkString.Substring(changelogEnd + 12);

                LinkLabel updateLink;
                WorkArea.Controls.Add(updateLink = new LinkLabel {
                    Text = updateLinkString,
                    Location = new Point(10, 120),
                    Size = new Size(ClientSize.Width - 20, 34),
                    Font = thankYou.Font,
                    Links = {
                        new LinkLabel.Link(websiteStart, websiteEnd - websiteStart, @"https://flash.moe/topmostfriend"),
                        new LinkLabel.Link(changelogStart, changelogEnd - changelogStart, @"https://flash.moe/topmostfriend/changelog.php"),
                    },
                });
                updateLink.LinkClicked += (s, e) => {
                    Process.Start((string)e.Link.LinkData);
                };

                string settingsLinkString = Locale.String(@"FirstRunThanksSettings");

                int settingsStart = settingsLinkString.IndexOf(@"[SETTINGS]");
                settingsLinkString = settingsLinkString.Substring(0, settingsStart) + settingsLinkString.Substring(settingsStart + 10);
                int settingsEnd = settingsLinkString.IndexOf(@"[/SETTINGS]");
                settingsLinkString = settingsLinkString.Substring(0, settingsEnd) + settingsLinkString.Substring(settingsEnd + 11);

                LinkLabel settingsLink;
                WorkArea.Controls.Add(settingsLink = new LinkLabel {
                    Text = settingsLinkString,
                    Location = new Point(10, 160),
                    Size = new Size(ClientSize.Width - 20, 30),
                    Font = thankYou.Font,
                    Links = {
                        new LinkLabel.Link(settingsStart, settingsEnd - settingsStart),
                    },
                });
                settingsLink.LinkClicked += (s, e) => {
                    SettingsWindow.Display();
                };

                WorkArea.Controls.Add(new Label {
                    Text = Locale.String(@"FirstRunThanksAdmin"),
                    Location = new Point(10, 200),
                    Size = new Size(ClientSize.Width - 20, 80),
                });
            });
        }

        public void CheckRestartNeeded() {
            if(Settings.Get(Program.ALWAYS_ADMIN_SETTING, false))
                UAC.RestartElevated();
            Close();
        }
    }
}
