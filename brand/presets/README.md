# Scene preset thumbnail sources

This folder holds the raw Rust screenshots that become the visual chips on
the Display page. They live next to the code on purpose so a future
edit never loses the original.

Naming convention: `<name>-source.png` - the "source" suffix is the
signal that this file is the master copy. Built artifacts live in
the app's data folder at runtime, but the *source of truth* is here.

When a new biome is added, drop the source PNG here with the same
naming pattern, then update `DisplayPresets.cs` to point at it.
