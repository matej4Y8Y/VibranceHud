using System;
using System.Threading;
using System.Windows.Forms;
using VibranceHud.License;
using VibranceHud.SystemTweaks;

namespace VibranceHud
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Elevated relaunch to apply one admin-only FPS tweak, then exit - no UI, no tray.
            if (SystemTweakService.IsHeadlessTweakInvocation(args))
                return SystemTweakService.RunHeadless(args);

            // Elevated relaunch to apply one NVAPI driver tweak removed in
            // v0.9.0 - see docs/design/specs/2026-07-29-remove-nvidia-tweaks.md.

            // One copy per user session. Deliberately AFTER the headless branch above:
            // those elevated helpers are short-lived, run alongside the real app on
            // purpose, and must never be turned away by the mutex.
            if (!SingleInstance.TryAcquire())
            {
                SingleInstance.SignalExistingInstance();
                return 0;
            }

            // Clean up old crash logs first - this runs before any UI or device init
            // so a permissions error or full disk doesn't take the app down with it.
            // Always best-effort: Cleanup() never throws.
            CrashLog.Cleanup();

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Hook every place an unhandled exception can land so a single bug
            // never silently kills the process. The global try/catch around
            // Application.Run below only sees what Application.Run rethrows -
            // background-thread and WinForms-internal exceptions need separate
            // handlers, otherwise the very first crash after install looks like
            // "the app just disappeared".
            Application.ThreadException += (s, e) => ShowFatal(
                "PlexusX hit an unexpected problem and had to close.\n\n" +
                "The details below would help fix it - please include them in your report.",
                e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                ShowFatal("PlexusX hit a serious problem and had to close.\n\n" +
                    "The details below would help fix it - please include them in your report.",
                    ex ?? new Exception("(no exception info)"));
            };

            try
            {
                // License gate: if no valid key on disk, show the activation dialog
                // BEFORE the tray is created. The dialog is modal so the user can't
                // reach the main window until they enter a key or close the app.
                var license = new LicenseService();
                if (!license.HasValidLicense)
                {
                    using var dialog = new LicenseDialog(license);
                    if (dialog.ShowDialog() != DialogResult.OK)
                        return 0; // user closed the dialog - exit the app
                }

                Application.Run(new TrayApplicationContext(license));
            }
            catch (Exception ex)
            {
                ShowFatal(
                    "PlexusX couldn't start:\n\n" +
                    "Make sure at least one monitor is connected and try again. " +
                    "If this keeps happening, please report it.",
                    ex);
            }
            finally
            {
                // Also covers the early `return 0` when the user closes the activation
                // dialog: without releasing here, a cancelled activation would leave the
                // mutex held until the process fully unwound, and an immediate retry
                // could be bounced as "already running".
                SingleInstance.Release();
            }
            return 0;
        }

        /// <summary>One place to format and show a fatal-error MessageBox. Used by both
                /// the Application.Run catch and the AppDomain/ThreadException hooks so the
                /// user always sees a real dialog with something they can copy into a bug
                /// report, instead of a silent process exit. Writes a per-crash log to
                /// %LocalAppData%\PlexusX\crashes\ BEFORE the dialog so the path is visible
                /// in the message itself - users can attach the file without hunting for it.</summary>
                private static void ShowFatal(string prefix, Exception ex)
                {
                    string logPath = "";
                    try { logPath = CrashLog.Write(ex); } catch { /* CrashLog never throws, but defensive */ }
                    try
                    {
                        var body = prefix + "\n\n" + ex.Message;
                        if (!string.IsNullOrEmpty(logPath))
                            body += "\n\nA crash report was written to:\n" + logPath +
                                    "\nPlease attach that file to your bug report.";
                        MessageBox.Show(body, "PlexusX",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch
                    {
                        // Last-ditch: if MessageBox itself can't show (no interactive desktop,
                        // UAC prompt active, etc.), there's nothing more we can do.
                    }
                }
    }
}
