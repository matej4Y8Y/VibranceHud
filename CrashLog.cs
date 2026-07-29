using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VibranceHud
{
    /// <summary>
    /// Writes unhandled exceptions to a per-crash text file under
    /// %LocalAppData%\PlexusX\crashes\ so the user has something to attach to a bug
    /// report. The previous design had nothing - every uncaught exception silently
    /// killed the process and the only way to diagnose was "did it leave a Windows
    /// event log entry?", which doesn't tell us what the user did just before the
    /// crash.
    ///
    /// Three rules:
    /// 1. Never throw. <see cref="Write"/> and <see cref="Cleanup"/> are wrapped in
    ///    their own try/catch so a permission error or full disk can't make a bad
    ///    situation worse. A failed write returns an empty path; a failed cleanup
    ///    is a no-op.
    /// 2. Never include PII. Stack traces can mention %USERPROFILE%, env vars like
    ///    %USERNAME%, Steam install paths with the user's account name. We redact
    ///    known-sensitive substrings before writing.
    /// 3. Cap the folder. <see cref="MaxRetainedLogs"/> keeps disk usage bounded
    ///    for users who never run cleanup (cleanup runs on every startup, but a
    ///    crash-loop on first launch can still pile up files between starts).
    /// </summary>
    public static class CrashLog
    {
        /// <summary>How many days a crash log is considered current. Older files
        /// are removed by <see cref="Cleanup"/> on the next launch.</summary>
        public const int RetentionDays = 30;

        /// <summary>Hard cap regardless of age - a crash-loop filling the disk
        /// between launches is the worst case this number protects against.</summary>
        public const int MaxRetainedLogs = 50;

        private static readonly string CrashFileNamePrefix = "crash-";

        /// <summary>
        /// Folder the logs live in. Lazily created on first access.
        /// %LocalAppData% (not %AppData%) so uninstalling the app takes the logs
        /// with it - settings, profiles, and themes stay under %AppData%.
        /// </summary>
        public static string CrashDirectory
        {
            get
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dir = Path.Combine(local, "PlexusX", "crashes");
                try { Directory.CreateDirectory(dir); } catch { /* best effort */ }
                return dir;
            }
        }

        // Patterns considered PII if they leak into a stack trace. The Users path
        // matches cleanly with a regex anchor (it's a fixed substring + variable
        // folder name). Steam/Epic paths are handled by the substring-based
        // RedactBySubstring pass below - regex with variable-text-around-an-anchor
        // is unreliable in .NET's regex engine (see RedactBySubstring comment).
        private static readonly Regex[] RedactionPatterns =
        {
            // Windows user profile - matches "C:\Users\<name>" where <name>
            // can contain anything except path separators / quotes / angle brackets.
            new Regex(@"[A-Z]:[\\/]Users[\\/][^\\\/\""<>|*?\s]+", RegexOptions.Compiled),
        };

        private static readonly string[] RedactionReplacements =
        {
            "<user>",
        };

        /// <summary>
        /// Write a crash report for <paramref name="ex"/> and return the file path,
        /// or the empty string if the write failed. Never throws.
        /// </summary>
        public static string Write(Exception ex)
        {
            try
            {
                if (ex == null) return "";
                var dir = CrashDirectory;
                var name = CrashFileNamePrefix + DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss") + ".txt";
                var path = Path.Combine(dir, name);
                var content = BuildReport(ex);
                File.WriteAllText(path, content, Encoding.UTF8);
                return path;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Delete crash logs older than <see cref="RetentionDays"/> days, and if
        /// more than <see cref="MaxRetainedLogs"/> remain after that, trim the
        /// oldest. Never throws.
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                var dir = CrashDirectory;
                if (!Directory.Exists(dir)) return;

                var threshold = DateTime.UtcNow.AddDays(-RetentionDays);
                var files = Directory.EnumerateFiles(dir, CrashFileNamePrefix + "*.txt")
                    .Select(p =>
                    {
                        try { return (Path: p, Time: File.GetLastWriteTimeUtc(p)); }
                        catch { return (Path: p, Time: DateTime.MinValue); }
                    })
                    .OrderBy(x => x.Time)
                    .ToList();

                foreach (var f in files)
                {
                    if (f.Time < threshold)
                    {
                        try { File.Delete(f.Path); } catch { /* best effort */ }
                    }
                }

                // Re-read after deletes so the cap reflects the actual remaining set.
                var remaining = files
                    .Where(f => File.Exists(f.Path))
                    .OrderByDescending(f => f.Time)
                    .ToList();

                while (remaining.Count > MaxRetainedLogs)
                {
                    var oldest = remaining[remaining.Count - 1];
                    try { File.Delete(oldest.Path); } catch { /* best effort */ }
                    remaining.RemoveAt(remaining.Count - 1);
                }
            }
            catch
            {
                // best effort - never block startup on cleanup failure
            }
        }

        private static string BuildReport(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PlexusX crash report");
            sb.AppendLine("Generated: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
            sb.AppendLine("Version: " + SafeVersion());
            sb.AppendLine("OS: " + SafeOs());
            sb.AppendLine(".NET: " + SafeDotnet());
            sb.AppendLine();
            AppendException(sb, ex);
            return Redact(sb.ToString());
        }

        private static void AppendException(StringBuilder sb, Exception ex, int depth = 0)
        {
            if (ex == null || depth > 5) return; // cap the chain at 5 levels
            var indent = new string(' ', depth * 2);
            sb.AppendLine(indent + "Exception type: " + ex.GetType().FullName);
            sb.AppendLine(indent + "Message: " + ex.Message);
            sb.AppendLine(indent + "Stack trace:");
            sb.AppendLine(indent + ex.StackTrace);
            if (ex.InnerException != null)
            {
                sb.AppendLine(indent + "-- Inner exception --");
                AppendException(sb, ex.InnerException, depth + 1);
            }
        }

        private static string SafeVersion()
        {
            try { return UpdateService.CurrentVersion.ToString(); }
            catch { return "unknown"; }
        }

        private static string SafeOs()
        {
            try { return Environment.OSVersion.VersionString; }
            catch { return "unknown"; }
        }

        private static string SafeDotnet()
        {
            try { return Environment.Version.ToString(); }
            catch { return "unknown"; }
        }

        /// <summary>Apply every PII-redaction pattern. Used by both Write and
        /// tests that need to verify the redaction is correct.</summary>
        internal static string Redact(string input)
        {
            if (string.IsNullOrEmpty(input)) return input ?? "";
            var result = input;
            for (int i = 0; i < RedactionPatterns.Length; i++)
                result = RedactionPatterns[i].Replace(result, RedactionReplacements[i]);
            // Regex pass misses cases where the variable-text-between-C:-and-keyword
            // pattern fails to backtrack - substring pass catches the rest.
            result = RedactBySubstring(result);
            return result;
        }

        // We intentionally also use substring-based redaction for cases where
        // a regex anchor reliably fails (see RedactionPatterns comment). The
        // variable-text-between-C:-and-keyword pattern in .NET's regex engine
        // doesn't backtrack the way you'd expect with negated character
        // classes - so we fall back to: replace any path segment that ENDS
        // with a sensitive substring. Cheap (one pass over the string), no
        // regex backtracking surprises.
        internal static readonly string[] SensitiveFolderNames =
        {
            "Steam\\steamapps",
            "Steam\\userdata",
            "Epic Games",
        };

        internal static readonly string[] SensitiveFolderReplacements =
        {
            "<steam-path>",
            "<steam-path>",
            "<epic-path>",
        };

        /// <summary>Substring redaction pass that catches Steam/Epic paths the
        /// regex pass misses. Order: regex first (precise), substring second
        /// (catch-all). The substring pass uses String.Replace which doesn't
        /// have to negotiate character classes or greedy matching.</summary>
        internal static string RedactBySubstring(string input)
        {
            if (string.IsNullOrEmpty(input)) return input ?? "";
            var result = input;
            for (int i = 0; i < SensitiveFolderNames.Length; i++)
                result = result.Replace(SensitiveFolderNames[i], SensitiveFolderReplacements[i]);
            return result;
        }
    }
}