# client-settings Specification

## Purpose
TBD - created by archiving change data-driven-rpg-client. Update Purpose after archive.

## Requirements

### Requirement: 系统设置本地持久化
系统设置 SHALL 独立文件保存，按显示与声音页签分类，至少允许调整分辨率与音量。

#### Scenario: 重启后保留
- **WHEN** 修改并保存系统设置后重启客户端
- **THEN** 分辨率与音量采用保存值

### Requirement: 角色 HUD 设置隔离
每个角色 SHALL 按 `role_id` 保存角色设置。三条 9 格技能栏默认只显示第一条，其他两条可分别开启；HUD 调整模式 SHALL 可移动技能栏、系统按钮条、角色头像、小地图和聊天框，并保存相对屏幕位置。

#### Scenario: 角色间独立
- **WHEN** 修改角色 A 的技能栏显隐和 HUD 位置，再选择角色 B
- **THEN** 角色 B 使用自身设置或默认值，切回 A 时恢复 A 的设置

#### Scenario: 分辨率变化
- **WHEN** 保存 HUD 布局后改变分辨率
- **THEN** 可调元素保持在可视区域内

#### Scenario: 完成和退出调整
- **WHEN** 进入 HUD 调整并移动任意元素
- **THEN** 松开鼠标不会保存；完成 HUD 调整保存全部草稿；退出 HUD 调整丢弃草稿并还原进入前布局；离开界面未完成的调整也不保存

#### Scenario: 调整外恢复默认
- **WHEN** 未进入 HUD 调整时恢复默认布局
- **THEN** 全部可调元素恢复默认位置并立即保存

#### Scenario: 调整内恢复默认
- **WHEN** 进入 HUD 调整后恢复默认布局
- **THEN** 仅将草稿恢复默认位置，不保存；完成后保存默认布局，退出则恢复进入前布局

### Requirement: 启动窗口居中
客户端 SHALL 在启动并应用已保存的窗口分辨率后，将游戏窗口居中到当前所在显示器；嵌入运行时由引擎在宿主视口内居中。

#### Scenario: 使用保存分辨率启动
- **WHEN** 玩家打开独立游戏窗口
- **THEN** 窗口使用保存的分辨率并在当前显示器中央显示
