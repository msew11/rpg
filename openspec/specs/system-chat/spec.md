# system-chat Specification

## Purpose
提供游戏内可见的系统消息记录，让保存和加载等运行错误在玩家继续游玩时仍可查看，并为后续聊天消息提供独立的展示区域。

## Requirements

### Requirement: 游戏内系统消息
系统 SHALL 在游戏画面提供 Chat 面板，并按发生顺序显示系统消息；新会话 SHALL 不继承旧会话的消息。

#### Scenario: 存档失败
- **WHEN** 保存正式槽位或卸载区块写入临时存档失败
- **THEN** Chat 面板显示说明失败和可能丢失数据的系统消息，游戏仍可继续操作

#### Scenario: 新游戏
- **WHEN** 玩家放弃当前会话并开始新游戏
- **THEN** 旧会话的系统消息不出现在新会话中
