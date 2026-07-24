namespace VibranceHud.Cs2
{
    /// <summary>
    /// The recommended Steam launch options for CS2. We deliberately do NOT edit these
    /// ourselves (they live in Steam's own files, which Steam rewrites while running) - the
    /// UI shows them with a Copy button so the user pastes them into Steam themselves.
    ///
    /// The important one is <c>+exec autoexec.cfg</c>: CS2 does not auto-run autoexec.cfg, so
    /// without it none of the config tweaks this app writes would actually take effect.
    /// </summary>
    public static class Cs2LaunchOptions
    {
        public const string Recommended = "-novid -high -nojoy +exec autoexec.cfg";
    }
}
