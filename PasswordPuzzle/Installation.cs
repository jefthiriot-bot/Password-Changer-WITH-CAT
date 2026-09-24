using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Windows.Forms;

namespace PasswordPuzzle
{
    public static class Installation
    {
        public const string TaskName = "PasswordPuzzle-Rotation";
        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Password Puzzle");
        public static readonly string Exe = Path.Combine(Folder, "PasswordPuzzle.exe");

        static dynamic Service()
        {
            dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
            service.Connect();
            return service;
        }

        public static void RegisterTask()
        {
            string xml = TaskDefinition.Create(Exe, Folder, DateTime.UtcNow.AddMinutes(1));
            dynamic service = Service();
            try
            {
                dynamic root = service.GetFolder(@"\");
                try
                {
                    // SYSTEM and elevated administrators only. Standard users cannot edit/run the task.
                    dynamic task = root.RegisterTask(TaskName, xml, 6, "SYSTEM", null, 5, "D:P(A;;GA;;;SY)(A;;GA;;;BA)");
                    Marshal.FinalReleaseComObject(task);
                }
                finally { Marshal.FinalReleaseComObject(root); }
            }
            finally { Marshal.FinalReleaseComObject(service); }
        }

        public static void StartWorker()
        {
            dynamic service = Service();
            try
            {
                dynamic root = service.GetFolder(@"\");
                try
                {
                    dynamic task = root.GetTask(TaskName);
                    try { dynamic run = task.Run(null); Marshal.FinalReleaseComObject(run); }
                    finally { Marshal.FinalReleaseComObject(task); }
                }
                finally { Marshal.FinalReleaseComObject(root); }
            }
            finally { Marshal.FinalReleaseComObject(service); }
        }

        public static bool TaskExists()
        {
            try
            {
                dynamic service = Service();
                try
                {
                    dynamic root = service.GetFolder(@"\");
                    try
                    {
                        dynamic task = root.GetTask(TaskName);
                        try { return (bool)task.Enabled; }
                        finally { Marshal.FinalReleaseComObject(task); }
                    }
                    finally { Marshal.FinalReleaseComObject(root); }
                }
                finally { Marshal.FinalReleaseComObject(service); }
            }
            catch (COMException) { return false; }
        }

        public static bool InstallIfNeeded()
        {
            if (string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(Exe), StringComparison.OrdinalIgnoreCase)) return false;
            Directory.CreateDirectory(Folder);
            Storage.RejectLink(Folder);
            Storage.RejectLink(Exe);
            // Program Files inherits administrator-only write access. Keep runtime code out of writable download folders.
            if (File.Exists(Exe))
            {
                var installed = FileVersionInfo.GetVersionInfo(Exe);
                var installedVersion = new Version(installed.FileMajorPart, installed.FileMinorPart, installed.FileBuildPart, installed.FilePrivatePart);
                if (Assembly.GetExecutingAssembly().GetName().Version > installedVersion)
                {
                    Storage.EnsureDirectory();
                    // Wait for account work to finish, then replace only the executable.
                    // The vault, app password and saved schedules remain untouched.
                    using (Storage.Lock())
                    {
                        string staged = Path.Combine(Folder, Guid.NewGuid().ToString("N") + ".exe");
                        try
                        {
                            File.Copy(Application.ExecutablePath, staged, false);
                            File.Replace(staged, Exe, null);
                        }
                        catch (IOException error)
                        {
                            throw new IOException("Close the installed Password Puzzle app, wait for any password change to finish, then open this new EXE again to update it.", error);
                        }
                        finally { if (File.Exists(staged)) File.Delete(staged); }
                    }
                    RegisterTask();
                    CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
                    CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Password Puzzle"));
                }
                Process.Start(new ProcessStartInfo(Exe) { UseShellExecute = true });
                return true;
            }
            File.Copy(Application.ExecutablePath, Exe, false);
            Storage.EnsureDirectory();
            RegisterTask();
            CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
            CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Password Puzzle"));
            Process.Start(new ProcessStartInfo(Exe) { UseShellExecute = true });
            return true;
        }

        static void CreateShortcut(string directory)
        {
            Directory.CreateDirectory(directory);
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            try
            {
                dynamic shortcut = shell.CreateShortcut(Path.Combine(directory, "Password Puzzle.lnk"));
                try
                {
                    shortcut.TargetPath = Exe;
                    shortcut.IconLocation = Exe + ",0";
                    shortcut.WorkingDirectory = Folder;
                    shortcut.Description = "Manage local account passwords and login puzzles";
                    shortcut.Save();
                }
                finally { Marshal.FinalReleaseComObject(shortcut); }
            }
            finally { Marshal.FinalReleaseComObject(shell); }
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            bool worker = args.Length == 1 && args[0] == "--worker";
            try
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT) return 1;
                if (worker) { Storage.Worker(); return 0; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                    throw new InvalidOperationException("Open Password Puzzle as an administrator.");
#if RESET_APP
                if (!File.Exists(Installation.Exe) || !File.Exists(Storage.VaultPath))
                    throw new InvalidOperationException("Open PasswordPuzzle.exe first to set up your app password and the accounts you want to manage.");
#else
                if (Installation.InstallIfNeeded()) return 0;
#endif
                Storage.EnsureDirectory();
                ThemeManager.Sync(Storage.ReadLocked().Theme);
                Application.ThreadException += (sender, e) => MessageBox.Show(e.Exception.Message, "Password Puzzle", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (!UnlockDialog.Open(null, false)) return 0;
                if (!Installation.TaskExists()) Installation.RegisterTask();
#if RESET_APP
                Application.Run(new ResetWindow());
#else
                Application.Run(new MainWindow());
#endif
                return 0;
            }
            catch (Exception e)
            {
                if (!worker) MessageBox.Show(e.Message, "Password Puzzle could not start", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Never log secrets or dump the vault on failure. Task Scheduler retains the exit status.
                return 1;
            }
        }
    }
}
