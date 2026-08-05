using System;
using System.Collections.Generic;
using System.Linq;

namespace VibranceHud.Keybinds
{
    /// <summary>What kind of thing a command is, so the palette can group them.</summary>
    public enum CommandCategory
    {
        Combat,
        Utility,
        Comms,
        Fun,
    }

    /// <summary>
    /// One bindable command.
    /// </summary>
    /// <param name="Id">Stable key used in saved binds. Never change one - it would silently
    /// repoint somebody's existing bind at a different command.</param>
    /// <param name="Command">Exactly what goes in the game's config after the key.</param>
    public sealed record GameCommand(
        string Id,
        string Label,
        string Command,
        CommandCategory Category,
        string Description);

    /// <summary>
    /// The bindable commands PlexusX offers, per game.
    ///
    /// **Everything here is a command a player can already type into the game's own console.**
    /// That is the line, and it is deliberate rather than timid. Things like the gc.collect
    /// levitation trick are exploits: they get people banned, and shipping them would put
    /// PlexusX on cheat lists, which would take the legitimate 95% of this feature down with
    /// it. The product's whole position is being the honest tool in a category full of snake
    /// oil - see docs/BRAND-AND-GROWTH.md.
    ///
    /// Nothing here automates aim, fires multiple attacks from one press, or reveals anything
    /// the game does not already show you.
    /// </summary>
    public static class GameCommands
    {
        // ---- Rust ----------------------------------------------------------------------
        //
        // Item ids in the craft commands are Rust's own; they are what the console expects.

        private static readonly GameCommand[] Rust =
        {
            // -- Combat -------------------------------------------------------------------
            new("rust.craft.bandage", "Craft bandage", "craft.add -2072273936 1",
                CommandCategory.Combat, "Queues one bandage without opening the crafting menu."),
            new("rust.craft.bandage5", "Craft 5 bandages", "craft.add -2072273936 5",
                CommandCategory.Combat, "A full raid's worth in one press."),
            new("rust.craft.syringe", "Craft syringe", "craft.add 1079279582 1",
                CommandCategory.Combat, "Queues one medical syringe."),
            new("rust.craft.syringe5", "Craft 5 syringes", "craft.add 1079279582 5",
                CommandCategory.Combat, "Five syringes."),
            new("rust.craft.cancel", "Cancel crafting", "craft.canceltask 0",
                CommandCategory.Combat, "Stops the current craft and refunds it."),
            new("rust.combatlog", "Combat log", "consoletoggle;combatlog",
                CommandCategory.Combat,
                "Opens the console with your last exchanges already printed - who hit you, for how much."),
            new("rust.crouchjump", "Crouch jump", "+duck;+jump",
                CommandCategory.Combat, "Crouch and jump together, for windows and loot rooms."),
            new("rust.crouchtoggle", "Toggle crouch", "duck",
                CommandCategory.Combat, "Stay crouched without holding the key."),

            // -- Movement lives under Utility so Combat stays about fighting ---------------
            new("rust.autorun", "Auto-run", "forward;sprint",
                CommandCategory.Utility, "Runs forward until you press a movement key."),
            new("rust.autoswim", "Auto-swim", "forward;sprint;jump",
                CommandCategory.Utility, "Auto-run that keeps you at the surface while swimming."),

            new("rust.fov.wide", "FOV 90", "graphics.fov 90",
                CommandCategory.Utility, "Widest field of view Rust allows."),
            new("rust.fov.default", "FOV 75", "graphics.fov 75",
                CommandCategory.Utility, "Back to the default field of view."),
            new("rust.fov.zoom", "FOV 60", "graphics.fov 60",
                CommandCategory.Utility, "Narrow view - reads distant players more clearly."),

            new("rust.perf.fps", "Show FPS", "perf 1",
                CommandCategory.Utility, "Frame counter in the corner."),
            new("rust.perf.full", "Show FPS + ping", "perf 2",
                CommandCategory.Utility, "Frames, ping and memory."),
            new("rust.perf.off", "Hide the counter", "perf 0",
                CommandCategory.Utility, "Turns the readout off again."),

            new("rust.grass.off", "Grass off", "grass.displacement false",
                CommandCategory.Utility, "Stops grass bending underfoot. Cheap frames."),
            new("rust.grass.on", "Grass on", "grass.displacement true",
                CommandCategory.Utility, "Puts grass displacement back."),
            new("rust.waves.off", "Flat water", "graphics.waves 0",
                CommandCategory.Utility, "Kills wave motion - helps on the coast."),
            new("rust.lefthand", "Left-handed model", "graphics.vm_horizontal_flip true",
                CommandCategory.Utility, "Flips the weapon to the left of the screen."),
            new("rust.righthand", "Right-handed model", "graphics.vm_horizontal_flip false",
                CommandCategory.Utility, "Flips it back."),

            new("rust.hud", "Toggle HUD", "global.hud",
                CommandCategory.Utility, "Hides the interface - for clean screenshots and clips."),
            new("rust.console", "Toggle console", "consoletoggle",
                CommandCategory.Utility, "Opens and closes the F1 console."),
            new("rust.kill", "Respawn", "kill",
                CommandCategory.Utility, "Kills your character. Careful."),

            // -- Comms --------------------------------------------------------------------
            new("rust.voice", "Push to talk", "+voice",
                CommandCategory.Comms, "Hold to speak."),
            new("rust.audio.quiet", "Mute master", "audio.master 0",
                CommandCategory.Comms, "Silences the game without touching Windows volume."),
            new("rust.audio.normal", "Unmute master", "audio.master 1",
                CommandCategory.Comms, "Back to full game volume."),
            new("rust.voices.mute", "Mute voice chat", "audio.voices 0",
                CommandCategory.Comms, "Keeps game sound, drops other players' voices."),
            new("rust.voices.unmute", "Unmute voice chat", "audio.voices 1",
                CommandCategory.Comms, "Voices back on."),

            // -- Fun ----------------------------------------------------------------------
            new("rust.gesture.wave", "Wave", "gesture wave",
                CommandCategory.Fun, "Friendly, allegedly."),
            new("rust.gesture.ok", "OK sign", "gesture ok",
                CommandCategory.Fun, "The OK gesture."),
            new("rust.gesture.thumbsup", "Thumbs up", "gesture thumbsup",
                CommandCategory.Fun, "Thumbs up."),
            new("rust.gesture.thumbsdown", "Thumbs down", "gesture thumbsdown",
                CommandCategory.Fun, "Thumbs down."),
            new("rust.gesture.shrug", "Shrug", "gesture shrug",
                CommandCategory.Fun, "Shrug."),
            new("rust.gesture.point", "Point", "gesture point",
                CommandCategory.Fun, "Point at something."),
            new("rust.gesture.clap", "Clap", "gesture clap",
                CommandCategory.Fun, "Applause."),
        };

        // ---- Counter-Strike 2 -----------------------------------------------------------
        //
        // Buy binds are the single most-wanted bind set in CS2 and nothing has a decent UI
        // for building them.

        private static readonly GameCommand[] Cs2 =
        {
            new("cs2.buy.ak", "Buy AK-47", "buy ak47",
                CommandCategory.Combat, "Also buys Galil if you're on CT and can't."),
            new("cs2.buy.m4", "Buy M4A1-S", "buy m4a1",
                CommandCategory.Combat, "The CT rifle."),
            new("cs2.buy.awp", "Buy AWP", "buy awp",
                CommandCategory.Combat, "The AWP."),
            new("cs2.buy.deagle", "Buy Deagle", "buy deagle",
                CommandCategory.Combat, "Desert Eagle."),
            new("cs2.buy.armour", "Buy vest + helmet", "buy vesthelm",
                CommandCategory.Combat, "Full armour."),
            new("cs2.buy.defuser", "Buy defuse kit", "buy defuser",
                CommandCategory.Combat, "CT only."),
            new("cs2.buy.nades", "Buy full nades", "buy flashbang;buy smokegrenade;buy hegrenade;buy molotov",
                CommandCategory.Combat, "Flash, smoke, HE and molotov in one press."),

            new("cs2.cleardecals", "Clear decals", "r_cleardecals",
                CommandCategory.Utility, "Wipes bullet holes and blood so you can see."),
            new("cs2.righthand", "Swap gun hand", "toggle cl_righthand 0 1",
                CommandCategory.Utility, "Moves the weapon to the other side of the screen."),
            new("cs2.showteam", "Show team equipment", "+cl_show_team_equipment",
                CommandCategory.Utility, "Hold to see what your team has."),
            new("cs2.netgraph", "Toggle net graph", "toggle cl_showfps 0 1",
                CommandCategory.Utility, "FPS and network readout."),

            new("cs2.voice", "Push to talk", "+voicerecord",
                CommandCategory.Comms, "Hold to speak."),
            new("cs2.radio.needed", "Radio: need backup", "playerradio needbackup",
                CommandCategory.Comms, "Calls for backup without the radio menu."),

            new("cs2.spray", "Spray menu", "+spray_menu",
                CommandCategory.Fun, "Hold to open the spray wheel."),
        };

        /// <summary>Commands available for a game. Empty for games we have not catalogued yet -
        /// an empty list is an honest "nothing here", where inventing plausible commands would
        /// write something broken into somebody's config.</summary>
        public static IReadOnlyList<GameCommand> For(string? gameId) => gameId switch
        {
            "rust" => Rust,
            "cs2" => Cs2,
            _ => Array.Empty<GameCommand>(),
        };

        public static GameCommand? ById(string? gameId, string? commandId) =>
            commandId == null ? null : For(gameId).FirstOrDefault(c => c.Id == commandId);

        /// <summary>Commands for a game, grouped in the order the palette shows them.</summary>
        public static IEnumerable<IGrouping<CommandCategory, GameCommand>> Grouped(string? gameId) =>
            For(gameId).GroupBy(c => c.Category).OrderBy(g => g.Key);
    }
}
