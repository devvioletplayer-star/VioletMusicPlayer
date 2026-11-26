using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VioletSongCheck;

/// <summary>The app's only window: pick the music mod, pick where to save, create the spreadsheet.</summary>
public sealed class MainForm : Form
{
    private readonly TextBox modBox = new() { Dock = DockStyle.Fill };
    private readonly Button modBrowse = new() { Text = "Browse…", AutoSize = true };
    private readonly Label songsLabel = new() { AutoSize = true, ForeColor = VioletTheme.Muted, Padding = new Padding(0, 0, 0, 4) };

    private readonly TextBox folderBox = new() { Dock = DockStyle.Fill };
    private readonly Button folderBrowse = new() { Text = "Browse…", AutoSize = true };
    private readonly TextBox nameBox = new() { Dock = DockStyle.Fill };
    private readonly CheckBox openWhenDone = new() { Text = "Open the spreadsheet when it's done", AutoSize = true, Checked = true };

    private readonly Button create = VioletTheme.Primary(new Button { Text = "Check songs and create spreadsheet", AutoSize = true, Padding = new Padding(12, 4, 12, 4) });
    private readonly VioletProgress progress = new() { Dock = DockStyle.Fill, Height = 20, Margin = new Padding(8, 3, 0, 3) };
    private readonly Label status = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0), MaximumSize = new Size(640, 0) };

    private List<SongEntry> songs = new();
    private bool busy;

    public MainForm()
    {
        Text = "Violet Song Check";
        Font = VioletTheme.UiFont();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Padding = new Padding(14), Width = 680 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 440));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var intro = new Label
        {
            Text = "Checks every song in [Violet] Music Solo Player for silence, extra sound after the song, replays and sudden endings, " +
                   "and saves the results as an Excel spreadsheet.",
            AutoSize = true, MaximumSize = new Size(640, 0), Padding = new Padding(0, 0, 0, 4), ForeColor = VioletTheme.Muted,
        };
        root.Controls.Add(intro, 0, 0); root.SetColumnSpan(intro, 3);

        var modHeading = new VioletHeading("Music mod");
        root.Controls.Add(modHeading, 0, 1); root.SetColumnSpan(modHeading, 3);
        root.Controls.Add(Lbl("Music mod folder:"), 0, 2); root.Controls.Add(modBox, 1, 2); root.Controls.Add(modBrowse, 2, 2);
        root.Controls.Add(songsLabel, 1, 3); root.SetColumnSpan(songsLabel, 2);

        var sheetHeading = new VioletHeading("Spreadsheet");
        root.Controls.Add(sheetHeading, 0, 4); root.SetColumnSpan(sheetHeading, 3);
        root.Controls.Add(Lbl("Save in folder:"), 0, 5); root.Controls.Add(folderBox, 1, 5); root.Controls.Add(folderBrowse, 2, 5);
        root.Controls.Add(Lbl("File name:"), 0, 6); root.Controls.Add(nameBox, 1, 6);
        var ext = new Label { Text = ".xlsx", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = VioletTheme.Muted };
        root.Controls.Add(ext, 2, 6);
        root.Controls.Add(openWhenDone, 1, 7); root.SetColumnSpan(openWhenDone, 2);

        var go = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 12, 0, 0) };
        go.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        go.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        go.Controls.Add(create, 0, 0);
        go.Controls.Add(progress, 1, 0);
        root.Controls.Add(go, 0, 8); root.SetColumnSpan(go, 3);
        root.Controls.Add(status, 0, 9); root.SetColumnSpan(status, 3);

        // The violet header band on top, the form below it; the window sizes itself to fit both.
        var page = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var header = new VioletHeader("Violet Song Check", "Find the songs in [Violet] Music Solo Player that need fixing") { Dock = DockStyle.Fill };
        header.SetRight(VioletTheme.TextSizePicker(this, () => busy));
        page.Controls.Add(header, 0, 0);
        page.Controls.Add(root, 0, 1);
        Controls.Add(page);
        VioletTheme.Apply(this);
        AcceptButton = create;

        folderBox.Text = DownloadsFolder();
        nameBox.Text = $"Violet Music Solo Player - Song Check {DateTime.Now:yyyy-MM-dd}";

        modBrowse.Click += (_, _) => BrowseMod();
        folderBrowse.Click += (_, _) => BrowseFolder();
        modBox.Leave += (_, _) => LoadSongs();
        create.Click += async (_, _) => await Create();
        FormClosing += (_, e) =>
        {
            if (busy && MessageBox.Show(this, "The check is still running. Stop and close?", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                e.Cancel = true;
        };
        Shown += (_, _) =>
        {
            var found = SongCatalog.FindModFolder();
            if (found != null) { modBox.Text = found; LoadSongs(); }
            else SongsSay($"Couldn't find \"{SongCatalog.DefaultModName}\" automatically. Click Browse… and pick its folder.", VioletTheme.Amber);
        };
    }

    private static Label Lbl(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };

    private void LoadSongs()
    {
        songs = new();
        var folder = modBox.Text.Trim();
        try
        {
            if (!File.Exists(Path.Combine(folder, "meta.json"))) { SongsSay("That folder isn't a Penumbra mod (it has no meta.json).", VioletTheme.Amber); return; }
            songs = SongCatalog.Load(folder);
            if (songs.Count == 0) SongsSay("No songs found in that mod.", VioletTheme.Amber);
            else SongsSay($"{songs.Count} songs found.", VioletTheme.Good);
        }
        catch (Exception e)
        {
            SongsSay($"Couldn't read the mod: {e.Message}", VioletTheme.Bad);
        }
        finally
        {
            create.Enabled = !busy && songs.Count > 0;
        }
    }

    private void BrowseMod()
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

    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Where should the spreadsheet be saved?",
            UseDescriptionForTitle = true,
            InitialDirectory = Directory.Exists(folderBox.Text) ? folderBox.Text : DownloadsFolder(),
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) folderBox.Text = dlg.SelectedPath;
    }

    private async Task Create()
    {
        if (songs.Count == 0) { LoadSongs(); if (songs.Count == 0) return; }

        var folder = folderBox.Text.Trim();
        var name = nameBox.Text.Trim();
        if (name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) name = name[..^5];
        if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            MessageBox.Show(this, "Type a file name without any of these characters:  \\ / : * ? \" < > |", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            nameBox.Focus();
            return;
        }
        try { Directory.CreateDirectory(folder); }
        catch (Exception e)
        {
            MessageBox.Show(this, $"Couldn't use that folder:\n{e.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var path = Path.Combine(folder, name + ".xlsx");
        if (File.Exists(path) && MessageBox.Show(this, $"\"{name}.xlsx\" already exists in that folder. Replace it?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        SetBusy(true);
        progress.Maximum = songs.Count;
        progress.Value = 0;
        var list = songs.ToList();
        var modName = SongCatalog.DefaultModName;
        var report = new Progress<(int Done, string Text)>(p => { progress.Value = p.Done; Say(p.Text); });
        try
        {
            await Task.Run(() =>
            {
                IProgress<(int, string)> r = report;
                var results = new List<SongReport>();
                for (var i = 0; i < list.Count; i++)
                {
                    r.Report((i, $"Checking {i + 1} of {list.Count}: {list[i].Name}…"));
                    results.Add(SongAnalyzer.Analyse(list[i]));
                }
                r.Report((list.Count, "Writing the spreadsheet…"));
                ReportWriter.Write(path, modName, results);
            });
            Say($"Saved {Path.GetFileName(path)} in {folder}", VioletTheme.Good);
            if (openWhenDone.Checked) Open(path, select: false);
            else Open(path, select: true);
        }
        catch (IOException e)
        {
            Say("Couldn't save the spreadsheet.", VioletTheme.Bad);
            MessageBox.Show(this, $"Couldn't save the spreadsheet. If it's open in Excel, close it and try again.\n\n{e.Message}",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception e)
        {
            Say("Something went wrong.", VioletTheme.Bad);
            MessageBox.Show(this, e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Say(string text, Color? color = null)
    {
        status.Text = text;
        status.ForeColor = color ?? VioletTheme.Text;
    }

    private void SongsSay(string text, Color color)
    {
        songsLabel.Text = text;
        songsLabel.ForeColor = color;
    }

    private void SetBusy(bool value)
    {
        busy = value;
        foreach (var c in new Control[] { modBox, modBrowse, folderBox, folderBrowse, nameBox, openWhenDone })
            c.Enabled = !value;
        create.Enabled = !value && songs.Count > 0;
        UseWaitCursor = value;
    }

    private static void Open(string path, bool select)
    {
        try
        {
            Process.Start(select
                ? new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }
                : new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            // no program for .xlsx, or Explorer unavailable: not important
        }
    }

    /// <summary>The user's real Downloads folder (follows it if it was moved), else &lt;profile&gt;\Downloads.</summary>
    private static string DownloadsFolder()
    {
        try
        {
            var downloads = new Guid("374DE290-123F-4565-9164-39C4925E467B");
            if (SHGetKnownFolderPath(downloads, 0, IntPtr.Zero, out var p) == 0)
            {
                var s = Marshal.PtrToStringUni(p);
                Marshal.FreeCoTaskMem(p);
                if (!string.IsNullOrEmpty(s)) return s;
            }
        }
        catch
        {
            // fall through
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint flags, IntPtr token, out IntPtr path);
}
