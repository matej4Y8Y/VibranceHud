PlexusX 0.9.7

A bug-fix release. Several things that quietly didn't work now do.

IMPORTANT - THE UPDATE BUG

Updating could put you on an OLDER version.
If you installed a new version and the app came back showing an older number, that was
this. The updater checked whether a newer version existed online, but never checked
whether the file it was about to install was older than what you already had - and if it
couldn't reach the internet it skipped checking altogether and installed whatever it
found. It now refuses to install anything older than what you're running, online or not.

If it already happened to you: press Windows+R, type %TEMP%, press Enter, and delete any
files starting with PlexusX-Setup. Then install this version.

WHAT ELSE IS FIXED

The setup screen had invisible text.
On the second onboarding step, the label next to the startup toggle disappeared if you
picked a dark theme - you got a switch with no idea what it did. All the text on that
screen now follows the theme you choose.

The app is less than half the size.
156 MB down to 69 MB installed.

More FPS tweaks, and they're real ones.
Six new toggles in FPS Tweaks, held to the same rule as the rest: each one does something
documented and measurable, no filler.
  - Give Games Top Scheduling - moves games into Windows' highest priority class
  - Favour the Active Window - longer CPU turns for the game you're playing
  - Stop CPU Power Throttling - Windows slows things down to save power; pointless on a
    desktop
  - Turn Off Mouse Acceleration - makes aim 1:1 with your hand. Windows ships this ON,
    and it's the most common thing wrong with an aim setup
  - Hardware GPU Scheduling (advanced) - usually lower latency, needs a restart
  - Disable Fullscreen Optimisations (advanced) - lower latency, slower alt-tab

Every tweak is reversible and reads its real state, so the toggles stay honest after a
restart.

No more phantom crash pop-ups.
Some background timers kept running after a window closed and could throw an error
dialog on what was a completely normal exit.

Your monitor is left as you found it.
Closing PlexusX now restores your graphics driver's vibrance setting instead of leaving
it applied after you quit.

ALREADY IN 0.9.5 AND 0.9.6, IN CASE YOU SKIPPED THEM

Vibrance works on AMD and Intel - the 0-100% range genuinely did nothing on those cards
before. It also reaches every monitor now, not just one. Sliders don't lag while
dragging. Any key can be a hotkey, including single keys like K or PageDown, and the app
tells you if another program already owns the shortcut you picked.

SCREEN SHARE

Still being worked on. Whether your colours reach people watching depends on your
graphics setup in ways we're still pinning down. If they don't show for you, tell us on
Discord and include your graphics card - that's genuinely the most useful thing you can
send. Share your whole screen rather than a single game window, and play in borderless
rather than exclusive fullscreen.

Settings, profiles, crosshairs and your licence all carry over untouched.

https://discord.gg/Gha6kYq4e
