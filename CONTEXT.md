# CONTEXT.md — 领域事实与硬约束

WatchMe：屏幕顶部胶囊热区 + 下拉便签板的 Windows 桌面工具（WPF + .NET 8）。前身是 oil-oil 的 macOS 应用 NotchNotes 的 fork，2026-09-19 用 WPF 全面重写为 Windows 原生应用；同日按用户指令进一步聚焦为**便签形态（v2.0）**：界面参考「好用便签」（闭源，官网 haoyong333.com，未开源，仅参考其交互形态），复刻其基本功能——快速添加、标签分类、待办勾选。Markdown 编辑器、文件暂存架、剪贴板历史、Quick Look、防休眠（咖啡模式）等模块已在 v2.0 移除（代码见 git 历史），托盘菜单只剩 显示/新建便签/设置/退出。

## 产品概念 → Windows 实现

| 功能 | 实现 |
|---|---|
| 顶部热区 | `NotchCapsuleWindow`（顶部中央 150×14 胶囊，`WS_EX_NOACTIVATE`，悬停/点击触发） |
| 下拉便签板 | `NotchPanelWindow`（420×600，Root Border 高度动画 0→600） |
| 便签板 | `StickyBoardView`：快速添加框 + 标签筛选 chips + 彩色卡片列表（标签色轮换 pastel 色板） |
| 便签数据 | `NoteStore`（notes.json **v2**：id/text/tag/createdAt/updatedAt/done + tags 列表；兼容读取 v1 的 savedAt） |
| 标签 | 内置 默认/工作/生活，新标签随用随建；`NormalizeTag` 空白回落"默认" |
| 待办 | 卡片圆圈勾选 `IsDone`，文字划线 |
| 全局快捷键 | `RegisterHotKey`（默认 Ctrl+Alt+W，`MessageWindow` HWND_MESSAGE=-3 接收；被占用时托盘气泡提示） |
| 开机自启 | HKCU `...\CurrentVersion\Run`（仅设置保存时写注册表，启动路径绝不碰，防误删手动配置） |
| 主题 | Dark/Light 双字典 `ThemeManager` 融合切换 |

## 实测环境事实（2026-09-19，本机 2560×1600 @150%）

- **悬浮窗定位必须走物理像素**：`ScreenLocator` 枚举显示器（物理 RECT）+ `GetDpiForWindow` 换算窗口宽，`SetWindowPos(SWP_NOSIZE)` 置顶居中。窗口未布局时 `GetWindowRect` 返回 0×0，不能作为尺寸来源。
- **DPI 非感知进程的截屏是物理裁剪不是缩放**：调试截屏时左上角 1707×1067 只是屏幕的左 2/3，勿据此判断 UI 位置；以 `GetWindowRect` 数值为准。
- **JsonFile 必须 `PropertyNameCaseInsensitive=true`**：曾因大小写敏感，手写的 settings.json（PowerShell ConvertTo-Json 输出小写键）被静默忽略回落默认——热键"看起来改了却没生效"的根因。
- **面板与子视图共享同一 Store 实例**：曾出现面板和便签板各自 new NoteStore 的双实例 bug，数据互不相通；现在便签板持有 Store，面板经 `Board.Store` 访问。
- `HWND_MESSAGE` 是 `-3`；P/Invoke 用经典 `DllImport`（`LibraryImport` 需要 unsafe 且对含 string 的结构体支持差）。
- 悬浮窗（胶囊）必须保持 `WS_EX_NOACTIVATE`；面板可激活（否则无法输入），但悬停展开路径不调用 Activate。
- **合成键盘无法触发系统热键**（Windows 设计）：SendKeys 对 RegisterHotKey 的键无效；热键只能人工验证。合成键对普通快捷键（Ctrl+N/Esc/Ctrl+V）有效，但会被搜狗等 IME 拦截字母——UI 注入优先走剪贴板粘贴或 UIA ValuePattern。
- **用户活跃使用的机器上鼠标注入不可靠**：物理光标会被真实操作抢占；PostMessage 合成点击不进 WPF 输入路由。交互 E2E 用 UIA（`ValuePattern.SetValue` + `SetFocus` 可靠）。
- PowerShell 5.1 的 `Add-Type` 只支持 C#5（无字符串插值/`out var`）；EnumWindows 闭包内 `[ref]` struct 回写丢字段——窗口枚举逻辑放 C# 帮助类。
- 无 BOM 的 UTF-8 ps1 会被 PowerShell 5.1 按 GBK 读取，中文字符串会炸引号——自动化脚本避免中文或加 BOM。

## 持久化布局

```
%APPDATA%\WatchMe\
├─ notes.json     便签（NoteFile v2：notes + tags）
├─ settings.json  设置（AppSettings：trigger/theme/hotkey/screen/autostart）
└─ crash.log      启动/未处理异常日志
```

## 已验证行为（2026-09-19 v2.0 实机）

启动无崩溃 → 胶囊物理居中 → 悬停展开便签板（添加框/chips/卡片就位）→ UIA 注入添加便签 → notes.json 落盘（v2，含 tags）→ 鼠标离开 800ms 自动收起 → 胶囊复现。旧 v1 notes.json 与含多余字段的 settings.json 均兼容加载（有单测）。24/24 单测通过，构建零警告。
