## 在游戏画面按顺序显示系统消息。
class_name ChatPanel
extends PanelContainer

## 显示消息历史的文本区域。
@onready var history: RichTextLabel = $Content/History


## 追加一条系统消息并滚动到底部。
func add_system_message(message: String) -> void:
	history.add_text("[系统] %s\n" % message)
	history.scroll_to_line(history.get_line_count() - 1)


## 新会话开始时清空旧消息。
func clear_messages() -> void:
	history.clear()
