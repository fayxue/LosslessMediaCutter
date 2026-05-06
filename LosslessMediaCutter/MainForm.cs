using LibVLCSharp.WinForms;
using LosslessMediaCutter.Controls;
using LosslessMediaCutter.Dialogs;
using LosslessMediaCutter.Models;
using LosslessMediaCutter.Services;

namespace LosslessMediaCutter;

/// <summary>
/// 主窗体：浅色现代风格，三段式布局。
///   [顶] 圆角白色卡片包裹 VideoView，未加载时显示 “Preview” 占位文本。
///        播放时卡片边框高亮为 #4A90E2。
///   [中] 圆角卡片内嵌 TimelineControl —— 蓝色游标 / 半透明蓝色区间 / 蓝色竖线手柄。
///   [底] 左：两行 4 列圆角按钮（主/次/警示三色）；右：Tab 切换的列表 / 日志卡片。
/// </summary>
public sealed class MainForm : Form
{
    private readonly IMediaPlayerService _player;
    private readonly IFFmpegService _ffmpeg;
    private readonly ICutSegmentManager _segments;

    // ── 顶部预览 ─────────────────────────────────────────────────
    private readonly CardPanel _previewCard = new()
    {
        Dock = DockStyle.Fill,
        FillColor = Color.White,
        BorderColor = UiTheme.Border,
    };
    private readonly Panel _videoHost = new()
    {
        Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(0x2C, 0x3E, 0x50),
        Margin = new Padding(0),
    };
    private readonly VideoView _videoView = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    private readonly Label _lblPreviewPlaceholder = new()
    {
        Text = "Preview",
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 14f, FontStyle.Bold),
        BackColor = Color.FromArgb(0x2C, 0x3E, 0x50),
    };
    private readonly Label _lblOverlayTime = new()
    {
        Text = "00:00:00.000 / 00:00:00.000",
        AutoSize = true,
        ForeColor = Color.White,
        BackColor = Color.FromArgb(140, 0, 0, 0),
        Font = new Font("Consolas", 10f, FontStyle.Bold),
        Padding = new Padding(8, 3, 8, 3),
        Location = new Point(12, 12),
    };

    // ── 中部时间轴 ──────────────────────────────────────────────
    private readonly CardPanel _timelineCard = new() { Dock = DockStyle.Fill };
    private readonly TimelineControl _timeline = new() { Dock = DockStyle.Fill };

    // ── 底部按钮 ─────────────────────────────────────────────────
    private readonly Button _btnOpen          = new() { Text = "📂  打开"       };
    private readonly Button _btnPlayPause     = new() { Text = "▶  播放",     Enabled = false };
    private readonly Button _btnSetStart      = new() { Text = "⏴  设为开始", Enabled = false };
    private readonly Button _btnSetEnd        = new() { Text = "⏵  设为结束", Enabled = false };
    private readonly Button _btnAddSegment    = new() { Text = "＋  添加到列表", Enabled = false };
    private readonly Button _btnDeleteSegment = new() { Text = "✖  删除选中",  Enabled = false };
    private readonly Button _btnSave          = new() { Text = "💾  保存",     Enabled = false };
    private readonly Button _btnCancel        = new() { Text = "⏹  取消任务",  Enabled = false };

    // ── 状态显示 ────────────────────────────────────────────────
    private readonly Label _lblPending = new()
    {
        AutoSize = true,
        ForeColor = UiTheme.Text,
        Font = UiTheme.Body,
        Text = "待添加：[未设置] → [未设置]",
    };
    private readonly Label _lblInfo = new()
    {
        AutoSize = true,
        ForeColor = UiTheme.TextMuted,
        Font = UiTheme.Body,
        Text = "未加载",
    };

    // ── 列表 + 日志 ────────────────────────────────────────────
    private readonly CardPanel _rightCard = new() { Dock = DockStyle.Fill };
    private readonly SegmentedTabPanel _tabs = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _gridSegments = new()
    {
        Dock = DockStyle.Fill,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoGenerateColumns = false,
        ReadOnly = true,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
        EnableHeadersVisualStyles = false,
        GridColor = UiTheme.BorderSoft,
    };
    private readonly TextBox _txtLog = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        ReadOnly = true,
        Dock = DockStyle.Fill,
        BackColor = Color.White,
        ForeColor = UiTheme.Text,
        BorderStyle = BorderStyle.None,
        Font = new Font("Consolas", 10f),
    };

    private readonly ProgressBar _progress = new()
    {
        Style = ProgressBarStyle.Marquee,
        Visible = false,
        Height = 4,
        Dock = DockStyle.Bottom,
    };

    private TimeSpan _pendingStart = TimeSpan.Zero;
    private TimeSpan _pendingEnd = TimeSpan.Zero;
    private MediaInfo? _mediaInfo;
    private CancellationTokenSource? _cts;

    public MainForm(IMediaPlayerService player, IFFmpegService ffmpeg, ICutSegmentManager segments)
    {
        _player = player;
        _ffmpeg = ffmpeg;
        _segments = segments;

        Text = "无损音视频截取工具 (Lossless Media Cutter)";
        ClientSize = new Size(1280, 820);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1080, 680);
        AllowDrop = true;
        Font = UiTheme.Body;
        ForeColor = UiTheme.Text;
        BackColor = UiTheme.BgTop;
        KeyPreview = true;
        DoubleBuffered = true;

        BuildLayout();
        WireEvents();
    }

    #region Layout

    private void BuildLayout()
    {
        // 渐变背景
        var bg = new GradientPanel { Dock = DockStyle.Fill };
        Controls.Add(bg);
        Controls.Add(_progress);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14, 14, 14, 8),
            BackColor = Color.Transparent,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // 顶部预览
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // 中部时间轴
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 270)); // 底部
        bg.Controls.Add(root);

        // ── 顶部预览卡 ─────────────────────────────
        var previewWrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 8) };
        previewWrapper.Controls.Add(_previewCard);

        var previewInner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(8) };
        _videoHost.Controls.Add(_videoView);
        _videoHost.Controls.Add(_lblPreviewPlaceholder);
        _videoHost.Controls.Add(_lblOverlayTime);
        _videoView.Visible = false;             // 加载完成后再显示
        _lblOverlayTime.Visible = false;
        previewInner.Controls.Add(_videoHost);
        _previewCard.Controls.Add(previewInner);

        // VideoView 圆角裁剪（直接对 VideoView 设 Region 会和 VLC 渲染冲突，改为对宿主面板设圆角）
        RoundedShape.ApplyRegion(_videoHost, 6);

        root.Controls.Add(previewWrapper, 0, 0);

        // ── 中部时间轴卡 ───────────────────────────
        var timelineWrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 8) };
        var timelineInner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(10, 8, 10, 8) };
        timelineInner.Controls.Add(_timeline);
        _timelineCard.Controls.Add(timelineInner);
        timelineWrapper.Controls.Add(_timelineCard);
        root.Controls.Add(timelineWrapper, 0, 1);

        // ── 底部 ───────────────────────────────────
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 480));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        bottom.Controls.Add(BuildButtonsPanel(), 0, 0);
        bottom.Controls.Add(BuildRightPanel(),    1, 0);

        root.Controls.Add(bottom, 0, 2);

        _player.Attach(_videoView);
    }

    private Control BuildButtonsPanel()
    {
        var card = new CardPanel { Dock = DockStyle.Fill };

        // 外层：上边距14、左右16、下8；行：两行按钮 + 弹性状态行
        var inner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(16, 14, 16, 8),
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
        };
        inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // 行1：按钮
        inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // 行2：按钮（含上间距）
        inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // 行3：状态文本

        // ── 按钮行构造辅助（4 列，等宽，列间距 8px）──────────────
        // 每列 25% 宽；按钮 Dock=Fill；单元格 Margin 实现间距（右侧单元格左 Margin=8）
        TableLayoutPanel MakeButtonRow(Button b0, Button b1, Button b2, Button b3,
            Padding rowMargin)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = rowMargin,
            };
            for (int i = 0; i < 4; i++)
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Button[] btns = { b0, b1, b2, b3 };
            for (int i = 0; i < 4; i++)
            {
                var b = btns[i];
                b.Dock = DockStyle.Fill;
                // 列间距：第 1~3 列左侧留 8px
                b.Margin = i == 0 ? new Padding(0) : new Padding(8, 0, 0, 0);
                row.Controls.Add(b, i, 0);
            }
            return row;
        }

        // 统一样式辅助
        static void Style(Button b, Color bg, Color fg)
        {
            b.FlatStyle  = FlatStyle.Flat;
            b.BackColor  = bg;
            b.ForeColor  = fg;
            b.Font       = new Font("Segoe UI", 9f, FontStyle.Regular);
            b.Cursor     = Cursors.Hand;
            b.FlatAppearance.BorderColor    = Color.FromArgb(60, 255, 255, 255);
            b.FlatAppearance.BorderSize     = 1;
            b.FlatAppearance.MouseOverBackColor  = ControlPaint.Light(bg, 0.18f);
            b.FlatAppearance.MouseDownBackColor  = ControlPaint.Dark(bg, 0.10f);
        }

        // 蓝色：打开
        Style(_btnOpen,          Color.FromArgb(52,  110, 235), Color.White);
        // 绿色：播放/暂停
        Style(_btnPlayPause,     Color.FromArgb(39,  160,  90), Color.White);
        // 青灰：标记开始 / 标记结束
        Style(_btnSetStart,      Color.FromArgb(52,  148, 165), Color.White);
        Style(_btnSetEnd,        Color.FromArgb(52,  148, 165), Color.White);
        // 蓝紫：添加到列表
        Style(_btnAddSegment,    Color.FromArgb(90,   80, 200), Color.White);
        // 橙红：删除选中
        Style(_btnDeleteSegment, Color.FromArgb(210,  70,  50), Color.White);
        // 绿色（深）：保存
        Style(_btnSave,          Color.FromArgb(30,  140,  80), Color.White);
        // 灰色：取消任务
        Style(_btnCancel,        Color.FromArgb(110, 110, 120), Color.White);

        // Row1: 打开 | 播放/暂停 | 设为开始 | 设为结束
        var row1 = MakeButtonRow(_btnOpen, _btnPlayPause, _btnSetStart, _btnSetEnd,
            new Padding(0, 0, 0, 0));
        // Row2: 添加到列表 | 删除选中 | 保存 | 取消任务（行间距 8px = 上 Margin）
        var row2 = MakeButtonRow(_btnAddSegment, _btnDeleteSegment, _btnSave, _btnCancel,
            new Padding(0, 8, 0, 0));

        inner.Controls.Add(row1, 0, 0);
        inner.Controls.Add(row2, 0, 1);

        // 状态文本（底部对齐）
        var statusPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 6, 0, 0),
        };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        statusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        statusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        _lblPending.Margin = new Padding(2, 0, 0, 2);
        _lblInfo.Margin    = new Padding(2, 0, 0, 0);
        statusPanel.Controls.Add(_lblPending, 0, 0);
        statusPanel.Controls.Add(_lblInfo,    0, 1);

        inner.Controls.Add(statusPanel, 0, 2);
        card.Controls.Add(inner);

        var wrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 0, 12, 0) };
        wrapper.Controls.Add(card);
        return wrapper;
    }

    private Control BuildRightPanel()
    {
        var wrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        wrapper.Controls.Add(_rightCard);

        // 列表
        _gridSegments.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "#", Width = 44,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter },
        });
        _gridSegments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "开始", Width = 130 });
        _gridSegments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "结束", Width = 130 });
        _gridSegments.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "时长",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
        _gridSegments.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = UiTheme.Text,
            SelectionBackColor = UiTheme.Primary,
            SelectionForeColor = Color.White,
            Font = UiTheme.Body,
            Padding = new Padding(6, 0, 6, 0),
        };
        _gridSegments.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = UiTheme.RowAlt,
            ForeColor = UiTheme.Text,
            SelectionBackColor = UiTheme.Primary,
            SelectionForeColor = Color.White,
        };
        _gridSegments.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = UiTheme.HeaderBg,
            ForeColor = UiTheme.Text,
            Font = UiTheme.Strong,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 6, 0),
        };

        _tabs.AddTab("列表", _gridSegments);
        _tabs.AddTab("日志", _txtLog);
        _rightCard.Controls.Add(_tabs);

        return wrapper;
    }

    #endregion

    #region Wiring

    private void WireEvents()
    {
        _previewCard.Paint += (_, _) => { /* 边框颜色变更通过 Invalidate 触发 */ };

        DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += async (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                await LoadFileAsync(files[0]);
        };

        _btnOpen.Click += async (_, _) =>
        {
            using var ofd = new OpenFileDialog
            {
                Title = "选择音视频文件",
                Filter = "媒体文件|*.mp4;*.mkv;*.mov;*.avi;*.flv;*.ts;*.m4v;*.webm;*.mp3;*.wav;*.aac;*.flac;*.m4a|所有文件|*.*",
            };
            if (ofd.ShowDialog(this) == DialogResult.OK)
                await LoadFileAsync(ofd.FileName);
        };
        _btnPlayPause.Click += (_, _) => TogglePlayPause();

        _btnSetStart.Click += (_, _) =>
        {
            _pendingStart = TimeSpan.FromMilliseconds(_player.PositionMs);
            if (_pendingEnd <= _pendingStart) _pendingEnd = _pendingStart;
            UpdatePendingLabel();
        };
        _btnSetEnd.Click += (_, _) =>
        {
            _pendingEnd = TimeSpan.FromMilliseconds(_player.PositionMs);
            UpdatePendingLabel();
        };
        _btnAddSegment.Click += (_, _) =>
        {
            try
            {
                _segments.Add(_pendingStart, _pendingEnd);
                _pendingStart = _pendingEnd = TimeSpan.Zero;
                UpdatePendingLabel();
                _tabs.Select(0); // 切到“列表”
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "提示"); }
        };
        _btnDeleteSegment.Click += (_, _) =>
        {
            if (TryGetSelectedSegment(out var seg) && seg is not null) _segments.Remove(seg);
        };
        _btnSave.Click   += async (_, _) => await OnSaveAsync();
        _btnCancel.Click += (_, _) => _cts?.Cancel();

        _player.PositionChanged += (_, ms) => SafeInvoke(() =>
        {
            _timeline.PositionMs = ms;
            UpdateOverlayTime();
            UpdatePreviewBorder();
        });
        _player.MediaLoaded += (_, ms) => SafeInvoke(() =>
        {
            _timeline.DurationMs = ms;
            UpdateOverlayTime();
            EnablePlaybackControls(true);
            UpdatePreviewBorder();
        });

        _timeline.SeekRequested += (_, ms) => _player.Seek(ms);
        _timeline.SegmentEdited += (_, seg) =>
        {
            _segments.Update(seg, seg.Start, seg.End);
            RefreshSegmentGrid();
        };
        _timeline.SegmentSelectionChanged += (_, seg) =>
        {
            if (seg is null) { _gridSegments.ClearSelection(); return; }
            for (int i = 0; i < _segments.Segments.Count; i++)
            {
                if (ReferenceEquals(_segments.Segments[i], seg))
                {
                    _gridSegments.ClearSelection();
                    if (i < _gridSegments.RowCount) _gridSegments.Rows[i].Selected = true;
                    break;
                }
            }
        };

        _segments.Changed += (_, _) => SafeInvoke(RefreshSegmentGrid);

        _gridSegments.SelectionChanged += (_, _) =>
        {
            var hasSel = TryGetSelectedSegment(out var seg);
            _btnDeleteSegment.Enabled = hasSel;
            _timeline.SelectSegment(hasSel ? seg : null);
        };
        _gridSegments.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < _segments.Segments.Count)
                _player.Seek((long)_segments.Segments[e.RowIndex].Start.TotalMilliseconds);
        };

        KeyDown += (_, e) =>
        {
            if (!_btnPlayPause.Enabled) return;
            switch (e.KeyCode)
            {
                case Keys.Space:  TogglePlayPause();             e.Handled = true; break;
                case Keys.I:      _btnSetStart.PerformClick();   e.Handled = true; break;
                case Keys.O:      _btnSetEnd.PerformClick();     e.Handled = true; break;
                case Keys.A:      if (_btnAddSegment.Enabled)    _btnAddSegment.PerformClick(); e.Handled = true; break;
                case Keys.Delete: if (_btnDeleteSegment.Enabled) _btnDeleteSegment.PerformClick(); e.Handled = true; break;
                case Keys.Left:   _player.StepBackward();        e.Handled = true; break;
                case Keys.Right:  _player.StepForward();         e.Handled = true; break;
            }
        };
    }

    private void TogglePlayPause()
    {
        if (_player.IsPlaying) { _player.Pause(); _btnPlayPause.Text = "▶ 播放"; }
        else                   { _player.Play();  _btnPlayPause.Text = "❚❚ 暂停"; }
        UpdatePreviewBorder();
    }

    private void UpdatePreviewBorder()
    {
        _previewCard.BorderColor = _player.IsPlaying ? UiTheme.Primary : UiTheme.Border;
        _previewCard.BorderWidth = _player.IsPlaying ? 2f : 1f;
        _previewCard.Invalidate();
    }

    private bool TryGetSelectedSegment(out CutSegment? seg)
    {
        seg = null;
        if (_gridSegments.SelectedRows.Count == 0) return false;
        var idx = _gridSegments.SelectedRows[0].Index;
        if (idx < 0 || idx >= _segments.Segments.Count) return false;
        seg = _segments.Segments[idx];
        return true;
    }

    private void EnablePlaybackControls(bool enabled)
    {
        _btnPlayPause.Enabled = enabled;
        _btnSetStart.Enabled  = enabled;
        _btnSetEnd.Enabled    = enabled;
        _btnSave.Enabled      = enabled && _segments.Segments.Count > 0;
        UpdatePendingLabel();
    }

    private void UpdatePendingLabel()
    {
        var hasMark = _pendingStart > TimeSpan.Zero || _pendingEnd > TimeSpan.Zero;
        _lblPending.Text = hasMark
            ? $"待添加：{CutSegment.Format(_pendingStart)}  →  {CutSegment.Format(_pendingEnd)}"
            : "待添加：[未设置] → [未设置]";
        _btnAddSegment.Enabled = _pendingEnd > _pendingStart;
    }

    private void UpdateOverlayTime()
    {
        _lblOverlayTime.Text =
            $"{CutSegment.Format(TimeSpan.FromMilliseconds(_player.PositionMs))} / " +
            $"{CutSegment.Format(TimeSpan.FromMilliseconds(_player.DurationMs))}";
    }

    private void RefreshSegmentGrid()
    {
        _gridSegments.SuspendLayout();
        _gridSegments.Rows.Clear();
        for (int i = 0; i < _segments.Segments.Count; i++)
        {
            var s = _segments.Segments[i];
            _gridSegments.Rows.Add(
                (i + 1).ToString(),
                CutSegment.Format(s.Start),
                CutSegment.Format(s.End),
                CutSegment.Format(s.Duration));
        }
        _gridSegments.ResumeLayout();

        _timeline.SetSegments(_segments.Segments);

        var hasAny = _segments.Segments.Count > 0;
        _btnSave.Enabled = _player.DurationMs > 0 && hasAny;
    }

    private void SafeInvoke(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired) BeginInvoke(action);
        else action();
    }

    #endregion

    #region Actions

    private async Task LoadFileAsync(string filePath)
    {
        try
        {
            EnablePlaybackControls(false);
            _segments.Clear();
            _lblInfo.Text = "正在解析…";
            AppendLog($"加载文件: {filePath}", highlight: true);

            _mediaInfo = await _ffmpeg.ProbeAsync(filePath);
            await _player.LoadAsync(filePath);

            _lblPreviewPlaceholder.Visible = false;
            _videoView.Visible = true;
            _lblOverlayTime.Visible = true;
            _videoView.BringToFront();
            _lblOverlayTime.BringToFront();

            _lblInfo.Text =
                $"{_mediaInfo.FormatName} | {_mediaInfo.VideoCodec} {_mediaInfo.Width}×{_mediaInfo.Height}@{_mediaInfo.FrameRate:F2}fps | " +
                $"{_mediaInfo.AudioCodec} | 时长 {CutSegment.Format(_mediaInfo.Duration)}";
            _timeline.DurationMs = (long)_mediaInfo.Duration.TotalMilliseconds;
            UpdateOverlayTime();
            EnablePlaybackControls(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "加载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            AppendLog("ERROR: " + ex.Message);
            _lblInfo.Text = "加载失败";
        }
    }

    private async Task OnSaveAsync()
    {
        if (_mediaInfo is null || _segments.Segments.Count == 0) return;
        var ext = Path.GetExtension(_mediaInfo.FilePath);
        var baseName = Path.GetFileNameWithoutExtension(_mediaInfo.FilePath) + "_cut";

        try
        {
            _cts = new CancellationTokenSource();
            ToggleBusy(true);
            var log = new Progress<string>(line => AppendLog(line));

            if (_segments.Segments.Count == 1)
            {
                using var sfd = new SaveFileDialog { Filter = $"原始格式 (*{ext})|*{ext}|所有文件|*.*", FileName = baseName + ext };
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                await _ffmpeg.CutSegmentAsync(_mediaInfo.FilePath, sfd.FileName, _segments.Segments[0], log, _cts.Token);
                ShowDone(sfd.FileName);
            }
            else
            {
                using var dlg = new SaveModeDialog(_segments.Segments.Count);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                if (dlg.Mode == SaveMode.MergeIntoOne)
                {
                    using var sfd = new SaveFileDialog { Filter = $"原始格式 (*{ext})|*{ext}|所有文件|*.*", FileName = baseName + "_merged" + ext };
                    if (sfd.ShowDialog(this) != DialogResult.OK) return;
                    await _ffmpeg.CutAndConcatAsync(_mediaInfo.FilePath, sfd.FileName, _segments.Segments, log, _cts.Token);
                    ShowDone(sfd.FileName);
                }
                else
                {
                    using var fbd = new FolderBrowserDialog { Description = "选择输出目录" };
                    if (fbd.ShowDialog(this) != DialogResult.OK) return;
                    var outputs = await _ffmpeg.CutSegmentsAsync(_mediaInfo.FilePath, fbd.SelectedPath, baseName, _segments.Segments, log, _cts.Token);
                    ShowDone(string.Join("\n", outputs));
                }
            }
        }
        catch (OperationCanceledException) { AppendLog("任务已取消。"); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            AppendLog("ERROR: " + ex.Message);
        }
        finally
        {
            ToggleBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void ShowDone(string output)
    {
        AppendLog("完成: " + output, highlight: true);
        MessageBox.Show(this, "已完成：\n" + output, "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ToggleBusy(bool busy)
    {
        _progress.Visible = busy;
        _btnSave.Enabled  = !busy && _segments.Segments.Count > 0;
        _btnOpen.Enabled  = !busy;
        _btnCancel.Enabled = busy;
    }

    /// <summary>
    /// 关键操作日志使用 #4A90E2 高亮（通过插入带颜色标签的文本到 RichTextBox 才能真正染色，
    /// 此处保持普通 TextBox 以匹配设计稿样式，仅以 “▶ ” 前缀标识高亮项；
    /// 若后续切换为 RichTextBox 可直接替换 _txtLog 类型实现真正颜色高亮）。
    /// </summary>
    private void AppendLog(string line, bool highlight = false)
    {
        SafeInvoke(() =>
        {
            var prefix = highlight ? "▶ " : "  ";
            _txtLog.AppendText(prefix + line + Environment.NewLine);
        });
    }

    #endregion

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        base.OnFormClosed(e);
    }
}
