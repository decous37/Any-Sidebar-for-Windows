# Any Sidebar for Windows

一个轻量的 Windows AI 侧边栏，让对话留在正在阅读的文档旁。

沿用 [Mac 版](https://github.com/decous37/Any-Sidebar-for-Mac) 的界面：划选文字、手动截取代码或图表，把资料留在引用区，再直接问 AI。按阅读任务保存会话，不因切换文件换对话。

当前是 **0.1.1 开发预览版**，提供 x64、ARM64 下载包。本次修复截图显示和记事本文档划词，已通过 Windows 针对性检查，详见[验证记录](docs/validation-0.1.1.md)。ARM64 实际运行、多屏缩放和真实模型效果仍待验证。

## 下载与开始

1. 在 [下载页](https://github.com/decous37/Any-Sidebar-for-Windows/releases/tag/v0.1.1)选择 `win-x64.zip`；使用 ARM Windows 的设备选择 `win-arm64.zip`。
2. 完整解压后运行 `AnySidebarWindows.exe`。需要 [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)；包内已包含 .NET 运行时。
3. 在设置中保存自己的 DeepSeek API Key，即可开始对话。自动划词需先确认采集说明，再开启。

这是未签名的预览版，Windows 可能显示未知发布者提示。可用下载页的 `SHA256SUMS.txt` 核对文件；不需要关闭系统安全保护。可选运行包内 `Install.ps1`，添加开始菜单入口。

## 能做什么

- **划词引用**：从支持 Windows UI Automation 的阅读区域接收选文，默认仅最新，也可累积。不抢焦点、不自动发送。
- **手动读屏**：框选区域或选择窗口，截图先进入草稿；可配问题，也可以只发图片。
- **连续讨论**：新建和切换会话，引用 AI 回答，复制、追问或重新生成。
- **轻量侧栏**：亮暗主题、四档字号、模型与思考菜单、一键到底。桌面入口和系统托盘可展开、收起及退出。

自备 [DeepSeek API Key](https://platform.deepseek.com/)。软件免费，无账号、会员和每日次数限制；模型费用由你的 DeepSeek 账户承担。

## 平台与快捷键

面向 **Windows 10 22H2+、Windows 11，x64 和 ARM64**。使用原生 WPF 窗口和 WebView2 展示前端，不需要本地 HTTP 服务。

| 默认快捷键 | 作用 |
| --- | --- |
| **Ctrl+Alt+A** | 框选读屏，再按一次取消 |
| **Ctrl+Alt+S** | 展开／收起侧边栏 |

两组均可录入修改或清除。与其他软件冲突时会提示，保留原组合。

划词依赖阅读软件提供选区，并非每个 PDF 阅读器或编辑器都支持。支持记事本文档选区；密码和普通表单输入不采集；受保护、高权限或安全桌面窗口可能无法读取，应用不会要求常驻管理员权限。

## 从源码构建

需要 Node.js 24.15+（24.x）或26.x、npm、.NET 10 SDK，以及用于图标构建的 Chromium。正常运行还需要 [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)。

```sh
git clone https://github.com/decous37/Any-Sidebar-for-Windows.git
cd Any-Sidebar-for-Windows
npm ci
npx playwright install chromium
npm run build
npm run publish:windows
npm run package
```

发布目录在 `build/publish/win-x64` 和 `build/publish/win-arm64`；ZIP 在 `release/`。发布包携带 .NET 运行时，需完整解压并运行 `AnySidebarWindows.exe`，不能只复制这个 exe。

包内 `Install.ps1` 可复制到当前用户应用目录并建立开始菜单入口，不清空会话和 Key、不要求管理员权限、不修改系统执行策略。脚本受系统限制时可直接运行完整解压目录中的应用。

Mac 可执行前端和领域检查、交叉编译 Windows 目标，但不能运行 WPF 系统界面。原生窗口、划词、截图和热键必须在 Windows 验证，不能以模拟测试代替。

[开发说明](docs/development.md) · [使用指南](docs/user-guide.md)

## 资料与隐私

选文、截图、对话和偏好先保存在本机。发送、重试或重新生成时，相关资料及历史才直接提交到 DeepSeek 官方 API。没有项目方中转、遥测或云同步。

Key 保存在当前用户的 **Windows 凭据管理器**。会话和截图位于 `%LOCALAPPDATA%\AnySidebarForWindows`，文件没有额外加密；它们与 Mac 和 Chrome 的记录分开。自动划词默认关闭，首次采集前需要确认说明。不持续录屏，不记录键盘文本或剪贴板历史。

不自动读取整篇文档。图像能力按模型配置判断；无法看清的内容需要补充更清晰截图，AI 回答请核对重要结论。

[完整隐私说明](docs/privacy.md)

## 反馈与许可

[问题反馈](https://github.com/decous37/Any-Sidebar-for-Windows/issues) · [更新记录](CHANGELOG.md) · [参与贡献](CONTRIBUTING.md)

反馈时附系统、应用与阅读软件版本、复现步骤；请隐藏 Key 和私人文档。源码和界面基于 Mac 版，保留 [MIT 许可](LICENSE)；第三方声明见 [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt)。与 DeepSeek、Microsoft 和 Apple 没有官方关联。
