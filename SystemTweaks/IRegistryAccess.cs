namespace VibranceHud.SystemTweaks
{
    /// <summary>Which registry hive a value lives under.</summary>
    public enum RegistryRoot { CurrentUser, LocalMachine }

    /// <summary>The kind of registry value to write (gaming tweaks are almost all DWORDs).</summary>
    public enum RegistryKind { DWord, String }

    /// <summary>
    /// The narrow slice of the Windows registry the tweaks need. Abstracted so tweak logic
    /// can be tested against an in-memory fake - no touching HKLM in a unit test.
    /// </summary>
    public interface IRegistryAccess
    {
        /// <summary>The value's data as a string, or null if the value doesn't exist.</summary>
        string? GetValue(RegistryRoot root, string subKey, string name);

        void SetValue(RegistryRoot root, string subKey, string name, string value, RegistryKind kind);

        /// <summary>Remove a value (used to restore "the value simply wasn't there").</summary>
        void DeleteValue(RegistryRoot root, string subKey, string name);
    }
}
