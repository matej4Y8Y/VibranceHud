using System;
using Microsoft.Win32;

namespace VibranceHud.SystemTweaks
{
    /// <summary>
    /// The real registry, backing <see cref="IRegistryAccess"/>. Deliberately thin - all the
    /// tweak logic lives (and is tested) in <see cref="RegistryTweak"/>; this just does I/O.
    ///
    /// DWORDs are normalised to unsigned decimal both ways, so a value like NetworkThrottling's
    /// 0xFFFFFFFF reads back as "4294967295" (not the signed "-1"), matching how the catalog
    /// writes it - otherwise IsApplied would never agree with Apply.
    /// </summary>
    public sealed class RegistryAccess : IRegistryAccess
    {
        private static RegistryKey Hive(RegistryRoot root) =>
            root == RegistryRoot.LocalMachine ? Registry.LocalMachine : Registry.CurrentUser;

        public string? GetValue(RegistryRoot root, string subKey, string name)
        {
            using var key = Hive(root).OpenSubKey(subKey);
            var raw = key?.GetValue(name);
            return raw switch
            {
                null => null,
                int i => ((uint)i).ToString(),   // DWORD -> unsigned decimal
                _ => raw.ToString(),
            };
        }

        public void SetValue(RegistryRoot root, string subKey, string name, string value, RegistryKind kind)
        {
            using var key = Hive(root).CreateSubKey(subKey, writable: true);
            if (kind == RegistryKind.DWord)
                key.SetValue(name, unchecked((int)uint.Parse(value)), RegistryValueKind.DWord);
            else
                key.SetValue(name, value, RegistryValueKind.String);
        }

        public void DeleteValue(RegistryRoot root, string subKey, string name)
        {
            using var key = Hive(root).OpenSubKey(subKey, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
    }
}
