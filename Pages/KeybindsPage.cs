using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using VibranceHud.Games;
using VibranceHud.Keybinds;

namespace VibranceHud.Pages
{
    /// <summary>
    /// Drag a command onto a key.
    ///
    /// Only reachable with a game selected, because a bind is meaningless without one - the
    /// commands, the syntax and the file they end up in are all per-game. The nav hides this
    /// tab at Desktop rather than showing an empty page.
    ///
    /// Everything offered here is something the player could already type into the game's own
    /// console. No exploits - see the note on <see cref="GameCommands"/> for why that is a
    /// business decision and not squeamishness.
    /// </summary>
    public sealed class KeybindsPage : GlowPage
    {
        private const int Pad = 28;
        private const int PaletteW = 250;

        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly GameSelection _selection;

        private readonly KeyboardView _keyboard;
        private readonly Panel _palette;
        private readonly Panel _bindList;
        private readonly Label _bindListCaption;
        private readonly Label _runningWarning;
        private readonly Label _title;
        private readonly Label _hint;
        private readonly Label _status;
        private readonly GlassButton _apply;
        private readonly GlassButton _clearAll;

        public KeybindsPage(AppSettings settings, SettingsStore store, GameSelection selection)
        {
            _settings = settings;
            _store = store;
            _selection = selection;

            AutoScroll = true;
            Font = new Font(Theme.FontFamily, 9.5f);

            _title = new Label
            {
                Text = "Keybinds",
                ForeColor = Theme.Text,
                Font = new Font(Theme.FontFamily, 18f, FontStyle.Bold),
                Location = new Point(Pad - 2, Pad),
                AutoSize = true,
                BackColor = Color.Transparent,
            };
            Controls.Add(_title);

            _hint = new Label
            {
                ForeColor = Theme.TextDim,
                Font = new Font(Theme.FontFamily, 9f),
                Location = new Point(Pad, Pad + 30),
                AutoSize = true,
                MaximumSize = new Size(760, 0),
                BackColor = Color.Transparent,
            };
            Controls.Add(_hint);

            // The palette is a real scrollable panel, not painted, because it holds a
            // variable number of draggable items and each needs its own hit testing.
            _palette = new Panel
            {
                BackColor = Color.Transparent,
                AutoScroll = true,
            };
            Controls.Add(_palette);

            _keyboard = new KeyboardView();
            _keyboard.CommandDropped += (_, e) => Assign(e.Key, e.CommandId);
            // Right-click clears - the fastest way to undo a bind without hunting for a button.
            _keyboard.KeyActivated += (_, e) =>
            {
                if (e.Button == MouseButtons.Right) Assign(e.Key, "");
            };
            Controls.Add(_keyboard);

            _bindListCaption = new Label
            {
                Text = UiHelpers.Spaced("YOUR BINDS"),
                Font = new Font(Theme.FontFamily, 7.5f, FontStyle.Bold),
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(300, 18),
            };
            Controls.Add(_bindListCaption);

            // The keyboard shows where the binds are; this shows what they all are, which is
            // the question you actually have when checking your setup or reading it out to
            // somebody. Scanning sixty keys for the handful that are lit is the wrong job.
            _bindList = new Panel { BackColor = Color.Transparent, AutoScroll = true };
            Controls.Add(_bindList);

            // Rust rewrites keys.cfg when it exits, so anything written while it is running is
            // silently thrown away. Same warning the Rust settings page already carries.
            _runningWarning = new Label
            {
                ForeColor = Color.FromArgb(240, 180, 90),
                Font = new Font(Theme.FontFamily, 8.5f),
                BackColor = Color.Transparent,
                AutoSize = true,
                MaximumSize = new Size(760, 0),
                Visible = false,
            };
            Controls.Add(_runningWarning);

            _apply = new GlassButton { Text = "Write to game config", Kind = GlassButtonKind.Primary };
            _apply.Click += (_, _) => Apply();
            Controls.Add(_apply);

            _clearAll = new GlassButton { Text = "Clear all", Kind = GlassButtonKind.Ghost };
            _clearAll.Click += (_, _) => ClearAll();
            Controls.Add(_clearAll);

            _status = new Label
            {
                ForeColor = Theme.TextDim,
                Font = new Font(Theme.FontFamily, 8.5f),
                AutoSize = true,
                MaximumSize = new Size(760, 0),
                BackColor = Color.Transparent,
            };
            Controls.Add(_status);

            _selection.Changed += (_, _) => Rebuild();

            Resize += (_, _) => LayoutContent();
            HandleCreated += (_, _) => { Rebuild(); LayoutContent(); };
            Rebuild();
        }

        // ---- layout ---------------------------------------------------------------------

        private void LayoutContent()
        {
            if (Width <= 0) return;

            int top = Pad + 62;
            int right = Width - Pad - SystemInformation.VerticalScrollBarWidth;
            int boardW = Math.Max(320, right - Pad - PaletteW - 20);

            _palette.SetBounds(Pad, top, PaletteW, Math.Max(200, Height - top - 100));

            int boardX = Pad + PaletteW + 20;
            int boardH = _keyboard.PreferredHeightFor(boardW);
            _keyboard.SetBounds(boardX, top, boardW, boardH);

            int y = top + boardH + 18;

            _bindListCaption.SetBounds(boardX, y, boardW, 18);
            y += 22;
            _bindList.SetBounds(boardX, y, boardW, 150);
            y += 162;

            _runningWarning.Location = new Point(boardX, y);
            if (_runningWarning.Visible) y += _runningWarning.Height + 10;

            _apply.SetBounds(boardX, y, 190, 34);
            _clearAll.SetBounds(boardX + 200, y, 110, 34);
            _status.Location = new Point(boardX, y + 44);

            AutoScrollMinSize = new Size(0, y + 100);
        }

        // ---- content --------------------------------------------------------------------

        private void Rebuild()
        {
            var game = _selection.Current;
            _palette.Controls.Clear();

            if (game == null)
            {
                _hint.Text = "Pick a game at the bottom left to set up its binds.";
                SetEnabled(false);
                _keyboard.Bound = new Dictionary<string, string>();
                _keyboard.Invalidate();
                return;
            }

            var commands = GameCommands.For(game.Id);
            if (commands.Count == 0)
            {
                // Honest rather than empty-and-mysterious: we simply have not catalogued this
                // game's console commands, and inventing them would write junk into a config.
                _hint.Text = $"PlexusX doesn't have a command list for {game.DisplayName} yet. "
                           + "Rust and Counter-Strike 2 are covered.";
                SetEnabled(false);
                _keyboard.Bound = new Dictionary<string, string>();
                _keyboard.Invalidate();
                return;
            }

            _hint.Text = $"Drag a command onto a key. Right-click a key to clear it. "
                       + $"Nothing reaches {game.DisplayName} until you press write.";
            SetEnabled(true);
            BuildPalette(game.Id);
            RefreshKeyboard();
            SetStatus("", Theme.TextDim);
        }

        private void SetEnabled(bool on)
        {
            _apply.Enabled = on;
            _clearAll.Enabled = on;
            _palette.Visible = on;
        }

        private void BuildPalette(string gameId)
        {
            int y = 0;

            foreach (var group in GameCommands.Grouped(gameId))
            {
                _palette.Controls.Add(new Label
                {
                    Text = UiHelpers.Spaced(group.Key.ToString().ToUpperInvariant()),
                    Font = new Font(Theme.FontFamily, 7.5f, FontStyle.Bold),
                    ForeColor = Theme.TextDim,
                    BackColor = Color.Transparent,
                    Location = new Point(2, y),
                    Size = new Size(PaletteW - 24, 18),
                });
                y += 22;

                foreach (var command in group)
                {
                    var item = new CommandChip(command)
                    {
                        Location = new Point(2, y),
                        Size = new Size(PaletteW - 24, 34),
                    };
                    _palette.Controls.Add(item);
                    y += 38;
                }

                y += 8;
            }
        }

        private void RefreshKeyboard()
        {
            var game = _selection.Current;
            if (game == null) return;

            var bound = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var bind in KeybindSet.For(_settings.Keybinds, game.Id))
            {
                var command = GameCommands.ById(game.Id, bind.CommandId);
                if (command != null) bound[bind.Key] = command.Label;
            }

            _keyboard.Bound = bound;
            _keyboard.Invalidate();
            RefreshBindList(game.Id);
            RefreshRunningWarning(game.Id);
        }

        /// <summary>The flat list of everything currently bound, newest layout order by key.</summary>
        private void RefreshBindList(string gameId)
        {
            _bindList.Controls.Clear();

            var binds = KeybindSet.For(_settings.Keybinds, gameId)
                .Select(b => (Bind: b, Command: GameCommands.ById(gameId, b.CommandId)))
                .Where(x => x.Command != null)
                .OrderBy(x => x.Bind.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (binds.Count == 0)
            {
                _bindList.Controls.Add(new Label
                {
                    Text = "Nothing bound yet. Drag a command from the left onto a key.",
                    ForeColor = Theme.TextDim,
                    Font = new Font(Theme.FontFamily, 8.5f),
                    BackColor = Color.Transparent,
                    AutoSize = false,
                    Size = new Size(_bindList.Width - 20, 20),
                    Location = new Point(2, 4),
                });
                return;
            }

            int y = 0;
            foreach (var (bind, command) in binds)
            {
                var row = new BindRow(bind.Key, command!.Label)
                {
                    Location = new Point(2, y),
                    Size = new Size(Math.Max(200, _bindList.Width - 24), 28),
                };
                var key = bind.Key;
                row.Cleared += (_, _) => Assign(key, "");
                _bindList.Controls.Add(row);
                y += 32;
            }
        }

        /// <summary>
        /// Warn when the game is running, for games that rewrite the file we edit.
        ///
        /// Rust regenerates keys.cfg from memory when it exits, so binds written while it is
        /// open are silently discarded - the write appears to succeed and then nothing
        /// happens, which is the worst kind of failure.
        /// </summary>
        private void RefreshRunningWarning(string gameId)
        {
            bool running = false;
            try
            {
                var game = Games.SupportedGames.ById(gameId);
                running = game != null &&
                    System.Diagnostics.Process.GetProcessesByName(game.ProcessName).Length > 0;
            }
            catch { /* a failed process query is not worth a warning of its own */ }

            _runningWarning.Visible = running;
            if (running)
            {
                var name = Games.SupportedGames.ById(gameId)?.DisplayName ?? "The game";
                _runningWarning.Text =
                    $"⚠  {name} is running. Close it first - it rewrites its config on exit and "
                    + "would throw these away.";
            }
            LayoutContent();
        }

        // ---- behaviour ------------------------------------------------------------------

        private void Assign(string key, string commandId)
        {
            if (_selection.Current is not { } game) return;

            _settings.Keybinds = KeybindSet.Assign(_settings.Keybinds, game.Id, key, commandId);
            _store.Save(_settings);
            RefreshKeyboard();

            var command = GameCommands.ById(game.Id, commandId);
            SetStatus(command == null
                ? $"Cleared {key}. Press write to update {game.DisplayName}."
                : $"{command.Label} on {key}. Press write to update {game.DisplayName}.",
                Theme.TextDim);
        }

        private void ClearAll()
        {
            if (_selection.Current is not { } game) return;

            _settings.Keybinds = KeybindSet.ClearGame(_settings.Keybinds, game.Id);
            _store.Save(_settings);
            RefreshKeyboard();
            SetStatus("All binds cleared. Press write to remove them from the game too.", Theme.TextDim);
        }

        /// <summary>
        /// Write the binds into the game's own config.
        ///
        /// Explicit rather than automatic: this edits a file the user may have hand-written,
        /// and doing that silently every time somebody drags a chip would be alarming. The
        /// writer only ever replaces its own marked block, so anything around it survives.
        /// </summary>
        private void Apply()
        {
            if (_selection.Current is not { } game) return;
            if (_selection.Detected is not { } detected)
            {
                SetStatus($"{game.DisplayName} doesn't look installed any more.",
                    Color.FromArgb(240, 130, 130));
                return;
            }

            var path = ConfigPathFor(game.Id, detected.InstallDir);
            if (path == null)
            {
                SetStatus($"PlexusX doesn't know where {game.DisplayName} keeps its config.",
                    Color.FromArgb(240, 130, 130));
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                var existing = File.Exists(path) ? File.ReadAllText(path) : "";

                // Back the file up once, before the first edit, and never overwrite that
                // backup - the point is to preserve what the user had before PlexusX ever
                // touched it, not what it looked like last time.
                var backup = path + ".plexusx-backup";
                if (File.Exists(path) && !File.Exists(backup)) File.Copy(path, backup);

                var block = KeybindWriter.Build(_settings.Keybinds, game.Id);
                File.WriteAllText(path, KeybindWriter.Merge(existing, block));

                int count = KeybindSet.For(_settings.Keybinds, game.Id).Count;
                SetStatus($"Wrote {count} bind{(count == 1 ? "" : "s")} to {Path.GetFileName(path)}. "
                        + "Restart the game, or run the config from its console, to pick them up.",
                    Theme.Accent);
            }
            catch (Exception ex)
            {
                SetStatus("Couldn't write the config: " + ex.Message, Color.FromArgb(240, 130, 130));
            }
        }

        /// <summary>Where each game keeps the file we append binds to.</summary>
        private static string? ConfigPathFor(string gameId, string installDir) => gameId switch
        {
            "rust" => Path.Combine(installDir, "cfg", "keys.cfg"),
            "cs2" => Path.Combine(installDir, "game", "csgo", "cfg", "autoexec.cfg"),
            _ => null,
        };

        private void SetStatus(string text, Color colour)
        {
            _status.ForeColor = colour;
            _status.Text = text;
        }

        /// <summary>
        /// One row of the "your binds" list: the key, what is on it, and a way to remove it.
        ///
        /// Owner-drawn as a single control rather than three stacked ones, so a long list is a
        /// handful of controls instead of a hundred - these sit on the transparent card, and
        /// every transparent control repaints the glass underneath it.
        /// </summary>
        private sealed class BindRow : Control
        {
            private static readonly Font KeyFont = new(Theme.FontFamily, 8f, FontStyle.Bold);
            private static readonly Font LabelFont = new(Theme.FontFamily, 8.5f);

            private readonly string _key;
            private readonly string _label;
            private bool _hover;
            private bool _overClear;

            public event EventHandler? Cleared;

            public BindRow(string key, string label)
            {
                _key = key;
                _label = label;
                SetStyle(ControlStyles.UserPaint
                       | ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.SupportsTransparentBackColor
                       | ControlStyles.ResizeRedraw, true);
                BackColor = Color.Transparent;
                Cursor = Cursors.Hand;
            }

            private Rectangle ClearRect => new(Width - 30, 0, 30, Height);

            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hover = false; _overClear = false; Invalidate();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                bool over = ClearRect.Contains(e.Location);
                if (over == _overClear) return;
                _overClear = over;
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (e.Button == MouseButtons.Left && ClearRect.Contains(e.Location))
                    Cleared?.Invoke(this, EventArgs.Empty);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                if (_hover)
                {
                    using var back = new SolidBrush(Color.FromArgb(28, Theme.GlassEdge));
                    using var path = Glass.RoundedPath(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 6);
                    g.FillPath(back, path);
                }

                // Key badge, so the key reads as a key rather than as another word.
                var badge = new Rectangle(4, 4, 62, Height - 8);
                using (var fill = new SolidBrush(Color.FromArgb(60, Theme.Accent)))
                using (var path = Glass.RoundedPath(badge, 4))
                    g.FillPath(fill, path);
                TextRenderer.DrawText(g, _key.ToUpperInvariant(), KeyFont, badge, Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis);

                TextRenderer.DrawText(g, _label, LabelFont,
                    new Rectangle(74, 0, Width - 110, Height), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                TextRenderer.DrawText(g, "✕", LabelFont, ClearRect,
                    _overClear ? Theme.Accent : Theme.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        /// <summary>
        /// One draggable command in the palette.
        ///
        /// Starts a real drag-drop rather than a click-then-click flow, because the thing
        /// being asked for is spatial - you are choosing a place on a keyboard, and the
        /// gesture should match.
        /// </summary>
        private sealed class CommandChip : Control
        {
            private readonly GameCommand _command;
            private bool _hover;

            public CommandChip(GameCommand command)
            {
                _command = command;
                SetStyle(ControlStyles.UserPaint
                       | ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer
                       | ControlStyles.SupportsTransparentBackColor
                       | ControlStyles.ResizeRedraw, true);
                BackColor = Color.Transparent;
                Cursor = Cursors.Hand;
                // The description is long; a tooltip keeps the chip small without losing it.
                new ToolTip { InitialDelay = 300 }.SetToolTip(this, command.Description);
            }

            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Left)
                    DoDragDrop(_command.Id, DragDropEffects.Copy);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
                using var path = Glass.RoundedPath(rect, 7);

                using (var fill = new SolidBrush(Color.FromArgb(_hover ? 70 : 34, Theme.GlassEdge)))
                    g.FillPath(fill, path);
                using (var pen = new Pen(Color.FromArgb(_hover ? 130 : 60, Theme.GlassEdge), 1f))
                    g.DrawPath(pen, path);

                // Was an undisposed Font allocated on every repaint - a leak, not just churn.
                TextRenderer.DrawText(g, _command.Label, Design.Fonts.CaptionBold,
                    new Rectangle(10, 0, Width - 16, Height), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
