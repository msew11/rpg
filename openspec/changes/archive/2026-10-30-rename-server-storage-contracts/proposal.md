# Proposal

## Why

三个模块的 Repo 接口只提供持久化 IO，内存数据、修改跟踪和保存基线已由 LoginCache、RoleCache、WorldCache 管理。统一采用 Storage 命名，避免账号摘要与 Role、World 风格不一致及仓库职责混淆。

## What Changes

- 接口统一为 ILoginStorage、IRoleStorage、IWorldStorage，实现为 LoginStorage、RoleStorage、WorldStorage。
- 同步类型文件、Godot UID、装配、Actor、共同落盘依赖与测试替身和调用点，移除这六个旧名称。
- 文档明确 Cache 管理内存状态，Storage 承接加载、保存和恢复；不修改存档格式、协议、空值契约或业务流程。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

无。纯命名重构，现有 server-storage、server-write-back、server-map-state 的行为和验收场景保持，设置 skip_specs: true。

## Impact

影响三个模块 Storage 目录、Actor、MockServer 与 ServerModules 装配、RepoItemBatch 引用及现有存储测试和文档。接口为公开源码类型，仓库内消费者一次迁移，不保留旧名称别名；不增加依赖。RepoItemBatch 和 RepoRoleIdSequence 的独立类型不在本次重命名范围。保留用户已有 ServerModules.cs 改动，不纳入本次提交。
