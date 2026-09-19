# WatchMe 项目规则

原生 Windows 桌面应用：顶部胶囊热区 + 下拉**便签板**（快速添加、标签分类、待办勾选）。WPF (.NET 8) + C# 12，xUnit 测试。界面形态参考「好用便签」。

## 构建与验证（改动后必跑）

```powershell
dotnet build WatchMe.sln          # 零警告零错误才算过
dotnet test WatchMe.sln           # 测试全绿才算过
dotnet run --project src/WatchMe  # 实机启动（托盘 + 顶部胶囊）
```

UI/交互改动必须实机运行验证：胶囊悬停展开、Esc/失焦收起、便签添加落盘（`%APPDATA%\WatchMe\notes.json`）、标签筛选。

## 结构约定

- `src/WatchMe/` 主应用。分层：`Core/`（纯逻辑）、`Notch/`（胶囊+面板+控制器）、`Notes/`（便签模型+便签板 UI）、`Shell/`（托盘/热键/自启）、`Interop/`（Win32 P/Invoke）、`Settings/`、`Themes/`。
- `src/WatchMe.Tests/` xUnit。**所有业务逻辑必须可无 GUI 测试**：外部依赖走接口注入。
- 新领域事实写 `CONTEXT.md`；架构决策写 `docs/adr/`。
- 产品边界：**只做便签 MVP**（添加/编辑/删除/标签/待办/主题/热键/自启）。Markdown 编辑、暂存架、剪贴板历史、防休眠等已在 v2.0 剔除，不要以"补功能"名义加回；新功能先过用户。

## 硬约束

- **Win32 坐标一律物理像素**，与 WPF DIP 之间只允许通过 `GetDpiForWindow` 换算（见 `ScreenLocator`）。禁止用未布局窗口的 `GetWindowRect` 推导位置（读数为 0）。
- `HWND_MESSAGE` 是 `-3`；P/Invoke 用经典 `DllImport`。
- 胶囊窗必须保持 `WS_EX_NOACTIVATE`；面板可激活（要输入），悬停展开路径不得 Activate。
- 便签板的 `NoteStore` 是唯一实例（面板经 `Board.Store` 访问），禁止再 new 第二个。
- `JsonFile` 保持 `PropertyNameCaseInsensitive=true`（外部工具写的小写键 settings.json 必须能读）。
- 崩溃日志统一写 `%APPDATA%\WatchMe\crash.log`（`App.LogCrash`），启动期代码必须包在 `StartupCore` 的 try/catch 里。
- 启动路径（`ApplySettings(save:false)`）绝不写注册表，防误删用户手动配置的自启。
- 品牌：应用名 `WatchMe`，bundle id `dev.threetwoa.WatchMe`，作者 threetwoa。

## temp/ 本地工作区（Git 忽略）

实验脚本、截图、调研产物一律放 `temp/`；凭据出现时立即写入 `temp/secrets/`（不询问、不回显、不二次索要）。`.codegraph/` 为本地索引，已忽略；Stop hook 自动增量同步，无需手动维护。
