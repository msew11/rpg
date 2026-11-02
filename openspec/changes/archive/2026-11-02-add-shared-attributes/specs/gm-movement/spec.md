## Purpose

提供开发期当前角色的移速覆盖命令及可见勾选控件，确保覆盖设置由服务器按角色保存并在重启后恢复，同时维持多人移动和正常属性计算之间的明确边界。

## ADDED Requirements

### Requirement: 命令覆盖与角色保存
服务端 SHALL 支持 movespeed <有限非负数> 和 movespeed off，只影响当前控制角色。每次成功回复前 SHALL 将开关和速度写入该角色 .role.json，off 保留最近速度。重进及重启 SHALL 恢复设置；不得限制滑条范围、属性上限或小数位，也不做额外 GM 权限检查。

#### Scenario: 高速与重启
- **WHEN** 纯客户端执行 movespeed 12345.125 并在成功后重启服务器
- **THEN** 当前角色恢复该覆盖，其他角色不变，正常属性 map 不变

#### Scenario: 关闭与保存失败
- **WHEN** 执行 movespeed off 或角色文件无法写入
- **THEN** 前者保存关闭且保留预设值；后者不提示保存成功并允许重试

### Requirement: 面板使用服务器状态
GM 面板 SHALL 提供启用勾选及速度控件，未启用时禁用调速。进入响应及命令反馈 SHALL 携带保存的 GM 状态，面板操作通过现有 Gm/GmRt 发命令；EnterWorld 请求不得增加 GM 字段，本地 .env 不再保存这两个状态。

#### Scenario: 切角恢复
- **WHEN** 两个角色分别保存启用与关闭状态并切换
- **THEN** 面板显示各角色服务器设置，关闭恢复正常属性移速而非本地环境速度
