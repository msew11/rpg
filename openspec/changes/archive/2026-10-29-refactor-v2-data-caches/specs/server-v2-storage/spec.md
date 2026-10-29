## MODIFIED Requirements

### Requirement: 修改跟踪与串行写回
V2 SHALL 由所属分片独占可变工作区。LoginContext、RoleContext、WorldContext SHALL 只持有所属 LoginCache、RoleCache、WorldCache 引用及基类借用服务，不直接持有数据、保存基线或 Commit。模块 Cache SHALL 组织各类 DataCache；DataCache SHALL 持有不可变数据值及成功保存基线，内聚数据管理、提交和内容比较，共性通过抽象复用。模块 Cache SHALL 提供聚合导出和提交，直接修改子缓存也必须反映聚合待写状态。保存 SHALL 在邮箱回合内执行；成功通过 Cache 更新各 DataCache 基线，失败保留最新内存及待写标记；仓库仅接收不可变导出值，不得持有业务状态。

#### Scenario: 合并与恢复原值
- **WHEN** 同表多次修改后保存，或修改后恢复到保存基线
- **THEN** 分别只写最终变化内容，或不执行文件写入；未变化表不写入

#### Scenario: 保存期间与重试
- **WHEN** 保存暂时阻塞或失败
- **THEN** 本分片后续回合等待，其他分片仍可工作；失败不丢失最新内存，修复后可重试并重启恢复

#### Scenario: 子缓存修改与导出隔离
- **WHEN** Handler 在所属回合修改一个 DataCache，随后导出并再次修改
- **THEN** 模块待写状态立即反映变化，已导出的不可变数据保持原值；默认数组按空数组处理

### Requirement: 地图对象快照与变化块
WorldCache SHALL 组织固定规格及 DataCacheWorldObject；后者 SHALL 独占对象数组、保存基线和待写块，通过 Commit 替换当前值，默认数组按空数组。DataCacheWorldObject SHALL 按规格分组并按内容比较，不使用数组引用相等；跨块移动标记来源和目标，恢复原值取消待写。WorldCache SHALL 导出不可变 DataWorldSnapshot 供仓库使用。对象 SHALL 保留 ObjectId、Revision、X/Y、MonsterTypeId、ItemId、Count，不增加编号重复、数值范围、载荷组合或道具引用校验。

#### Scenario: 合并移动删除与恢复
- **WHEN** 连续修改、跨块移动、删除末个对象，或恢复保存基线
- **THEN** 仅最终变化块待写，移动来源与目标均待写，空块引用移除；恢复原值不执行写入

#### Scenario: 独立数据语义
- **WHEN** 可解析对象包含重复编号、负值、地图外坐标或混合载荷
- **THEN** 按独立 V2 数据读写，不调用旧版对象或地图业务校验
