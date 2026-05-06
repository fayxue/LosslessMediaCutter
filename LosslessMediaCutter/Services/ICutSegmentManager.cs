using LosslessMediaCutter.Models;

namespace LosslessMediaCutter.Services;

public interface ICutSegmentManager
{
    IReadOnlyList<CutSegment> Segments { get; }

    event EventHandler? Changed;

    CutSegment Add(TimeSpan start, TimeSpan end);
    void Remove(CutSegment segment);
    void Clear();
    void Update(CutSegment segment, TimeSpan start, TimeSpan end);
}
