using System;
using System.Threading;
using System.Windows.Forms;
using VibranceHud.License;

namespace VibranceHud
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // The elevated headless relaunches are both gone: the NVAPI driver tweak in v0.9.0,
            // and the admin-only registry tweak with the system-tweak engine itself. PlexusX
            // no longer changes anything under the hood, so it never needs to run elevated.

            // One copy per user session.
            if (!SingleInstance.TryAcquire())
            {
                SingleInstance.SignalExistingInstance();
                return 0;
            }

            // Clean up old crash logs first - this runs before any UI or device init
            // so a permissions error or full disk doesn't take the app down with it.
            // Always best-effort: Cleanup() never throws.
            CrashLog.Cleanup();

            // PerMonitorV2, not SystemAware.
            //
            // SystemAware makes Windows bitmap-stretch the entire window at any scale factor
            // above 100% - which is most gaming laptops and every 1440p/4K monitor - and
            // stretched text is the single strongest "cheap app" signal there is. V2 renders
            // natively at the monitor's own DPI, and additionally scales the non-client area
            // and dialogs, which V1 leaves behind.
            //
            // This only works because layout goes through Design.Tokens; with the old
            // hardcoded pixel positions, per-monitor awareness would have moved the blur
            // problem into a clipping problem.
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Scroll what the pointer is over, not what has focus. Windows sends the wheel to
            // the focused control, so after clicking a nav button the wheel went up the
            // navigation bar's chain and the page never saw it - the page just looked stuck.
            WheelRouter.Install();

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
                // Beta gate, before anything else. If the published minimum version is above
                // this build, the beta is over and this copy doesn't run - no tray icon, no
                // overlay, no licence check. Uses only the cached requirement so startup never
                // waits on the network; the refresh that can raise it happens later, in the
                // background. A machine that has never reached the status file is never
                // blocked, so a user with no internet keeps working.
                var minimum = AppStatusService.CachedMinimum();
                if (VersionGate.IsBlocked(UpdateService.CurrentVersion, minimum))
                {
                    using var ended = new BetaEndedWindow(AppStatusService.CachedMessage());
                    ended.ShowDialog();
                    return 0;
                }

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
                        // Deliberately a native MessageBox rather than GlassDialog. This runs
                        // when the themed UI is the thing that failed, and can fire before
                        // Theme has ever been applied - so it must not depend on any of it.
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
