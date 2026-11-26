namespace VioletSongCheck;

public enum SongStatus { NeedsFixing, WorthAListen, SilenceAtEnd, Clean, CouldNotRead }

/// <summary>What the check found in one song.</summary>
public sealed record SongReport(
    SongEntry Song, double Duration, double LeadSilence, double TrailSilence,
    SongStatus Status, string Details, string Fix, double? SuggestedEnd);

/// <summary>
/// Measures each song's loudness ten times a second and looks for:
///   • a silent gap (1 s or more) followed by more sound: a replay of the song (sounds like its opening),
///     a long stretch of other audio (a minute or more: needs fixing), or a short tail (worth a listen);
///   • a song that is loud right up to its last moment with almost no silence after it (may be cut off);
///   • 5 s or more of silence at the end.
/// "Silence" is quieter than 2% of the song's typical loudness.
/// </summary>
public static class SongAnalyzer
{
    private const int WindowsPerSecond = 10;

    public static SongReport Analyse(SongEntry song)
    {
        float[] rms;
        try { rms = Loudness(song.FilePath); }
        catch (Exception e)
        {
            return new SongReport(song, song.Length.TotalSeconds, 0, 0, SongStatus.CouldNotRead,
                $"Couldn't read the song: {e.Message}", "Check the file, or reinstall the song with Soundy.", null);
        }

        var n = rms.Length;
        var duration = n / (double)WindowsPerSecond;
        if (n == 0)
            return new SongReport(song, 0, 0, 0, SongStatus.CouldNotRead, "The song has no audio.", "Reinstall the song with Soundy.", null);

        var median = Median(rms);
        var threshold = Math.Max(40f / 32768f, median * 0.02f);
        bool Quiet(int k) => rms[k] < threshold;

        var lead = 0;
        while (lead < n && Quiet(lead)) lead++;
        var end = n;
        while (end > 0 && Quiet(end - 1)) end--;
        var leadSec = lead / (double)WindowsPerSecond;
        var trailSec = (n - end) / (double)WindowsPerSecond;

        if (end <= lead)
            return new SongReport(song, duration, duration, 0, SongStatus.NeedsFixing, "The song is silent.", "Reinstall the song with Soundy.", null);

        // Silent gaps of a second or more inside the sound.
        int gapStart = -1, gapEnd = -1, run = -1;
        for (var k = lead; k < end; k++)
        {
            if (Quiet(k)) { if (run < 0) run = k; }
            else
            {
                if (run >= 0 && k - run >= WindowsPerSecond) { gapStart = run; gapEnd = k; }
                run = -1;
            }
        }

        var status = SongStatus.Clean;
        string details = "", fix = "";
        double? suggestedEnd = null;

        if (gapStart >= 0)
        {
            var extra = (end - gapEnd) / (double)WindowsPerSecond;
            var gapLen = (gapEnd - gapStart) / (double)WindowsPerSecond;
            var at = gapStart / (double)WindowsPerSecond;
            if (extra >= 3)
            {
                suggestedEnd = at;
                var m = Math.Min(200, end - gapEnd);
                var replay = m >= 50 && Correlation(rms, lead, rms, gapEnd, m) > 0.6;
                if (replay)
                {
                    status = SongStatus.NeedsFixing;
                    details = $"The song ends at {Fmt(at)}. After {gapLen:0} s of silence it starts again from the beginning ({Fmt(extra)} more).";
                    fix = $"Set the End marker at about {Fmt(at)} in Violet Song Converter and upload the result with Soundy, or reinstall the song.";
                }
                else if (extra >= 60)
                {
                    status = SongStatus.NeedsFixing;
                    details = $"The song ends at {Fmt(at)}. After {gapLen:0} s of silence, {Fmt(extra)} of other audio follows (often talking).";
                    fix = $"Set the End marker at about {Fmt(at)} in Violet Song Converter and upload the result with Soundy, or reinstall the song.";
                }
                else
                {
                    status = SongStatus.WorthAListen;
                    details = $"After {gapLen:0} s of silence at {Fmt(at)}, {extra:0} more seconds of sound follow.";
                    fix = $"Listen to the last {extra:0} seconds. If they aren't part of the song, set the End marker at about {Fmt(at)}.";
                }
            }
        }

        if (status == SongStatus.Clean && trailSec < 2)
        {
            // Loud in the second before the sound stops, with almost no silence after it: may be cut off.
            int a = Math.Max(lead, end - 25), b = Math.Max(lead, end - 15);
            var level = b > a ? rms.Skip(a).Take(b - a).Average() : 0;
            if (level > 0.8 * median)
            {
                status = SongStatus.WorthAListen;
                details = $"Stays loud until about {Fmt(end / (double)WindowsPerSecond)}, then stops suddenly.";
                fix = "Listen to the ending. If it was cut off, reinstall the song with Soundy from a better source (trimming can't add the missing part back).";
            }
        }

        if (status == SongStatus.Clean && trailSec >= 5)
        {
            status = SongStatus.SilenceAtEnd;
            details = $"{trailSec:0} s of silence after the song.";
            fix = "Optional: Auto-trim silence in Violet Song Converter, or keep it as a pause before the next song.";
        }

        if (leadSec >= 3)
            details = (details.Length > 0 ? details + " " : "") + $"{leadSec:0} s of silence before the song starts.";

        return new SongReport(song, duration, leadSec, trailSec, status, details, fix, suggestedEnd);
    }

    /// <summary>Loudness (RMS of all channels) for every tenth of a second.</summary>
    private static float[] Loudness(string path)
    {
        var (provider, owner) = ScdAudio.Open(path);
        using (owner)
        {
            var ch = provider.WaveFormat.Channels;
            var perWindow = Math.Max(1, provider.WaveFormat.SampleRate / WindowsPerSecond) * ch;
            var buffer = new float[perWindow * 20];
            var result = new List<float>();
            double sum = 0;
            var count = 0;
            int read;
            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var i = 0; i < read; i++)
                {
                    sum += buffer[i] * buffer[i];
                    if (++count == perWindow)
                    {
                        result.Add((float)Math.Sqrt(sum / count));
                        sum = 0;
                        count = 0;
                    }
                }
            }
            return result.ToArray();
        }
    }

    private static float Median(float[] values)
    {
        var copy = (float[])values.Clone();
        Array.Sort(copy);
        return copy[copy.Length / 2];
    }

    private static double Correlation(float[] x, int xi, float[] y, int yi, int count)
    {
        double mx = 0, my = 0;
        for (var i = 0; i < count; i++) { mx += x[xi + i]; my += y[yi + i]; }
        mx /= count; my /= count;
        double sxy = 0, sxx = 0, syy = 0;
        for (var i = 0; i < count; i++)
        {
            var dx = x[xi + i] - mx; var dy = y[yi + i] - my;
            sxy += dx * dy; sxx += dx * dx; syy += dy * dy;
        }
        return sxx <= 0 || syy <= 0 ? 0 : sxy / Math.Sqrt(sxx * syy);
    }

    public static string Fmt(double seconds)
    {
        var s = (int)Math.Round(Math.Max(0, seconds));
        return $"{s / 60}:{s % 60:00}";
    }

    public static string Label(SongStatus s) => s switch
    {
        SongStatus.NeedsFixing => "Needs fixing",
        SongStatus.WorthAListen => "Worth a listen",
        SongStatus.SilenceAtEnd => "Silence at end",
        SongStatus.CouldNotRead => "Couldn't read",
        _ => "Clean",
    };
}
