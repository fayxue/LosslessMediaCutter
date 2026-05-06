using System.Drawing.Drawing2D;

namespace LosslessMediaCutter.Controls;

/// <summary>
/// UI 设计令牌：集中管理设计稿中的色板、圆角、字号等。
/// </summary>
internal static class UiTheme
{
    public static readonly Color BgTop      = Color.FromArgb(0xF5, 0xF7, 0xFA);
    public static readonly Color BgBottom   = Color.FromArgb(0xE9, 0xEC, 0xF0);
    public static readonly Color CardBg     = Color.White;
    public static readonly Color Border     = Color.FromArgb(0xD0, 0xD5, 0xDC);
    public static readonly Color BorderSoft = Color.FromArgb(0xE5, 0xE8, 0xEE);

    public static readonly Color Primary    = Color.FromArgb(0x4A, 0x90, 0xE2);
    public static readonly Color Secondary  = Color.FromArgb(0x7F, 0x8C, 0x8D);
    public static readonly Color Danger     = Color.FromArgb(0xE7, 0x4C, 0x3C);
    public static readonly Color Text       = Color.FromArgb(0x2C, 0x3E, 0x50);
    public static readonly Color TextMuted  = Color.FromArgb(0x7F, 0x8C, 0x8D);

    public static readonly Color HeaderBg   = Color.FromArgb(0xF0, 0xF2, 0xF5);
    public static readonly Color RowAlt     = Color.FromArgb(0xFA, 0xFB, 0xFC);

    public const int Radius = 8;
    public const int ButtonRadius = 6;

    public static readonly Font Body   = new("Segoe UI", 9.5f);
    public static readonly Font Strong = new("Segoe UI", 10f, FontStyle.Bold);
    public static readonly Font Title  = new("Segoe UI", 14f, FontStyle.Bold);
    public static readonly Font Mono   = new("Consolas", 9.5f);
}

/// <summary>GDI+ 圆角矩形构造工具。</summary>
internal static class RoundedShape
{
    public static GraphicsPath Build(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(r);
            return path;
        }
        var d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>把控件 Region 设为圆角，用于裁剪 VideoView 等不能自绘的控件。</summary>
    public static void ApplyRegion(Control c, int radius)
    {
        c.Resize -= OnResize;
        c.Resize += OnResize;
        Apply(c);

        void OnResize(object? s, EventArgs e) => Apply(c);
        void Apply(Control ctl)
        {
            if (ctl.Width <= 0 || ctl.Height <= 0) return;
            using var p = Build(new RectangleF(0, 0, ctl.Width, ctl.Height), radius);
            ctl.Region = new Region(p);
        }
    }
}

/// <summary>背景为垂直渐变的容器。</summary>
internal sealed class GradientPanel : Panel
{
    public Color Top    { get; set; } = UiTheme.BgTop;
    public Color Bottom { get; set; } = UiTheme.BgBottom;

    public GradientPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 0 || Height <= 0) return;
        using var brush = new LinearGradientBrush(
            new Rectangle(0, 0, Width, Height), Top, Bottom, LinearGradientMode.Vertical);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}

/// <summary>白色圆角卡片：背景 + 细灰边 + 可选阴影。可作为预览框 / 列表容器外壳。</summary>
internal sealed class CardPanel : Panel
{
    public int CornerRadius { get; set; } = UiTheme.Radius;
    public Color BorderColor { get; set; } = UiTheme.Border;
    public Color FillColor   { get; set; } = UiTheme.CardBg;
    public float BorderWidth { get; set; } = 1f;

    public CardPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e) { /* 透明，由 OnPaint 处理 */ }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 2 || Height <= 2) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = RoundedShape.Build(rect, CornerRadius);
        using var fill = new SolidBrush(FillColor);
        g.FillPath(fill, path);
        using var pen = new Pen(BorderColor, BorderWidth);
        g.DrawPath(pen, path);
    }
}

/// <summary>设计稿要求的扁平圆角按钮。支持三种语义色 + hover 阴影 + 按下缩放。</summary>
internal sealed class FlatRoundButton : Button
{
    public enum Kind { Primary, Secondary, Danger }

    private bool _hover;
    private bool _pressed;

    public Kind Style { get; set; } = Kind.Primary;
    public int CornerRadius { get; set; } = UiTheme.ButtonRadius;

    public FlatRoundButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        ForeColor = Color.White;
        Font = UiTheme.Body;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e)   { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // 必须调用 base：让 WinForms 透明背景模拟沿父控件链正确清底。
        // 若此处什么都不做，Windows 不会擦除旧 DC 内容，相邻控件上一帧的
        // 文字/图形会透过按钮圆角之外的未绘制区域"幽灵"叠显出来。
        base.OnPaintBackground(pevent);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var baseColor = Style switch
        {
            Kind.Primary  => UiTheme.Primary,
            Kind.Danger   => UiTheme.Danger,
            _             => UiTheme.Secondary,
        };
        if (!Enabled) baseColor = Color.FromArgb(160, baseColor);
        else if (_pressed) baseColor = Darken(baseColor, 0.15f);
        else if (_hover) baseColor = Lighten(baseColor, 0.08f);

        // 按下时做 0.98 倍缩放微动画
        var scale = _pressed ? 0.98f : 1f;
        var w = Width * scale;
        var h = Height * scale;
        var x = (Width  - w) / 2f;
        var y = (Height - h) / 2f;
        var rect = new RectangleF(x + 0.5f, y + 0.5f, w - 1f, h - 1f);

        using var path = RoundedShape.Build(rect, CornerRadius);

        // hover 时绘制柔和阴影
        if (_hover && Enabled)
        {
            using var shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0));
            using var shadowPath = RoundedShape.Build(
                new RectangleF(rect.X, rect.Y + 2, rect.Width, rect.Height), CornerRadius);
            g.FillPath(shadow, shadowPath);
        }

        using (var fill = new SolidBrush(baseColor))
            g.FillPath(fill, path);

        // 文本
        TextRenderer.DrawText(g, Text, Font, Rectangle.Round(rect), ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    private static Color Lighten(Color c, float pct) =>
        Color.FromArgb(c.A,
            (int)Math.Min(255, c.R + 255 * pct),
            (int)Math.Min(255, c.G + 255 * pct),
            (int)Math.Min(255, c.B + 255 * pct));

    private static Color Darken(Color c, float pct) =>
        Color.FromArgb(c.A,
            (int)Math.Max(0, c.R - 255 * pct),
            (int)Math.Max(0, c.G - 255 * pct),
            (int)Math.Max(0, c.B - 255 * pct));
}

/// <summary>
/// 简易 Tab 切换器：顶部分段按钮 + 内容容器。
/// 用于“列表 / 日志”切换，避免引入完整 TabControl 的视觉冲突。
/// </summary>
internal sealed class SegmentedTabPanel : Panel
{
    private readonly FlowLayoutPanel _header;
    private readonly Panel _body;
    private readonly List<(Button btn, Control content)> _tabs = new();
    private int _selected = -1;

    public SegmentedTabPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;

        _header = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.Transparent,
            Padding = new Padding(8, 6, 8, 0),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        _body = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(8, 4, 8, 8) };

        Controls.Add(_body);
        Controls.Add(_header);
    }

    public void AddTab(string title, Control content)
    {
        var idx = _tabs.Count;
        var btn = new Button
        {
            Text = title,
            AutoSize = false,
            Width = 80,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            Font = UiTheme.Body,
            Margin = new Padding(0, 0, 6, 0),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => Select(idx);

        content.Dock = DockStyle.Fill;
        content.Visible = false;

        _tabs.Add((btn, content));
        _header.Controls.Add(btn);
        _body.Controls.Add(content);

        if (_selected < 0) Select(0);
        else PaintTabs();
    }

    public void Select(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _selected = index;
        for (int i = 0; i < _tabs.Count; i++)
            _tabs[i].content.Visible = (i == index);
        PaintTabs();
    }

    private void PaintTabs()
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            var b = _tabs[i].btn;
            var on = (i == _selected);
            b.BackColor = on ? UiTheme.Primary : UiTheme.HeaderBg;
            b.ForeColor = on ? Color.White : UiTheme.Text;
        }
    }
}
