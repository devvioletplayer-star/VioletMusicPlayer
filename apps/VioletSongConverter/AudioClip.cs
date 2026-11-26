using NAudio.Wave;

namespace VioletSongConverter;

/// <summary>A whole song decoded into memory (interleaved float samples), for the editor.</summary>
public sealed class AudioClip
{
    public float[] Samples { get; }
    public int Channels { get; }
    public int SampleRate { get; }
    public long Frames => Samples.Length / Channels;
    public double Duration => Frames / (double)SampleRate;

    private AudioClip(float[] samples, int channels, int rate)
    {
        Samples = samples;
        Channels = channels;
        SampleRate = rate;
    }

    public static AudioClip Load(string scdPath)
    {
        var (provider, owner) = ScdAudio.Open(scdPath);
        using (owner)
        {
            var fmt = provider.WaveFormat;
            var chunk = new float[fmt.SampleRate * fmt.Channels];
            var all = new List<float>(fmt.SampleRate * fmt.Channels * 300);
            int n;
            while ((n = provider.Read(chunk, 0, chunk.Length)) > 0)
                all.AddRange(new ArraySegment<float>(chunk, 0, n));
            if (all.Count == 0) throw new InvalidDataException("the song has no audio");
            return new AudioClip(all.ToArray(), fmt.Channels, fmt.SampleRate);
        }
    }

    public long FrameAt(double seconds) => Math.Clamp((long)Math.Round(seconds * SampleRate), 0, Frames);

    /// <summary>Loudest absolute sample (all channels) per bucket, for drawing the waveform.</summary>
    public float[] Peaks(int buckets)
    {
        var peaks = new float[buckets];
        var frames = Frames;
        for (var b = 0; b < buckets; b++)
        {
            var from = frames * b / buckets * Channels;
            var to = frames * (b + 1) / buckets * Channels;
            float max = 0;
            for (var i = from; i < to; i++)
            {
                var v = Math.Abs(Samples[i]);
                if (v > max) max = v;
            }
            peaks[b] = max;
        }
        return peaks;
    }

    /// <summary>Where sound starts and stops: the first and last moments louder than near-silence.</summary>
    public (double Start, double End) SoundBounds(float threshold = 0.004f)
    {
        var win = Math.Max(1, SampleRate / 20); // 50 ms
        long first = -1, last = -1;
        for (long f = 0; f < Frames; f += win)
        {
            if (!Loud(f, win, threshold)) continue;
            if (first < 0) first = f;
            last = Math.Min(Frames, f + win);
        }
        if (first < 0) return (0, Duration);
        return (first / (double)SampleRate, last / (double)SampleRate);
    }

    private bool Loud(long frame, int count, float threshold)
    {
        var from = frame * Channels;
        var to = Math.Min(Samples.Length, (frame + count) * Channels);
        double sum = 0;
        for (var i = from; i < to; i++) sum += Samples[i] * Samples[i];
        return to > from && Math.Sqrt(sum / (to - from)) > threshold;
    }
}

/// <summary>Plays (or encodes) a stretch of a clip, with optional fade in / fade out at its ends.</summary>
public sealed class ClipRange : ISampleProvider
{
    private readonly AudioClip clip;
    private readonly long start, end, fadeIn, fadeOut;
    private long position; // frame

    public ClipRange(AudioClip clip, double startSec, double endSec, double fadeInSec, double fadeOutSec, double fromSec)
    {
        this.clip = clip;
        start = clip.FrameAt(startSec);
        end = Math.Max(start, clip.FrameAt(endSec));
        fadeIn = (long)(fadeInSec * clip.SampleRate);
        fadeOut = (long)(fadeOutSec * clip.SampleRate);
        position = Math.Clamp(clip.FrameAt(fromSec), start, end);
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(clip.SampleRate, clip.Channels);
    }

    public WaveFormat WaveFormat { get; }

    /// <summary>Current position in seconds (from the beginning of the song).</summary>
    public double Seconds => Interlocked.Read(ref position) / (double)clip.SampleRate;

    /// <summary>Jumps to a time (kept inside the start/end range). Safe while playing.</summary>
    public void Seek(double seconds) => Interlocked.Exchange(ref position, Math.Clamp(clip.FrameAt(seconds), start, end));

    public int Read(float[] buffer, int offset, int count)
    {
        var ch = clip.Channels;
        var pos = Interlocked.Read(ref position);
        var frames = (int)Math.Min(count / ch, end - pos);
        if (frames <= 0) return 0;
        for (var f = 0; f < frames; f++)
        {
            var frame = pos + f;
            float gain = 1;
            if (fadeIn > 0 && frame - start < fadeIn) gain = Math.Min(gain, (frame - start) / (float)fadeIn);
            if (fadeOut > 0 && end - frame < fadeOut) gain = Math.Min(gain, (end - frame) / (float)fadeOut);
            var src = frame * ch;
            for (var c = 0; c < ch; c++) buffer[offset + f * ch + c] = clip.Samples[src + c] * gain;
        }
        Interlocked.Exchange(ref position, pos + frames);
        return frames * ch;
    }
}
