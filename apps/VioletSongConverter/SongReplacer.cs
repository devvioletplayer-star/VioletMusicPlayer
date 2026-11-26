using NAudio.Wave;

namespace VioletSongConverter;

/// <summary>
/// Replaces a song in the music mod with new audio, in place: encodes it (Ogg Vorbis), wraps it in the song's own
/// .scd, backs the old song up (kept a week), writes the new one, and checks that it reads back. If it doesn't, the
/// old song goes back. Used by the editor (Replace song in mod) and by Shrink selected songs.
/// </summary>
public static class SongReplacer
{
    public sealed record Result(long OldSize, long NewSize);

    public static Result Replace(SongEntry song, ISampleProvider audio, long frames,
        IProgress<double>? encoding = null, IProgress<string>? steps = null)
    {
        var path = song.FilePath;
        var rate = audio.WaveFormat.SampleRate;
        var old = File.ReadAllBytes(path);
        var ogg = OggEncoder.Encode(audio, frames, encoding);
        var bytes = ScdWriter.Rebuild(old, ogg); // checks the old file's layout before anything is changed

        steps?.Report("Saving the backup and the new song…");
        var madeBackup = SongBackups.Find(path) == null;
        SongBackups.Prepare(song, old);
        var unchanged = true; // true while the song in the mod is still the old one
        try
        {
            SongFile.Write(path, bytes);
            unchanged = false;
            steps?.Report("Checking the new song…");
            if (!ReadsBack(path, frames, rate))
            {
                SongFile.Write(path, old);
                unchanged = true;
                throw new InvalidDataException("the new song didn't read back correctly, so the old song was put back");
            }
        }
        catch
        {
            // Keep the backup unless the song is certainly unchanged (then it isn't needed).
            if (madeBackup && unchanged) SongBackups.Delete(path);
            throw;
        }
        return new Result(old.Length, bytes.Length);
    }

    /// <summary>True if the song file opens and plays to (about) the expected length.</summary>
    private static bool ReadsBack(string path, long expectedFrames, int rate)
    {
        try
        {
            var (samples, owner) = ScdAudio.Open(path);
            using (owner)
            {
                var channels = samples.WaveFormat.Channels;
                var buffer = new float[rate * channels];
                long total = 0;
                int n;
                while ((n = samples.Read(buffer, 0, buffer.Length)) > 0) total += n;
                return Math.Abs(total / channels - expectedFrames) <= rate / 10;
            }
        }
        catch
        {
            return false;
        }
    }

    public static string Mb(long bytes) => $"{bytes / 1048576.0:0.0} MB";
}
