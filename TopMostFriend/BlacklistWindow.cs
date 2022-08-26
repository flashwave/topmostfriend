using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TopMostFriend {
    public class BlacklistWindow : Form {
        public readonly List<string> Blacklist = new List<string>();

        public static string[] Display(string title, string[] items) {
            using (BlacklistWindow blacklist = new BlacklistWindow(title, items)) {
                if (blacklist.ShowDialog() == DialogResult.OK)
                    return blacklist.Blacklist.ToArray();
            }

            return null;
        }

        private const int SPACING = 6;
        private const int BUTTON_WIDTH = 75;
        private const int BUTTON_HEIGHT = 23;

        private readonly Button AddButton;
        private readonly Button EditButton;
        private readonly Button RemoveButton;
        private readonly ListBox BlacklistView;

        public BlacklistWindow(string title, string[] items) {
            Blacklist.AddRange(items);
            Text = title;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(410, 203);
            MinimizeBox = MaximizeBox = false;
            MinimumSize = Size;
            DialogResult = DialogResult.Cancel;
            TopMost = true;

            BlacklistView = new ListBox {
                TabIndex = 101,
                IntegralHeight = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(SPACING, SPACING),
                ClientSize = new Size(ClientSize.Width - BUTTON_WIDTH - (int)(SPACING * 3.5), ClientSize.Height - (int)(SPACING * 2.5)),
            };
            BlacklistView.SelectedIndexChanged += BlacklistView_SelectedIndexChanged;
            BlacklistView.MouseDoubleClick += BlacklistView_MouseDoubleClick;

            AddButton = new Button {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Text = Locale.String(@"BlacklistAdd"),
                ClientSize = new Size(BUTTON_WIDTH, BUTTON_HEIGHT),
                Location = new Point(BlacklistView.Width + (SPACING * 2), SPACING),
                TabIndex = 201,
            };
            EditButton = new Button {
                Text = Locale.String(@"BlacklistEdit"),
                Location = new Point(AddButton.Location.X, AddButton.Location.Y + AddButton.Height + SPACING),
                Enabled = false,
                Anchor = AddButton.Anchor,
                ClientSize = AddButton.ClientSize,
                TabIndex = 202,
            };
            RemoveButton = new Button {
                Text = Locale.String(@"BlacklistRemove"),
                Location = new Point(AddButton.Location.X, EditButton.Location.Y + AddButton.Height + SPACING),
                Enabled = false,
                Anchor = AddButton.Anchor,
                ClientSize = AddButton.ClientSize,
                TabIndex = 203,
            };

            AddButton.Click += AddButton_Click;
            EditButton.Click += EditButton_Click;
            RemoveButton.Click += RemoveButton_Click;

            CancelButton = new Button {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Text = Locale.String(@"BlacklistCancel"),
                Location = new Point(AddButton.Location.X, ClientSize.Height - AddButton.ClientSize.Height - SPACING),
                ClientSize = AddButton.ClientSize,
                TabIndex = 10001,
            };
            Button acceptButton = new Button {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Text = Locale.String(@"BlacklistDone"),
                Location = new Point(AddButton.Location.X, ClientSize.Height - ((AddButton.ClientSize.Height + SPACING) * 2)),
                ClientSize = AddButton.ClientSize,
                TabIndex = 10002,
            };
            acceptButton.Click += AcceptButton_Click;

            Controls.AddRange(new Control[] {
                BlacklistView, AddButton, EditButton, RemoveButton, (Control)(AcceptButton = acceptButton), (Control)CancelButton,
            });

            RefreshList();
        }

        private void AcceptButton_Click(object sender, EventArgs e) {
            DialogResult = DialogResult.OK;
            Close();
        }

        private class BlacklistEditorWindow : Form {
            public string Original { get; }
            public string String { get => TextBox.Text; }

            private readonly TextBox TextBox;

            public BlacklistEditorWindow(string original = null) {
                Original = original ?? string.Empty;
                Text = Locale.String(original == null ? @"BlacklistEditorAdding" : @"BlacklistEditorEditing", original);
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedSingle;
                ClientSize = new Size(500, 39);
                MaximizeBox = MinimizeBox = false;
                MaximumSize = MinimumSize = Size;
                TopMost = true;

                Button cancelButton = new Button {
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right,
                    Location = new Point(ClientSize.Width - 75 - 8, 8),
                    Name = @"cancelButton",
                    Size = new Size(75, 23),
                    TabIndex = 102,
                    Text = Locale.String(@"BlacklistEditorCancel"),
                };
                cancelButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

                Button saveButton = new Button {
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right,
                    Location = new Point(cancelButton.Location.X - 75 - 5, cancelButton.Location.Y),
                    Name = @"saveButton",
                    Size = new Size(75, 23),
                    TabIndex = 101,
                    Text = Locale.String(@"BlacklistEditorSave"),
                };
                saveButton.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

                TextBox = new TextBox {
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                    Location = new Point(8, 9),
                    Name = @"deviceSelection", // ??????
                    Size = new Size(ClientSize.Width - 8 - cancelButton.Width - saveButton.Width - 19, 23),
                    TabIndex = 100,
                    Text = Original,
                };

                AcceptButton = saveButton;
                CancelButton = cancelButton;

                Controls.AddRange(new Control[] {
                    TextBox, saveButton, cancelButton,
                });
            }
        }

        private void AddButton_Click(object sender, EventArgs e) {
            using (BlacklistEditorWindow bew = new BlacklistEditorWindow()) {
                if (bew.ShowDialog() == DialogResult.OK)
                    SafeAdd(bew.String);
            }

            RefreshList();
        }

        private void EditButton_Click(object sender, EventArgs e) {
            using (BlacklistEditorWindow bew = new BlacklistEditorWindow(BlacklistView.SelectedItem as string)) {
                if (bew.ShowDialog() == DialogResult.OK) {
                    Blacklist.Remove(bew.Original);
                    SafeAdd(bew.String);
                }
            }

            RefreshList();
        }

        private void SafeAdd(string str) {
            if (!Blacklist.Contains(str))
                Blacklist.Add(str);
        }

        private void RemoveButton_Click(object sender, EventArgs e) {
            Blacklist.Remove(BlacklistView.SelectedItem as string);
            RefreshList();
        }

        private void BlacklistView_MouseDoubleClick(object sender, MouseEventArgs e) {
            if (BlacklistView.SelectedIndex < 0
                || BlacklistView.IndexFromPoint(e.Location) != BlacklistView.SelectedIndex)
                return;

            EditButton.PerformClick();
        }

        private void BlacklistView_SelectedIndexChanged(object sender, EventArgs e) {
            EditButton.Enabled = RemoveButton.Enabled = BlacklistView.SelectedIndex >= 0;
        }

        public void RefreshList() {
            object selected = BlacklistView.SelectedValue;

            BlacklistView.Items.Clear();
            BlacklistView.Items.AddRange(Blacklist.ToArray());

            if (selected != null && BlacklistView.Items.Contains(selected))
                BlacklistView.SelectedIndex = BlacklistView.Items.IndexOf(selected);
        }
    }
}
