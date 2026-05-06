namespace LosslessMediaCutter.Models;

/// <summary>
/// 表示一段待截取的时间区间。所有时间均以毫秒精度的 <see cref="TimeSpan"/> 存储。
/// </summary>
public sealed class CutSegment
{
    public Guid Id { get; } = Guid.NewGuid();

    public TimeSpan Start { get; set; }

    public TimeSpan End { get; set; }

    public TimeSpan Duration => End - Start;

    public string DisplayName =>
        $"{Format(Start)}  →  {Format(End)}   ({Format(Duration)})";

    public static string Format(TimeSpan t) =>
        $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}.{t.Milliseconds:D3}";

    public override string ToString() => DisplayName;
}
