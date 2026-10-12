## ADDED Requirements

### Requirement: 世界内部工具按职责归档

项目 SHALL 将世界内部无状态 C# 坐标计算、实体树编解码和怪物可见位置查询工具放在 `scripts/world/utils/`；持有会话状态的区块生命周期仍属于世界边界层。移动文件 SHALL 保持现有类型接口及运行行为。

#### Scenario: 检查世界工具目录

- **WHEN** 开发者查看 `scripts/world/utils/`
- **THEN** 可找到 `WorldMath`、`WorldEntityTreeCodec` 和 `WorldMonsterQuery`，而 `WorldChunkLifecycle` 仍在 `scripts/world/`

#### Scenario: 移动后运行世界

- **WHEN** 玩家移动并穿越回环边界，或保存恢复怪物实体
- **THEN** 坐标、存档和怪物显示结果与移动前一致
