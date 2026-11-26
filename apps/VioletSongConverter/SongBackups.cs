using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VioletSongConverter;

/// <summary>A song's backup: the song as it was before it was first replaced, kept until <see cref="ExpiresUtc"/>.</summary>
public sealed record SongBackup(string SongPath, string SongName, string Group, DateTime SavedUtc, DateTime ExpiresUtc)
{
    /// <summary>"Mon Nov 16" in the user's own date format.</summary>
    public static string Day(DateTime utc) => utc.ToLocalTime().ToString("ddd MMM d");
}

/// <summary>
/// Backups of songs replaced in the music mod, in %AppData%\Violet\Song backups (two files each: the old .scd and a
/// small .json saying where it came from). A backup is kept for a week after the song's last replace and then
/// deleted, because Soundy's songs are large. Replacing a song again within that week keeps the first backup (the song
/// as Soundy made it, before any edits) and starts the week again.
/// </summary>
public static class SongBackups
{
    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(7);

    /// <summary>The group name stored for files taken out by Remove unused files (they belong to no group).</summary>
    public const string UnusedGroup = "(unused file)";

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Violet", "Song backups");

    private static string Key(string songPath)
    {
        var full = Path.GetFullPath(songPath).ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(full)))[..16];
    }

    private static string DataFile(string songPath) => Path.Combine(Folder, Key(songPath) + ".scd");
    private static string InfoFile(string songPath) => Path.Combine(Folder, Key(songPath) + ".json");

    /// <summary>The song's backup, or null if it has none (or it has run out, in which case it's deleted now).</summary>
    public static SongBackup? Find(string songPath)
    {
        try
        {
            var info = InfoFile(songPath);
            var data = DataFile(songPath);
            if (!File.Exists(info) || !File.Exists(data)) return null;
            var b = JsonSerializer.Deserialize<SongBackup>(File.ReadAllText(info));
            if (b == null) return null;
            if (b.ExpiresUtc <= DateTime.UtcNow) { Delete(songPath); return null; }
            return b;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Makes sure the song has a backup before it's replaced: copies <paramref name="currentBytes"/> if it has none,
    /// otherwise keeps the older one. Either way the backup now lasts a week from today.
    /// </summary>
    public static SongBackup Prepare(SongEntry song, byte[] currentBytes)
    {
        Directory.CreateDirectory(Folder);
        var now = DateTime.UtcNow;
        var existing = Find(song.FilePath);
        var backup = new SongBackup(song.FilePath, song.Name, song.Group, existing?.SavedUtc ?? now, now + KeepFor);
        if (existing == null) File.WriteAllBytes(DataFile(song.FilePath), currentBytes);
        File.WriteAllText(InfoFile(song.FilePath), JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true }));
        return backup;
    }

    /// <summary>Unused files taken out in the last week whose place is still empty (so they can be put back).</summary>
    public static List<SongBackup> RemovedFiles()
    {
        var list = new List<SongBackup>();
        try
        {
            if (!Directory.Exists(Folder)) return list;
            foreach (var info in Directory.GetFiles(Folder, "*.json"))
            {
                try
                {
                    var b = JsonSerializer.Deserialize<SongBackup>(File.ReadAllText(info));
                    if (b != null && b.Group == UnusedGroup && b.ExpiresUtc > DateTime.UtcNow &&
                        File.Exists(Path.ChangeExtension(info, ".scd")) && !File.Exists(b.SongPath))
                        list.Add(b);
                }
                catch
                {
                    // skip it
                }
            }
        }
        catch
        {
            // none
        }
        return list;
    }

    /// <summary>Backs an unused file up (kept a week) and then deletes it from the mod.</summary>
    public static void RemoveUnused(string path)
    {
        var entry = new SongEntry(Path.GetFileName(path), UnusedGroup, path, TimeSpan.Zero);
        Prepare(entry, File.ReadAllBytes(path));
        File.Delete(path);
    }

    /// <summary>Puts the backup back in place of the song, then removes the backup.</summary>
    public static void Restore(string songPath)
    {
        var data = DataFile(songPath);
        if (!File.Exists(data)) throw new FileNotFoundException("the backup is gone");
        SongFile.Write(songPath, File.ReadAllBytes(data));
        Delete(songPath);
    }

    public static void Delete(string songPath)
    {
        try { File.Delete(DataFile(songPath)); } catch { /* try again next time */ }
        try { File.Delete(InfoFile(songPath)); } catch { /* try again next time */ }
    }

    /// <summary>Deletes every backup whose week is up. Called when the app starts.</summary>
    public static void CleanUp()
    {
        try
        {
            if (!Directory.Exists(Folder)) return;
            foreach (var info in Directory.GetFiles(Folder, "*.json"))
            {
                try
                {
                    var b = JsonSerializer.Deserialize<SongBackup>(File.ReadAllText(info));
                    if (b != null && b.ExpiresUtc > DateTime.UtcNow) continue;
                    File.Delete(Path.ChangeExtension(info, ".scd"));
                    File.Delete(info);
                }
                catch
                {
                    // leave it; the next start tries again
                }
            }
            // A song file whose note is missing can't be restored, so it only takes up space.
            foreach (var data in Directory.GetFiles(Folder, "*.scd"))
                if (!File.Exists(Path.ChangeExtension(data, ".json")))
                    try { File.Delete(data); } catch { /* next time */ }
        }
        catch
        {
            // not important
        }
    }
}

/// <summary>Writes a song file in the mod safely: to a temporary file next to it first, then swapped in.</summary>
public static class SongFile
{
    public static void Write(string path, byte[] bytes)
    {
        var temp = path + ".violet-new";
        File.WriteAllBytes(temp, bytes);
        try
        {
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(temp); } catch { /* ignore */ }
            throw;
        }
    }
}
