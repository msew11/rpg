# development-conventions Specification

## Purpose
TBD - created by archiving change align-agent-conventions. Update Purpose after archive.
## Requirements
### Requirement: 功能测试可按统一约定发现和追踪

项目 SHALL 使 `tests/core/` 之外的可执行 GDScript 功能测试脚本以 `test_` 开头，并在文件头用简明中文说明验收目的及对应的 `openspec/specs/` 需求或场景。检查入口 SHALL 引用当前测试路径。

#### Scenario: 检查功能测试

- **WHEN** 开发者查看功能测试文件并运行 `tools/check.ps1`
- **THEN** 测试文件名符合前缀约定，文件头可追踪到规格，检查入口能执行全部功能测试

### Requirement: 非核心脚本声明可检索且有职责说明

项目 SHALL 为新增或修改的非核心 GDScript 顶层类显式声明唯一 `class_name`，并为类、常量、类属性和方法提供简明中文职责注释。

#### Scenario: 查找和阅读非核心脚本

- **WHEN** 开发者在 Rider 中按类名查找脚本并阅读其声明
- **THEN** 顶层类具有显式名称，成员声明附近有中文职责说明
