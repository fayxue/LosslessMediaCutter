namespace LosslessMediaCutter.Dialogs;

public enum SaveMode
{
    MergeIntoOne,
    SeparateFiles,
}

/// <summary>多段保存策略选择对话框（纯代码构建，无 Designer.cs 依赖）。</summary>
public sealed class SaveModeDialog : Form
{
    public SaveMode Mode { get; private set; } = SaveMode.MergeIntoOne;

    public SaveModeDialog(int segmentCount)
    {
        Text = "选择保存方式";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(380, 170);

        var lbl = new Label
        {
            Text = $"检测到 {segmentCount} 段截取区间，请选择输出方式：",
            AutoSize = true,
            Location = new Point(16, 16),
        };

        var rbMerge = new RadioButton
        {
            Text = "合并为单个文件（FFmpeg concat demuxer，要求编码一致）",
            AutoSize = true,
            Location = new Point(20, 50),
            Checked = true,
        };
        var rbSeparate = new RadioButton
        {
            Text = "分别保存为独立文件（每段一个文件）",
            AutoSize = true,
            Location = new Point(20, 78),
        };

        var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(190, 125), Width = 80 };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(280, 125), Width = 80 };

        btnOk.Click += (_, _) => Mode = rbMerge.Checked ? SaveMode.MergeIntoOne : SaveMode.SeparateFiles;

        Controls.AddRange(new Control[] { lbl, rbMerge, rbSeparate, btnOk, btnCancel });
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }
}
