using LosslessMediaCutter.Models;

namespace LosslessMediaCutter.Services;

public interface IFFmpegService
{
    /// <summary>使用 ffprobe 异步解析媒体信息。</summary>
    Task<MediaInfo> ProbeAsync(string inputPath, CancellationToken ct = default);

    /// <summary>无损截取单段。</summary>
    Task CutSegmentAsync(string inputPath, string outputPath, CutSegment segment,
        IProgress<string>? log = null, CancellationToken ct = default);

    /// <summary>无损截取多段为独立文件。返回输出路径列表。</summary>
    Task<IReadOnlyList<string>> CutSegmentsAsync(string inputPath, string outputDirectory,
        string baseName, IReadOnlyList<CutSegment> segments,
        IProgress<string>? log = null, CancellationToken ct = default);

    /// <summary>无损截取多段并合并为单个文件 (concat demuxer)。</summary>
    Task CutAndConcatAsync(string inputPath, string outputPath,
        IReadOnlyList<CutSegment> segments,
        IProgress<string>? log = null, CancellationToken ct = default);
}
