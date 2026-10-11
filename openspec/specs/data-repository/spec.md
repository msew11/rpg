# data-repository Specification

## Purpose

定义所有数据仓库及其统一管理入口的上层契约，为后续按用途和数据类型实现仓库提供稳定边界。

## Requirements

### Requirement: 仓库抽象
核心框架 SHALL 提供名为 `DataRepo` 的抽象基类。具体仓库 SHALL 能声明承载的数据脚本类型及单例或多例形态；核心框架 SHALL 不实现任何具体仓库或数据类。

#### Scenario: 单例仓库设计
- **WHEN** 后续功能设计只承载一个数据实例的仓库
- **THEN** 该仓库能够通过 `DataRepo` 契约声明其数据类型和单例形态

#### Scenario: 多例仓库设计
- **WHEN** 后续功能设计承载一组同类型数据实例的仓库
- **THEN** 该仓库能够通过 `DataRepo` 契约声明其数据类型和多例形态

### Requirement: 管理入口抽象
核心框架 SHALL 提供名为 `DataRepoCache` 的抽象基类，定义按仓库脚本类型获取 `DataRepo` 的统一入口。核心框架 SHALL 不实例化该类，也 SHALL 不注册 AutoLoad 单例。

#### Scenario: 后续管理器实现
- **WHEN** 后续功能实现统一仓库管理单例
- **THEN** 它能够继承 `DataRepoCache` 并提供具体的仓库查找行为

### Requirement: 命名边界
后续仓库及其承载的数据类 SHALL 分别以 `repo_` 和 `data_` 为前缀；仓库名称 SHALL 使用用途段和数据内容段区分职责。核心框架 SHALL 不预先声明具体名称或数据类型。

#### Scenario: 具体仓库留待后续
- **WHEN** 检查本次核心框架
- **THEN** 只有 `data_repo` 与 `data_repo_cache` 上层抽象，不包含 `repo_player`、`repo_player_item` 等具体仓库或对应数据类
