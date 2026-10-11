## MODIFIED Requirements

### Requirement: 命名边界
后续仓库及其承载的数据类 SHALL 一般分别采用 `Repo` 和 `Data` 前缀；ECS 世界仓库采用已确定的专名 `DataRepoEcsWorld`。仓库名称 SHALL 使用用途段和数据内容段区分职责。除 `DataEcsWorld` 外，核心框架 SHALL 不预先声明功能仓库或数据类型。

#### Scenario: 具体仓库留待后续
- **WHEN** 检查核心框架
- **THEN** 包含数据仓库上层抽象及 `DataEcsWorld`，不包含具体仓库
