# core-code-documentation Specification

## Purpose
规定核心 GDScript 类与方法的中文注释要求，便于阅读和维护。

## Requirements

### Requirement: 核心类与方法注释

`scripts/core/` 中新增或修改的 GDScript 类与方法 SHALL 使用简明中文注释说明职责或行为，避免仅复述名称或代码。

#### Scenario: 审查核心代码

- **WHEN** 审查 `scripts/core/` 中的类与方法声明
- **THEN** 每个声明前都有能说明职责或行为的简明中文注释
