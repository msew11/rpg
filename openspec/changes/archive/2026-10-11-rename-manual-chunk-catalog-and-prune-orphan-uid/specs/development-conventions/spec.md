## MODIFIED Requirements

### Requirement: 功能测试可按统一约定发现和追踪

项目 SHALL 使 `tests/core/` 之外的可执行 GDScript 功能测试脚本以 `test_` 开头，非测试脚本不使用该前缀，并在测试文件头用简明中文说明验收目的及对应的 `openspec/specs/` 需求或场景。检查入口 SHALL 引用当前测试路径。

#### Scenario: 检查功能测试

- **WHEN** 开发者查看功能测试文件并运行 `tools/check.ps1`
- **THEN** 测试文件名符合前缀约定，非测试脚本不会被误认为测试，文件头可追踪到规格，检查入口能执行全部功能测试
