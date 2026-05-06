using LosslessMediaCutter.Models;

namespace LosslessMediaCutter.Services;

public sealed class CutSegmentManager : ICutSegmentManager
{
    private readonly List<CutSegment> _segments = new();

    public IReadOnlyList<CutSegment> Segments => _segments;

    public event EventHandler? Changed;

    public CutSegment Add(TimeSpan start, TimeSpan end)
    {
        if (end <= start) throw new ArgumentException("结束时间必须大于开始时间。");
        var seg = new CutSegment { Start = start, End = end };
        _segments.Add(seg);
        Sort();
        Changed?.Invoke(this, EventArgs.Empty);
        return seg;
    }

    public void Remove(CutSegment segment)
    {
        if (_segments.Remove(segment))
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (_segments.Count == 0) return;
        _segments.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Update(CutSegment segment, TimeSpan start, TimeSpan end)
    {
        if (end <= start) return;
        segment.Start = start;
        segment.End = end;
        Sort();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Sort() => _segments.Sort((a, b) => a.Start.CompareTo(b.Start));
}
