## MODIFIED Requirements

### Requirement: Normal 普通鼠标
客户端 SHALL 在登录、选角与游戏世界使用指定 Normal.gsa 的首帧作为普通鼠标，原始热点为 (2, 2)，资源随项目发布。普通与按下鼠标 SHALL 统一按 1.25 倍最近邻缩放至 51×44，显示热点同步缩放至 (2.5, 2.5)。客户端 SHALL 在左键按下时立即播放完整 Click.gsa 两帧，按每帧 0.08 秒播放并保持末帧至释放；释放后恢复 Normal，登录、选角与游戏 UI 均生效。

#### Scenario: 全流程普通指针
- **WHEN** 启动客户端并进入登录、选角或游戏世界
- **THEN** 普通鼠标显示放大约 125% 的完整 Normal 首帧，点击热点同步缩放

#### Scenario: UI 按下反馈
- **WHEN** 左键按下在任意页面及背包物品上，随后释放
- **THEN** 按下立即使用 Click 动画，释放恢复 Normal，不受 GUI 消费影响
