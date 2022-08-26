using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace TopMostFriend {
    public class WindowInfo {
        public IntPtr Handle { get; }
        public int ProcessId { get; }

        public Process Owner => Process.GetProcessById(ProcessId);

        public string Title => Win32.GetWindowTextString(Handle);

        public bool IsTopMost {
            get => (Win32.GetWindowLongPtr(Handle, Win32.GWL_EXSTYLE).ToInt32() & Win32.WS_EX_TOPMOST) > 0;
            set {
                Win32.SetWindowPos(
                    Handle, new IntPtr(value ? Win32.HWND_TOPMOST : Win32.HWND_NOTOPMOST),
                    0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_SHOWWINDOW
                );
            }
        }

        public bool IsOwnWindow
            => Owner == Process.GetCurrentProcess();

        public Icon Icon {
            get {
                IntPtr icon = Win32.SendMessage(Handle, Win32.WM_GETICON, Win32.ICON_SMALL2, 0);

                if(icon == IntPtr.Zero) {
                    icon = Win32.SendMessage(Handle, Win32.WM_GETICON, Win32.ICON_SMALL, 0);

                    if(icon == IntPtr.Zero) {
                        icon = Win32.SendMessage(Handle, Win32.WM_GETICON, Win32.ICON_BIG, 0);

                        if(icon == IntPtr.Zero) {
                            icon = Win32.GetClassLongPtr(Handle, Win32.GCL_HICON);

                            if(icon == IntPtr.Zero)
                                icon = Win32.GetClassLongPtr(Handle, Win32.GCL_HICONSM);
                        }
                    }
                }

                return icon == IntPtr.Zero ? null : Icon.FromHandle(icon);
            }
        }

        public Image IconBitmap
            => Icon?.ToBitmap();

        public WindowInfo(IntPtr handle)
            : this(handle, FindOwnerId(handle)) { }

        public WindowInfo(IntPtr handle, int processId) {
            Handle = handle;
            ProcessId = processId;
        }

        public void SwitchTo() {
            Win32.SwitchToThisWindow(Handle, false);
        }

        public bool ToggleTopMost(bool switchWindow = true) {
            bool expected = !IsTopMost;
            IsTopMost = expected;
            bool success = IsTopMost == expected;
            if(switchWindow && expected && success)
                SwitchTo();
            return success;
        }

        public bool ToggleTopMostElevated(bool switchWindow = true) {
            return UAC.ToggleWindowTopMost(this, switchWindow) == 0;
        }

        public static int FindOwnerId(IntPtr hWnd) {
            Win32.GetWindowThreadProcessId(hWnd, out int procId);
            return procId;
        }

        public static WindowInfo GetForegroundWindow() {
            return new WindowInfo(Win32.GetForegroundWindow());
        }

        public static IEnumerable<WindowInfo> GetAllWindows(bool includeHidden = false) {
            List<IntPtr> windows = new List<IntPtr>();
            Win32.EnumWindows(new Win32.EnumWindowsProc((hWnd, lParam) => {
                if(includeHidden || Win32.IsWindowVisible(hWnd))
                    windows.Add(hWnd);
                return true;
            }), 0);
            return windows.Select(w => new WindowInfo(w));
        }
    }
}
