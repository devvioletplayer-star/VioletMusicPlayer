using NAudio.Wave;
using OggVorbisEncoder;

namespace VioletSongConverter;

/// <summary>
/// Encodes audio to Ogg Vorbis (the format Soundy's songs use inside their .scd files), using the OggVorbisEncoder
/// package (fully managed, so nothing needs installing).
/// </summary>
public static class OggEncoder
{
    /// <summary>
    /// Every song is saved at Vorbis quality 0.6, about 192 kbps (Violet's choice, Nov 9: no quality list). YouTube
    /// serves at most about 160 kbps, so this keeps everything the song has.
    /// </summary>
    public const float Quality = 0.6f;

    /// <summary>The nominal bitrate of <see cref="Quality"/>, for deciding whether a song is already small.</summary>
    public const int Kbps = 192;

    /// <summary>Encodes everything the source gives. <paramref name="progress"/> gets 0..1 when the length is known.</summary>
    public static byte[] Encode(ISampleProvider source, long totalFrames, IProgress<double>? progress = null)
    {
        var channels = source.WaveFormat.Channels;
        var rate = source.WaveFormat.SampleRate;
        var info = VorbisInfo.InitVariableBitRate(channels, rate, Quality);

        var stream = new OggStream(new Random().Next());
        var comments = new Comments();
        comments.AddTag("ENCODER", "Violet Song Converter");
        stream.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
        stream.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(comments));
        stream.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));

        using var output = new MemoryStream();
        Flush(stream, output, force: true); // the headers get pages of their own

        var state = ProcessingState.Create(info);
        const int Block = 4096;
        var interleaved = new float[Block * channels];
        long done = 0;
        var lastPercent = -1;
        int read;
        while ((read = source.Read(interleaved, 0, interleaved.Length)) > 0)
        {
            var frames = read / channels;
            var planar = new float[channels][];
            for (var c = 0; c < channels; c++)
            {
                var ch = planar[c] = new float[frames];
                for (var f = 0; f < frames; f++) ch[f] = interleaved[f * channels + c];
            }
            state.WriteData(planar, frames);
            Drain(state, stream, output);

            done += frames;
            var percent = totalFrames > 0 ? (int)Math.Min(100, done * 100 / totalFrames) : -1;
            if (percent != lastPercent && percent >= 0)
            {
                lastPercent = percent;
                progress?.Report(percent / 100.0);
            }
        }

        state.WriteEndOfStream();
        Drain(state, stream, output);
        Flush(stream, output, force: true);
        return output.ToArray();
    }

    private static void Drain(ProcessingState state, OggStream stream, Stream output)
    {
        while (!stream.Finished && state.PacketOut(out OggPacket packet))
        {
            stream.PacketIn(packet);
            Flush(stream, output, force: false);
        }
    }

    private static void Flush(OggStream stream, Stream output, bool force)
    {
        while (stream.PageOut(out OggPage page, force))
        {
            output.Write(page.Header, 0, page.Header.Length);
            output.Write(page.Body, 0, page.Body.Length);
        }
    }
}
