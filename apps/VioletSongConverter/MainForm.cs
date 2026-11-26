namespace VioletSongConverter;

/// <summary>The main window: list the songs, shrink the ticked ones to save space, or edit (trim) one.</summary>
public sealed class MainForm : Form
{
    private readonly TextBox modBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
    private readonly Button modBrowse = new() { Text = "Browse…", AutoSize = true };
    private readonly Button reload = new() { Text = "Reload", AutoSize = true };

    private readonly ListView list = new()
    {
        View = View.Details, CheckBoxes = true, FullRowSelect = true, GridLines = false, HideSelection = false,
        Dock = DockStyle.Fill,
    };
    private readonly Button selectAll = new() { Text = "Select all", AutoSize = true };
    private readonly Button selectNone = new() { Text = "Select none", AutoSize = true };
    private readonly Button biggestFirst = new() { Text = "Biggest first", AutoSize = true };
    private readonly Button edit = new() { Text = "Edit song…", AutoSize = true, Enabled = false };
    private readonly Button restore = new() { Text = "Restore backup…", AutoSize = true, Enabled = false };
    private readonly Label backupLabel = new() { AutoSize = true, ForeColor = VioletTheme.Muted, Padding = new Padding(4, 6, 0, 0) };

    /// <summary>Shown when no song is highlighted: Edit and Restore work on the highlighted song, never the ticked ones.</summary>
    private const string EditHint = "Click a song's name to highlight it for Edit song or Restore backup. Tick boxes are only for Shrink.";
    private readonly Label countLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(8, 6, 0, 0), ForeColor = VioletTheme.Lilac };

    private readonly Label shrinkNote = new()
    {
        AutoSize = true, ForeColor = VioletTheme.Muted, Margin = new Padding(0, 0, 0, 6),
        Anchor = AnchorStyles.Left | AnchorStyles.Right, // wraps to the window's width
        Text = "Re-saves the ticked songs at about 192 kbps, in place. They sound the same in game: songs from YouTube are at most " +
               "about 160 kbps, and Soundy saves them at about 500 kbps. Songs that are already small are skipped. " +
               "Each song's old copy is kept as a backup for 7 days, then deleted. " +
               "Remove unused files takes out song files that no song group uses, such as old copies Soundy leaves behind " +
               "when a song is replaced; they're also kept as backups for 7 days.",
    };

    private readonly Button shrink = VioletTheme.Primary(new Button { Text = "Shrink selected songs", AutoSize = true, Padding = new Padding(12, 4, 12, 4) });
    private readonly Button removeUnused = new() { Text = "Remove unused files…", AutoSize = true };
    private readonly Button putBack = new() { Text = "Put back removed files", AutoSize = true, Enabled = false };
    private readonly VioletProgress progress = new() { Dock = DockStyle.Fill, Height = 20, Margin = new Padding(8, 3, 8, 3) };
    private readonly Label status = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0) };


    private bool busy;

    public MainForm()
    {
        Text = "Violet Song Converter";
        Font = VioletTheme.UiFont();
        MinimumSize = new Size(660, 540);
        Size = new Size(840, 690);
        StartPosition = FormStartPosition.CenterScreen;

        list.Columns.Add("Song", 280);
        list.Columns.Add("Group", 170);
        list.Columns.Add("Length", 70, HorizontalAlignment.Right);
        list.Columns.Add("Size", 80, HorizontalAlignment.Right);
        list.Columns.Add("File", 220);


        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // the window's width, so long notes wrap
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // "Songs" heading
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // mod folder
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // list
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // select buttons
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // "Save space" heading
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // what it does
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // shrink + status

        root.Controls.Add(new VioletHeading("Songs") { Margin = new Padding(0, 0, 0, 6) });
        root.Controls.Add(Row("Music mod folder:", modBox, modBrowse, reload));
        root.Controls.Add(list);

        var selRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 8) };
        selRow.Controls.AddRange(new Control[] { selectAll, selectNone, biggestFirst, countLabel,
            new Label { Width = 20, AutoSize = false }, edit, restore, backupLabel });
        backupLabel.Text = EditHint;
        root.Controls.Add(selRow);

        root.Controls.Add(new VioletHeading("Save space"));

        root.Controls.Add(shrinkNote);

        var goRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 4, Margin = new Padding(0, 6, 0, 0) };
        goRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        goRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        goRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        goRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        goRow.Controls.Add(shrink, 0, 0);
        goRow.Controls.Add(removeUnused, 1, 0);
        goRow.Controls.Add(putBack, 2, 0);
        goRow.Controls.Add(progress, 3, 0);
        goRow.Controls.Add(status, 0, 1);
        goRow.SetColumnSpan(status, 4);
        root.Controls.Add(goRow);

        Controls.Add(root);
        var header = new VioletHeader("Violet Song Converter", "Shrink [Violet] Music Solo Player songs to save space, or trim one and put it back");
        header.SetRight(VioletTheme.TextSizePicker(this, () => busy));
        Controls.Add(header);
        root.BringToFront();
        VioletTheme.Apply(this);

        modBrowse.Click += (_, _) => BrowseModFolder();
        reload.Click += (_, _) => LoadSongs();
        selectAll.Click += (_, _) => SetAll(true);
        selectNone.Click += (_, _) => SetAll(false);
        biggestFirst.Click += (_, _) => SortBy(SizeColumn, descending: true);
        list.ItemChecked += (_, _) => UpdateCount();
        list.ColumnClick += (_, e) => SortBy(e.Column);
        list.SelectedIndexChanged += (_, _) => UpdateSongButtons();
        list.ItemActivate += (_, _) => OpenEditor();
        edit.Click += (_, _) => OpenEditor();
        restore.Click += (_, _) => RestoreBackup();
        shrink.Click += async (_, _) => await ShrinkSelected();
        removeUnused.Click += (_, _) => RemoveUnusedFiles();
        putBack.Click += (_, _) => PutBackRemovedFiles();
        FormClosing += (_, e) =>
        {
            if (busy && MessageBox.Show(this, "Songs are still being shrunk. Stop and close? Songs already shrunk stay shrunk, and the rest are left as they were.", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                e.Cancel = true;
        };

        Shown += (_, _) =>
        {
            SongBackups.CleanUp(); // backups older than a week are deleted to save space
            var found = SongCatalog.FindModFolder();
            if (found != null) { modBox.Text = found; LoadSongs(); }
            else
            {
                Say($"Couldn't find \"{SongCatalog.DefaultModName}\" automatically. Click Browse… and pick its folder in your Penumbra mods folder.", VioletTheme.Amber);
                UpdateCount();
            }
        };
    }

    /// <summary>A labelled row: label, a stretching control, then buttons.</summary>
    private static Control Row(string label, Control main, params Control[] buttons)
    {
        var t = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 + buttons.Length, Margin = new Padding(0, 0, 0, 6) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        t.ColumnStyles.Add(main is ComboBox ? new ColumnStyle(SizeType.AutoSize) : new ColumnStyle(SizeType.Percent, 100));
        foreach (var _ in buttons) t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        if (main is TextBox) main.Dock = DockStyle.Fill;
        t.Controls.Add(main, 1, 0);
        for (var i = 0; i < buttons.Length; i++) t.Controls.Add(buttons[i], 2 + i, 0);
        return t;
    }

    // ---------- loading ----------

    private void LoadSongs()
    {
        list.BeginUpdate();
        list.Items.Clear();
        try
        {
            var folder = modBox.Text.Trim();
            if (!File.Exists(Path.Combine(folder, "meta.json")))
            {
                Say("That folder isn't a Penumbra mod (it has no meta.json).", VioletTheme.Amber);
                return;
            }
            var songs = SongCatalog.Load(folder);
            foreach (var s in songs)
            {
                var item = new ListViewItem(s.Name) { Tag = s };
                item.SubItems.Add(s.Group);
                item.SubItems.Add(s.Length > TimeSpan.Zero ? $"{(int)s.Length.TotalMinutes}:{s.Length.Seconds:00}" : "?");
                item.SubItems.Add(SongReplacer.Mb(FileSize(s.FilePath)));
                item.SubItems.Add(Path.GetRelativePath(folder, s.FilePath));
                list.Items.Add(item);
            }
            if (songs.Count == 0) Say("No songs found in that mod.", VioletTheme.Amber);
            else Say($"Found {songs.Count} songs ({SongReplacer.Mb(songs.Sum(s => FileSize(s.FilePath)))}). Tick songs to shrink them, or select one and click Edit song.");
        }
        catch (Exception e)
        {
            Say($"Couldn't read the mod: {e.Message}", VioletTheme.Bad);
        }
        finally
        {
            list.EndUpdate();
            VioletTheme.FitColumns(list);
            UpdateCount();
        }
    }

    private void BrowseModFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = $"Pick the \"{SongCatalog.DefaultModName}\" folder inside your Penumbra mods folder",
            UseDescriptionForTitle = true,
            InitialDirectory = Directory.Exists(modBox.Text) ? modBox.Text : "",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        modBox.Text = dlg.SelectedPath;
        LoadSongs();
    }

    // ---------- selection ----------

    private void SetAll(bool value)
    {
        list.BeginUpdate();
        foreach (ListViewItem i in list.Items) i.Checked = value;
        list.EndUpdate();
        UpdateCount();
    }

    private void UpdateCount()
    {
        var n = list.CheckedItems.Count;
        countLabel.Text = $"{n} of {list.Items.Count} selected";
        shrink.Enabled = !busy && n > 0;
        removeUnused.Enabled = !busy && list.Items.Count > 0;
        putBack.Enabled = !busy && SongBackups.RemovedFiles().Count > 0;
        UpdateSongButtons();
    }

    /// <summary>
    /// Edit needs one highlighted song; Restore backup also needs that song to have a backup (greyed out otherwise).
    /// The note beside them names the highlighted song, so it's clear which song they act on.
    /// </summary>
    private void UpdateSongButtons()
    {
        var song = SongToEdit();
        edit.Enabled = !busy && song != null;
        var backup = song == null ? null : SongBackups.Find(song.FilePath);
        restore.Enabled = !busy && backup != null;
        backupLabel.Text = song == null
            ? EditHint
            : backup == null
                ? $"Highlighted: {song.Name}"
                : $"Highlighted: {song.Name}  ·  backup from {SongBackup.Day(backup.SavedUtc)}, kept until {SongBackup.Day(backup.ExpiresUtc)}";
        backupLabel.ForeColor = song == null ? VioletTheme.Muted : VioletTheme.Lilac;
    }

    private int sortColumn = -1;
    private bool sortDescending;

    private const int SizeColumn = 3;

    /// <summary>Sorts by a column; clicking the same header again reverses the order.</summary>
    private void SortBy(int column, bool? descending = null)
    {
        sortDescending = descending ?? (column == sortColumn && !sortDescending);
        sortColumn = column;
        var items = list.Items.Cast<ListViewItem>().ToList();
        Func<ListViewItem, IComparable> key;
        if (column == 2) key = i => ((SongEntry)i.Tag!).Length;
        else if (column == SizeColumn) key = i => FileSize(((SongEntry)i.Tag!).FilePath);
        else key = i => i.SubItems[column].Text;
        items = (sortDescending ? items.OrderByDescending(key) : items.OrderBy(key)).ToList();
        list.BeginUpdate();
        list.Items.Clear();
        list.Items.AddRange(items.ToArray());
        list.EndUpdate();
    }

    // ---------- editing ----------

    /// <summary>The highlighted song (ticks don't count: they're only for Shrink).</summary>
    private SongEntry? SongToEdit() =>
        list.SelectedItems.Count == 1 ? (SongEntry)list.SelectedItems[0].Tag! : null;

    private void OpenEditor()
    {
        if (busy) return;
        var song = SongToEdit();
        if (song == null) return;
        using var editor = new EditorForm(song);
        editor.ShowDialog(this);
        if (editor.Replaced)
        {
            LoadSongs(); // shows the new length
            Reselect(song.FilePath);
            Say($"\"{song.Name}\" was replaced in the mod. Its backup is kept for 7 days; Restore backup puts it back.", VioletTheme.Good);
        }
        else UpdateSongButtons();
    }

    private void RestoreBackup()
    {
        if (busy) return;
        var song = SongToEdit();
        var backup = song == null ? null : SongBackups.Find(song.FilePath);
        if (song == null || backup == null) { UpdateSongButtons(); return; }

        var question = $"Put back the backup of \"{song.Name}\" ({song.Group}) from {SongBackup.Day(backup.SavedUtc)}?\n\n" +
                       "The changed song in the mod is replaced by the backup, and the backup is then removed.";
        if (MessageBox.Show(this, question, "Restore backup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            SongBackups.Restore(song.FilePath);
        }
        catch (Exception e)
        {
            MessageBox.Show(this, $"Couldn't restore the backup:\n{e.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        LoadSongs();
        Reselect(song.FilePath);
        Say($"\"{song.Name}\" is back the way it was before it was changed.", VioletTheme.Good);
    }

    private void Reselect(string path)
    {
        foreach (ListViewItem i in list.Items)
        {
            if (!string.Equals(((SongEntry)i.Tag!).FilePath, path, StringComparison.OrdinalIgnoreCase)) continue;
            i.Selected = true;
            i.EnsureVisible();
            break;
        }
        UpdateSongButtons();
    }

    // ---------- shrinking ----------

    private static long FileSize(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }

    /// <summary>
    /// Re-saves each ticked song at about 192 kbps, in place (with a 7-day backup each). Songs already at or near
    /// that size are skipped, so shrinking twice doesn't lower the quality again.
    /// </summary>
    private async Task ShrinkSelected()
    {
        var songs = list.CheckedItems.Cast<ListViewItem>().Select(i => (SongEntry)i.Tag!).ToList();
        if (songs.Count == 0) return;
        var limitKbps = OggEncoder.Kbps * 1.3; // at or below this a song is already small

        var until = SongBackup.Day(DateTime.UtcNow + SongBackups.KeepFor);
        var question = $"Shrink {songs.Count} song{(songs.Count == 1 ? "" : "s")} to about {OggEncoder.Kbps} kbps?\n\n" +
                       "Each song is re-saved in place, with the same name and group. Songs that are already small are skipped.\n\n" +
                       $"The old copies are kept as backups for 7 days, until {until}, then deleted. Until then they still use the space; " +
                       "to undo a song, highlight it and click Restore backup.";
        if (MessageBox.Show(this, question, "Shrink songs", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        SetBusy(true);
        progress.Minimum = 0;
        progress.Maximum = songs.Count;
        progress.Value = 0;
        var failed = new List<string>();
        var skipped = new List<string>();
        long before = 0, after = 0;
        var report = new Progress<(int Done, string Text)>(p => { progress.Value = p.Done; Say(p.Text); });

        await Task.Run(() =>
        {
            IProgress<(int, string)> r = report;
            for (var i = 0; i < songs.Count; i++)
            {
                var s = songs[i];
                var index = i; // the progress note runs later, so it needs its own copy
                var label = $"Shrinking {i + 1} of {songs.Count}: {s.Name}";
                r.Report((i, label + "…"));
                try
                {
                    var size = FileSize(s.FilePath);
                    if (s.Length.TotalSeconds > 0 && size * 8 / 1000.0 / s.Length.TotalSeconds <= limitKbps)
                    {
                        skipped.Add(s.Name);
                        continue;
                    }
                    var clip = AudioClip.Load(s.FilePath);
                    var whole = new ClipRange(clip, 0, clip.Duration, 0, 0, 0);
                    var encoding = new Relay<double>(p => r.Report((index, $"{label}… {p:P0}")));
                    var result = SongReplacer.Replace(s, whole, clip.Frames, encoding);
                    before += result.OldSize;
                    after += result.NewSize;
                }
                catch (Exception e)
                {
                    failed.Add($"{s.Name}: {e.Message}");
                }
            }
            r.Report((songs.Count, "Done."));
        });

        SetBusy(false);
        var done = songs.Count - failed.Count - skipped.Count;
        LoadSongs(); // shows the new sizes
        var text = done > 0
            ? $"Shrank {done} song{(done == 1 ? "" : "s")}: {SongReplacer.Mb(before)} → {SongReplacer.Mb(after)} (saves {SongReplacer.Mb(before - after)} once the backups are deleted on {until})."
            : "No songs were shrunk.";
        if (skipped.Count > 0) text += $" {skipped.Count} already small, skipped.";
        if (failed.Count > 0) text += $" {failed.Count} failed.";
        Say(text, failed.Count > 0 ? VioletTheme.Bad : done > 0 ? VioletTheme.Good : VioletTheme.Amber);
        if (failed.Count > 0)
            MessageBox.Show(this, "These songs couldn't be shrunk (they're unchanged):\n\n" + string.Join("\n", failed), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    // ---------- unused files ----------

    /// <summary>Lists the song files no group uses, and after a confirm backs them up (7 days) and deletes them.</summary>
    private void RemoveUnusedFiles()
    {
        if (busy) return;
        var folder = modBox.Text.Trim();
        List<string> unused;
        try { unused = SongCatalog.UnusedSongFiles(folder); }
        catch (Exception e) { Say($"Couldn't look for unused files: {e.Message}", VioletTheme.Bad); return; }
        if (unused.Count == 0)
        {
            Say("No unused song files: every song file in the mod is used by a song group.", VioletTheme.Good);
            return;
        }

        var total = unused.Sum(f => FileSize(f));
        var shown = unused.Take(15).Select(f => $"  {Path.GetRelativePath(folder, f)}  ({SongReplacer.Mb(FileSize(f))})");
        var more = unused.Count > 15 ? $"\n  …and {unused.Count - 15} more" : "";
        var until = SongBackup.Day(DateTime.UtcNow + SongBackups.KeepFor);
        var question = $"Remove {unused.Count} song file{(unused.Count == 1 ? "" : "s")} that no song group uses ({SongReplacer.Mb(total)})?\n\n" +
                       string.Join("\n", shown) + more + "\n\n" +
                       $"They're kept as backups for 7 days, until {until}, then deleted. Until then, Put back removed files brings them back.";
        if (MessageBox.Show(this, question, "Remove unused files", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        var failed = new List<string>();
        long removed = 0;
        foreach (var f in unused)
        {
            try
            {
                var size = FileSize(f);
                SongBackups.RemoveUnused(f);
                removed += size;
            }
            catch (Exception e)
            {
                failed.Add($"{Path.GetRelativePath(folder, f)}: {e.Message}");
            }
        }
        LoadSongs();
        var done = unused.Count - failed.Count;
        Say($"Removed {done} unused file{(done == 1 ? "" : "s")} ({SongReplacer.Mb(removed)}, freed once the backups are deleted on {until})." +
            (failed.Count > 0 ? $" {failed.Count} couldn't be removed." : ""), failed.Count > 0 ? VioletTheme.Bad : VioletTheme.Good);
        if (failed.Count > 0)
            MessageBox.Show(this, "These files couldn't be removed (they're unchanged):\n\n" + string.Join("\n", failed), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    /// <summary>Puts back every unused file removed in the last week (greyed out when there are none).</summary>
    private void PutBackRemovedFiles()
    {
        if (busy) return;
        var removed = SongBackups.RemovedFiles();
        if (removed.Count == 0) { UpdateCount(); return; }
        var question = $"Put back {removed.Count} removed file{(removed.Count == 1 ? "" : "s")}?\n\n" +
                       string.Join("\n", removed.Take(15).Select(b => $"  {b.SongName}  (removed {SongBackup.Day(b.SavedUtc)})")) +
                       (removed.Count > 15 ? $"\n  …and {removed.Count - 15} more" : "");
        if (MessageBox.Show(this, question, "Put back removed files", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        var failed = new List<string>();
        foreach (var b in removed)
        {
            try { SongBackups.Restore(b.SongPath); }
            catch (Exception e) { failed.Add($"{b.SongName}: {e.Message}"); }
        }
        LoadSongs();
        var done = removed.Count - failed.Count;
        Say($"Put back {done} file{(done == 1 ? "" : "s")}." + (failed.Count > 0 ? $" {failed.Count} couldn't be put back." : ""),
            failed.Count > 0 ? VioletTheme.Bad : VioletTheme.Good);
        if (failed.Count > 0)
            MessageBox.Show(this, "These files couldn't be put back:\n\n" + string.Join("\n", failed), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    /// <summary>
    /// Passes progress on at once, on the same thread. (A Progress&lt;T&gt; made on a worker thread hands its reports to
    /// the thread pool, so a late one could land after the final summary and overwrite it.)
    /// </summary>
    private sealed class Relay<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value) => action(value);
    }

    /// <summary>Shows a status line, coloured for good news, warnings and errors.</summary>
    private void Say(string text, Color? color = null)
    {
        status.Text = text;
        status.ForeColor = color ?? VioletTheme.Text;
    }

    private void SetBusy(bool value)
    {
        busy = value;
        foreach (var c in new Control[] { modBox, modBrowse, reload, list, selectAll, selectNone, biggestFirst })
            c.Enabled = !value;
        UseWaitCursor = value;
        UpdateCount();
    }
}
