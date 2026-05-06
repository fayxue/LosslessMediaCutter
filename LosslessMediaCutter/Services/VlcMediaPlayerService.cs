using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace LosslessMediaCutter.Services;

/// <summary>
/// 基于 LibVLCSharp 的播放服务实现。所有事件已转回 UI 线程外，调用方需自行 Invoke。
/// </summary>
public sealed class VlcMediaPlayerService : IMediaPlayerService
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private long _durationMs;

    public VlcMediaPlayerService()
    {
        _libVlc = new LibVLC(enableDebugLogs: false);
        _player = new MediaPlayer(_libVlc);

        _player.TimeChanged += (_, e) => PositionChanged?.Invoke(this, e.Time);
        _player.LengthChanged += (_, e) =>
        {
            _durationMs = e.Length;
            MediaLoaded?.Invoke(this, e.Length);
        };
    }

    public long DurationMs => _durationMs;
    public long PositionMs => _player.Time;
    public bool IsPlaying => _player.IsPlaying;

    public event EventHandler<long>? PositionChanged;
    public event EventHandler<long>? MediaLoaded;

    public void Attach(VideoView view) => view.MediaPlayer = _player;

    public Task LoadAsync(string filePath, CancellationToken ct = default)
    {
        var media = new Media(_libVlc, new Uri(filePath));
        _player.Media = media;
        // 立即 Play 后 Pause，可触发 LengthChanged，便于尽快拿到时长
        _player.Play();
        _player.SetPause(true);
        return Task.CompletedTask;
    }

    public void Play() => _player.Play();
    public void Pause() => _player.SetPause(true);
    public void Stop() => _player.Stop();

    public void Seek(long positionMs)
    {
        if (_durationMs <= 0) return;
        positionMs = Math.Clamp(positionMs, 0, _durationMs);
        _player.Time = positionMs;
    }

    /// <summary>
    /// LibVLC 没有公开“后退一帧”，统一以约一帧步长 (≈ 1000/Fps，缺省 40ms) 进行 seek。
    /// 前进直接调用 NextFrame() 以获得更精确的逐帧效果。
    /// </summary>
    public void StepForward()
    {
        if (_durationMs <= 0) return;
        try { _player.NextFrame(); }
        catch { Seek(_player.Time + FrameStepMs); }
    }

    public void StepBackward()
    {
        if (_durationMs <= 0) return;
        Seek(_player.Time - FrameStepMs);
    }

    private long FrameStepMs
    {
        get
        {
            var fps = _player.Fps;
            if (fps is > 1f and < 1000f) return Math.Max(1, (long)(1000f / fps));
            return 40;
        }
    }

    public void Dispose()
    {
        try { _player.Stop(); } catch { /* ignore */ }
        _player.Dispose();
        _libVlc.Dispose();
    }
}
