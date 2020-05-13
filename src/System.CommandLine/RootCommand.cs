// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace System.CommandLine
{
    /// <summary>
    /// A command representing an application entry point.
    /// </summary>
    public class RootCommand : Command
    {
        /// <summary>
        /// Create a new instance of RootCommand
        /// </summary>
        /// <param name="description">The description of the command shown in help.</param>
        public RootCommand(string description = "") : base(ExecutableName, description)
        {
        }

        /// <summary>
        /// The name of the command. Defaults to the executable name.
        /// </summary>
        public override string Name
        {
            get => base.Name;
            set
            {
                base.Name = value;
                AddAlias(Name);
            }
        }

        private static readonly Lazy<string> _executablePath = new Lazy<string>(() =>
        {
            return GetAssembly().Location;
        });

        private static readonly Lazy<string> _executableName = new Lazy<string>(() =>
        {

            var location = GetApplicationArguments().FirstOrDefault();
            return Path.GetFileNameWithoutExtension(location).Replace(" ", "");
        });
        
        private static string[]? s_arguments;

        private static string[] GetApplicationArguments()
        {
            // Environment.GetCommandLineArgs doesn't include arguments passed to the runtime.
            // We use a native API to get all arguments.

            if (s_arguments != null)
            {
                return s_arguments;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                s_arguments = File.ReadAllText($"/proc/{Process.GetCurrentProcess().Id}/cmdline").Split('\0');
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IntPtr ptr = GetCommandLine();
                string commandLine = Marshal.PtrToStringAuto(ptr);
                s_arguments = CommandLineToArgs(commandLine);
            }
            else
            {
                throw new PlatformNotSupportedException($"{nameof(GetApplicationArguments)} is not supported on this platform.");
            }

            return s_arguments;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetCommandLine();

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

        public static string[] CommandLineToArgs(string commandLine)
        {
            int argc;
            var argv = CommandLineToArgvW(commandLine, out argc);
            if (argv == IntPtr.Zero)
                throw new Win32Exception();
            try
            {
                var args = new string[argc];
                for (var i = 0; i < args.Length; i++)
                {
                    var p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                    args[i] = Marshal.PtrToStringUni(p);
                }

                return args;
            }
            finally
            {
                Marshal.FreeHGlobal(argv);
            }
        }
        private static Assembly GetAssembly() =>
            Assembly.GetEntryAssembly() ??
            Assembly.GetExecutingAssembly();

        /// <summary>
        /// The name of the currently running executable.
        /// </summary>
        public static string ExecutableName => _executableName.Value;

        /// <summary>
        /// The path to the currently running executable.
        /// </summary>
        public static string ExecutablePath => _executablePath.Value;
    }
}
