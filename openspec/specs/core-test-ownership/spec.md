# core-test-ownership Specification

## Purpose
明确 `main` 与 `develop` 的核心测试归属，并防止开发分支偏离核心测试契约。

## Requirements

### Requirement: 核心测试目录归属

`main` SHALL 在 `tests/core/` 维护核心契约测试。`develop` SHALL 通过合并 `main` 获取该目录内容，并保持与最新 `main` 一致。

#### Scenario: 开发分支修改核心测试

- **WHEN** 指向 `develop` 的提交或拉取请求修改、新增或删除 `tests/core/` 中的文件，导致它与最新 `main` 不同
- **THEN** 分支归属检查失败

#### Scenario: 开发分支维护功能测试

- **WHEN** 指向 `develop` 的提交或拉取请求仅修改 `tests/core/` 之外的功能测试
- **THEN** 核心测试归属检查不因此失败
