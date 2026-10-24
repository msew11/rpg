## Why
通信与文件日志默认持续输出，需提供运行期控制并避免关闭时日志专用字符串分配。

## What Changes
- GM 增加控制台日志勾选开关。
- EnvService 保存内存标记，默认关闭，不持久化。
- LogService 在格式化前检查开关；关闭时跳过请求日志专用解码。

## Capabilities
### New Capabilities
无。
### Modified Capabilities
- `console-logging`: 增加运行期开关与关闭时的惰性生成约束。

## Impact
EnvService、LogService、NetService、服务构造、GM 面板及回归测试；持久化格式不变。
