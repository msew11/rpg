# 设计

EnvService 持有速度和 `RPG_MOVEMENT_SPEED`，复用现有单键 `.env` 保存逻辑。读取时仅接受 60–360 范围内 10 的倍数，缺失或无效使用 180；写入成功后才更改内存。

客户端 WorldManager 和模拟服务端 MockServer 均绑定同次启动 EnvService 的速度读取委托，每帧使用最新值。独立测试中的实例继续使用 180 默认值。GM HSlider 显示当前整数速度，滑动保存失败时恢复原值并提示。
