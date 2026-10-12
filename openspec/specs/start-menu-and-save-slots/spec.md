# start-menu-and-save-slots Specification

## Purpose
TBD - created by archiving change add-start-menu-and-save-slots. Update Purpose after archive.

## Requirements

### Requirement: 标题入口
项目 SHALL 从 `scenes/main.tscn` 启动时显示开始界面，提供“开始游戏”和“读取进度”选项，且标题界面不推进玩家移动。

#### Scenario: 开始新游戏
- **WHEN** 玩家点击“开始游戏”
- **THEN** 系统创建处于初始位置、初始测试地图状态的新会话，并显示可操作的世界

#### Scenario: 返回标题
- **WHEN** 玩家在游戏中返回标题并再次开始游戏
- **THEN** 系统创建新会话，先前未保存的运行状态不进入新游戏

### Requirement: 五个本地存档槽位
项目 SHALL 在读取与保存界面固定提供编号 1 至 5 的独立槽位；游戏中 SHALL 能将当前会话的临时存档与活跃状态合并后保存到指定槽位，并允许覆盖该槽位。

#### Scenario: 保存不同进度
- **WHEN** 玩家把不同世界状态分别保存到两个槽位
- **THEN** 两个槽位各自保留保存时的状态，且界面显示其占用状态

#### Scenario: 空槽位
- **WHEN** 玩家从标题页查看尚未保存的槽位
- **THEN** 该槽位显示为空且不能读取

### Requirement: 恢复本地进度
项目 SHALL 从有效槽位建立会话临时存档，恢复玩家连续位置、朝向和已改变的测试地块，且 SHALL 在读取前验证文件结构与当前测试地图的兼容性。

#### Scenario: 跨运行读取
- **WHEN** 玩家保存后退出游戏并重新启动，选择该槽位
- **THEN** 玩家位置、朝向和地块改动恢复到保存时状态

#### Scenario: 无效存档
- **WHEN** 槽位文件损坏或与当前测试地图不兼容
- **THEN** 界面将其标为不可读取，当前会话不被部分覆盖
