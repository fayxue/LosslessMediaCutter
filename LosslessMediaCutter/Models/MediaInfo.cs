namespace LosslessMediaCutter.Models;

/// <summary>
/// 通过 ffprobe 解析得到的简要媒体信息。
/// </summary>
public sealed record MediaInfo(
    string FilePath,
    TimeSpan Duration,
    string FormatName,
    string? VideoCodec,
    string? AudioCodec,
    int? Width,
    int? Height,
    double? FrameRate,
    long? BitRate);
