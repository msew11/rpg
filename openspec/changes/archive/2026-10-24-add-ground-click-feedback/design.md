## Context

MainController 在 _UnhandledInput 逆投影点击并交给 WorldService。UI 消费的输入不进入该路径。原始素材是 IANI v1：头部记录图块、完整画布及帧数，circle 是 6 个 32×32 图块拼成的单帧 68×41 图像，Normal 是 41×35 两帧，uiroot.lua 指定 holdframe=1、热点 (2, 2)。

## Goals / Non-Goals

实现指定普通指针和完整地面光圈。没有新增攻击、拾取指针状态或移动规则；不声称已确认 circle 的原版用途。

## Decisions

- 导入工具严格顺序解码 IANI 的 ARGB4444 图块并拼接、裁掉边缘填充，校验完整帧数和文件末尾；运行只加载项目内 PNG。
- 主场景启动时通过 Input.SetCustomMouseCursor 设置硬件普通鼠标和热点，退出时恢复。避免首次导入前项目设置提前加载尚未导入的 PNG。登录、选角和世界共用普通指针。Normal 按原 holdframe 使用首帧。
- circle 只有单帧，使用 Tween 在 0.5 秒内淡出并释放。保留原始尺寸，中心对齐点击世界坐标；挂在世界根节点、地图上方和角色下方，跟随摄像机自然变换。每次点击可独立完成反馈，离开世界随父节点清理。
- 只在 HandleInput 成功后创建反馈；右键、松开、UI 点击及未进入世界均不创建。

## Risks / Trade-offs

- 原始预览仅含首图块 → 使用完整画布解码并目视验证；记录源哈希与完整尺寸。
- 未找到原版调用及特效时长 → 0.5 秒淡出为本项目表现参数，后续可以调整。
