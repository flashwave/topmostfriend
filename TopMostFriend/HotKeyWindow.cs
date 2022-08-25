using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TopMostFriend {
    public sealed class HotKeyWindow : Form {
        public class HotKeyInfo {
            public string Name { get; }
            public int Atom { get; }
            public int Key { get; }
            public Action Action { get; }

            public HotKeyInfo(string name, int atom, int key, Action action) {
                Name = name ?? throw new ArgumentNullException(nameof(name));
                Atom = atom;
                Key = key;
                Action = action ?? throw new ArgumentNullException(nameof(action));
            }
        }

        private readonly List<HotKeyInfo> RegisteredHotKeys = new List<HotKeyInfo>();

        public HotKeyWindow() {
            ShowInTaskbar = false;
            Text = string.Empty;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(1, 1);
            Location = new Point(-9999, -9999);
            CreateHandle();
            Hide();
        }

        protected override void OnFormClosing(FormClosingEventArgs e) {
            e.Cancel = e.CloseReason == CloseReason.UserClosing;
        }

        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);

            if(m.Msg == Win32.WM_HOTKEY) {
                int keyCode = m.LParam.ToInt32();

                lock (RegisteredHotKeys)
                    RegisteredHotKeys.FirstOrDefault(x => x.Key == keyCode)?.Action.Invoke();
            }
        }

        protected override void Dispose(bool disposing) {
            lock (RegisteredHotKeys) {
                HotKeyInfo[] hotKeys = RegisteredHotKeys.ToArray();

                foreach(HotKeyInfo hotKey in hotKeys)
                    Unregister(hotKey.Atom);
            }

            base.Dispose(disposing);
        }

        public int Register(string name, Win32ModKeys modifiers, Keys key, Action action) {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if(string.IsNullOrEmpty(name))
                name = Guid.NewGuid().ToString();

            int atom = Win32.GlobalAddAtom(name);
            int keyCode = ((ushort)key << 16) | (ushort)modifiers;

            if (atom == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), @"Atom creation failed.");

            if (!Win32.RegisterHotKey(Handle, atom, modifiers, key)) {
                Win32.GlobalDeleteAtom((ushort)atom);
                throw new Win32Exception(Marshal.GetLastWin32Error(), @"Hotkey registration failed.");
            }

            lock(RegisteredHotKeys)
                RegisteredHotKeys.Add(new HotKeyInfo(name, atom, keyCode, action));

            return atom;
        }

        public void Unregister(int id) {
            if (id < 1)
                return;

            lock (RegisteredHotKeys) {
                if (!RegisteredHotKeys.Any(x => x.Atom == id))
                    return;
                RegisteredHotKeys.RemoveAll(x => x.Atom == id);
            }

            Win32.UnregisterHotKey(Handle, id);
            Win32.GlobalDeleteAtom((ushort)id);
        }

        public void Unregister(string name) {
            int atom = 0;

            lock (RegisteredHotKeys)
                atom = RegisteredHotKeys.FirstOrDefault(x => x.Name == name)?.Atom ?? 0;

            Unregister(atom);
        }
    }
}
