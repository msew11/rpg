## MODIFIED Requirements

### Requirement: 服务持有具体配置缓存
ConfigService SHALL 构造并持有 MapCfgCache、ItemCfgCache、BasicCfgCache 和 EquipmentCfgCache，通过统一基类加载而不按业务配置类型分支解析。单例以 Basic.Value、多例以 Map.Items、Item.Items 和 Equipment.Items 访问。服务释放 SHALL 释放全部缓存，释放后通过服务或已取得缓存引用读取 SHALL 抛异常。

#### Scenario: 缓存与服务生命周期
- **WHEN** 通过服务取得缓存后释放服务
- **THEN** 后续服务访问、已有缓存的 Value/Items 和索引查询均被拒绝

## ADDED Requirements

### Requirement: 加载后索引与统一关联检测
配置缓存 SHALL 提供整表读取后的索引构建扩展点及 Cache 级 Validate 重载入口。全部表及索引加载完成后，配置服务 SHALL 逐缓存执行关联检测，直接使用缓存 Validate 重载；关联检测不得依赖扫描或加载顺序。已有初始地图关联检查 SHALL 保留。索引或检测失败 SHALL 阻止服务构造、保留文件和原因诊断并释放已创建缓存。

#### Scenario: 全表就绪后检测
- **WHEN** 道具表先于装备表加载，或基础表先于所引用的地图配置完成
- **THEN** 检测阶段可读取全部目标表与道具分类索引，合法引用通过

#### Scenario: 失败诊断
- **WHEN** 索引构建失败、装备引用缺失或初始地图不存在
- **THEN** 启动失败且异常指向相应配置及原因，不能使用部分配置启动
