## MODIFIED Requirements

### Requirement: 服务持有具体配置缓存
ConfigService SHALL 构造并持有 MapCfgCache、ItemCfgCache 和 BasicCfgCache，通过统一基类加载而不按业务配置类型分支解析。单例数据以 Map.Value 和 Basic.Value 访问，多例以 Item.Items 访问。服务释放 SHALL 释放缓存，释放后通过服务或已取得缓存引用读取 SHALL 抛异常。

#### Scenario: 缓存与服务生命周期
- **WHEN** 通过服务取得缓存后释放服务
- **THEN** 后续服务访问、已有缓存的 Value/Items 访问均被拒绝
