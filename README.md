# TrayBright — 夜间模式色温调节（托盘工具）

Windows 10/11 任务栏托盘**色温（夜间模式）调节工具**：左键点击托盘图标弹出滑块面板，屏幕变为暖色护眼模式，类似 f.lux / Windows 夜灯，但可自由拖动任意色温。支持色温记忆（下次开机自动恢复）、托盘滚轮微调、开机自启、平滑过渡。

## 功能

- **托盘常驻**：左键点击图标弹出深色滑块面板（仿 Twinkle Tray 风格），点击面板外或按 Esc 关闭
- **强度滑块**：0-100，**数字越小色温越强**（0 = 1700K 最暖，100 = 6500K 关闭/原色）
- **预设按钮**：烛光 4% / 日落 35% / 舒适 58% / 关闭 100% 一键切换
- **软件开关**：右键菜单"启用夜间模式"可临时开关（恢复原色），不退出程序
- **记忆功能**：每次调节自动保存，下次开机自动恢复上次色温
- **托盘滚轮**：鼠标悬停托盘图标上滚动滚轮，强度 ±2/格 微调
- **平滑过渡**：色温渐变过渡（默认 250ms，可在设置文件调整）
- **多屏幕**：所有显示器同步应用
- **退出还原**：退出程序自动恢复系统原色
- **开机自启**：右键菜单一键开关（写入 HKCU 注册表 Run 键，无需管理员权限）
- **单文件 exe**：自包含 .NET 运行时，无任何安装依赖

## 下载 exe（从 Releases）

1. 打开 **Releases** 页面：https://github.com/linglanxv/TrayBright/releases
2. 下载最新版 `TrayBright.exe`，双击运行，托盘出现月亮图标

> 版本发布方式：推送 `v*` 标签（如 `v1.0.0`）时，Actions 会自动构建并创建 Release 附加 exe。

## 如何云端编辑代码

- **网页编辑**：在 GitHub 仓库中直接点开任意文件，点右上角铅笔图标即可编辑并提交，推送后 Actions 自动重新构建
- **Codespaces**：在仓库页面按 <kbd>.</kbd>（句号键）打开浏览器版 VS Code，内置 .NET SDK
- **发布新版本**：`git tag v1.x.x && git push --tags`，或在网页端创建 Release/标签

## 本地开发（可选）

需要 .NET 8 SDK：

```powershell
dotnet publish TrayBright/TrayBright.csproj -c Release -r win-x64 -o publish
.\publish\TrayBright.exe
```

## 设置文件

位置：`%AppData%\TrayBright\settings.json`

```json
{
  "RestoreOnStartup": true,
  "StartWithWindows": false,
  "FadeMilliseconds": 250,
  "Strength": 35
}
```

## 技术说明

色温通过 Win32 `SetDeviceGammaRamp`（显卡 Gamma Ramp）实现，为软件级调整，与 f.lux 原理相同，对任何显示器都有效（无需 DDC/CI 支持）。开尔文转 RGB 使用 Tanner Helland 近似算法。

## 目录结构

```
TrayBright/
├── TrayBright.csproj      # 项目配置（单文件发布）
├── Program.cs             # 入口 + 单实例互斥
├── TrayAppContext.cs      # 托盘图标、菜单、记忆/恢复、滚轮、平滑过渡
├── PopupForm.cs           # 弹出式色温面板 UI（滑块 + 预设）
├── SmoothSlider.cs        # 自绘平滑滑块控件（支持渐变轨道）
├── ColorTemperature.cs    # 色温控制（Gamma Ramp + 开尔文转 RGB）
└── Settings.cs            # 设置持久化（记忆功能）
```
