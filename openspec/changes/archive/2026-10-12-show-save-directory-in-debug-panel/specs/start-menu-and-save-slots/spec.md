# Spec Delta

## ADDED Requirements

### Requirement: 调试面板显示存档目录
项目 SHALL 在 Debug 面板显示当前正式存档槽位的本机绝对目录路径，并允许玩家选中复制该路径；该信息 SHALL 与实际使用的存档目录一致。

#### Scenario: 默认存档目录
- **WHEN** 玩家打开游戏并查看 Debug 面板
- **THEN** 面板显示五个正式槽位对应的绝对存档目录

#### Scenario: 自定义存档目录
- **WHEN** 运行环境为存档管理器指定了其他目录
- **THEN** Debug 面板显示该目录的绝对路径，不继续显示默认目录
