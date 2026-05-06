using System.ComponentModel;
using System.Drawing.Drawing2D;
using LosslessMediaCutter.Models;

namespace LosslessMediaCutter.Controls;

/// <summary>
/// 浅色风格自定义时间轴控件（GDI+ 自绘）。
/// 视觉要点（与设计稿一致）：
///   - 白色卡片背景 + 圆角 + 灰色细边框（容器外层完成，本控件只画轨道）
///   - 横向时间尺：标记 0s/4s/5s/10s 等关键时间点，刻度线为细灰色
///   - 截取区间：#4A90E2 半透明渐变填充 (20%→10%)，区间边缘实线高亮
///   - 当前播放位置：蓝色圆点
///   - 开始/结束标记：蓝色竖线，可拖拽，悬浮显示 HH:mm:ss.fff
/// </summary>
public sealed class TimelineControl : Control
{
    public TimelineControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw
                 | ControlStyles.Selectable
                 | ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        ForeColor = UiTheme.TextMuted;
        Font = UiTheme.Body;
        Height = 78;
        Cursor = Cursors.Hand;
    }

    #region Public state

    private long _durationMs;
    [Browsable(false)]
    public long DurationMs
    {
        get => _durationMs;
        set { _durationMs = Math.Max(0, value); Invalidate(); }
    }

    private long _positionMs;
    [Browsable(false)]
    public long PositionMs
    {
        get => _positionMs;
        set { _positionMs = Math.Clamp(value, 0, Math.Max(0, _durationMs)); Invalidate(); }
    }

    private readonly List<CutSegment> _segments = new();
    [Browsable(false)]
    public IReadOnlyList<CutSegment> Segments => _segments;

    public CutSegment? SelectedSegment { get; private set; }

    public void SetSegments(IEnumerable<CutSegment> segs)
    {
        _segments.Clear();
        _segments.AddRange(segs);
        if (SelectedSegment is not null && !_segments.Contains(SelectedSegment))
            SelectedSegment = null;
        Invalidate();
    }

    public void SelectSegment(CutSegment? seg)
    {
        SelectedSegment = seg;
        Invalidate();
    }

    #endregion

    #region Events

    public event EventHandler<long>? SeekRequested;
    public event EventHandler<CutSegment>? SegmentEdited;
    public event EventHandler<CutSegment?>? SegmentSelectionChanged;

    #endregion

    #region Layout helpers

    private const int RulerHeight = 22;
    private const int HandleHalfWidth = 4;
    private const int SidePad = 12;
    private int TrackTop => RulerHeight + 6;
    private int TrackBottom => Height - 10;
    private int TrackHeight => TrackBottom - TrackTop;
    private int TrackLeft   => SidePad;
    private int TrackRight  => Width - SidePad;
    private int TrackWidth  => Math.Max(1, TrackRight - TrackLeft);

    private float MsToX(long ms)
    {
        if (_durationMs <= 0) return TrackLeft;
        var ratio = (double)ms / _durationMs;
        return (float)(TrackLeft + ratio * TrackWidth);
    }

    private long XToMs(int x)
    {
        if (_durationMs <= 0) return 0;
        var ratio = Math.Clamp((double)(x - TrackLeft) / TrackWidth, 0, 1);
        return (long)(ratio * _durationMs);
    }

    #endregion

    #region Hit testing

    private enum DragMode { None, Caret, SegmentStart, SegmentEnd, SegmentMove }

    private DragMode _drag = DragMode.None;
    private CutSegment? _dragSegment;
    private long _dragMoveOffsetMs;

    private (DragMode mode, CutSegment? seg) HitTest(int x, int y)
    {
        foreach (var seg in _segments)
        {
            var sx = MsToX((long)seg.Start.TotalMilliseconds);
            var ex = MsToX((long)seg.End.TotalMilliseconds);
            if (Math.Abs(x - sx) <= HandleHalfWidth && y >= TrackTop && y <= TrackBottom)
                return (DragMode.SegmentStart, seg);
            if (Math.Abs(x - ex) <= HandleHalfWidth && y >= TrackTop && y <= TrackBottom)
                return (DragMode.SegmentEnd, seg);
        }
        foreach (var seg in _segments)
        {
            var sx = MsToX((long)seg.Start.TotalMilliseconds);
            var ex = MsToX((long)seg.End.TotalMilliseconds);
            if (x > sx + HandleHalfWidth && x < ex - HandleHalfWidth && y >= TrackTop && y <= TrackBottom)
                return (DragMode.SegmentMove, seg);
        }
        return (DragMode.Caret, null);
    }

    #endregion

    #region Mouse

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_durationMs <= 0 || e.Button != MouseButtons.Left) return;

        var (mode, seg) = HitTest(e.X, e.Y);
        _drag = mode;
        _dragSegment = seg;

        if (mode == DragMode.Caret)
        {
            var ms = XToMs(e.X);
            PositionMs = ms;
            SeekRequested?.Invoke(this, ms);
            SelectedSegment = null;
            SegmentSelectionChanged?.Invoke(this, null);
        }
        else if (seg is not null)
        {
            SelectedSegment = seg;
            SegmentSelectionChanged?.Invoke(this, seg);
            if (mode == DragMode.SegmentMove)
                _dragMoveOffsetMs = XToMs(e.X) - (long)seg.Start.TotalMilliseconds;
        }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_durationMs <= 0) return;

        var ms = XToMs(e.X);
        _hoverText = CutSegment.Format(TimeSpan.FromMilliseconds(ms));
        _hoverX = e.X;

        if (_drag == DragMode.None)
        {
            var (mode, _) = HitTest(e.X, e.Y);
            Cursor = mode is DragMode.SegmentStart or DragMode.SegmentEnd
                ? Cursors.SizeWE
                : (mode == DragMode.SegmentMove ? Cursors.SizeAll : Cursors.Hand);
            Invalidate();
            return;
        }

        switch (_drag)
        {
            case DragMode.Caret:
                PositionMs = ms;
                SeekRequested?.Invoke(this, ms);
                break;
            case DragMode.SegmentStart when _dragSegment is not null:
            {
                var newStart = TimeSpan.FromMilliseconds(Math.Min(ms, (long)_dragSegment.End.TotalMilliseconds - 10));
                _dragSegment.Start = newStart < TimeSpan.Zero ? TimeSpan.Zero : newStart;
                SegmentEdited?.Invoke(this, _dragSegment);
                break;
            }
            case DragMode.SegmentEnd when _dragSegment is not null:
            {
                var maxEnd = TimeSpan.FromMilliseconds(_durationMs);
                var newEnd = TimeSpan.FromMilliseconds(Math.Max(ms, (long)_dragSegment.Start.TotalMilliseconds + 10));
                _dragSegment.End = newEnd > maxEnd ? maxEnd : newEnd;
                SegmentEdited?.Invoke(this, _dragSegment);
                break;
            }
            case DragMode.SegmentMove when _dragSegment is not null:
            {
                var len = (long)_dragSegment.Duration.TotalMilliseconds;
                var newStart = ms - _dragMoveOffsetMs;
                newStart = Math.Clamp(newStart, 0, _durationMs - len);
                _dragSegment.Start = TimeSpan.FromMilliseconds(newStart);
                _dragSegment.End   = TimeSpan.FromMilliseconds(newStart + len);
                SegmentEdited?.Invoke(this, _dragSegment);
                break;
            }
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _drag = DragMode.None;
        _dragSegment = null;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverText = null;
        Invalidate();
    }

    private string? _hoverText;
    private int _hoverX;

    #endregion

    #region Painting

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        DrawRuler(g);
        DrawTrackBackground(g);
        DrawSegments(g);
        DrawCaret(g);
        DrawHoverTooltip(g);

        if (_durationMs <= 0)
        {
            using var br = new SolidBrush(UiTheme.TextMuted);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("加载媒体后将在此显示时间轴", Font, br, ClientRectangle, sf);
        }
    }

    private void DrawTrackBackground(Graphics g)
    {
        var rect = new RectangleF(TrackLeft, TrackTop, TrackWidth, TrackHeight);
        using var path = RoundedShape.Build(rect, 4);
        using var br = new SolidBrush(Color.FromArgb(0xF3, 0xF5, 0xF8));
        g.FillPath(br, path);
        using var pen = new Pen(UiTheme.BorderSoft);
        g.DrawPath(pen, path);
    }

    private void DrawRuler(Graphics g)
    {
        if (_durationMs <= 0) return;

        using var pen = new Pen(Color.FromArgb(0xC8, 0xCE, 0xD6));
        using var labelBr = new SolidBrush(UiTheme.TextMuted);

        // 自适应主刻度间隔（毫秒）：保证两刻度像素间距 ≥ 70
        long[] candidates = { 100, 200, 500, 1000, 2000, 5000, 10_000, 30_000, 60_000, 300_000, 600_000, 1_800_000, 3_600_000 };
        long step = candidates[^1];
        foreach (var c in candidates)
        {
            var pixel = (c / (double)_durationMs) * TrackWidth;
            if (pixel >= 70) { step = c; break; }
        }

        for (long t = 0; t <= _durationMs; t += step)
        {
            var x = MsToX(t);
            g.DrawLine(pen, x, RulerHeight - 6, x, RulerHeight);
            var label = FormatRulerLabel(t, step);
            var sz = g.MeasureString(label, Font);
            g.DrawString(label, Font, labelBr, x - sz.Width / 2f, 2);
        }

        // 末端刻度（总时长）
        {
            var x = MsToX(_durationMs);
            g.DrawLine(pen, x, RulerHeight - 6, x, RulerHeight);
        }
    }

    private static string FormatRulerLabel(long ms, long step)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        if (step >= 60_000)
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes}:{t.Seconds:D2}";
        if (step >= 1000)
            return t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes}:{t.Seconds:D2}" : $"{t.Seconds}s";
        return $"{ms / 1000.0:0.0}s";
    }

    private void DrawSegments(Graphics g)
    {
        foreach (var seg in _segments)
        {
            var sx = MsToX((long)seg.Start.TotalMilliseconds);
            var ex = MsToX((long)seg.End.TotalMilliseconds);
            if (ex - sx < 1) ex = sx + 1;
            var rect = RectangleF.FromLTRB(sx, TrackTop + 1, ex, TrackBottom - 1);
            var isSel = seg == SelectedSegment;

            // 半透明蓝色渐变（顶部 20% → 底部 10%）
            using (var brush = new LinearGradientBrush(
                rect,
                Color.FromArgb(51, UiTheme.Primary),   // ~20%
                Color.FromArgb(26, UiTheme.Primary),   // ~10%
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(brush, rect);
            }

            // 区间边缘实线高亮
            using var border = new Pen(isSel ? UiTheme.Primary : Color.FromArgb(180, UiTheme.Primary), isSel ? 2f : 1.4f);
            g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);

            // 起止竖线手柄（蓝色）
            using var handlePen = new Pen(UiTheme.Primary, 2.5f);
            g.DrawLine(handlePen, sx, TrackTop - 2, sx, TrackBottom + 2);
            g.DrawLine(handlePen, ex, TrackTop - 2, ex, TrackBottom + 2);
        }
    }

    private void DrawCaret(Graphics g)
    {
        if (_durationMs <= 0) return;
        var x = MsToX(_positionMs);
        var y = (TrackTop + TrackBottom) / 2f;

        // 细线
        using var line = new Pen(Color.FromArgb(120, UiTheme.Primary), 1f);
        g.DrawLine(line, x, TrackTop, x, TrackBottom);

        // 蓝色圆点
        const float r = 6f;
        using var fill = new SolidBrush(UiTheme.Primary);
        g.FillEllipse(fill, x - r, y - r, r * 2, r * 2);
        using var ring = new Pen(Color.White, 1.5f);
        g.DrawEllipse(ring, x - r, y - r, r * 2, r * 2);
    }

    private void DrawHoverTooltip(Graphics g)
    {
        if (string.IsNullOrEmpty(_hoverText) || _durationMs <= 0) return;

        var size = g.MeasureString(_hoverText, Font);
        var x = Math.Min(Math.Max(_hoverX - size.Width / 2, TrackLeft), TrackRight - size.Width - 4);
        var y = (float)TrackTop - size.Height - 2;
        if (y < 0) y = TrackBottom + 2;

        var rect = new RectangleF(x - 4, y - 1, size.Width + 8, size.Height + 2);
        using var path = RoundedShape.Build(rect, 4);
        using var bg = new SolidBrush(Color.FromArgb(230, UiTheme.Primary));
        g.FillPath(bg, path);
        using var br = new SolidBrush(Color.White);
        g.DrawString(_hoverText, Font, br, x, y);
    }

    #endregion
}
