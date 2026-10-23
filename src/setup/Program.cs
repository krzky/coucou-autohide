using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CoucouAutoHideSetup
{
    internal static class Program
    {
        private const string AppName = "Coucou auto-hide";
        private const string Version = "1.1.0";
        private const string ExeName = "CoucouAutoHide.exe";
        private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CoucouAutoHide";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private static string DefaultDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\CoucouAutoHide"); }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            bool silent = Has(args, "--silent");
            bool uninstall = Has(args, "--uninstall");
            bool noAutostart = Has(args, "--no-autostart");
            bool noShortcut = Has(args, "--no-shortcut");
            bool noLaunch = Has(args, "--no-launch");
            string dir = Value(args, "--dir=") ?? DefaultDir;

            try
            {
                if (uninstall)
                {
                    if (!silent)
                    {
                        var answer = MessageBox.Show(
                            "Remove " + AppName + " from this account?\n\n" +
                            "The app, its Start Menu shortcut and its autostart entry will be deleted.",
                            AppName + " — uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (answer != DialogResult.Yes) return;
                    }
                    Uninstall(dir, silent);
                    if (!silent) MessageBox.Show(AppName + " has been removed.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (silent)
                {
                    Install(dir, !noAutostart, !noShortcut, !noLaunch);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var form = new SetupForm(dir, !noAutostart))
                {
                    if (form.ShowDialog() != DialogResult.OK) return;
                    Install(form.TargetDir, form.Autostart, true, true);
                    MessageBox.Show(
                        AppName + " " + Version + " is installed.\n\n" +
                        "It waits for Coucou to be running and then slides the overlay behind the top edge of the screen. " +
                        "Move the pointer to the top edge to bring it back.\n\n" +
                        "To remove it: Settings → Apps → " + AppName + ", or run the uninstaller from the install folder.",
                        AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                if (silent) { Console.Error.WriteLine(ex.Message); Environment.Exit(1); }
                MessageBox.Show("Setup failed:\n\n" + ex.Message, AppName + " — error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool Has(string[] args, string flag)
        {
            foreach (var a in args) if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string Value(string[] args, string prefix)
        {
            foreach (var a in args)
                if (a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return a.Substring(prefix.Length).Trim('"');
            return null;
        }

        private static void Install(string dir, bool autostart, bool shortcut, bool launch)
        {
            Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, ExeName);
            string uninstaller = Path.Combine(dir, "Uninstall.exe");

            StopApp();

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.exe"))
            {
                if (stream == null) throw new InvalidOperationException("payload missing from the installer");
                using (var file = File.Create(exe)) stream.CopyTo(file);
            }

            // Keep a copy of this installer next to the app so the uninstall entry has something to run.
            string self = Assembly.GetExecutingAssembly().Location;
            if (!string.Equals(self, uninstaller, StringComparison.OrdinalIgnoreCase)) File.Copy(self, uninstaller, true);

            if (shortcut) CreateShortcut(exe);
            WriteUninstallEntry(dir, exe, uninstaller);
            if (autostart) SetAutostart(exe); else ClearAutostart();

            if (launch) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }

        private static void Uninstall(string dir, bool silent)
        {
            StopApp();
            ClearAutostart();

            using (var key = Registry.CurrentUser.OpenSubKey(UninstallKey, true))
                if (key != null) { key.Close(); Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); }

            DeleteShortcut();

            string self = Assembly.GetExecutingAssembly().Location;
            try { if (File.Exists(Path.Combine(dir, ExeName))) File.Delete(Path.Combine(dir, ExeName)); } catch { }
            try { if (File.Exists(Path.Combine(dir, "README.txt"))) File.Delete(Path.Combine(dir, "README.txt")); } catch { }

            // This process lives in dir (as Uninstall.exe); let a detached shell remove the rest.
            var psi = new ProcessStartInfo("cmd.exe",
                "/c ping -n 3 127.0.0.1 >nul & del /f /q \"" + self + "\" & rmdir /s /q \"" + dir + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
        }

        private static void StopApp()
        {
            foreach (var p in Process.GetProcessesByName("CoucouAutoHide"))
            {
                try { p.Kill(); p.WaitForExit(3000); } catch { }
            }
        }

        private static void SetAutostart(string exe)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                if (key != null) key.SetValue("CoucouAutoHide", "\"" + exe + "\"");
        }

        private static void ClearAutostart()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                if (key != null && key.GetValue("CoucouAutoHide") != null) key.DeleteValue("CoucouAutoHide", false);
        }

        private static void WriteUninstallEntry(string dir, string exe, string uninstaller)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                if (key == null) return;
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", Version);
                key.SetValue("Publisher", "Kay (krzky)");
                key.SetValue("InstallLocation", dir);
                key.SetValue("DisplayIcon", exe);
                key.SetValue("UninstallString", "\"" + uninstaller + "\" --uninstall");
                key.SetValue("QuietUninstallString", "\"" + uninstaller + "\" --uninstall --silent");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", 320, RegistryValueKind.DWord);
            }
        }

        private static string ShortcutPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");
            }
        }

        private static void CreateShortcut(string exe)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic lnk = shell.CreateShortcut(ShortcutPath);
            lnk.TargetPath = exe;
            lnk.WorkingDirectory = Path.GetDirectoryName(exe);
            lnk.IconLocation = exe + ",0";
            lnk.Description = "Slides the Coucou overlay behind the top edge of the screen";
            lnk.Save();
        }

        private static void DeleteShortcut()
        {
            try { if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath); } catch { }
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly TextBox _path;
        private readonly CheckBox _autostart;

        public SetupForm(string dir, bool autostart)
        {
            Text = "Coucou auto-hide " + "1.1.0" + " — setup";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(470, 250);
            Font = new Font("Segoe UI", 9f);

            var title = new Label
            {
                Text = "Coucou auto-hide",
                Font = new Font("Segoe UI Semibold", 15f),
                AutoSize = true,
                Location = new Point(18, 16)
            };

            var blurb = new Label
            {
                Text = "Slides the Coucou overlay up behind the top edge of the screen and brings it back\n" +
                       "when the pointer touches that edge. It moves Coucou's window only — nothing is\n" +
                       "patched or injected, and Coucou itself is untouched.",
                AutoSize = true,
                Location = new Point(20, 52)
            };

            var pathLabel = new Label { Text = "Install for this account in:", AutoSize = true, Location = new Point(20, 118) };
            _path = new TextBox { Text = dir, Location = new Point(22, 138), Width = 340 };
            var browse = new Button { Text = "Change…", Location = new Point(370, 137), Width = 80 };
            browse.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog { SelectedPath = _path.Text })
                    if (dlg.ShowDialog() == DialogResult.OK) _path.Text = dlg.SelectedPath;
            };

            _autostart = new CheckBox
            {
                Text = "Start with Windows",
                Checked = autostart,
                AutoSize = true,
                Location = new Point(22, 170)
            };

            var install = new Button { Text = "Install", Location = new Point(270, 202), Width = 90, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", Location = new Point(366, 202), Width = 84, DialogResult = DialogResult.Cancel };

            Controls.AddRange(new Control[] { title, blurb, pathLabel, _path, browse, _autostart, install, cancel });
            AcceptButton = install;
            CancelButton = cancel;
        }

        public string TargetDir { get { return _path.Text.Trim(); } }
        public bool Autostart { get { return _autostart.Checked; } }
    }
}
