using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TopMostFriend {
    [Flags]
    public enum Win32ModKeys : uint {
        [Description(@"Alt")]
        MOD_ALT = 0x0001,
        [Description(@"Ctrl")]
        MOD_CONTROL = 0x0002,
        [Description(@"Shift")]
        MOD_SHIFT = 0x0004,
        [Description(@"Windows")]
        MOD_WIN = 0x0008,
        MOD_NOREPEAT = 0x4000,
    }

    public static class Win32 {
        public const int HWND_TOPMOST = -1;
        public const int HWND_NOTOPMOST = -2;

        public const int SWP_NOSIZE = 0x0001;
        public const int SWP_NOMOVE = 0x0002;
        public const int SWP_SHOWWINDOW = 0x0040;

        public const int GWL_EXSTYLE = -20;

        public const int GCL_HICON = -14;
        public const int GCL_HICONSM = -34;

        public const int WS_EX_TOPMOST = 0x08;

        public const int HT_CAPTION = 0x02;

        public const int WM_GETICON = 0x007F;
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int WM_HOTKEY = 0x0312;

        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;
        public const int ICON_SMALL2 = 2;

        public delegate bool EnumWindowsProc([In] IntPtr hWnd, [In] int lParam);

        [DllImport(@"user32")]
        public static extern bool SetProcessDPIAware();

        [DllImport(@"user32", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport(@"user32", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            return new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        [DllImport(@"user32", EntryPoint = "GetWindowLong")]
        public static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport(@"user32", EntryPoint = "GetWindowLongPtr")]
        public static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport(@"user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport(@"user32", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport(@"user32", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        public static string GetWindowTextString(IntPtr hwnd) {
            int length = GetWindowTextLength(hwnd) + 1;
            StringBuilder sb = new StringBuilder(length);
            GetWindowText(hwnd, sb, length);
            return sb.ToString();
        }

        public static IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex) {
            if (IntPtr.Size > 4)
                return GetClassLongPtr64(hWnd, nIndex);
            return new IntPtr(GetClassLongPtr32(hWnd, nIndex));
        }

        [DllImport(@"user32", EntryPoint = "GetClassLong")]
        public static extern uint GetClassLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport(@"user32", EntryPoint = "GetClassLongPtr")]
        public static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport(@"user32", SetLastError = true)]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport(@"user32", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int uFlags);

        [DllImport(@"user32", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport(@"user32")]
        public static extern bool ReleaseCapture();

        [DllImport(@"user32", SetLastError = true)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport(@"user32", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, [MarshalAs(UnmanagedType.U4)] Win32ModKeys fsModifiers, [MarshalAs(UnmanagedType.U4)] Keys vk);

        [DllImport(@"user32", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport(@"user32", SetLastError = true)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, int lParam);

        [DllImport(@"kernel32", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern ushort GlobalAddAtom(string lpString);

        [DllImport(@"kernel32", SetLastError = true, ExactSpelling = true)]
        public static extern ushort GlobalDeleteAtom(ushort nAtom);
    }
}
