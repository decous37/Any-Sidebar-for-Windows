# Any Sidebar for Windows · 任务清单

版本1.0；用户授权整体设计和实施。每项未完成前保持待执行；先完成T1–T35可运行初版，再执行T36–T45集中验证。完成依据为实际文件与后续验证，不将上传等同于功能通过。

| 任务 | 文件 | 依赖 | 实现与完成条件 | 状态 |
| --- | --- | --- | --- | --- |
| T1 | 文档四份 | 无 | 固定Mac基线、Windows差异及验收映射，差异审阅 | 完成 |
| T2 | package/锁文件/构建配置 | T1 | 复制独立前端所需依赖，移除Mac专用脚本 | 待执行 |
| T3 | contracts/* | T1 | 复制能力/契约/提示，Windows命名及键码 | 待执行 |
| T4 | apps/windows/web/bridge.ts | T2,T3 | WebView2异步响应、关联ID、超时和操作查询 | 待执行 |
| T5 | 前端protocol/设置 | T4 | Windows来源/资产URL/快捷键标签/采集文案 | 待执行 |
| T6 | native/Reading.Core项目 | T1 | 可移植.NET10项目及序列化约定 | 待执行 |
| T7 | Core/Models.cs | T6 | 全部状态、冻结结构、默认值、错误及DTO | 待执行 |
| T8 | Core/Context.cs模型能力 | T7 | 模型查找、思考选项、图片兼容性 | 待执行 |
| T9 | Core/Context.cs上下文 | T8 | 系统/历史/资料/问题，图片默认问题和来源区分 | 待执行 |
| T10 | Core/Context.cs预算 | T9 | 实际JSON编码、图像/文本/输出额度边界 | 待执行 |
| T11 | Infrastructure/SqliteRepository.cs | T7 | 原子快照、模式版本、WAL/恢复、不静默清空 | 待执行 |
| T12 | Infrastructure/AttachmentStore.cs | T7 | PNG校验、不可变文件、SHA、受控读取和回收 | 待执行 |
| T13 | Core运行时基础 | T7,T11 | 单锁复制提交、序号、操作去重及重启恢复 | 待执行 |
| T14 | Core运行时会话 | T13 | 新建/切换/重命名/删除及独立草稿 | 待执行 |
| T15 | Core运行时草稿 | T14 | 文本/引文/图片修改、归属、版本与精确清理 | 待执行 |
| T16 | Core运行时选文 | T15 | 来源租约、序号、最新/累积和迟到取消保护 | 待执行 |
| T17 | Core运行时发送 | T10,T15 | 准备→预算→接受事务→单请求启动 | 待执行 |
| T18 | Core运行时流式 | T17 | 批量保存、终态刷新、停止与任务隔离 | 待执行 |
| T19 | Core运行时重放 | T18 | 冻结重试、重生成恢复原回答 | 待执行 |
| T20 | Core视图 | T14,T19 | 分页、脱敏、图片受控URL、恢复输入 | 待执行 |
| T21 | Infrastructure/DeepSeekTransport.cs | T9 | HTTPS请求、图片真实字节、SSE和错误/超时映射 | 待执行 |
| T22 | Windows/CredentialVault.cs | T6 | 当前用户凭据读写删，未知状态和Key校验 | 待执行 |
| T23 | Windows项目/Win32.cs | T6 | WPF/WebView2/托盘依赖、manifest、DPI及原生声明 | 待执行 |
| T24 | Windows/HotkeyService.cs | T23 | 两组注册、暂停、录入、候选冲突与回滚 | 待执行 |
| T25 | Windows/SelectionService.cs | T16,T23 | 后台UIA、只读/密码排除、鼠标释放稳定确认 | 待执行 |
| T26 | Windows/CaptureService.cs | T12,T23 | 虚拟桌面区域覆盖层、Esc/取消及物理坐标 | 待执行 |
| T27 | Windows/CaptureService.cs窗口 | T26 | 窗口选择、排除自身、来源与原任务固定 | 待执行 |
| T28 | Windows/DesktopShell.cs | T23 | 入口拖动、面板靠近/收起、托盘和右键菜单 | 待执行 |
| T29 | Windows/WebBridge.cs | T3,T20,T28 | 来源/参数校验、白名单命令与事件 | 待执行 |
| T30 | Windows本地资源 | T29 | 本地映射、图片拦截、外链限制、WebView2缺失提示 | 待执行 |
| T31 | Windows/App.cs | T21–T30 | 单实例、生命周期、系统服务、Key及请求组装 | 待执行 |
| T32 | 前端最后整合 | T5,T31 | 完整聊天、返回/抽屉/收起/图文与采集状态 | 待执行 |
| T33 | scripts图标/资源 | T23 | 从既有SVG导出ICO，资源白名单 | 待执行 |
| T34 | scripts构建/打包 | T31,T33 | x64/arm64自包含ZIP、审计、WebView2说明 | 待执行 |
| T35 | docs/README/许可 | T32,T34 | 安装/使用/隐私/开发/限制，与源码一致；初版全部接线 | 待执行 |
| T36 | tests/web | T35 | 从实际行为提炼桥接/发送/引用/主题布局回归 | 待执行 |
| T37 | native/Reading.Checks | T35 | 领域/预算/流式/存储/竞态及恢复可执行检查 | 待执行 |
| T38 | 集中静态检查 | T36,T37 | 类型、lint、差异、文档链接、敏感文件检查通过 | 待执行 |
| T39 | 集中领域/前端检查 | T38 | 运行全部新回归并修复，不真实付费调用 | 待执行 |
| T40 | 集中Windows编译 | T39 | Windows目标编译、两架构发布，修复实际错误 | 待执行 |
| T41 | .github/workflows | T40 | 手动集中触发托管Windows原生/打包检查并存证 | 待执行 |
| T42 | Windows系统检查 | T41 | 集中真实入口/快捷键/选文/截图/WebView2流程；缺环境如实标注 | 待执行 |
| T43 | 包审计与安全 | T40 | ZIP资源、身份、无Key/用户数据/开放测试入口 | 待执行 |
| T44 | 验收报告 | T39–T43 | checklist逐项证据、失败/未验证分开 | 待执行 |
| T45 | 仓库交付 | T44 | 最新提交和文档上传，核对远端与本机一致 | 待执行 |

执行顺序：T1→T2–T12→T13–T22→T23–T35→T36–T45；依赖指明的前置必须完成。开发期间分阶段上传，不提前运行功能测试；任务实现和验收通过分开记录。
