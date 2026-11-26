using System.ComponentModel;

namespace VioletSongConverter;

/// <summary>Draws a song's waveform with a draggable start (green) and end (red) marker and a playhead.
/// Drag a marker to move it; click anywhere else to move the playhead there.</summary>
public sealed class WaveformView : Control
{
    private AudioClip? clip;
    private float[]? peaks;
    private double start, end, playhead;
    private enum Drag { None, Start, End, Playhead }
    private Drag drag;

    private static readonly Color Back = Color.FromArgb(22, 20, 28);
    private static readonly Color Wave = VioletTheme.Lilac;
    private static readonly Color WaveCut = Color.FromArgb(74, 68, 88);
    private static readonly Color StartColor = VioletTheme.Good;
    private static readonly Color EndColor = VioletTheme.Bad;
    private static readonly Color PlayColor = Color.White;

    public event Action? MarkersChanged;
    /// <summary>Raised when a marker drag ends (MarkersChanged fires continuously while dragging).</summary>
    public event Action? MarkersCommitted;
    public event Action<double>? SeekRequested;

    public WaveformView()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        MinimumSize = new Size(200, 120);
        Cursor = Cursors.Hand;
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AudioClip? Clip
    {
        get => clip;
        set
        {
            clip = value;
            peaks = value?.Peaks(4000);
            start = 0;
            end = value?.Duration ?? 0;
            playhead = 0;
            Invalidate();
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Start { get => start; set { start = Math.Clamp(value, 0, Math.Max(0, end - 0.1)); Invalidate(); } }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double End
    {
        get => end;
        set
        {
            var max = clip?.Duration ?? value;
            end = Math.Clamp(value, Math.Min(start + 0.1, max), max);
            Invalidate();
        }
    }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Playhead { get => playhead; set { playhead = Math.Clamp(value, 0, clip?.Duration ?? 0); Invalidate(); } }

    private float X(double seconds) => clip == null || clip.Duration <= 0 ? 0 : (float)(seconds / clip.Duration * (Width - 1));
    private double Seconds(int x) => clip == null ? 0 : Math.Clamp(x / (double)Math.Max(1, Width - 1) * clip.Duration, 0, clip.Duration);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Back);
        if (clip == null || peaks == null)
        {
            TextRenderer.DrawText(g, "Loading…", Font, ClientRectangle, VioletTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var mid = Height / 2f;
        var sx = X(start);
        var ex = X(end);
        using (var keep = new Pen(Wave))
        using (var cut = new Pen(WaveCut))
        {
            for (var x = 0; x < Width; x++)
            {
                var i = (int)((long)x * peaks.Length / Math.Max(1, Width));
                var h = Math.Min(1f, peaks[i]) * (Height / 2f - VioletTheme.Px(4));
                g.DrawLine(x >= sx && x <= ex ? keep : cut, x, mid - h, x, mid + h);
            }
        }

        using (var shade = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
        {
            g.FillRectangle(shade, 0, 0, sx, Height);
            g.FillRectangle(shade, ex, 0, Width - ex, Height);
        }

        DrawMarker(g, sx, StartColor, "Start");
        DrawMarker(g, ex, EndColor, "End");
        using var play = new Pen(PlayColor, Math.Max(1.5f, VioletTheme.Px(1.5f)));
        var px = X(playhead);
        g.DrawLine(play, px, 0, px, Height);
    }

    private void DrawMarker(Graphics g, float x, Color color, string label)
    {
        using var pen = new Pen(color, Math.Max(2.5f, VioletTheme.Px(2.5f)));
        using var brush = new SolidBrush(color);
        g.DrawLine(pen, x, 0, x, Height);
        var m = VioletTheme.Px(10);
        g.FillRectangle(brush, x - m / 2f, 0, m, m);
        g.FillRectangle(brush, x - m / 2f, Height - m, m, m);
        var size = TextRenderer.MeasureText(label, Font);
        var tx = (int)Math.Clamp(x + VioletTheme.Px(6), 0, Math.Max(0, Width - size.Width));
        TextRenderer.DrawText(g, label, Font, new Point(tx, m + VioletTheme.Px(2)), color);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (clip == null || e.Button != MouseButtons.Left) return;
        if (Math.Abs(e.X - X(start)) <= VioletTheme.Px(7)) drag = Drag.Start;
        else if (Math.Abs(e.X - X(end)) <= VioletTheme.Px(7)) drag = Drag.End;
        else
        {
            drag = Drag.Playhead;
            Playhead = Seconds(e.X);
            SeekRequested?.Invoke(playhead);
        }
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (clip == null) return;
        if (drag == Drag.None)
        {
            Cursor = Math.Abs(e.X - X(start)) <= VioletTheme.Px(7) || Math.Abs(e.X - X(end)) <= VioletTheme.Px(7) ? Cursors.SizeWE : Cursors.Hand;
            return;
        }
        var s = Seconds(e.X);
        switch (drag)
        {
            case Drag.Start: Start = s; MarkersChanged?.Invoke(); break;
            case Drag.End: End = s; MarkersChanged?.Invoke(); break;
            case Drag.Playhead: Playhead = s; SeekRequested?.Invoke(playhead); break;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (drag is Drag.Start or Drag.End) MarkersCommitted?.Invoke();
        drag = Drag.None;
        Capture = false;
    }
}
