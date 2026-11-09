# Windows 平台核对记录

日期：2026-11-09。以下为接口能力调研，不代表已经实现或通过运行测试。

## 已核对的系统接口

| 能力 | 官方接口或资料 | 需要保留的边界 |
| --- | --- | --- |
| 跨应用划词 | [TextPattern.GetSelection](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.textpattern.getselection) | 依赖目标软件提供选区；没有支持时不能声称已读取。不能退化为模拟复制并监视剪贴板。 |
| 只读资料判断 | [TextPattern.IsReadOnlyAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.textpattern.isreadonlyattribute) | TextPattern 本身是读取接口，不代表内容不可编辑。应额外排除密码和编辑区域。 |
| 全局快捷键 | [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey) | 注册可能冲突；MOD_NOREPEAT 可避免长按不断重复，不能取代操作状态校验。 |
| 系统凭据 | [CredWriteW](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credwritew) | 凭据进入当前用户凭据集合；读取、替换和清除失败需分别处理，不伪报未配置。 |
| 本地前端 | [WebView2 本地资源](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content) | 可使用本地虚拟域，无需开放 HTTP 服务；应限制消息来源、导航和资源访问。 |
| 截图排除自身 | [SetWindowDisplayAffinity](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity) | Windows 10 2004 起支持 WDA_EXCLUDEFROMCAPTURE；它不是内容保密保证，仍需正确隐藏本应用和覆盖层。 |
| 跨系统构建条件 | [NETSDK1100](https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1100) | .NET 可显式下载 Windows 目标包供非 Windows 环境编译；编译成功不代表能在 Mac 运行 WPF，更不代表真实功能验收。 |

## 与 Mac 的差异

- Windows 的采集、进程权限和受保护窗口行为不同，不能照搬 macOS 辅助功能／屏幕权限开关的文案或状态。
- 软件若以更高权限运行，常规用户权限的辅助功能访问可能受限。产品应提示可用替代方式，不以要求常驻管理员模式代替适配。
- Windows 快捷键采用独立默认值，保留两组快捷键、录入、清除、冲突及暂停录入期间触发的完整规则。
- 图像能力与模型限制继续按共享资料配置管理，不因移植而声称新的模型质量验证已经完成。

## 测试安排

按用户要求，在基本实现完整后集中进行构建、模拟回归、包审计和必要的真实 Windows 检查。现在不启动虚拟机，不请求真实模型，不将接口文档作为实测证据。
