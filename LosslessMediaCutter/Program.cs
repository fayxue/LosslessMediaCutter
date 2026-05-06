using LosslessMediaCutter.Services;

namespace LosslessMediaCutter;

internal static class Program
{
    /// <summary>
    /// 应用程序入口。完成 LibVLC 初始化与依赖装配。
    /// </summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // LibVLCSharp 需要在第一个使用 LibVLC 的实例创建之前调用 Core.Initialize()
        LibVLCSharp.Shared.Core.Initialize();

        using var ffmpegService = new FFmpegService();
        using var playerService = new VlcMediaPlayerService();
        var segmentManager = new CutSegmentManager();

        Application.Run(new MainForm(playerService, ffmpegService, segmentManager));
    }
}
