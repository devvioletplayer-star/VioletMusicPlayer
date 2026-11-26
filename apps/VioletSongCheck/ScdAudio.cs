using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VioletSongCheck;

/// <summary>
/// Reads the audio out of an FFXIV .scd file.
///
/// Layout used: 0x0E = size of the file header (where the tables start); tables + 0x0C = offset of the
/// audio entry list. An audio entry is a 0x20 header (0x00 data size, 0x04 channels, 0x08 sample rate,
/// 0x0C codec, 0x18 size of the extra codec data), the extra data, then the audio itself.
/// Codecs: 0x06 Ogg Vorbis (what Soundy writes), 0x01 16-bit PCM, 0x0C Microsoft ADPCM.
/// </summary>
public static class ScdAudio
{
    private sealed record Entry(int Channels, int SampleRate, int Codec, int ExtraOffset, int ExtraSize, int DataOffset, int DataSize);

    private static Entry ReadEntry(byte[] b)
    {
        if (b.Length < 0x40 || b[0] != 'S' || b[1] != 'E' || b[2] != 'D' || b[3] != 'B')
            throw new InvalidDataException("not an FFXIV sound (.scd) file");
        int tables = BitConverter.ToUInt16(b, 0x0E);
        int list = (int)BitConverter.ToUInt32(b, tables + 0x0C);
        int e = (int)BitConverter.ToUInt32(b, list);
        int dataSize = (int)BitConverter.ToUInt32(b, e);
        int channels = (int)BitConverter.ToUInt32(b, e + 0x04);
        int rate = (int)BitConverter.ToUInt32(b, e + 0x08);
        int codec = BitConverter.ToInt32(b, e + 0x0C);
        int extra = (int)BitConverter.ToUInt32(b, e + 0x18);
        int data = e + 0x20 + extra;
        if (data + dataSize > b.Length) dataSize = b.Length - data;
        if (dataSize <= 0 || rate <= 0) throw new InvalidDataException("the file has no audio");
        return new Entry(channels, rate, codec, e + 0x20, extra, data, dataSize);
    }

    /// <summary>Song length without decoding (Vorbis: last page's sample count). Zero if unknown.</summary>
    public static TimeSpan Length(string path)
    {
        try
        {
            var b = File.ReadAllBytes(path);
            var en = ReadEntry(b);
            if (en.Codec == 0x01) return TimeSpan.FromSeconds(en.DataSize / (double)(en.SampleRate * en.Channels * 2));
            for (int i = b.Length - 27; i >= en.DataOffset; i--)
                if (b[i] == 'O' && b[i + 1] == 'g' && b[i + 2] == 'g' && b[i + 3] == 'S')
                {
                    long granule = BitConverter.ToInt64(b, i + 6);
                    return granule > 0 ? TimeSpan.FromSeconds(granule / (double)en.SampleRate) : TimeSpan.Zero;
                }
        }
        catch
        {
            // shown as unknown
        }
        return TimeSpan.Zero;
    }

    /// <summary>Opens the song's audio as floating-point samples. Dispose the returned stream when done.</summary>
    public static (ISampleProvider Samples, IDisposable Owner) Open(string path)
    {
        var b = File.ReadAllBytes(path);
        var en = ReadEntry(b);
        var data = new MemoryStream(b, en.DataOffset, en.DataSize, writable: false);

        switch (en.Codec)
        {
            case 0x06:
                if (en.DataSize < 4 || b[en.DataOffset] != 'O' || b[en.DataOffset + 1] != 'g' || b[en.DataOffset + 2] != 'g' || b[en.DataOffset + 3] != 'S')
                    throw new InvalidDataException("the Vorbis audio is in the game's encrypted form, which this app doesn't read");
                var vorbis = new VorbisWaveReader(data, true);
                return (vorbis, vorbis);

            case 0x01:
                var pcm = new RawSourceWaveStream(data, new WaveFormat(en.SampleRate, 16, en.Channels));
                return (pcm.ToSampleProvider(), pcm);

            case 0x0C:
                // The extra data is a WAVEFORMATEX describing the ADPCM blocks.
                using (var br = new BinaryReader(new MemoryStream(b, en.ExtraOffset, en.ExtraSize, false)))
                {
                    var fmt = WaveFormat.FromFormatChunk(br, en.ExtraSize);
                    var raw = new RawSourceWaveStream(data, fmt);
                    var decoded = WaveFormatConversionStream.CreatePcmStream(raw);
                    return (decoded.ToSampleProvider(), decoded);
                }

            default:
                throw new InvalidDataException($"unsupported audio type 0x{en.Codec:X}");
        }
    }
}
