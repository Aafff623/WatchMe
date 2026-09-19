# WatchMe 项目规则

原生 Windows 桌面应用：顶部胶囊热区 + 下拉面板，集成多标签 Markdown 笔记、文件暂存架、防休眠、剪贴板历史。WPF (.NET 8) + C# 12，xUnit 测试。

## 构建与验证（改动后必跑）

```powershell
dotnet build WatchMe.sln          # 零警告零错误才算过
dotnet test WatchMe.sln           # 47 个测试全绿才算过
dotnet run --project src/WatchMe  # 实机启动（托盘 + 顶部胶囊）
```

UI/交互改动必须实机运行验证：胶囊悬停展开、Esc/失焦收起、托盘菜单、笔记保存（`%APPDATA%\WatchMe\notes.json`）。

## 结构约定

- `src/WatchMe/` 主应用。分层：`Core/`（纯逻辑）、`Notch/`（胶囊+面板+控制器）、`Notes/`、`Shelf/`、`Clipboard/`、`KeepAwake/`、`Shell/`（托盘/热键/自启）、`Interop/`（Win32 P/Invoke）、`Settings/`、`Themes/`。
- `src/WatchMe.Tests/` xUnit。**所有业务逻辑必须可无 GUI 测试**：进程启动、时间等外部依赖走接口注入（参照 `KeepAwakeControllerTests` 的 Fake 模式）。
- 新领域事实写 `CONTEXT.md`；架构决策写 `docs/adr/`。

## 硬约束

- **Win32 坐标一律物理像素**，与 WPF DIP 之间只允许通过 `GetDpiForWindow` 换算（见 `ScreenLocator`）。禁止用未布局窗口的 `GetWindowRect` 推导位置（读数为 0）。
- `HWND_MESSAGE` 是 `-3`；P/Invoke 用经典 `DllImport`（`LibraryImport` 源生成器需要 unsafe 且对含 string 的结构体支持差）。
- 悬浮窗（胶囊/面板）必须保持 `WS_EX_NOACTIVATE`，绝不能在悬停时抢键盘焦点。
- 崩溃日志统一写 `%APPDATA%\WatchMe\crash.log`（`App.LogCrash`），新增启动期代码必须包在 `StartupCore` 的 try/catch 里。
- 品牌：应用名 `WatchMe`，bundle id `dev.threetwoa.WatchMe`，作者 threetwoa。

## temp/ 本地工作区（Git 忽略）

实验脚本、截图、调研产物一律放 `temp/`；凭据出现时立即写入 `temp/secrets/`（不询问、不回显、不二次索要）。`.codegraph/` 为本地索引，已忽略；Stop hook 自动增量同步，无需手动维护。
