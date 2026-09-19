# ADR-0001：放弃 macOS Swift 代码，以 WPF (.NET 8) 全面重写为 Windows 原生应用

日期：2026-09-19 · 状态：已接受

## 背景

本仓库原为 oil-oil/NotchNotes（macOS 刘海笔记应用，Swift 6 纯 SPM，约 4700 行）的 fork，remote 名为 Aafff623/WatchMe 但代码内无 WatchMe 字样，品牌（bundle id `io.github.oiloil.*`、8 处 oil 链接、NotchNotes 命名）与仓库定位完全脱节。上游根目录无 LICENSE。

## 决策

1. **删除全部 Swift 代码**（Sources/Tests/Vendor/Resources/Scripts/docs/Package*.swift/release.yml），git 历史保留可随时回溯。
2. **以 WPF + .NET 8 重写**为 Windows 原生应用，功能对标原版并扩展（剪贴板历史、开机自启、多显示器选择、主题跟随系统）。
3. **品牌迁移**：应用名 WatchMe，bundle id `dev.threetwoa.WatchMe`，根 LICENSE 为 MIT (Copyright 2026 threetwoa)；README 保留一行 "Inspired by NotchNotes" 致谢。

## 理由

- 用户明确要求"基本脱离 fork，做 Windows 版本"。
- 上游无根 LICENSE：保留 Swift 代码再改牌存在许可含糊；全面重写为全新 C# 代码无此包袱。
- 原版核心体验（无边框置顶不抢焦点悬浮窗、全局拖拽、Quick Look、托盘、防休眠）在 Windows 上由 Win32/WPF 成熟机制一一对应（见 CONTEXT.md 映射表），WPF 的 P/Invoke 生态最顺。

## 后果

- 优点：单一平台单一栈，无跨平台兼容层；许可清晰；功能可比原版更 Windows 原生。
- 代价：放弃 macOS 用户；上游更新无法直接合并（产品概念层面的借鉴仍可参考 git 历史）。
- 保留约束：悬浮窗不抢焦点、暂存架只存路径引用、防休眠退出必恢复——这三条是原版经过验证的行为契约，移植时不得破坏。
