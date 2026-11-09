# 开发说明

Windows项目独立维护，不需要Mac或Chrome仓库。React界面来自Mac版，系统实现使用.NET10、WPF和WebView2；领域核心不依赖Windows。

## 目录

- `apps/windows/web`：Windows前端、状态、桥接适配。
- `src/ui`：共享来源的消息、输入、Markdown、模型等组件。
- `contracts`：桥接白名单、模型能力、提示、公开样例及图标SVG。
- `native/Reading.Core`：领域状态、上下文、预算、会话与请求。
- `native/Reading.Infrastructure`：SQLite、PNG附件、官方DeepSeek流式传输。
- `native/AnySidebar.Windows`：WPF窗口、托盘、UIAutomation、截图、凭据及全局快捷键。
- `scripts`：独立构建、图标、发布、包审计和安装。
- `docs/specs/windows-sidebar`：规格、设计、任务与验收。

## 构建

Node24.15+（24.x）或26.x，npm和.NET10 SDK。图标构建使用Chromium，运行使用WebView2。

```sh
npm ci
npx playwright install chromium
npm run build
npm run publish:windows -- win-x64 win-arm64
npm run package
```

非Windows构建需下载Windows目标包，脚本显式启用WindowsTargeting；编译不代表真实系统检查。若.NET不在PATH，可使用构建时的 `ANY_SIDEBAR_DOTNET` 指定SDK可执行文件，不用于改变正式应用的模型接口。

发布时先刷新网页资源和图标，并清理该架构旧输出，避免混入旧界面或旧文件。

发布包采用完整目录和自包含.NET运行时，WebView2需系统提供。没有自动更新服务。安装脚本只操作当前用户应用目录与开始菜单，不触碰数据目录、凭据或安全策略。

## 集中验证

按用户要求初版完整接线后再执行：

```sh
npm run typecheck
npm run lint
npm run test:web
npm run test:native
npm run test:e2e
npm run build
npm run publish:windows
npm run package
```

模拟接口不读取真实Key、不自动计费；Windows窗口、选区、热键和像素截图需要真实Windows。可以在GitHub托管Windows runner集中执行构建及可自动化系统检查，手动虚拟机矩阵另外记录。

## 维护约定

操作ID去重，准备→冻结→预算→接受→流式保存。只清理本次提交内容，不删除准备时新增草稿。图片保留SHA256和来源，重试/重生成使用冻结消息及当前选项。不静默截断资料，不自动重试付费请求。

Key在原生服务读取，只出现在官方HTTPS认证头，不回传页面。桥接仅本地域、白名单命令和受控资产路径；UI不取得任意文件访问或外部脚本能力。

检查记录见[验收报告](validation-report.md)，未执行/失败/未验证必须分别列出，不能用模拟界面或编译成功冒充实机通过。
