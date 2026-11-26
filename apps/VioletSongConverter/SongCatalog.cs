using System.Text.Json;

namespace VioletSongConverter;

/// <summary>One song in the music mod.</summary>
public sealed record SongEntry(string Name, string Group, string FilePath, TimeSpan Length);

/// <summary>Finds [Violet] Music Solo Player (or [Violet] Music Mod, its old name) through Penumbra's settings and lists its songs.</summary>
public static class SongCatalog
{
    public const string DefaultModName = "[Violet] Music Solo Player";
    /// <summary>The mod's name before it was renamed; copies installed earlier may still use it.</summary>
    public const string OldModName = "[Violet] Music Mod";

    /// <summary>The music mod's folder, found from Penumbra's settings under its current or old name; null if it can't be found.</summary>
    public static string? FindModFolder(string? modName = null)
    {
        foreach (var name in modName != null ? new[] { modName } : new[] { DefaultModName, OldModName })
        {
            var found = FindModFolderNamed(name);
            if (found != null) return found;
        }
        return null;
    }

    private static string? FindModFolderNamed(string modName)
    {
        foreach (var root in PenumbraModRoots())
        {
            if (!Directory.Exists(root)) continue;
            var direct = Path.Combine(root, modName);
            if (File.Exists(Path.Combine(direct, "meta.json"))) return direct;

            // The folder may have a different name from the mod; look at each mod's own name.
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var meta = Path.Combine(dir, "meta.json");
                if (!File.Exists(meta)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(meta));
                    if (doc.RootElement.TryGetProperty("Name", out var n) &&
                        string.Equals(n.GetString(), modName, StringComparison.OrdinalIgnoreCase))
                        return dir;
                }
                catch
                {
                    // not a readable mod; skip it
                }
            }
        }
        return null;
    }

    /// <summary>Penumbra's mod folder, from its settings file (both the current and the older location).</summary>
    private static IEnumerable<string> PenumbraModRoots()
    {
        var configs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "pluginConfigs");
        foreach (var file in new[] { Path.Combine(configs, "Penumbra", "config", "penumbra.json"), Path.Combine(configs, "Penumbra.json") })
        {
            string? dir = null;
            try
            {
                if (File.Exists(file))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    if (doc.RootElement.TryGetProperty("ModDirectory", out var d)) dir = d.GetString();
                }
            }
            catch
            {
                // unreadable; try the next one
            }
            if (!string.IsNullOrWhiteSpace(dir)) yield return dir!;
        }
    }

    /// <summary>Every song option in the mod (any option that replaces a .scd sound file).</summary>
    public static List<SongEntry> Load(string modFolder)
    {
        var songs = new List<SongEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in ReadGroups(modFolder))
        {
            var groupName = group.TryGetProperty("Name", out var gn) ? gn.GetString() ?? "" : "";
            if (!group.TryGetProperty("Options", out var options) || options.ValueKind != JsonValueKind.Array) continue;
            foreach (var opt in options.EnumerateArray())
            {
                if (!opt.TryGetProperty("Files", out var files) || files.ValueKind != JsonValueKind.Object) continue;
                string? rel = null;
                foreach (var f in files.EnumerateObject())
                {
                    if (!f.Name.EndsWith(".scd", StringComparison.OrdinalIgnoreCase)) continue;
                    rel = f.Value.GetString();
                    if (f.Name.StartsWith("sound/", StringComparison.OrdinalIgnoreCase)) break; // prefer the emote sound
                }
                if (string.IsNullOrEmpty(rel)) continue;

                var path = Path.Combine(modFolder, rel.Replace('\\', Path.DirectorySeparatorChar));
                if (!File.Exists(path) || !seen.Add(path)) continue;
                var name = opt.TryGetProperty("Name", out var on) ? on.GetString() ?? Path.GetFileNameWithoutExtension(path) : Path.GetFileNameWithoutExtension(path);
                songs.Add(new SongEntry(name, groupName, path, ScdAudio.Length(path)));
            }
        }
        return songs.OrderBy(s => s.Group, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Song files (.scd) in the mod folder that no option of any group uses: old copies Soundy leaves behind when a
    /// song is replaced, and similar leftovers. Every "Files" list in meta.json, default_mod.json and group_*.json
    /// counts as use. Returns nothing if the mod's settings can't be read, so nothing is ever removed by mistake.
    /// </summary>
    public static List<string> UnusedSongFiles(string modFolder)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var settings = new List<string> { Path.Combine(modFolder, "meta.json"), Path.Combine(modFolder, "default_mod.json") };
        settings.AddRange(Directory.GetFiles(modFolder, "group_*.json"));
        var readAny = false;
        foreach (var file in settings.Where(File.Exists))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                CollectFiles(doc.RootElement, modFolder, used);
                readAny = true;
            }
            catch
            {
                return new List<string>(); // a settings file can't be read: don't guess what's unused
            }
        }
        if (!readAny || used.Count == 0) return new List<string>();

        return Directory.EnumerateFiles(modFolder, "*.scd", SearchOption.AllDirectories)
            .Where(f => !used.Contains(Path.GetFullPath(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Adds the full path of every file named in any "Files" object, anywhere in the JSON.</summary>
    private static void CollectFiles(JsonElement e, string modFolder, HashSet<string> used)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                {
                    if (p.NameEquals("Files") && p.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var f in p.Value.EnumerateObject())
                            if (f.Value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(f.Value.GetString()))
                                used.Add(Path.GetFullPath(Path.Combine(modFolder, f.Value.GetString()!.Replace('\\', Path.DirectorySeparatorChar))));
                    }
                    else CollectFiles(p.Value, modFolder, used);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in e.EnumerateArray()) CollectFiles(item, modFolder, used);
                break;
        }
    }

    /// <summary>Groups live inside meta.json (current Penumbra) or in group_*.json files (older mods).</summary>
    private static IEnumerable<JsonElement> ReadGroups(string modFolder)
    {
        var meta = Path.Combine(modFolder, "meta.json");
        if (File.Exists(meta))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(meta));
            if (doc.RootElement.TryGetProperty("Groups", out var groups) && groups.ValueKind == JsonValueKind.Array)
            {
                var list = groups.EnumerateArray().Select(g => g.Clone()).ToList();
                foreach (var g in list) yield return g;
                yield break;
            }
        }
        foreach (var file in Directory.GetFiles(modFolder, "group_*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            JsonElement el;
            using (var doc = JsonDocument.Parse(File.ReadAllText(file))) el = doc.RootElement.Clone();
            yield return el;
        }
    }
}
