# Discord message — about the Windows warning

Post this **before** people start downloading, and pin it. Being told first reads as
nothing to hide; being surprised by it reads as something hidden.

---

**Heads up before you install PlexusX**

Windows is going to warn you. You'll get "Windows protected your PC" — click **More info**,
then **Run anyway**. A couple of antivirus engines might flag it too.

Here's the real reason, because you deserve better than "just turn your antivirus off":

PlexusX isn't code-signed yet. A signing certificate costs a few hundred a year and needs
identity checks — it's coming, but it isn't done. Without it Windows has no publisher name
to check, so it warns about the file itself.

On top of that, PlexusX does three things scanners don't like seeing together: it changes
Windows settings, it asks for admin to do it, and it watches which programs are running so
it can spot your game launching. That's the whole product. An unsigned installer that does
all three gets flagged on pattern, not on anything anyone found in it.

Two things worth knowing:

- It's around 2 engines out of ~70, and they report a generic heuristic — not a named threat
- After you've installed it, updates come through the app and **never show this warning
  again**. It's a one-time thing.

Don't take my word for it — scan it yourself before you run it, and ask me about anything it
reports. I'd rather answer that than have you quietly not install it.
