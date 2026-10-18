## ADDED Requirements

### Requirement: 系统设置本地持久化
系统设置 SHALL 独立文件保存，按显示与声音页签分类，至少允许调整分辨率与音量。

#### Scenario: 重启后保留
- **WHEN** 修改并保存系统设置后重启客户端
- **THEN** 分辨率与音量采用保存值

### Requirement: 角色 HUD 设置隔离
每个角色 SHALL 按 `role_id` 保存角色设置。三条 9 格技能栏默认只显示第一条，其他两条可分别开启；HUD 调整模式 SHALL 可移动技能栏、系统按钮条和角色头像，并保存相对屏幕位置。

#### Scenario: 角色间独立
- **WHEN** 修改角色 A 的技能栏显隐和 HUD 位置，再选择角色 B
- **THEN** 角色 B 使用自身设置或默认值，切回 A 时恢复 A 的设置

#### Scenario: 分辨率变化
- **WHEN** 保存 HUD 布局后改变分辨率
- **THEN** 可调元素保持在可视区域内
