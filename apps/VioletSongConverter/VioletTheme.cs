using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace VioletSongConverter;

/// <summary>
/// The Violet Music Player look for WinForms: the same colours as the plugin's /vmusic window, a violet
/// header band, lilac section headings, violet buttons, a dark song list with a violet header row and a
/// violet progress bar. Build the form, then call <see cref="Apply"/> once at the end of its constructor.
/// </summary>
internal static class VioletTheme
{
    // ---- the plugin's colours ----
    public static readonly Color Violet = FromF(0.357f, 0.243f, 0.549f);
    public static readonly Color VioletHover = FromF(0.45f, 0.32f, 0.67f);
    public static readonly Color VioletActive = FromF(0.29f, 0.19f, 0.45f);
    public static readonly Color Lilac = FromF(0.78f, 0.68f, 0.98f);
    public static readonly Color Muted = FromF(0.62f, 0.62f, 0.68f);
    public static readonly Color Good = FromF(0.50f, 0.88f, 0.58f);
    public static readonly Color Bad = FromF(1f, 0.47f, 0.47f);
    public static readonly Color Amber = FromF(0.95f, 0.72f, 0.25f);
    public static readonly Color HeaderSub = FromF(0.90f, 0.86f, 1f);
    public static readonly Color PillOn = FromF(0.20f, 0.60f, 0.35f);
    public static readonly Color Stop = FromF(0.62f, 0.22f, 0.25f);
    public static readonly Color StopHover = FromF(0.74f, 0.29f, 0.32f);
    public static readonly Color StopActive = FromF(0.50f, 0.17f, 0.20f);

    // ---- dark window colours (like the game's dark windows) ----
    public static readonly Color Back = Color.FromArgb(28, 26, 34);
    public static readonly Color Panel = Color.FromArgb(38, 35, 46);
    public static readonly Color PanelAlt = Color.FromArgb(33, 30, 41);
    public static readonly Color Input = Color.FromArgb(46, 42, 56);
    public static readonly Color Border = Color.FromArgb(78, 70, 98);
    public static readonly Color Text = Color.FromArgb(236, 233, 244);
    public static readonly Color DisabledButton = Color.FromArgb(62, 58, 72);

    private const string PrimaryTag = "violet:primary";
    private const string DangerTag = "violet:danger";

    private static Color FromF(float r, float g, float b) =>
        Color.FromArgb((int)Math.Round(r * 255), (int)Math.Round(g * 255), (int)Math.Round(b * 255));

    /// <summary>"v1.2.0", from the app's version.</summary>
    public static string VersionText()
    {
        var v = Assembly.GetEntryAssembly()?.GetName().Version;
        return v == null ? "" : $"v{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
    }

    /// <summary>Marks the main button of a window (bold). Call before <see cref="Apply"/>.</summary>
    public static T Primary<T>(T b) where T : ButtonBase { b.Tag = PrimaryTag; return b; }

    /// <summary>Marks a stop button (red, like the plugin's Stop). Call before <see cref="Apply"/>.</summary>
    public static T Danger<T>(T b) where T : ButtonBase { b.Tag = DangerTag; return b; }

    // ---------- text size (for people who need bigger text) ----------

    /// <summary>The text size choices on the header's Text size list, as a percentage on top of Windows' own Text size.</summary>
    public static readonly int[] TextSizeChoices = { 100, 125, 150, 175, 200, 250 };

    /// <summary>Windows' Text size (Settings → Accessibility → Text size) × the app's own Text size choice.</summary>
    public static float TextScale { get; private set; } = 1f;

    /// <summary>The app's own Text size choice, in percent (100 = as Windows has it).</summary>
    public static int TextSizePercent { get; private set; } = 100;

    /// <summary>Set when the Text size changes: Program opens the window again at the new size.</summary>
    public static bool ReopenRequested { get; set; }

    private static float dpiScale = 1f;

    /// <summary>Pixels for a size designed at 100%: follows the screen's scaling and the text size.</summary>
    public static int Px(float designPixels) => (int)Math.Round(designPixels * dpiScale * TextScale);

    /// <summary>The size every window's text is built on.</summary>
    public static Font UiFont() => new("Segoe UI", 9.5f * TextScale);

    /// <summary>Reads the screen scaling, Windows' Text size and the saved Text size choice. Call before making a window.</summary>
    public static void LoadScale()
    {
        try
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            dpiScale = g.DpiX / 96f;
        }
        catch
        {
            dpiScale = 1f;
        }
        TextSizePercent = LoadTextSizeChoice();
        TextScale = Math.Clamp(WindowsTextScale() * TextSizePercent / 100f, 1f, 3.5f);
    }

    /// <summary>Saves a new Text size choice (shared by Violet Song Converter and Violet Song Check).</summary>
    public static void SaveTextSizeChoice(int percent)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(new SharedSettings { TextSizePercent = percent }));
        }
        catch
        {
            // not important: the choice just won't be remembered
        }
    }

    private static string SettingsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Violet", "Song tools settings.json");

    private sealed class SharedSettings
    {
        public int TextSizePercent { get; set; } = 100;
    }

    private static int LoadTextSizeChoice()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return 100;
            var p = JsonSerializer.Deserialize<SharedSettings>(File.ReadAllText(SettingsFile))?.TextSizePercent ?? 100;
            return TextSizeChoices.Contains(p) ? p : 100;
        }
        catch
        {
            return 100;
        }
    }

    /// <summary>Windows' "Make text bigger" setting: 1.0 to 2.25.</summary>
    private static float WindowsTextScale()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility");
            if (key?.GetValue("TextScaleFactor") is int v && v >= 100 && v <= 225) return v / 100f;
        }
        catch
        {
            // not available: normal size
        }
        return 1f;
    }

    /// <summary>
    /// Makes every size given in pixels (gaps, widths, columns, window size) follow the text size, so bigger
    /// text never gets cut off. The window is kept inside the screen.
    /// </summary>
    private static void ScaleLayout(Form form)
    {
        var f = dpiScale * TextScale;
        form.AutoScaleMode = AutoScaleMode.None; // this does the scaling instead
        if (Math.Abs(f - 1f) < 0.01f) return;
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Size Fit(Size s) => new(Math.Min(s.Width, area.Width), Math.Min(s.Height, area.Height));
        if (form.MinimumSize != Size.Empty) form.MinimumSize = Fit(Scale(form.MinimumSize, f));
        if (!form.AutoSize) form.Size = Fit(Scale(form.Size, f));
        foreach (Control c in form.Controls) ScaleControl(c, f);
    }

    private static Size Scale(Size s, float f) => new((int)Math.Round(s.Width * f), (int)Math.Round(s.Height * f));

    private static Padding Scale(Padding p, float f) =>
        new((int)Math.Round(p.Left * f), (int)Math.Round(p.Top * f), (int)Math.Round(p.Right * f), (int)Math.Round(p.Bottom * f));

    private static void ScaleControl(Control c, float f)
    {
        c.Margin = Scale(c.Margin, f);
        if (c is not (VioletHeader or VioletHeading)) c.Padding = Scale(c.Padding, f);
        if (c is not (VioletHeader or VioletHeading or VioletProgress))
        {
            if (!c.AutoSize && c.Dock is DockStyle.None or DockStyle.Left or DockStyle.Right) c.Width = (int)Math.Round(c.Width * f);
            if (c.MinimumSize != Size.Empty) c.MinimumSize = Scale(c.MinimumSize, f);
            if (c.MaximumSize != Size.Empty) c.MaximumSize = Scale(c.MaximumSize, f); // 0 stays 0 (no limit)
        }
        if (c is VioletProgress) c.Height = Math.Max(c.Height, Px(20));
        if (c is TableLayoutPanel t)
        {
            foreach (ColumnStyle cs in t.ColumnStyles) if (cs.SizeType == SizeType.Absolute) cs.Width *= f;
            foreach (RowStyle rs in t.RowStyles) if (rs.SizeType == SizeType.Absolute) rs.Height *= f;
        }
        if (c is ListView lv)
            foreach (ColumnHeader col in lv.Columns) col.Width = (int)Math.Round(col.Width * f);
        foreach (Control child in c.Controls) ScaleControl(child, f);
    }

    /// <summary>The header's "Text size" list. Picking a size saves it and opens the window again at that size,
    /// unless <paramref name="busy"/> says the window is in the middle of something.</summary>
    public static Control TextSizePicker(Form form, Func<bool> busy)
    {
        var label = new Label { Text = "Text size", AutoSize = true, ForeColor = HeaderSub, BackColor = Color.Transparent, Anchor = AnchorStyles.Left,
                                Margin = new Padding(0, 0, 6, 0) };
        var list = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 76, Margin = Padding.Empty };
        foreach (var p in TextSizeChoices) list.Items.Add($"{p}%");
        list.SelectedIndex = Math.Max(0, Array.IndexOf(TextSizeChoices, TextSizePercent));
        var tips = new ToolTip();
        tips.SetToolTip(list, "Makes all the text in Violet Song Converter and Violet Song Check bigger.\n" +
                              "Windows' own Text size (Settings → Accessibility) is followed too.");
        list.SelectionChangeCommitted += (_, _) =>
        {
            var p = TextSizeChoices[list.SelectedIndex];
            if (p == TextSizePercent) return;
            if (busy())
            {
                list.SelectedIndex = Array.IndexOf(TextSizeChoices, TextSizePercent);
                MessageBox.Show(form, "Wait until it's finished, then change the text size.", form.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SaveTextSizeChoice(p);
            ReopenRequested = true;
            form.Close();
        };
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
        row.Controls.Add(label);
        row.Controls.Add(list);
        return row;
    }

    /// <summary>The app's icon (app.ico, built into the program), or null if it's missing.</summary>
    public static Icon? AppIcon()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico");
            return stream == null ? null : new Icon(stream);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Styles the form and everything in it, and gives it the app's icon.</summary>
    public static void Apply(Form form)
    {
        form.Icon = AppIcon() ?? form.Icon;
        form.BackColor = Back;
        form.ForeColor = Text;
        if (form.IsHandleCreated) TintTitleBar(form);
        else form.HandleCreated += (_, _) => TintTitleBar(form);
        StyleChildren(form);
        ScaleLayout(form);
        FitCombos(form);
    }

    /// <summary>
    /// A drawn drop-down is taller than Windows reports to the layout (it reports the plain height, not the drawn
    /// item height), so at larger text its row was too short and the bottom was cut off. This gives each one room.
    /// </summary>
    private static void FitCombos(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is ComboBox cb && cb.DrawMode == DrawMode.OwnerDrawFixed)
            {
                cb.MinimumSize = new Size(cb.MinimumSize.Width, cb.ItemHeight + Px(8));
                // Wide enough for the longest choice plus the arrow (the Text size list was cut off at large text).
                var widest = 0;
                foreach (var item in cb.Items)
                    widest = Math.Max(widest, TextRenderer.MeasureText(cb.GetItemText(item), cb.Font).Width);
                var need = widest + SystemInformation.VerticalScrollBarWidth + Px(14);
                if (cb.Width < need) cb.Width = need;
            }
            FitCombos(c);
        }
    }

    private static void StyleChildren(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            StyleOne(c);
            StyleChildren(c);
        }
    }

    private static void StyleOne(Control c)
    {
        switch (c)
        {
            case Button b:
                if (b.Tag as string == DangerTag) StyleButton(b, Stop, StopHover, StopActive, bold: false);
                else StyleButton(b, Violet, VioletHover, VioletActive, bold: b.Tag as string == PrimaryTag);
                break;
            case TextBox t:
                t.BackColor = Input;
                t.ForeColor = Text;
                t.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox cb:
                StyleCombo(cb);
                break;
            case NumericUpDown n:
                n.BackColor = Input;
                n.ForeColor = Text;
                n.BorderStyle = BorderStyle.FixedSingle;
                break;
            case CheckBox x:
                x.FlatStyle = FlatStyle.Flat;
                x.FlatAppearance.BorderColor = Lilac;
                x.FlatAppearance.CheckedBackColor = Violet;
                x.FlatAppearance.MouseOverBackColor = Input;
                x.ForeColor = Text;
                break;
            case ListView lv:
                StyleList(lv);
                break;
        }
    }

    // ---------- buttons ----------

    private static void StyleButton(Button b, Color normal, Color hover, Color down, bool bold)
    {
        b.UseVisualStyleBackColor = false;
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = hover;
        b.FlatAppearance.MouseDownBackColor = down;
        b.ForeColor = Color.White;
        b.Cursor = Cursors.Hand;
        if (bold) b.Font = new Font(b.Font, FontStyle.Bold);
        var pad = b.Padding;
        b.Padding = new Padding(Math.Max(pad.Left, 8), Math.Max(pad.Top, 2), Math.Max(pad.Right, 8), Math.Max(pad.Bottom, 2));
        void Sync() => b.BackColor = b.Enabled ? normal : DisabledButton;
        Sync();
        b.EnabledChanged += (_, _) => Sync();
    }

    // ---------- drop-downs ----------

    private static void StyleCombo(ComboBox cb)
    {
        cb.FlatStyle = FlatStyle.Flat;
        cb.BackColor = Input;
        cb.ForeColor = Text;
        cb.DrawMode = DrawMode.OwnerDrawFixed;
        cb.ItemHeight = cb.Font.Height + Px(4);
        // Scrolling the mouse wheel over a closed drop-down changed its choice without anyone noticing; only an open
        // list scrolls now.
        cb.MouseWheel += (_, e) => { if (!cb.DroppedDown && e is HandledMouseEventArgs h) h.Handled = true; };
        cb.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            var selected = (e.State & DrawItemState.Selected) != 0;
            using (var back = new SolidBrush(selected ? Violet : Input))
                e.Graphics.FillRectangle(back, e.Bounds);
            var r = e.Bounds;
            r.Inflate(-3, 0);
            TextRenderer.DrawText(e.Graphics, cb.GetItemText(cb.Items[e.Index]), cb.Font, r,
                selected ? Color.White : Text, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        };
    }

    // ---------- the song list ----------

    private static void StyleList(ListView lv)
    {
        lv.BackColor = Panel;
        lv.ForeColor = Text;
        lv.BorderStyle = BorderStyle.None;
        lv.OwnerDraw = true;
        typeof(Control).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(lv, true);

        lv.DrawColumnHeader += DrawHeader;
        lv.DrawItem += (_, e) => e.DrawDefault = false;
        lv.DrawSubItem += DrawCell;
        lv.ItemChecked += (_, e) => { if (e.Item.ListView != null) lv.Invalidate(e.Item.Bounds); };
        // The tick box is drawn at the chosen text size, but Windows only reacts to clicks in its own small, unscaled box.
        // Clicks anywhere on the drawn box tick it too; clicks Windows already handles (its own box) are left to it.
        lv.MouseDown += (_, e) =>
        {
            if (!lv.CheckBoxes || e.Button != MouseButtons.Left) return;
            var hit = lv.HitTest(e.Location);
            if (hit.Item == null || hit.Location == ListViewHitTestLocations.StateImage) return;
            var scale = Px(100) / 100f;
            if (e.X < hit.Item.Bounds.X + (int)(22 * scale)) hit.Item.Checked = !hit.Item.Checked;
        };
        // Owner-drawn lists can leave a row half-painted when the mouse moves over it; repaint the row.
        ListViewItem? hot = null;
        lv.MouseMove += (_, e) =>
        {
            var item = lv.GetItemAt(e.X, e.Y);
            if (item == hot) return;
            if (hot?.ListView != null) lv.Invalidate(hot.Bounds);
            if (item != null) lv.Invalidate(item.Bounds);
            hot = item;
        };

        void DarkScrollBars() => _ = SetWindowTheme(lv.Handle, "DarkMode_Explorer", null);
        if (lv.IsHandleCreated) DarkScrollBars();
        lv.HandleCreated += (_, _) => { DarkScrollBars(); FitColumns(lv); };

        var fitting = false;
        void Fit()
        {
            if (fitting) return;
            fitting = true;
            try { FitColumns(lv); }
            finally { fitting = false; }
        }
        lv.Resize += (_, _) => Fit();
        lv.ColumnWidthChanged += (_, e) => { if (e.ColumnIndex != lv.Columns.Count - 1) Fit(); };
    }

    /// <summary>Stretches the last column to the list's edge, so the violet header row reaches the side.
    /// Call again after filling the list (a scroll bar may have appeared).</summary>
    public static void FitColumns(ListView lv)
    {
        if (lv.Columns.Count == 0) return;
        var last = lv.Columns.Count - 1;
        var others = 0;
        for (var i = 0; i < last; i++) others += lv.Columns[i].Width;
        var min = Px(80);
        var width = Math.Max(min, lv.ClientSize.Width - others);
        if (lv.Columns[last].Width != width) lv.Columns[last].Width = width;
    }

    private static TextFormatFlags Align(HorizontalAlignment a) => a switch
    {
        HorizontalAlignment.Right => TextFormatFlags.Right,
        HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
        _ => TextFormatFlags.Left,
    };

    private static void DrawHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        var lv = (ListView)sender!;
        var g = e.Graphics;
        using (var back = new SolidBrush(Violet)) g.FillRectangle(back, e.Bounds);
        using (var line = new Pen(VioletActive))
        {
            g.DrawLine(line, e.Bounds.Right - 1, e.Bounds.Top + 3, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
            g.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }
        var r = e.Bounds;
        r.Inflate(-Px(6), 0);
        using var bold = new Font(lv.Font, FontStyle.Bold);
        TextRenderer.DrawText(g, e.Header?.Text ?? "", bold, r, Color.White,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine |
            Align(e.Header?.TextAlign ?? HorizontalAlignment.Left));
    }

    private static void DrawCell(object? sender, DrawListViewSubItemEventArgs e)
    {
        var lv = (ListView)sender!;
        var item = e.Item;
        if (item == null) return;
        var g = e.Graphics;
        var scale = Px(100) / 100f;
        var bounds = e.Bounds;
        if (e.ColumnIndex == 0) bounds.Width = lv.Columns[0].Width; // column 0 is given the whole row

        var selected = item.Selected;
        var back = selected ? Violet : (e.ItemIndex % 2 == 0 ? Panel : PanelAlt);
        using (var b = new SolidBrush(back)) g.FillRectangle(b, bounds);

        if (e.ColumnIndex == 0 && lv.CheckBoxes)
        {
            var size = Math.Min(bounds.Height - 2, (int)(14 * scale));
            var box = new Rectangle(bounds.X + (int)(4 * scale), bounds.Y + (bounds.Height - size) / 2, size, size);
            DrawCheck(g, box, item.Checked, scale);
            bounds = Rectangle.FromLTRB(box.Right + (int)(3 * scale), bounds.Top, bounds.Right, bounds.Bottom);
        }

        bounds.Inflate(-(int)(4 * scale), 0);
        var align = e.ColumnIndex < lv.Columns.Count ? lv.Columns[e.ColumnIndex].TextAlign : HorizontalAlignment.Left;
        TextRenderer.DrawText(g, e.SubItem?.Text ?? "", lv.Font, bounds, selected ? Color.White : Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | Align(align));
    }

    private static void DrawCheck(Graphics g, Rectangle r, bool on, float scale)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Rounded(r, 3 * scale))
        {
            using var fill = new SolidBrush(on ? Lilac : Input);
            using var edge = new Pen(on ? Lilac : Border, Math.Max(1f, scale));
            g.FillPath(fill, path);
            g.DrawPath(edge, path);
        }
        if (on)
        {
            using var tick = new Pen(VioletActive, Math.Max(2f, 2f * scale)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(tick, new[]
            {
                new PointF(r.Left + r.Width * 0.22f, r.Top + r.Height * 0.52f),
                new PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.72f),
                new PointF(r.Left + r.Width * 0.78f, r.Top + r.Height * 0.30f),
            });
        }
        g.SmoothingMode = old;
    }

    internal static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var d = Math.Max(1f, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        var p = new GraphicsPath();
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ---------- title bar ----------

    /// <summary>Dark title bar (Windows 10 2004+), tinted violet on Windows 11. Ignored where unsupported.</summary>
    private static void TintTitleBar(Form f)
    {
        try
        {
            var on = 1;
            _ = DwmSetWindowAttribute(f.Handle, 20, ref on, sizeof(int));      // dark mode
            var caption = ColorRef(VioletActive);
            _ = DwmSetWindowAttribute(f.Handle, 35, ref caption, sizeof(int)); // caption colour (Windows 11)
            var text = ColorRef(Color.White);
            _ = DwmSetWindowAttribute(f.Handle, 36, ref text, sizeof(int));    // caption text colour (Windows 11)
        }
        catch
        {
            // older Windows: keep the normal title bar
        }
    }

    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);
}

/// <summary>The violet band across the top of a window: big title, a subtitle and the version.</summary>
internal sealed class VioletHeader : Control
{
    private readonly string subtitle;
    private readonly string version;
    private Font? titleFont;
    private Control? right;

    public VioletHeader(string title, string subtitle, string? version = null)
    {
        Text = title;
        this.subtitle = subtitle;
        this.version = version ?? VioletTheme.VersionText();
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = VioletTheme.Violet;
        Margin = Padding.Empty;
        Dock = DockStyle.Top;
        UpdateFonts();
    }

    private int Pad => VioletTheme.Px(14);

    /// <summary>Puts a control (the Text size list) at the right of the band, vertically centred.</summary>
    public void SetRight(Control c)
    {
        right = c;
        Controls.Add(c);
        // A drop-down grows after it's styled and scaled; lay the band out again when it does.
        foreach (Control child in c.Controls) child.SizeChanged += (_, _) => PerformLayout();
        Height = GetPreferredSize(Size.Empty).Height;
        PerformLayout();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (right == null) return;
        var size = right.GetPreferredSize(Size.Empty);
        // A drawn drop-down can be taller or wider than its panel reports, so make room for what's really there.
        foreach (Control c in right.Controls)
        {
            size.Width = Math.Max(size.Width, c.Right + c.Margin.Right);
            size.Height = Math.Max(size.Height, c.Bottom + c.Margin.Bottom);
        }
        right.Size = size;
        if (Height < size.Height + Pad) Height = size.Height + Pad;
        right.Location = new Point(Math.Max(Pad, Width - Pad - size.Width), Math.Max(0, (Height - size.Height) / 2));
    }

    /// <summary>Where the text must stop so it doesn't run under the right-hand control.</summary>
    private int TextRight => right == null ? Width - Pad : right.Left - Pad;

    private void UpdateFonts()
    {
        titleFont?.Dispose();
        titleFont = new Font("Segoe UI Semibold", Font.Size * 1.55f, FontStyle.Regular);
        Height = GetPreferredSize(Size.Empty).Height;
    }

    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); UpdateFonts(); Invalidate(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); UpdateFonts(); }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var t = TextRenderer.MeasureText(Text, titleFont ?? Font);
        var s = TextRenderer.MeasureText(subtitle, Font);
        var r = right?.GetPreferredSize(Size.Empty) ?? Size.Empty;
        return new Size(Math.Max(t.Width, s.Width) + Pad * 2 + (r.Width > 0 ? r.Width + Pad : 0),
                        Math.Max(Pad * 2 + t.Height + s.Height, r.Height + Pad));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(VioletTheme.Violet);
        var pad = Pad;
        var t = TextRenderer.MeasureText(Text, titleFont ?? Font);
        TextRenderer.DrawText(g, Text, titleFont ?? Font, new Point(pad, pad - 2), Color.White, TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, subtitle, Font, new Rectangle(pad, pad - 2 + t.Height, Math.Max(0, TextRight - pad), Height), VioletTheme.HeaderSub,
            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        if (version.Length > 0)
        {
            var v = TextRenderer.MeasureText(version, Font);
            var y = pad - 2 + (t.Height - v.Height) / 2;
            TextRenderer.DrawText(g, version, Font, new Point(TextRight - v.Width, y), VioletTheme.HeaderSub, TextFormatFlags.NoPrefix);
        }
        using var edge = new Pen(VioletTheme.Lilac, Math.Max(1f, VioletTheme.Px(2)));
        g.DrawLine(edge, 0, Height - 1, Width, Height - 1);
    }
}

/// <summary>A lilac section heading with a rule under it, like the plugin's window.</summary>
internal sealed class VioletHeading : Control
{
    private Font? bold;

    public VioletHeading(string text)
    {
        Text = text;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 8, 0, 6);
        UpdateFont();
    }

    private void UpdateFont()
    {
        bold?.Dispose();
        bold = new Font(Font, FontStyle.Bold);
        Height = GetPreferredSize(Size.Empty).Height;
    }

    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); UpdateFont(); Invalidate(); }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var t = TextRenderer.MeasureText(Text, bold ?? Font);
        return new Size(t.Width, t.Height + VioletTheme.Px(6));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var t = TextRenderer.MeasureText(Text, bold ?? Font);
        TextRenderer.DrawText(g, Text, bold ?? Font, new Point(0, 0), VioletTheme.Lilac, TextFormatFlags.NoPrefix);
        using var rule = new Pen(VioletTheme.Border);
        var y = t.Height + VioletTheme.Px(2);
        g.DrawLine(rule, 0, y, Width, y);
    }
}

/// <summary>A violet progress bar (the Windows one is always green).</summary>
internal sealed class VioletProgress : Control
{
    private int minimum, maximum = 100, current;

    public VioletProgress()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 20;
        TabStop = false;
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum { get => minimum; set { minimum = value; Invalidate(); } }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum { get => maximum; set { maximum = value; Invalidate(); } }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value { get => current; set { current = value; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? VioletTheme.Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var h = Math.Min(Height - 2, VioletTheme.Px(16));
        var track = new RectangleF(1, (Height - h) / 2f, Width - 3, h);
        var radius = h / 2f;
        using (var path = VioletTheme.Rounded(track, radius))
        using (var back = new SolidBrush(VioletTheme.Input))
        using (var edge = new Pen(VioletTheme.Border))
        {
            g.FillPath(back, path);
            g.DrawPath(edge, path);
        }
        var span = Math.Max(1, maximum - minimum);
        var frac = Math.Clamp((current - minimum) / (float)span, 0f, 1f);
        if (frac <= 0) return;
        var fill = new RectangleF(track.X, track.Y, Math.Max(h, track.Width * frac), track.Height);
        using var fillPath = VioletTheme.Rounded(fill, radius);
        using var brush = new LinearGradientBrush(fill, VioletTheme.Violet, VioletTheme.Lilac, LinearGradientMode.Horizontal);
        g.FillPath(brush, fillPath);
    }
}
