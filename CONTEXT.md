# CONTEXT.md — 领域事实与硬约束

WatchMe：屏幕顶部胶囊热区 + 下拉面板的 Windows 桌面工具。前身为 oil-oil 的 macOS 应用 NotchNotes（Swift/NSPanel），本仓库为其 Windows 原生重写（WPF + .NET 8），与上游代码零共享，仅保留产品概念。

## 产品概念 → Windows 实现（移植映射表）

| 功能 | macOS 原实现 | 本仓库实现 |
|---|---|---|
| 顶部热区 | NSPanel + safeAreaInsets 刘海检测 | `NotchCapsuleWindow`（顶部中央 150×14 胶囊，`WS_EX_NOACTIVATE`） |
| 下拉面板 | NSPanel drawer + 动画 | `NotchPanelWindow`（Root Border 高度动画 0→620） |
| 全局鼠标监听 | NSEvent global monitor + 30Hz 轮询 | WPF 原生 `MouseEnter/Leave`（胶囊自身即可，无需钩子） |
| 多标签笔记 | NoteStore（UserDefaults+JSON 双写） | `NoteStore`（`%APPDATA%\WatchMe\notes.json`，原子写，删除可撤销，永保 ≥1 条） |
| Markdown 渲染 | Vendored MarkdownEngine (TextKit2) | MdXaml `MarkdownScrollViewer`（属性名是 `HereMarkdown`，不是 Markdown/PlainMarkdown）+ AvalonEdit 编辑器 |
| 图片粘贴 | LocalImageStore | `NoteImageStore`（Images/ 目录，`![..](正斜杠路径)`） |
| 文件暂存架 | FileShelfStore（路径引用、≤100） | `FileShelfStore`（shelf.json，去重不区分大小写，挤掉最旧） |
| Quick Look | QLPreviewPanel + QLThumbnailGenerator | `QuickLookWindow`（图片/文本≤128KB/MediaElement）+ `ShellThumbnailProvider`（IShellItemImageFactory） |
| 防休眠 | caffeinate 子进程 / pmset disablesleep | `SetThreadExecutionState` / `powercfg LIDACTION 0`（UAC 提权，状态文件崩溃恢复） |
| 菜单栏 | NSStatusItem | `TrayIconService`（Hardcodet NotifyIcon） |
| 全局快捷键 | NSEvent 键盘监听 | `RegisterHotKey`（默认 Ctrl+Alt+W，`MessageWindow` HWND_MESSAGE=-3 接收） |
| 剪贴板历史（新增） | — | `AddClipboardFormatListener` + `ClipboardHistoryStore`（连续重复抑制、图片按 SHA256 去重落盘） |
| 开机自启（新增） | — | HKCU `...\CurrentVersion\Run`，无需管理员 |

## 实测环境事实（2026-09-19，本机 2560×1600 @150%）

- **悬浮窗定位必须走物理像素**：`ScreenLocator` 枚举显示器（物理 RECT）+ `GetDpiForWindow` 换算窗口宽，`SetWindowPos(SWP_NOSIZE)` 置顶居中。窗口未布局时 `GetWindowRect` 返回 0×0，不能作为尺寸来源。
- **DPI 非感知进程的截屏是物理裁剪不是缩放**：调试截屏时左上角 1707×1067 只是屏幕的左 2/3，勿据此判断 UI 位置；以 `GetWindowRect` 数值为准。
- 本进程 system-DPI aware（`GetDpiForWindow`=144）；WPF `Left/Top` 是 DIP，与 Win32 坐标混用会错位。
- AvalonEdit 的 `TextArea.Background` 独立于外层 `TextEditor.Background`，主题化要两处都设。
- MdXaml 1.27 的 WPF 控件叫 `MarkdownScrollViewer`，命名空间 `clr-namespace:MdXaml;assembly=MdXaml`。
- WPF 模板项目 `dotnet new xunit` 默认 net10.0，与 net8.0-windows 主项目引用不兼容，需手改 TargetFramework。
- .NET 10 SDK 的 `dotnet new sln` 生成 `.slnx`（新版格式），CI 用 .NET 8 SDK 读不了；本仓库手写经典 `.sln`。
- `LibraryImport` 源生成器要求 `/unsafe` 且不支持 `CharSet` 结构体封送 → 统一用经典 `DllImport`。

## 持久化布局

```
%APPDATA%\WatchMe\
├─ notes.json        笔记（NoteFile v1）
├─ shelf.json        暂存架（ShelfFile v1，只存路径）
├─ settings.json     设置（AppSettings）
├─ clipboard\        剪贴板历史（entries.json + images/）
├─ Images\           笔记内嵌图片
├─ state\            防休眠状态文件（lid-never-sleep.active）
└─ crash.log         启动/未处理异常日志
```

## 已验证行为（2026-09-19 实机）

启动无崩溃 → 胶囊物理居中 (723,0)-(983,28) 虚拟 rect → 悬停展开面板（tab/工具栏/编辑/预览/暂存架就位）→ 鼠标离开 800ms 后自动收起 → 胶囊复现。47/47 单测通过。

## 深度 review + E2E 结论（2026-09-19 第二轮，强劲模型）

**修复的 P0**：剪贴板"插入笔记"会把整篇笔记复制一遍再越界崩溃（OnInsertToNote 传参错误 + Select 未钳制）；设置窗口因 ProbeHotkey 撞自己已注册的热键导致任何设置都保存失败（现仅热键变更时探测）；主题下拉映射颠倒（enum 顺序 0跟随/1浅/2深）。

**修复的 P1/P2**：剪贴板历史改为捕获即落盘（原来仅干净退出才保存，崩溃全丢）；上限变更时重建 store；缩略图缓存改 ConcurrentDictionary；启动路径不再写注册表（防误删用户手动自启）；powercfg 超时 Kill 的 Win32Exception；热键注册失败托盘气泡提示；粘贴图片包 COMException；启动期 UAC 恢复移后台线程；Activate() 失败时 ALT 键兜底抢前台。

**E2E 实证记录**：
- 本机 Ctrl+Alt+W 被输入法占用（RegisterHotKey err=1409）——热键占用是真实环境常态，气泡提示必要。
- SendKeys 合成按键**无法触发**系统热键（Windows 设计），热键功能只能人工验证；合成键对普通快捷键（Ctrl+N/Esc/Ctrl+V）有效。
- PowerShell EnumWindows 闭包内 `[ref]` struct 回写丢字段（B 恒 0）——窗口枚举必须放 C# 帮助类。
- 用户活跃使用的机器上鼠标注入不可靠（物理光标被抢占）；PostMessage 合成点击不进 WPF 输入路由。交互 E2E 需要空闲机器或 UIA 驱动，注入法到此为止。
- 遗留未自动化验证（需人工）：点击胶囊展开+打字保存、设置窗、Quick Look、托盘菜单。逻辑层由 44 个单测覆盖。
