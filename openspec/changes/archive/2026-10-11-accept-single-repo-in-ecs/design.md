## Context

原有 `EcsWorld` 可接收 `DataEcsWorld`，但调用方希望直接传入持有该数据的单例仓库。

## Goals / Non-Goals

**Goals:** 单例仓库明确提供数据；EcsWorld 接受仓库并操作其数据。

**Non-Goals:** 不实现存档、数据写入或多例仓库存取。

## Decisions

在 `DataRepoSingle<T>` 声明抽象只读 `Data` 属性。`EcsWorld` 增加 `DataRepoSingle<DataEcsWorld>` 构造函数，并从仓库取得同一实例。保留原有无参和数据实例构造函数，以维持既有调用方与核心测试行为。

## Risks / Trade-offs

具体单例仓库须实现 `Data` 属性；当前核心测试替身同次更新。
