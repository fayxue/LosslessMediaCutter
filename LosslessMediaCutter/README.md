# LosslessMediaCutter — 无损音视频截取工具

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)]()
[![Framework](https://img.shields.io/badge/.NET-8.0-purple.svg)]()

> 基于 **FFmpeg** 无损截取 + **LibVLCSharp** 实时预览的 Windows 桌面工具。  
> 支持多段标记、批量导出，截取过程不重新编码，保留原始画质与音质。

---

## ✨ 功能特性

| 功能 | 说明 |
|------|------|
| 📂 打开媒体 | 支持主流视频/音频格式（mp4、mkv、mov、mp3、flac 等） |
| ▶ 实时预览 | 内嵌 LibVLC 播放器，所见即所得 |
| ⏴⏵ 精确标记 | 拖拽时间轴或精确定位，标记片段起止点 |
| 📋 多段管理 | 可添加任意数量的截取片段，支持选中删除 |
| 💾 无损保存 | 调用 FFmpeg `-c copy` 模式，零质量损失 |
| 🔀 合并导出 | 多段可选择合并为单文件或分别保存 |
| 🎨 现代 UI | 深色风格 + 彩色语义按钮 + 自定义时间轴控件 |

---

## 🚀 快速开始

### 环境要求

- Windows 10 / 11（x64）
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- FFmpeg（`ffmpeg.exe` / `ffprobe.exe`）

### 配置 FFmpeg

将 `ffmpeg.exe` 和 `ffprobe.exe` 放入以下任一位置（按优先级）：

```
方式一：放到程序目录下的 ffmpeg\ 子目录（推荐）
  LosslessMediaCutter.exe
  ffmpeg\
    ffmpeg.exe
    ffprobe.exe

方式二：加入系统 PATH 环境变量
```

> 推荐从 [gyan.dev](https://www.gyan.dev/ffmpeg/builds/) 或
> [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases)
> 下载 Windows x64 静态构建版（essentials 即可）。

### 编译运行

```bash
git clone https://github.com/fredxue/LosslessMediaCutter.git
cd LosslessMediaCutter
dotnet build -c Debug
dotnet run --project LosslessMediaCutter
```

---

## 📖 使用说明

1. **打开文件** — 点击「📂 打开」选择音视频文件，播放器自动加载并显示时间轴。
2. **预览定位** — 点击时间轴任意位置跳转，或使用播放/暂停控制进度。
3. **标记片段**
   - 将进度移到目标位置，点击「⏴ 设为开始」。
   - 继续移到结束位置，点击「⏵ 设为结束」。
   - 点击「＋ 添加到列表」将该片段加入列表；时间轴高亮显示。
4. **管理片段** — 在列表中选中片段可高亮预览，点击「✖ 删除选中」移除。
5. **保存输出** — 点击「💾 保存」，多段时选择「合并为一个文件」或「分别保存」。
6. **取消任务** — 导出进行中可点击「⏹ 取消任务」中止。

---

## 🏗️ 技术栈

| 组件 | 版本 | 用途 |
|------|------|------|
| .NET 8 WinForms | 8.0 | 桌面 UI 框架 |
| LibVLCSharp.WinForms | 3.8.5 | 媒体播放器 |
| VideoLAN.LibVLC.Windows | 3.0.20 | VLC 本地库 |
| FFmpeg（外部） | ≥ 5.x | 无损截取与合并 |

### 项目结构

```
LosslessMediaCutter/
├── Models/
│   ├── CutSegment.cs           # 截取片段数据模型
│   └── MediaInfo.cs            # 媒体元数据（ffprobe 解析结果）
├── Services/
│   ├── IMediaPlayerService.cs  # 播放器服务接口
│   ├── VlcMediaPlayerService.cs
│   ├── IFFmpegService.cs       # FFmpeg 服务接口
│   ├── FFmpegService.cs        # ffmpeg/ffprobe 进程封装
│   ├── ICutSegmentManager.cs
│   └── CutSegmentManager.cs    # 片段列表管理
├── Controls/
│   ├── TimelineControl.cs      # 自定义时间轴 GDI+ 控件
│   └── UiKit.cs                # UI 辅助工具
├── Dialogs/
│   └── SaveModeDialog.cs       # 保存模式选择对话框
├── MainForm.cs                 # 主窗口
└── Program.cs                  # 入口 和 服务组合
```

---

## 🤝 参与贡献

欢迎提交 Issue 反馈问题，或 Fork 后发起 Pull Request：

1. Fork 本仓库
2. 创建特性分支：`git checkout -b feature/your-feature`
3. 提交变更：`git commit -m "feat: 添加新功能"`
4. 推送分支：`git push origin feature/your-feature`
5. 发起 Pull Request

---

## 📄 开源协议

本项目采用 [MIT License](LICENSE) 开源。  
FFmpeg 遵循 LGPL/GPL 协议，LibVLC 遵循 LGPL 协议，请在分发时注意合规。

---

## 🙏 致谢

- [FFmpeg](https://ffmpeg.org/) — 强大的音视频处理工具链
- [LibVLCSharp](https://github.com/videolan/libvlcsharp) — VLC 的 .NET 绑定
- [VideoLAN / VLC](https://www.videolan.org/) — 跨平台媒体播放器