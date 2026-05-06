using LibVLCSharp.WinForms;

namespace LosslessMediaCutter.Services;

public interface IMediaPlayerService : IDisposable
{
    /// <summary>当前总时长（毫秒）。</summary>
    long DurationMs { get; }

    /// <summary>当前播放位置（毫秒）。</summary>
    long PositionMs { get; }

    bool IsPlaying { get; }

    /// <summary>位置变化（用于驱动时间轴游标）。</summary>
    event EventHandler<long>? PositionChanged;

    /// <summary>媒体加载完成（已知时长）。</summary>
    event EventHandler<long>? MediaLoaded;

    /// <summary>把 VLC 视频输出绑定到一个 WinForms 控件。</summary>
    void Attach(VideoView view);

    Task LoadAsync(string filePath, CancellationToken ct = default);

    void Play();
    void Pause();
    void Stop();

    /// <summary>毫秒级 seek。</summary>
    void Seek(long positionMs);

    /// <summary>按帧或固定步长向前 / 向后微调一次（约一帧）。</summary>
    void StepForward();
    void StepBackward();
}
