PlexusX 0.9.8

The last beta build. Everything known to be broken is fixed, and the things that aren't
finished are named honestly below rather than left for you to discover.

IF YOU EVER GOT PUT ON AN OLDER VERSION

That was a real bug and it's fixed. The updater checked whether a newer version existed
online but never checked whether the file it was installing was older than what you
already had - and with no internet it skipped checking entirely and installed whatever it
found. It now refuses anything older than what you're running, online or not.

One-time cleanup if it happened to you: press Windows+R, type %TEMP%, press Enter, and
delete any files starting with PlexusX-Setup. Then install this version.

WHAT WORKS NOW THAT DIDN'T

Vibrance on AMD and Intel.
The 0-100% range only ever did anything on NVIDIA cards. On AMD and Intel the slider
moved and nothing happened. If you thought you were using it wrong - you weren't.

Vibrance on every monitor.
It was only being applied to one display, and not necessarily the one you play on.

Sliders don't lag while you drag them.

Any key can be a hotkey.
No more forced Ctrl/Alt/Shift combinations. Bind K, L, PageDown, F13, a mouse macro key -
whatever suits. Keys show their real names instead of codes like "0x22", and if another
program already owns your shortcut, the app now says so instead of silently doing nothing.

The setup screen no longer has invisible text.
Picking a dark theme during setup used to make the label next to the startup toggle
disappear, leaving a switch with no explanation.

No more phantom crash pop-ups.
Background timers could keep running after a window closed and throw an error dialog on
what was a perfectly normal exit.

Your monitor is left as you found it.
Closing PlexusX restores your graphics driver's vibrance instead of leaving it applied
after you quit.

Less than half the size. 156 MB down to 69 MB installed.

FPS TWEAKS

Six new ones, held to the same rule as the rest - each does something documented and
measurable, no filler:
  - Give Games Top Scheduling - moves games into Windows' highest priority class
  - Favour the Active Window - longer CPU turns for the game you're playing
  - Stop CPU Power Throttling - Windows slows things to save power; pointless on a desktop
  - Turn Off Mouse Acceleration - makes aim 1:1 with your hand. Windows ships this ON and
    it's the most common thing wrong with an aim setup
  - Hardware GPU Scheduling (advanced) - usually lower latency, needs a restart
  - Disable Fullscreen Optimisations (advanced) - lower latency, slower alt-tab

Every tweak is reversible and reads its real state, so the toggles stay honest across
restarts.

KNOWN LIMITATION - RECORDING AND SCREEN SHARE

The colour effect shows on your monitor but does not reliably appear in OBS, Discord
screen share or Medal.

The honest reason: the colour is applied at the very last step before your monitor, and
recording software copies the picture before that step. It works for some people because
their setup happens to apply it earlier. This needs a different approach and it's the main
thing being worked on after the beta - it isn't being ignored.

If it doesn't show for you, that's expected, not a fault with your PC. Reporting your
graphics card on Discord genuinely helps.

Your settings, profiles, crosshairs and licence all carry over untouched.

https://discord.gg/Gha6kYq4e
