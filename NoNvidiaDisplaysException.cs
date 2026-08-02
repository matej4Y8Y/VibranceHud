using System;

namespace VibranceHud
{
    /// <summary>
    /// The NVIDIA driver is here and answered, but it isn't driving any display we can reach.
    ///
    /// Its own type because the caller has to tell this apart from the driver not being there
    /// at all. Both end up on the software path, but only one of them means "you have no
    /// NVIDIA GPU" - and telling a laptop owner that is how a working app looks broken.
    /// </summary>
    public sealed class NoNvidiaDisplaysException : Exception
    {
        public NoNvidiaDisplaysException()
            : base("The NVIDIA driver is present but drives no reachable display.") { }
    }
}
