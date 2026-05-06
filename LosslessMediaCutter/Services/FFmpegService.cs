using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using LosslessMediaCutter.Models;

namespace LosslessMediaCutter.Services;

/// <summary>
/// 直接通过 <see cref="Process"/> 调用 ffmpeg / ffprobe 的轻量服务实现。
/// 默认从下列位置查找可执行文件：
/// 1) 与本程序同目录下的 ffmpeg/ 子目录
/// 2) 系统 PATH 中
/// </summary>
public sealed class FFmpegService : IFFmpegService, IDisposable
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public FFmpegService(string? ffmpegPath = null, string? ffprobePath = null)
    {
        _ffmpegPath = ffmpegPath ?? Locate("ffmpeg.exe");
        _ffprobePath = ffprobePath ?? Locate("ffprobe.exe");
    }

    private static string Locate(string exeName)
    {
        var local = Path.Combine(AppContext.BaseDirectory, "ffmpeg", exeName);
        if (File.Exists(local)) return local;

        var rootLocal = Path.Combine(AppContext.BaseDirectory, exeName);
        if (File.Exists(rootLocal)) return rootLocal;

        // 退回 PATH，由操作系统解析
        return exeName;
    }

    public void Dispose() { }

    #region Probe

    public async Task<MediaInfo> ProbeAsync(string inputPath, CancellationToken ct = default)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("源文件不存在", inputPath);

        var args = $"-v error -print_format json -show_format -show_streams \"{inputPath}\"";
        var (exit, stdout, stderr) = await RunProcessAsync(_ffprobePath, args, ct).ConfigureAwait(false);
        if (exit != 0)
            throw new InvalidOperationException($"ffprobe 解析失败: {stderr}");

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        var format = root.GetProperty("format");
        var formatName = format.TryGetProperty("format_name", out var fn) ? fn.GetString() ?? "" : "";
        var durStr = format.TryGetProperty("duration", out var ds) ? ds.GetString() : null;
        var bitStr = format.TryGetProperty("bit_rate", out var bs) ? bs.GetString() : null;

        var duration = TimeSpan.Zero;
        if (double.TryParse(durStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var sec))
            duration = TimeSpan.FromSeconds(sec);

        long? bitRate = long.TryParse(bitStr, out var b) ? b : null;

        string? vCodec = null, aCodec = null;
        int? width = null, height = null;
        double? fps = null;

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var s in streams.EnumerateArray())
            {
                var type = s.TryGetProperty("codec_type", out var ct1) ? ct1.GetString() : null;
                var codec = s.TryGetProperty("codec_name", out var cn) ? cn.GetString() : null;
                if (type == "video" && vCodec is null)
                {
                    vCodec = codec;
                    if (s.TryGetProperty("width", out var w)) width = w.GetInt32();
                    if (s.TryGetProperty("height", out var h)) height = h.GetInt32();
                    if (s.TryGetProperty("avg_frame_rate", out var fr))
                    {
                        var frStr = fr.GetString();
                        if (!string.IsNullOrEmpty(frStr) && frStr.Contains('/'))
                        {
                            var parts = frStr.Split('/');
                            if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num) &&
                                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var den) &&
                                den != 0)
                            {
                                fps = num / den;
                            }
                        }
                    }
                }
                else if (type == "audio" && aCodec is null)
                {
                    aCodec = codec;
                }
            }
        }

        return new MediaInfo(inputPath, duration, formatName, vCodec, aCodec, width, height, fps, bitRate);
    }

    #endregion

    #region Cut single

    public async Task CutSegmentAsync(string inputPath, string outputPath, CutSegment segment,
        IProgress<string>? log = null, CancellationToken ct = default)
    {
        var args = BuildLosslessCutArgs(inputPath, outputPath, segment);
        log?.Report($"[FFmpeg] {_ffmpegPath} {args}");
        var (exit, _, stderr) = await RunProcessAsync(_ffmpegPath, args, ct, log).ConfigureAwait(false);
        if (exit != 0)
            throw new InvalidOperationException(ParseFFmpegError(stderr));
    }

    #endregion

    #region Cut multi - separate files

    public async Task<IReadOnlyList<string>> CutSegmentsAsync(string inputPath, string outputDirectory,
        string baseName, IReadOnlyList<CutSegment> segments,
        IProgress<string>? log = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var ext = Path.GetExtension(inputPath);
        var outputs = new List<string>(segments.Count);

        for (int i = 0; i < segments.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var seg = segments[i];
            var outPath = Path.Combine(outputDirectory, $"{baseName}_part{i + 1:D2}{ext}");
            await CutSegmentAsync(inputPath, outPath, seg, log, ct).ConfigureAwait(false);
            outputs.Add(outPath);
        }
        return outputs;
    }

    #endregion

    #region Cut multi - concat

    public async Task CutAndConcatAsync(string inputPath, string outputPath,
        IReadOnlyList<CutSegment> segments,
        IProgress<string>? log = null, CancellationToken ct = default)
    {
        if (segments.Count == 0) throw new ArgumentException("无可合并区间", nameof(segments));

        var tempDir = Path.Combine(Path.GetTempPath(), "lmc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var baseName = Path.GetFileNameWithoutExtension(inputPath);
            var parts = await CutSegmentsAsync(inputPath, tempDir, baseName, segments, log, ct).ConfigureAwait(false);

            // 生成 concat demuxer 列表
            var listFile = Path.Combine(tempDir, "concat.txt");
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                // concat demuxer 要求路径中的单引号转义
                var safe = p.Replace("'", "'\\''");
                sb.Append("file '").Append(safe).AppendLine("'");
            }
            await File.WriteAllTextAsync(listFile, sb.ToString(), ct).ConfigureAwait(false);

            var args = $"-y -hide_banner -f concat -safe 0 -i \"{listFile}\" -c copy -map 0 -avoid_negative_ts make_zero \"{outputPath}\"";
            log?.Report($"[FFmpeg] {_ffmpegPath} {args}");
            var (exit, _, stderr) = await RunProcessAsync(_ffmpegPath, args, ct, log).ConfigureAwait(false);
            if (exit != 0)
                throw new InvalidOperationException(ParseFFmpegError(stderr));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* 忽略清理失败 */ }
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// 构建无损截取命令。
    /// 关键策略：
    ///   -ss 放在 -i 之前 → 使用快速 seek（基于 keyframe 索引），速度极快；
    ///   配合 -c copy 实现流复制；
    ///   -avoid_negative_ts make_zero 修正起始时间戳；
    ///   -map 0 保留所有原始流（视频/音频/字幕）。
    /// 注意：流复制模式只能在关键帧切割，因此实际起点可能略早于设定值，
    /// 这是 FFmpeg 的物理限制（要逐帧精确必须重编码）。
    /// </summary>
    private static string BuildLosslessCutArgs(string inputPath, string outputPath, CutSegment segment)
    {
        var ss = ToFFmpegTimestamp(segment.Start);
        var to = ToFFmpegTimestamp(segment.Duration); // 使用 -t 比 -to 在 fast-seek 下更稳定

        return $"-y -hide_banner -ss {ss} -i \"{inputPath}\" -t {to} " +
               $"-map 0 -c copy -avoid_negative_ts make_zero \"{outputPath}\"";
    }

    /// <summary>毫秒精度转 FFmpeg 时间戳 HH:MM:SS.mmm（不变文化避免逗号小数点问题）。</summary>
    public static string ToFFmpegTimestamp(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return string.Format(CultureInfo.InvariantCulture,
            "{0:00}:{1:00}:{2:00}.{3:000}",
            (int)t.TotalHours, t.Minutes, t.Seconds, t.Milliseconds);
    }

    private static string ParseFFmpegError(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr)) return "FFmpeg 执行失败（无 stderr 输出）";

        var lower = stderr.ToLowerInvariant();
        if (lower.Contains("permission denied"))
            return "权限不足：请检查输出路径是否有写入权限，或文件是否被其他程序占用。";
        if (lower.Contains("no such file or directory"))
            return "找不到文件：请确认输入路径正确，并避免使用网络断开的盘符。";
        if (lower.Contains("invalid data found"))
            return "输入文件已损坏或格式不被支持。";
        if (lower.Contains("codec not currently supported in container"))
            return "原始编码与目标容器不兼容，无法以 -c copy 直接保存，请改换输出容器（如 .mkv）。";
        if (lower.Contains("could not find tag for codec"))
            return "目标容器不支持该编码（建议保持与源文件同后缀）。";

        // 截取 stderr 的最后若干行作为友好提示
        var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var tail = string.Join('\n', lines[Math.Max(0, lines.Length - 6)..]);
        return $"FFmpeg 执行失败：\n{tail.Trim()}";
    }

    private static async Task<(int exitCode, string stdout, string stderr)> RunProcessAsync(
        string fileName, string arguments, CancellationToken ct, IProgress<string>? log = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdoutSb = new StringBuilder();
        var stderrSb = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stdoutSb.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stderrSb.AppendLine(e.Data);
            log?.Report(e.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException($"无法启动进程: {fileName}");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using (ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }))
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }

        return (process.ExitCode, stdoutSb.ToString(), stderrSb.ToString());
    }

    #endregion
}
