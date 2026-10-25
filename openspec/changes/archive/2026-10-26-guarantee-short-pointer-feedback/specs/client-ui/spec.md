## MODIFIED Requirements

### Requirement: Normal 普通鼠标
客户端 SHALL 在左键按下时立即播放 Click 两帧，每帧 0.08 秒。短按松开后仍完成两帧再恢复 Normal；长按保持末帧至释放后立即恢复 Normal。输入行为不因反馈延迟。

#### Scenario: 同帧点击与连续点击
- **WHEN** 左键按下和松开发生在同一渲染帧，或前一次短按动画未结束时再次点击
- **THEN** Click 两帧仍可见，新点击从首帧重新播放，实际点击行为即时执行
