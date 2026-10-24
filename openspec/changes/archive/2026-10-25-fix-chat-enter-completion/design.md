## Context

现有候选处理仅拦截 Tab、上下键及 Escape，回车进入 LineEdit 默认提交。

## Goals / Non-Goals

候选可见时回车确认当前项；无候选时仍提交。不改变发送按钮、命令格式或 IME 组合处理。

## Decisions

在 OnInputKey 中将 Enter 和 KpEnter 复用 Tab 的补全分支并消费事件，以阻止同一次按键继续触发 TextSubmitted。保留现有发送入口，避免为键盘补全修改按钮行为。

真实按键验收确认 Godot 默认未将小键盘回车作为输入框提交；无候选时显式将 KpEnter 接入既有 Submit，主键盘回车继续使用默认 TextSubmitted。

## Risks / Trade-offs

补全后候选框可能立即切换到下一阶段 → 用连续真实回车验证每次只完成一个阶段，并验证输入记录、背包版本及世界目标不变；覆盖无候选时的正常发送。
