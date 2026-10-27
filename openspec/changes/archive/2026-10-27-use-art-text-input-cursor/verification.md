# 验证记录

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 全部通过：构建零警告、零错误，249 个 .NET 用例、220 个 Godot xUnit 用例和全部场景验收通过。
- 指针场景新增真实登录输入：账号与密码分别悬停、点击聚焦及输入字符，确认 Ibeam 区域、密码遮蔽属性和移出恢复；保留普通点击、涟漪、UI 隔离与清理验收。
- 首轮运行暴露既有点击动画等待四个物理帧不足 0.08 秒的时序风险，改为等待 0.1 秒后通过完整重跑。
- Edit.gsa 解码为单帧 9×15，源配置热点 (5,8)，与 Fetch 共用导入及 1.25 倍最近邻缩放。来源和 SHA-256 已记录，现有 PNG 未变化。
- `openspec validate use-art-text-input-cursor` 通过；`openspec validate --specs` 29 项通过，保留既有 Purpose 占位和长需求等警告。
- 已核对 client-ui 新需求与实现、架构和路线图一致。硬件光标真实屏幕显示未做图形窗口目视验收；headless 可验证资源及交互，无法验证操作系统光标像素。
