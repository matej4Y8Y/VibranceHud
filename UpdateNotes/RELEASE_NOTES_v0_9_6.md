PlexusX 0.9.6

This one is mostly about the Vibrance slider actually doing something on your PC.
If it never seemed to work for you, it probably wasn't you.

WHAT'S NEW

Colours should now show up in screen share and recordings.
Whether your colours reached the people watching used to depend on your graphics card
and monitor setup - it worked for some people and not others, on the exact same version.
PlexusX now keeps Windows drawing the screen in the mode that recording software can
actually see. If your colours never showed up in Discord, OBS or Medal before, try
again with this version.

WHAT'S FIXED

Vibrance now works on AMD and Intel graphics.
Until now the 0-100% range only did anything on NVIDIA cards. On AMD and Intel the
slider moved and nothing changed on screen at all. It now applies properly on any GPU.

Vibrance reaches every monitor.
If you run more than one display, the effect was only being applied to one of them -
and not necessarily the one you actually play on. It now covers all of them.

Sliders don't lag while you drag.
Dragging Saturation or Gamma used to stutter, because every tiny mouse movement
triggered heavy work behind the scenes. Dragging is smooth now.

Any key can be a hotkey.
You're no longer forced into a Ctrl/Alt/Shift combination. Bind a single key if you
want - K, L, PageDown, F13, a mouse macro key, whatever suits you. Keys are also
named properly now instead of showing codes like "0x22".

Hotkeys tell you when they didn't work.
If another program already owns the shortcut you picked, the app now says so right
there instead of silently doing nothing.

Your monitor is left the way you found it.
Closing PlexusX now restores your graphics driver's vibrance setting. Previously it
stayed applied after you quit, which meant your screen kept the effect even with
PlexusX shut down.

No more phantom crash pop-ups.
A few background timers could keep running after a window closed, occasionally
throwing an error dialog on what was otherwise a completely normal exit.

GOOD TO KNOW

Screen share: if colours still don't show for viewers, tell us on Discord and include
your graphics card - that's exactly the case we're chasing. Share your whole screen
rather than a single game window, and play in borderless rather than exclusive
fullscreen.

Your settings, saved profiles, crosshairs and licence all carry over untouched.

Something not working? Come tell us on Discord:
https://discord.gg/Gha6kYq4e
