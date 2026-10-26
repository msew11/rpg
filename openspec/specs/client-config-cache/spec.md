# client-config-cache Specification

## Purpose

统一客户端配置的 JSON 形状、文件映射与缓存访问，使新增单例和多例配置遵循相同加载、校验、主键索引和生命周期规范。

## Requirements

### Requirement: 单例与多例缓存契约
系统 SHALL 提供 SingleCfgCache<TCfg> 和 MultiCfgCache<TKey, TCfg> 两种基类。单例 SHALL 将 JSON 根对象完整反序列化为一个 XxxCfg，通过 Value 读取；多例 SHALL 将 JSON 根数组中每个对象反序列化为一个 XxxCfg，通过只读 Items 按主键读取。根形状不匹配或出现 null 行 SHALL 抛异常。不得依赖 kind 或 items 包装。

#### Scenario: 两种缓存加载
- **WHEN** 单例文件是合法对象，多例文件是合法数组
- **THEN** 分别得到单个 Cfg 和以主键索引的多个 Cfg，空数组合法

#### Scenario: 错误根形状
- **WHEN** 单例收到数组，或多例收到对象、null 或包含 null 行的数组
- **THEN** 加载抛出包含文件路径的异常

### Requirement: 文件和主键标注
具体 XxxCfgCache SHALL 以 CfgFile 标注唯一 JSON 文件名，全部 JSON SHALL 平铺于 config/ 根目录且不允许子目录，加载器 SHALL 使用该文件名匹配根目录扫描结果，并按同名 xxx.schema.json 校验。多例 XxxCfg SHALL 恰有一个 CfgKey 标注的公开可读实例属性，其类型 SHALL 匹配 TKey；缺失/歧义/不匹配标注和缺失/null/重复键 SHALL 抛异常。

#### Scenario: 自定义主键
- **WHEN** 多例数据以 CfgKey 标注字符串 Code 属性
- **THEN** Items 以 Code 索引，不要求属性名为 Id 或类型为整数

#### Scenario: 错误标注或键
- **WHEN** 缓存缺少文件标注、文件名非法，或多例缺少/重复/不匹配主键标注，或数据键缺失、为 null 或重复
- **THEN** 加载失败并指明类型、文件或主键原因

#### Scenario: 文件加载错误
- **WHEN** 缺失配置文件、配置子目录、存在未注册 JSON、JSON 损坏、Schema 缺失或无效，或数据不符合 Schema
- **THEN** 构造失败并包含相关文件路径和原因

### Requirement: 服务持有具体配置缓存
ConfigService SHALL 构造并持有 MapCfgCache、ItemCfgCache、BasicCfgCache 和 EquipmentCfgCache，通过统一基类加载而不按业务配置类型分支解析。单例以 Basic.Value、多例以 Map.Items、Item.Items 和 Equipment.Items 访问。服务释放 SHALL 释放全部缓存，释放后通过服务或已取得缓存引用读取 SHALL 抛异常。

#### Scenario: 缓存与服务生命周期
- **WHEN** 通过服务取得缓存后释放服务
- **THEN** 后续服务访问、已有缓存的 Value/Items 和索引查询均被拒绝

### Requirement: 加载后索引与统一关联检测
配置缓存 SHALL 提供整表读取后的索引构建扩展点及 Cache 级 Validate 重载入口。全部表及索引加载完成后，配置服务 SHALL 逐缓存执行关联检测，直接使用缓存 Validate 重载；关联检测不得依赖扫描或加载顺序。已有初始地图关联检查 SHALL 保留。索引或检测失败 SHALL 阻止服务构造、保留文件和原因诊断并释放已创建缓存。

#### Scenario: 全表就绪后检测
- **WHEN** 道具表先于装备表加载，或基础表先于所引用的地图配置完成
- **THEN** 检测阶段可读取全部目标表与道具分类索引，合法引用通过

#### Scenario: 失败诊断
- **WHEN** 索引构建失败、装备引用缺失或初始地图不存在
- **THEN** 启动失败且异常指向相应配置及原因，不能使用部分配置启动
