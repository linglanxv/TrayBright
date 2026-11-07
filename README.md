# TrayBright — 托盘亮度滑块（仿 Twinkle Tray）

Windows 10/11 任务栏托盘亮度调节工具：左键点击托盘图标弹出深色滑块面板，支持多显示器、平滑过渡、**亮度记忆**（下次开机自动恢复到最后位置）、开机自启。

## 功能

- **托盘常驻**：左键点击图标弹出滑块面板，点击面板外或按 Esc 关闭
- **多显示器支持**：外接显示器走 DDC/CI（dxva2），笔记本内置屏幕走 WMI
- **记忆功能**：每次调节自动保存，右键菜单勾选"开机恢复上次亮度"后，下次开机自动恢复
- **平滑过渡**：亮度渐变过渡（默认 250ms，可在设置文件调整）
- **开机自启**：右键菜单一键开关（写入 HKCU 注册表 Run 键，无需管理员权限）
- **单文件 exe**：约 60-70MB（自包含 .NET 运行时），无任何安装依赖

## 如何获得 exe（无需本地开发环境）

项目使用 GitHub Actions 云端自动构建：

1. 在 GitHub 新建仓库（如 `TrayBright`）
2. 把本目录所有文件上传到仓库（网页端拖拽上传，或用 GitHub Desktop）
3. 打开仓库 **Actions** 标签页，等待 `Build TrayBright` 工作流跑完（约 2-3 分钟）
4. 在该次运行详情页底部 **Artifacts** 下载 `TrayBright-win-x64`，解压即得 `TrayBright.exe`
5. （可选）发布 Release 时会自动把 exe 附加到 Release 页面

## 如何云端编辑代码

- **网页编辑**：在 GitHub 仓库中直接点开任意文件，点右上角铅笔图标即可编辑并提交
- **Codespaces**：在仓库页面按 <kbd>.</kbd>（句号键）或 Code → Codespaces，打开浏览器版 VS Code，内置 .NET SDK，可直接在终端执行 `dotnet build` 验证

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
  "Remembered": { "ddc:DELL U2723QE": 40 }
}
```

## 常见问题

- **滑块无效？** 部分显示器需在 OSD 菜单中开启 DDC/CI 选项
- **双击 exe 出现两个托盘图标？** 程序有单实例互斥锁，重复启动会自动退出第二个实例
- **杀毒软件误报？** 单文件自包含 exe 常见误报，可在代码签名后发布

## 目录结构

```
TrayBright/
├── TrayBright.csproj      # 项目配置（单文件发布）
├── Program.cs             # 入口 + 单实例互斥
├── TrayAppContext.cs      # 托盘图标、菜单、记忆/恢复、平滑过渡
├── PopupForm.cs           # 弹出式亮度面板 UI
├── SmoothSlider.cs        # 自绘平滑滑块控件
├── MonitorManager.cs      # 显示器枚举与亮度控制（DDC/CI + WMI）
└── Settings.cs            # 设置持久化（记忆功能）
```
