## Context
格子原为 44×36 PanelContainer，默认主题同时填充单格与外层技能栏背景。
## Goals / Non-Goals
只调整技能栏几何和背景，不改技能业务或 HUD 操作。
## Decisions
格子保持现有 44 像素宽度并将高度设为 44；使用仅有边框的 StyleBoxFlat，外层用空样式避免透出另一层不透明背景。保留格内编号并居中，默认底部布局继续按自然尺寸计算。
## Risks / Trade-offs
格子增加高度后默认三条栏间隔继续按实际尺寸计算；通过现有布局检查和 Windows 图形截图验证。
