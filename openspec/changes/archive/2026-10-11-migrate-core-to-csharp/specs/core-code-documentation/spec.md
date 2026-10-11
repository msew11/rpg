## MODIFIED Requirements

### Requirement: 核心类与方法注释

`scripts/core/` 与 `tests/core/` 中新增或修改的 C# 顶层类、内部类、接口、枚举、常量、类属性和方法 SHALL 使用简明中文注释说明职责或行为，避免仅复述名称或代码。方法内局部变量不要求逐一注释。

#### Scenario: 审查核心代码

- **WHEN** 审查 `scripts/core/` 与 `tests/core/` 中的类型和成员声明
- **THEN** 每个声明前都有能说明职责、含义或行为的简明中文注释

#### Scenario: 审查方法内局部变量

- **WHEN** 方法使用仅在方法内生效的局部变量
- **THEN** 不因局部变量缺少逐项注释而违反本要求
