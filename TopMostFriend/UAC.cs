using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Forms;

namespace TopMostFriend {
    public static class UAC {
        private static bool? IsElevatedValue;
        private static string ExecutablePathValue;

        static UAC() {
            ExecutablePath = null;
        }

        public static bool IsElevated {
            get {
                if(!IsElevatedValue.HasValue)
                    using(WindowsIdentity identity = WindowsIdentity.GetCurrent())
                        IsElevatedValue = identity != null && new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);

                return IsElevatedValue.Value;
            }
        }

        public static string ExecutablePath {
            get => ExecutablePathValue;
            set {
                ExecutablePathValue = string.IsNullOrWhiteSpace(value) || !File.Exists(value)
                    ? Assembly.GetEntryAssembly().Location
                    : value;
            }
        }

        public static int RunElevatedTask(string args) {
            if(string.IsNullOrWhiteSpace(args))
                throw new ArgumentException(@"No arguments provided.", nameof(args));

            try {
                Process process = Process.Start(new ProcessStartInfo {
                    UseShellExecute = true,
                    FileName = ExecutablePath,
                    WorkingDirectory = Environment.CurrentDirectory,
                    Arguments = args,
                    Verb = @"runas",
                });

                process.WaitForExit();

                return process.ExitCode;
            } catch(Win32Exception ex) {
                return ex.ErrorCode;
            }
        }

        public static int ToggleWindowTopMost(WindowInfo window, bool switchWindow)
            => ToggleWindowTopMost(window.Handle, switchWindow);

        public static int ToggleWindowTopMost(IntPtr handle, bool switchWindow)
            => RunElevatedTask(switchWindow ? $@"--toggle={handle}" : $@"--toggle={handle} --background={handle}");

        public static void RestartElevated() {
            if(IsElevated)
                return;

            Program.Shutdown();

            Process.Start(new ProcessStartInfo {
                UseShellExecute = true,
                FileName = ExecutablePath,
                WorkingDirectory = Environment.CurrentDirectory,
                Arguments = string.Join(@" ", Environment.GetCommandLineArgs()),
                Verb = @"runas",
            });

            Application.Exit();
        }
    }
}
