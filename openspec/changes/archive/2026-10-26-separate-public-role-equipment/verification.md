# 验收记录

## 检查结果

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：通过，构建零警告、零错误，纯 .NET 220 项、Godot 运行时 219 项及全部场景冒烟通过。
- 最后补充旧存档空背包诊断后，`EquipmentServerTests` 9 项通过，纯 .NET 全集再次运行 221 项通过；重新执行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1` 通过。
- 场景检查覆盖七种装备、男女全部动作与八方向、实际点击/Ctrl 穿脱和替换、满包失败、旋转预览、素材预热、背包移动、丢弃拾取、切角、重启和小窗口。
- `openspec validate --specs`：29 项通过；`openspec validate separate-public-role-equipment --strict`：通过。主规格既有的 Purpose 占位、长度等提示仍保留，不在此次协议变更中重写无关要求。
- `git diff --check`：通过。

## 验收场景与证据

| 场景 | 证据 |
| --- | --- |
| BagPb 不含穿戴列表，删除字段保留编号与名称，公开装备无私有字段 | PublicEquipmentTests：描述符、字段保留、真实字节往返、传输方向与旧消息拒绝 |
| 无背包或私有 Role 即可转换装备，空列表卸下，协议对象不共享 | PublicEquipmentTests：独立转换、空装备、复制隔离、非法身份/版本/重复槽位/不适配配置 |
| 双版本分别过滤；背包整理不增加装备版本；穿脱保持实例与原子转移 | EquipmentServerTests、既有 BagServerTests 与装备场景冒烟 |
| 本人物品两侧完整校验，失败不产生部分替换；公开消息不能覆盖本人装备 | RoleEquipmentCacheTests、BagCacheTests：回退、同版本冲突、缺失载荷、跨列表实例重复、公私消息交错与普通监听读取 |
| 世界及窗口从独立装备快照同步，普通背包变更保持装备引用，切角清空 | RoleEquipmentCacheTests 的 ECS 回归及 EquipmentSmoke、ItemAssetSmoke |
| 已穿装备旧档继承原版本且读取不写盘，落地失败可重试，损坏背包保持路径诊断 | EquipmentServerTests：旧档迁移、共同落地重试与空背包诊断；地面物品共同恢复既有回归 |

## 一致性与范围

- 已同步 client-network、client-session、mock-server、role-bag、role-equipment 五份 delta specs；保留原有标题、Purpose 和未涉及的要求及场景。
- 架构和路线图记录公开装备边界、本人原子交付、双版本、旧档兼容及世界/UI 消费方式。
- 当前 MockServer 仍只有单连接当前角色；本次实现可独立编码的公开装备投影和消息契约，多人连接、玩家可见范围和远端广播留待独立 change。
- 全集检查发现既有地面落点边界断言把连续坐标误限于整数 3199，已改为实际地图范围 `[0, 3200)`，未修改落点实现。
- 本地提交包含配套协议生成文件与迁移后的消费者，不推送远程。
