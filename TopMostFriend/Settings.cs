using Microsoft.Win32;
using System;

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

        public static void SetDefault(string name, object value) {
            if (!Has(name))
                Set(name, value);
        }

        public static void Remove(string name) {
            GetRoot().DeleteValue(name, false);
        }
    }
}
