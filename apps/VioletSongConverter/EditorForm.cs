using System.Globalization;
using NAudio.Wave;

namespace VioletSongConverter;

/// <summary>
/// Song editor: play the song, set where it should start and end, optionally fade in/out, then put the trimmed song
/// straight back in the music mod in place of the old one (with a week-long backup).
/// </summary>
public sealed class EditorForm : Form
{
    private readonly SongEntry song;
    private AudioClip? clip;

    private readonly Label info = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 6), ForeColor = VioletTheme.Muted };
    private readonly WaveformView wave = new() { Dock = DockStyle.Fill };

    private readonly TextBox startBox = new() { Width = 80 };
    private readonly TextBox endBox = new() { Width = 80 };
    private readonly Button startHere = new() { Text = "Start at playhead", AutoSize = true };
    private readonly Button endHere = new() { Text = "End at playhead", AutoSize = true };
    private readonly Button autoTrim = new() { Text = "Auto-trim silence", AutoSize = true };
    private readonly Button resetMarks = new() { Text = "Reset", AutoSize = true };

    private readonly NumericUpDown fadeIn = new() { DecimalPlaces = 1, Increment = 0.5m, Maximum = 10, Width = 60 };
    private readonly NumericUpDown fadeOut = new() { DecimalPlaces = 1, Increment = 0.5m, Maximum = 10, Value = 2, Width = 60 };

    private readonly Button play = new() { Text = "▶ Play", AutoSize = true };
    private readonly Button stop = VioletTheme.Danger(new Button { Text = "■ Stop", AutoSize = true });
    private readonly Button playFromStart = new() { Text = "Play from start", AutoSize = true };
    private readonly Button hearEnding = new() { Text = "Hear the ending", AutoSize = true };
    private readonly Label timeLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(10, 6, 0, 0) };
    private readonly Label lengthLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(10, 6, 0, 0), ForeColor = VioletTheme.Lilac };

    private readonly Button replace = VioletTheme.Primary(new Button { Text = "Replace song in mod…", AutoSize = true, Padding = new Padding(12, 4, 12, 4) });
    private readonly Button close = new() { Text = "Close", AutoSize = true };
    private readonly Label status = new() { AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 8, 0, 0) };

    private WaveOutEvent? output;
    private ClipRange? playing;
    private readonly System.Windows.Forms.Timer ticker = new() { Interval = 50 };
    private bool updatingBoxes;

    /// <summary>True once the song in the mod was replaced, so the main window can show the new length and backup.</summary>
    public bool Replaced { get; private set; }

    private bool replacing;

    public EditorForm(SongEntry song)
    {
        this.song = song;

        Text = $"Edit song — {song.Name}";
        Font = VioletTheme.UiFont();
        MinimumSize = new Size(780, 560);
        Size = new Size(1000, 640);
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var i = 0; i < 5; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        info.Text = "Loading the song…";
        root.Controls.Add(info);
        root.Controls.Add(wave);

        root.Controls.Add(Flow(new Label { Text = "Start:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, startBox, startHere,
            Gap(), new Label { Text = "End:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, endBox, endHere,
            Gap(), autoTrim, resetMarks, lengthLabel));
        root.Controls.Add(Flow(new Label { Text = "Fade in (s):", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, fadeIn,
            Gap(), new Label { Text = "Fade out (s):", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, fadeOut,
            new Label { Text = "A short fade out avoids an abrupt cut.", AutoSize = true, ForeColor = VioletTheme.Muted, Padding = new Padding(10, 6, 0, 0) }));
        root.Controls.Add(Flow(play, stop, Gap(), playFromStart, hearEnding, timeLabel));
        root.Controls.Add(Flow(replace, Gap(), close));
        root.Controls.Add(status);
        Controls.Add(root);
        Controls.Add(new VioletHeader("Edit song", $"{song.Name}  ·  {song.Group}"));
        root.BringToFront();
        VioletTheme.Apply(this);
        CancelButton = close;

        SetEnabled(false);
        close.Enabled = true;

        // ---- events ----
        wave.MarkersChanged += () => ShowMarkers(restart: false);
        wave.MarkersCommitted += RestartIfPlaying;
        wave.SeekRequested += s =>
        {
            if (output?.PlaybackState == PlaybackState.Playing && playing != null) playing.Seek(s);
            else if (output?.PlaybackState == PlaybackState.Paused) StopPlayback(); // next Play starts from the new spot
            UpdateTime();
        };
        startBox.Leave += (_, _) => ApplyTyped(startBox, true);
        endBox.Leave += (_, _) => ApplyTyped(endBox, false);
        startBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { ApplyTyped(startBox, true); e.SuppressKeyPress = true; } };
        endBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { ApplyTyped(endBox, false); e.SuppressKeyPress = true; } };
        startHere.Click += (_, _) => { wave.Start = wave.Playhead; ShowMarkers(); };
        endHere.Click += (_, _) => { wave.End = wave.Playhead; ShowMarkers(); };
        autoTrim.Click += (_, _) => AutoTrim();
        resetMarks.Click += (_, _) => { if (clip == null) return; wave.Start = 0; wave.End = clip.Duration; ShowMarkers(); };
        play.Click += (_, _) => TogglePlay();
        stop.Click += (_, _) => { StopPlayback(); wave.Playhead = wave.Start; UpdateTime(); };
        playFromStart.Click += (_, _) => PlayFrom(wave.Start, keepPlaying: true);
        hearEnding.Click += (_, _) => PlayFrom(Math.Max(wave.Start, wave.End - 8), keepPlaying: true);
        fadeIn.ValueChanged += (_, _) => RestartIfPlaying();
        fadeOut.ValueChanged += (_, _) => RestartIfPlaying();
        replace.Click += async (_, _) => await Replace();
        close.Click += (_, _) => Close();
        ticker.Tick += (_, _) => { if (playing != null) { wave.Playhead = playing.Seconds; UpdateTime(); } };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Space && clip != null && ActiveControl is not ButtonBase and not TextBoxBase and not NumericUpDown)
            {
                TogglePlay();
                e.SuppressKeyPress = true;
            }
        };
        FormClosing += (_, e) =>
        {
            if (!replacing) return;
            e.Cancel = true; // the song in the mod is being written; closing now could leave it half done
            Say("Please wait: the song is still being saved.", VioletTheme.Amber);
        };
        FormClosed += (_, _) => { StopPlayback(); ticker.Dispose(); };
        Shown += async (_, _) => await LoadClip();
    }

    private static Control Gap() => new Label { Width = 14, AutoSize = false };

    private static FlowLayoutPanel Flow(params Control[] controls)
    {
        var f = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0, 6, 0, 0) };
        f.Controls.AddRange(controls);
        return f;
    }

    private void SetEnabled(bool on)
    {
        foreach (var c in new Control[] { startBox, endBox, startHere, endHere, autoTrim, resetMarks, fadeIn, fadeOut, play, stop, playFromStart, hearEnding, replace, wave })
            c.Enabled = on;
    }

    private async Task LoadClip()
    {
        try
        {
            clip = await Task.Run(() => AudioClip.Load(song.FilePath));
        }
        catch (Exception e)
        {
            info.Text = $"Couldn't open the song ({e.Message}).";
            info.ForeColor = VioletTheme.Bad;
            return;
        }
        wave.Clip = clip;
        info.Text = $"Full length {Fmt(clip.Duration)}. " +
                    "Drag the green Start and red End markers, or click the waveform to move the playhead. Space = play / pause.";
        SetEnabled(true);
        ShowMarkers();
        UpdateTime();
    }

    // ---------- markers ----------

    private void ShowMarkers(bool restart = true)
    {
        updatingBoxes = true;
        startBox.Text = Fmt(wave.Start);
        endBox.Text = Fmt(wave.End);
        updatingBoxes = false;
        lengthLabel.Text = $"Trimmed length: {Fmt(wave.End - wave.Start)}";
        if (restart) RestartIfPlaying();
    }

    private void ApplyTyped(TextBox box, bool isStart)
    {
        if (updatingBoxes || clip == null) return;
        if (TryParse(box.Text, out var s))
        {
            if (isStart) wave.Start = s; else wave.End = s;
        }
        else
        {
            Say("Type times like 4:50.5 (minutes:seconds) or 290.5 (seconds).", VioletTheme.Amber);
        }
        ShowMarkers();
    }

    private void AutoTrim()
    {
        if (clip == null) return;
        var (s, e) = clip.SoundBounds();
        wave.Start = Math.Max(0, s - 0.05);
        wave.End = Math.Min(clip.Duration, e + 0.25);
        ShowMarkers();
        Say("Trimmed the silence at both ends. Talking or other sound after the song still needs the End marker moved by hand.");
    }

    // ---------- playback ----------

    private void TogglePlay()
    {
        if (output?.PlaybackState == PlaybackState.Playing) { output.Pause(); play.Text = "▶ Play"; ticker.Stop(); return; }
        if (output?.PlaybackState == PlaybackState.Paused) { output.Play(); play.Text = "❚❚ Pause"; ticker.Start(); return; }
        var from = wave.Playhead < wave.Start || wave.Playhead >= wave.End - 0.05 ? wave.Start : wave.Playhead;
        PlayFrom(from, keepPlaying: true);
    }

    private void PlayFrom(double seconds, bool keepPlaying)
    {
        if (clip == null) return;
        StopPlayback();
        playing = new ClipRange(clip, wave.Start, wave.End, (double)fadeIn.Value, (double)fadeOut.Value, seconds);
        wave.Playhead = playing.Seconds;
        if (!keepPlaying) return;
        output = new WaveOutEvent { DesiredLatency = 120 };
        output.Init(playing);
        output.PlaybackStopped += (_, _) =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() =>
            {
                if (playing != null && playing.Seconds >= wave.End - 0.01) { play.Text = "▶ Play"; ticker.Stop(); wave.Playhead = playing.Seconds; UpdateTime(); }
            }));
        };
        output.Play();
        play.Text = "❚❚ Pause";
        ticker.Start();
    }

    private void RestartIfPlaying()
    {
        if (output?.PlaybackState == PlaybackState.Playing && playing != null)
            PlayFrom(Math.Clamp(playing.Seconds, wave.Start, wave.End), keepPlaying: true);
    }

    private void StopPlayback()
    {
        ticker.Stop();
        if (output != null)
        {
            var o = output;
            output = null;
            o.Stop();
            o.Dispose();
        }
        play.Text = "▶ Play";
    }

    private void UpdateTime() =>
        timeLabel.Text = clip == null ? "" : $"Playhead {Fmt(wave.Playhead)} / {Fmt(clip.Duration)}";

    // ---------- saving ----------

    /// <summary>
    /// Puts the trimmed song back in the mod in place of the old one: backs the old one up (kept a week), writes the
    /// new one in place, and checks that it reads back. If it doesn't, the old song goes back.
    /// </summary>
    private async Task Replace()
    {
        if (clip == null) return;
        StopPlayback();

        var length = wave.End - wave.Start;
        var existing = SongBackups.Find(song.FilePath);
        var until = SongBackup.Day(DateTime.UtcNow + SongBackups.KeepFor);
        var backupLine = existing == null
            ? $"A backup of the song as it is now is kept for 7 days, until {until}, then deleted to save space."
            : $"Your backup from {SongBackup.Day(existing.SavedUtc)} (the song before your first edit) is kept, now until {until}, then deleted to save space.";
        var question = $"Replace \"{song.Name}\" in {song.Group} with the trimmed song ({Fmt(length)})?\n\n{backupLine}\n\n" +
                       "To undo, highlight the song in the main window and click Restore backup.";
        if (MessageBox.Show(this, question, "Replace song in mod", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        var range = new ClipRange(clip, wave.Start, wave.End, (double)fadeIn.Value, (double)fadeOut.Value, wave.Start);
        var frames = clip.FrameAt(wave.End) - clip.FrameAt(wave.Start);
        var encoding = new Progress<double>(p => Say($"Making the new song… {p:P0}"));
        var steps = new Progress<string>(s => Say(s));

        SetEnabled(false);
        close.Enabled = false;
        replacing = true;
        UseWaitCursor = true;
        Say("Making the new song…");
        try
        {
            var result = await Task.Run(() => SongReplacer.Replace(song, range, frames, encoding, steps));
            Replaced = true;
            Say($"Replaced. The song is now {SongReplacer.Mb(result.NewSize)} (was {SongReplacer.Mb(result.OldSize)}). Backup kept until {until}.", VioletTheme.Good);
        }
        catch (Exception e)
        {
            Say("Couldn't replace the song.", VioletTheme.Bad);
            MessageBox.Show(this, $"Couldn't replace the song:\n{e.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            replacing = false;
            close.Enabled = true;
            UseWaitCursor = false;
            SetEnabled(true);
        }
    }

    private void Say(string text, Color? color = null)
    {
        status.Text = text;
        status.ForeColor = color ?? VioletTheme.Text;
    }

    // ---------- time text ----------

    private static string Fmt(double s)
    {
        if (s < 0) s = 0;
        var m = (int)(s / 60);
        return $"{m}:{(s - m * 60).ToString("00.0", CultureInfo.InvariantCulture)}";
    }

    private static bool TryParse(string text, out double seconds)
    {
        seconds = 0;
        var parts = text.Trim().Replace(',', '.').Split(':');
        if (parts.Length is < 1 or > 3) return false;
        foreach (var p in parts)
        {
            if (!double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v < 0) return false;
            seconds = seconds * 60 + v;
        }
        return true;
    }
}
