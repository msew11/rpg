## MODIFIED Requirements

### Requirement: 脚本目录反映职责
项目 SHALL 将 `main.gd`、`local_session.gd` 与测试内容读取器 `test_chunk_catalog.gd` 放在 `scripts/` 根目录，将世界状态、组件和 ECS System 放在 `scripts/world/`，并保持 `scripts/core/` 的通用框架代码不变。

#### Scenario: 检查项目脚本
- **WHEN** 开发者浏览 `scripts/` 目录
- **THEN** 入口脚本、测试内容读取器、世界规则、UI 与静态工具位于各自约定的位置，且项目仍从 `scenes/main.tscn` 启动
