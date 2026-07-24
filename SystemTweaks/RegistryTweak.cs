using System.Collections.Generic;

namespace VibranceHud.SystemTweaks
{
    /// <summary>
    /// One registry value a tweak controls.
    /// <paramref name="OffValue"/> null means "the stock state is that this value doesn't
    /// exist", so reverting deletes it rather than writing a made-up default.
    /// </summary>
    public sealed record RegistrySetting(
        RegistryRoot Root, string SubKey, string Name,
        string OnValue, string? OffValue, RegistryKind Kind = RegistryKind.DWord);

    /// <summary>
    /// A tweak expressed purely as registry values to set - the system-wide analogue of Rust's
    /// convar-based <see cref="Rust.Tweak"/>. Applied/reverted state is read back from the
    /// registry itself, so the toggle stays correct across app restarts.
    /// </summary>
    public sealed class RegistryTweak : ISystemTweak
    {
        private readonly IRegistryAccess _reg;
        private readonly IReadOnlyList<RegistrySetting> _settings;

        public RegistryTweak(IRegistryAccess reg, string id, string label, string description,
            string category, TweakTier tier, string appliedStatus, params RegistrySetting[] settings)
        {
            _reg = reg;
            Id = id;
            Label = label;
            Description = description;
            Category = category;
            Tier = tier;
            AppliedStatus = appliedStatus;
            _settings = settings;
        }

        public string Id { get; }
        public string Label { get; }
        public string Description { get; }
        public string Category { get; }
        public TweakTier Tier { get; }

        /// <summary>The plain-language line shown once the tweak is on.</summary>
        public string AppliedStatus { get; }

        /// <summary>HKLM writes need admin; HKCU ones don't.</summary>
        public bool RequiresAdmin
        {
            get
            {
                foreach (var s in _settings)
                    if (s.Root == RegistryRoot.LocalMachine) return true;
                return false;
            }
        }

        public bool IsApplied()
        {
            foreach (var s in _settings)
                if (_reg.GetValue(s.Root, s.SubKey, s.Name) != s.OnValue)
                    return false;
            return true;
        }

        public SystemTweakResult Apply()
        {
            foreach (var s in _settings)
                _reg.SetValue(s.Root, s.SubKey, s.Name, s.OnValue, s.Kind);
            return new SystemTweakResult(true, AppliedStatus);
        }

        public void Revert()
        {
            foreach (var s in _settings)
            {
                if (s.OffValue == null)
                    _reg.DeleteValue(s.Root, s.SubKey, s.Name);
                else
                    _reg.SetValue(s.Root, s.SubKey, s.Name, s.OffValue, s.Kind);
            }
        }
    }
}
