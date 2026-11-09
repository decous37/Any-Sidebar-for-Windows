# Any Sidebar for Windows · 技术设计

版本1.0；2026-11-09。依据用户整体适配授权，执行spec v1.0。

## 架构概览

采用 .NET 10 原生 WPF 桌面壳 + Microsoft Edge WebView2 承载原 React UI。与Mac一致：Web页面只管理展示和短暂输入，原生端负责会话、图片、凭据、模型请求及系统采集。没有Electron或本地HTTP端口。

应用拆成可在Mac执行领域检查的 `Reading.Core`、SQLite/附件/官方传输的 `Reading.Infrastructure`、Windows专属 `AnySidebar.Windows`。Mac前端复制到本仓库后去掉平台专用命名和权限文案，独立维护，保留MIT来源。

## 核心数据与接口

- `State` 包含 tasks、drafts、turns、snapshots、generations、assets、operations、preferences 和 activeTaskId。ID用UUID，时间毫秒，保存版本1。
- `Draft` 字段与Mac桥接保持一致：promptRevision、externalQuotes（含来源/选区租约）、单条assistantQuote、images；修改采用revision校验。
- `FrozenSubmission`、`RequestSnapshot` 保存原问题、完整引用、不可变图片标识/摘要、模型选项、消息及预算。请求快照不存Key，不存base64。
- `Runtime` 对状态变更串行化，通过复制→校验→事务落盘→替换状态更新。HTTP/采集不持有状态锁。操作ID先保留，重放返回原结果；准备和生成归原任务。
- `IRepository.Load/Save(State)`；SQLite单行版本化快照事务，WAL；启动校验和备份恢复，失败不静默重置。
- `IAttachments.PersistPng/Read/Delete`：UUID内部路径、PNG校验、SHA256、原图与缩略图；不存在/摘要不符拒绝。孤儿资源仅在无任何草稿/请求/准备引用时清理。
- `ITransport` 返回异步 `StreamDelta`（answer/reasoning/finishReason），接受冻结请求与取消令牌；原生凭据服务内部读取Key，仅以认证头使用。
- 桥接沿用Mac命令集合，参数白名单/UUID/长度/整数校验。WebView2使用 `https://anysidebar.local` 本地虚拟主机，消息/响应按messageId关联，epoch隔离；bootstrap系统版本与会话序号独立。
- 页面视图与持久化对象分离：不暴露Key、内部路径、sourceLease、文件身份；图片仅用受控 `/asset/{uuid}/preview|thumbnail` 资源。

## 模块设计

| 模块 | 文件归属 | 职责与依赖 |
| --- | --- | --- |
| M1 领域模型 | native/Reading.Core/Models.cs | 数据、故障、选区令牌、预算与视图 |
| M2 上下文/模型 | Core/Context.cs | 能力表、角色、历史、图文消息和实际JSON预算，不静默裁剪 |
| M3 任务运行时 | Core/ReadingRuntime.cs | 会话/草稿/请求/停止/恢复/选择归属和版本一致性 |
| M4 持久化 | Infrastructure/SqliteRepository.cs | 原子状态提交、备份恢复、凭据之外的数据 |
| M5 图片 | Infrastructure/AttachmentStore.cs | PNG、SHA、受控路径、引用回收 |
| M6 DeepSeek | Infrastructure/DeepSeekTransport.cs | 官方HTTPS、UTF8/SSE、超时、取消、错误映射、无自动重试 |
| M7 系统凭据 | Windows/CredentialVault.cs | CredRead/CredWrite/CredDelete，未知与未配置分开 |
| M8 选文 | Windows/SelectionService.cs | 后台STA UIAutomation TextPattern + 鼠标释放确认，租约及来源保护，排除编辑和密码 |
| M9 截图 | Windows/CaptureService.cs | 原生框选/窗口选择，物理像素坐标，隐藏自身后取可见内容，取消不提交 |
| M10 入口与快捷键 | Windows/DesktopShell.cs、HotkeyService.cs | WPF浮窗、伴随入口、NotifyIcon菜单、RegisterHotKey、录入暂停及冲突回滚 |
| M11 桥接 | Windows/WebBridge.cs | WebView2来源、导航、资源和契约，UI线程调度，不把Key回传 |
| M12 前端 | apps/windows/web + src/ui | 保留Mac全部交互，Windows桥接/快捷键/采集状态适配 |
| M13 工程 | scripts、.github/workflows | 本机及托管Windows构建、包审计、干净安装、集中验证产物 |

## 模块交互

1. 启动：单实例互斥→加载SQLite及附件→中断未完成请求→恢复偏好/热键→创建入口与侧栏→页面bootstrap。数据库异常可见，不覆盖原文件。
2. 选文：前台来源改变建立租约，后台读取只读TextPattern；鼠标按下不提交中间片段，释放后确认稳定选区。原任务路由/来源序号/引用ID共同保护。进入本应用不触发来源取消。
3. 截图：先固定taskId及operationId，隐藏自身窗口；覆盖层按整个虚拟桌面像素定位，取消返回原显隐。选区确认后关闭覆盖层并等待呈现，保存PNG→原任务草稿→恢复面板。删除任务/取消后迟到结果回收。
4. 发送：检查预期版本及空输入→保留操作与准备记录→冻结资料和选项→后台校验图片/预算/凭据→再次核对任务和停止状态→事务接受请求并精准清理→仅启动一次HTTP。新编辑不会被清理。
5. 流式：字节/逐行SSE分帧，保活不当内容；按200ms/8KiB批量提交并通知；结束刷新尾量。切会话不改变归属，停止只匹配原任务原操作。关闭面板不中断，退出取消并收尾。
6. 重试/重生成：沿用原消息与图片，用当前模型选项重新预算；新请求独立记录。重新生成期间显示新内容，失败/停止恢复原completedRequestId。
7. 设置：Key仅原生保存/替换/清除，页面回传配置状态。热键先尝试注册候选，成功才存，失败恢复旧；录入暂停两组，Esc取消；Ctrl+Alt+A/S为默认值。
8. 删除：确认→停止目标任务请求→删除独占任务资料→事务落盘→回收不再被任何状态引用的图片；Key与偏好不删除。

## 文件组织

`apps/windows/web` React适配；`src/ui`沿用展示；`contracts`能力/桥接/提示；`native/Reading.Core`可移植领域；`native/Reading.Infrastructure`存储/传输；`native/AnySidebar.Windows`原生平台；`tests/web`及`native/Reading.Checks`集中回归；`scripts`构建打包；`docs`中文文档；`build`和`release`忽略。

## 技术决策

- Windows10 22H2+/11，x64和arm64，PerMonitorV2 DPI。WPF随发布自包含.NET运行时，WebView2用系统Evergreen，缺失指向微软官方安装。
- UIA依赖目标阅读器，不模拟Ctrl+C、不管理员常驻、不宣称通用。选文后台有界，避免阻塞UI。
- 截图明确是当前可见像素；不承诺被遮挡窗口的隐藏内容。保留10MiB/9新图/16总图/24MiB限制。
- Windows凭据管理器保存API Key，不把Key加密文件放仓库，不需要Mac签名权限概念。
- Mac可交叉编译检查Windows目标，实际Windows打包/原生检查可用GitHub托管Windows runner集中执行；它不等同于手动虚拟机全矩阵验收。
- 初版完成前不运行功能测试或启动虚拟机；每个模块提交前可静态审阅及差异检查，不伪称测试通过。完成后集中测试，再修复失败。
- 首版版本0.1.0，保留原Mac视觉，名称Any Sidebar for Windows。未签名包不伪称已受微软信任，不改变系统安全设置。

## 授权记录

用户明确授权自行补齐规格、设计、任务与验收文档后直接开发；Windows快捷键自行决定；先初版后测试；独立仓库持续上传。
