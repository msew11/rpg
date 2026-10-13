# Design

## Context

`tools/check.ps1` 先读取 `GODOT_BIN`，再从 PATH 查找 `godot`、`godot4`，最后尝试两个固定安装路径。Scoop 安装提供指向控制台程序的 `godot-mono` shim。

## Goals / Non-Goals

**Goals:** 让现有查找流程识别 Scoop 的 Godot .NET 命令。

**Non-Goals:** 不安装编辑器或导出模板，不调整 Godot 版本要求。

## Decisions

- 将 `godot-mono` 加入 PATH 候选列表，并放在通用命令前面，避免机器同时装有标准版 Godot 时误选。
- 复用现有版本校验和导出流程；无需解析 Scoop 私有安装目录。

## Risks / Trade-offs

- PATH 中的 `godot-mono` 可能不是 4.7.2 → 现有版本校验会明确失败。
