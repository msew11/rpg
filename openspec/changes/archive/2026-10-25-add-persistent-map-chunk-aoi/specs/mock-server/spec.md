# Spec Delta

## ADDED Requirements

### Requirement: 角色地图身份持久化
服务端 SHALL 为角色保存正整数 MapId；新角色使用配置 initialMapId，旧存档缺失字段兼容原地图 1，读取不立即写盘，下次正常角色领域保存落地。选角和角色状态协议 SHALL 包含所属地图，移动目标 SHALL 在当前地图有效范围内。

#### Scenario: 旧角色迁移
- **WHEN** 读取无 MapId 的旧角色并正常保存角色领域
- **THEN** 读取时属于地图 1 且不改文件，保存后明确记录地图 1

#### Scenario: 指定初始地图
- **WHEN** 初始地图配置为合法的另一数值 ID 并创建角色
- **THEN** 新角色使用该地图且客户端按此身份选择静态地图和动态订阅

#### Scenario: 越界移动
- **WHEN** 请求超出所属地图的移动坐标
- **THEN** 请求失败，不改变移动目标、角色状态或 AOI
