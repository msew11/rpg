# 数据仓库框架

`main` 在 `scripts/core/` 只定义 `DataRepo` 和 `DataRepoCache` 两个抽象类。`DataRepo` 供具体仓库继承，声明承载的数据脚本类型及 `SINGLE`（一个对象）或 `MULTIPLE`（一组对象）形态。`DataRepoCache` 供未来的统一管理单例继承，定义按仓库脚本类型查找仓库的入口。目前没有具体仓库、数据类、缓存实现或 AutoLoad 配置。

具体仓库文件以 `repo_` 开头，随后用 `player`、`world` 等用途段区分数据归属，再以末段描述数据，例如未来的 `repo_player`、`repo_player_item`、`repo_world_monster`。承载的数据文件对应以 `data_` 开头。示例仅说明命名结构，不代表这些类型已实现。

Godot 4.7.2 的 GDScript 不支持给类声明泛型参数，例如 `DataRepo[DataPlayer]`。抽象仓库通过 `get_data_type() -> Script` 声明运行时类型；未来具体仓库可以在自身读写接口使用具体数据类作为静态类型。`get_storage_kind()` 只标识单例或多例，尚不规定读取、集合标识、写入或持久化行为。

`develop` 后续通过独立 change 实现管理单例和具体仓库，再确定对象生命周期、注册方式与数据操作。旧 `DataSource` 具名键契约已移除。
