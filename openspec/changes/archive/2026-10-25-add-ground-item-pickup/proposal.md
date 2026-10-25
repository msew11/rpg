# Proposal

## Why

地面道具已有显示与共同落地机制，需要补齐取回背包的操作，以及能识别可拾取物品的轮廓和鼠标反馈。单件丢弃无需选择数量，简化已经明确的操作。

## What Changes

- 左键点击地面道具，沿既有移动流程走近，一个逻辑瓦片内由服务端整份拾取。
- 优先补同类堆叠再占空格，空间不足整份失败；复用背包与地图共同提交、回滚、AOI 通知。
- 鼠标悬停地面图标或名称时沿图标 alpha 轮廓高亮，鼠标变为 Fetch，UI 覆盖、失焦、离开对象后清理。
- 数量为 1 时直接丢弃，多个时仍输入数量。

## Capabilities

### New Capabilities

- `ground-item-pickup`: 自动走近、权威整份拾取、悬停轮廓与 Fetch 反馈。

### Modified Capabilities

- `ground-item-drop`: 单件直接丢弃，地面交互可扩展到拾取。

## Impact

追加 bag.proto 协议，MockServer 增加绑定移动意图的待拾取目标，World 经 Session 发送已有移动及拾取请求；地面表现提供只读命中查询，MainController 协调点击及结果反馈，PointerCursor 复用 Fetch 资源。保留九服务、唯一地图缓存和既有文件事务，无拾取数量选择或通用调度框架。
