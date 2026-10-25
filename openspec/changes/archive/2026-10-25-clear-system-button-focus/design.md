## Context
CloseSystemMenu 调用 _systemButton.GrabFocus，导致系统设置打开后入口按钮保持高亮。
## Goals / Non-Goals
仅清除菜单关闭造成的系统按钮焦点残留，不禁用按钮或原生悬停、键盘操作。
## Decisions
菜单隐藏后改为 ReleaseFocus。覆盖真实系统按钮点击、设置条目点击和 Esc 关闭路径。
## Risks / Trade-offs
不再自动把键盘焦点交回入口；显式键盘导航继续使用原生规则。
