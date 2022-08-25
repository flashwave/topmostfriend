using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TopMostFriend {
    public static class Settings {
        private const string ROOT = @"Software\flash.moe\TopMostFriend";

        private static RegistryKey GetRoot() {
            RegistryKey root = Registry.CurrentUser.OpenSubKey(ROOT, true);

            if (root == null)
                root = Registry.CurrentUser.CreateSubKey(ROOT);

            return root;
        }

        public static T Get<T>(string name, T fallback = default) {
            try {
                return (T)Convert.ChangeType(GetRoot().GetValue(name, fallback), typeof(T));
            } catch {
                return fallback;
            }
        }

        public static string[] Get(string name, string[] fallback = null) {
            byte[] buffer = GetRoot().GetValue(name, null) as byte[];

            if (buffer == null)
                return fallback;

            List<string> strings = new List<string>();

            using (MemoryStream src = new MemoryStream(buffer))
            using (MemoryStream ms = new MemoryStream()) {
                int b;

                for(; ; ) {
                    b = src.ReadByte();

                    if (b == -1)
                        break;
                    else if (b != 0)
                        ms.WriteByte((byte)b);
                    else {
                        strings.Add(Encoding.UTF8.GetString(ms.ToArray()));
                        ms.SetLength(0);
                    }
                }
            }

            return strings.ToArray();
        }

        public static bool Has(string name) {
            try {
                GetRoot().GetValueKind(name);
                return true;
            } catch {
                return false;
            }
        }

        public static void Set(string name, object value) {
            if(value == null) {
                Remove(name);
                return;
            }

            switch(value) {
                case bool b:
                    value = b ? 1 : 0;
                    break;
            }

            GetRoot().SetValue(name, value);
        }

        public static void Set(string name, string[] values) {
            if(values == null || values.Length < 1) {
                Remove(name);
                return;
            }

            using (MemoryStream ms = new MemoryStream()) {
                foreach (string value in values) {
                    byte[] buffer = Encoding.UTF8.GetBytes(value);
                    ms.Write(buffer, 0, buffer.Length);
                    ms.WriteByte(0);
                }

                GetRoot().SetValue(name, ms.ToArray(), RegistryValueKind.Binary);
            }
        }

        public static void SetDefault(string name, object value) {
            if (!Has(name))
                Set(name, value);
        }

        public static void SetDefault(string name, string[] value) {
            if (!Has(name))
                Set(name, value);
        }

        public static void Remove(string name) {
            GetRoot().DeleteValue(name, false);
        }
    }
}
