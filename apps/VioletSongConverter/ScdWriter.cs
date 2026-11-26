namespace VioletSongConverter;

/// <summary>
/// Builds a new .scd song from an existing one and new Ogg Vorbis audio, so a trimmed song can go straight back
/// into the music mod in place of the old one.
///
/// The old file is used as the wrapper: everything before its audio stays exactly as it was (what Soundy wrote),
/// and only the audio part changes. Layout of the audio part (codec 0x06, Ogg Vorbis):
///   entry header (0x20): 0x00 audio size, 0x04 channels, 0x08 sample rate, 0x0C codec, 0x10 loop start,
///                        0x14 loop end, 0x18 size of the extra data, 0x1C (kept)
///   extra data:          a 0x20 Vorbis header (0x00 encryption mode, 0x10 seek table size, 0x14 Vorbis header
///                        size) followed by the seek table: the byte offset of each Ogg page in the audio
///   audio:               plain Ogg Vorbis
/// Checked against two Soundy songs: re-wrapping a song's own audio gives the same layout byte for byte
/// (only the seek table's last entry can differ, which is harmless: every entry points at a real page).
/// </summary>
public static class ScdWriter
{
    public static byte[] Rebuild(byte[] template, byte[] ogg)
    {
        if (template.Length < 0x40 || template[0] != 'S' || template[1] != 'E' || template[2] != 'D' || template[3] != 'B')
            throw new InvalidDataException("the song in the mod isn't an FFXIV sound (.scd) file");
        if (ogg.Length < 4 || ogg[0] != 'O' || ogg[1] != 'g' || ogg[2] != 'g' || ogg[3] != 'S')
            throw new InvalidDataException("the new audio isn't Ogg Vorbis");

        int tables = BitConverter.ToUInt16(template, 0x0E);
        int list = (int)BitConverter.ToUInt32(template, tables + 0x0C);
        int entry = (int)BitConverter.ToUInt32(template, list);
        int dataSize = (int)BitConverter.ToUInt32(template, entry);
        int codec = BitConverter.ToInt32(template, entry + 0x0C);
        int extra = (int)BitConverter.ToUInt32(template, entry + 0x18);
        if (codec != 0x06)
            throw new InvalidDataException("this song isn't stored the way Soundy stores songs (Ogg Vorbis), so it can't be replaced here");

        int vorbis = entry + 0x20;
        int mode = BitConverter.ToUInt16(template, vorbis);
        int seekSize = (int)BitConverter.ToUInt32(template, vorbis + 0x10);
        int headerSize = (int)BitConverter.ToUInt32(template, vorbis + 0x14);
        if (mode != 0 || headerSize != 0 || extra != 0x20 + seekSize)
            throw new InvalidDataException("this song is in the game's encrypted form, which can't be replaced here");

        // The audio must be the last thing in the file (as Soundy writes it), so nothing after it is lost.
        long end = (long)vorbis + extra + dataSize;
        if (end > template.Length || template.Length - end > 16)
            throw new InvalidDataException("this song's file has an unexpected layout, so it wasn't changed");

        var seek = SeekTable(ogg);
        using var output = new MemoryStream(entry + 0x40 + seek.Count * 4 + ogg.Length);
        output.Write(template, 0, entry);

        var head = template.AsSpan(entry, 0x20).ToArray();
        Put(head, 0x00, ogg.Length);     // audio size
        Put(head, 0x10, 0);              // loop start: the beginning
        Put(head, 0x14, ogg.Length);     // loop end: the whole song
        Put(head, 0x18, 0x20 + seek.Count * 4);
        output.Write(head);

        var vorbisHead = template.AsSpan(vorbis, 0x20).ToArray();
        Put(vorbisHead, 0x10, seek.Count * 4);
        output.Write(vorbisHead);
        foreach (var offset in seek) output.Write(BitConverter.GetBytes(offset));
        output.Write(ogg);

        var bytes = output.ToArray();
        Put(bytes, 0x10, bytes.Length); // file size
        return bytes;
    }

    /// <summary>Byte offset of each Ogg page, leaving out header pages that don't advance the song (as Soundy does).</summary>
    private static List<int> SeekTable(byte[] ogg)
    {
        var offsets = new List<int>();
        long? previous = null;
        var o = 0;
        while (o + 27 <= ogg.Length)
        {
            if (ogg[o] != 'O' || ogg[o + 1] != 'g' || ogg[o + 2] != 'g' || ogg[o + 3] != 'S')
                throw new InvalidDataException("the new audio has a damaged Ogg page");
            var granule = BitConverter.ToInt64(ogg, o + 6);
            int segments = ogg[o + 26];
            if (o + 27 + segments > ogg.Length) throw new InvalidDataException("the new audio ends in the middle of a page");
            var body = 0;
            for (var i = 0; i < segments; i++) body += ogg[o + 27 + i];
            if (previous == null || granule != previous) offsets.Add(o);
            previous = granule;
            o += 27 + segments + body;
        }
        if (o != ogg.Length) throw new InvalidDataException("the new audio ends in the middle of a page");
        return offsets;
    }

    private static void Put(byte[] b, int at, int value) => BitConverter.GetBytes(value).CopyTo(b, at);
}
