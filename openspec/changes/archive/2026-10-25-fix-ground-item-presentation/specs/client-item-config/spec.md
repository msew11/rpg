# Spec Delta

## ADDED Requirements

### Requirement: 独立地面素材配置
ItemCfg SHALL 支持可选字符串 GroundAssetId，缺失为空，非字符串被 Schema 拒绝。地面表现 SHALL 仅使用此键解析 item/item 图集区域，缺图使用占位；背包继续使用 IconAssetId。七件测试装备 SHALL 按来源记录 icon_small_code 显式绑定地面素材，不通过道具 ID 或背包图标名称推断。资源键只用于客户端，不进入实例协议及服务端存档。

#### Scenario: 地面与背包独立
- **WHEN** 同一道具分别显示在地面和背包
- **THEN** 使用各自配置的素材，缺失地面键时显示占位，不影响背包图标

#### Scenario: 类型校验
- **WHEN** groundAssetId 为非字符串
- **THEN** 配置加载失败并报告 Schema 错误
