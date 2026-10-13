# Spec Delta

## ADDED Requirements

### Requirement: 素体动作资源完整性

项目 SHALL 为两种 ErisEsra 素体分别提供待机、行走、跑步、攻击、交互、跳跃和转身的独立 PNG 精灵表，并记录来源、帧布局与当前游戏中的播放状态。

#### Scenario: 查找素体动作

- **WHEN** 开发者查看任一尺寸的角色模板资源目录
- **THEN** 七种动作各有独立 PNG，且来源说明能区分已播放与待接入的动作
