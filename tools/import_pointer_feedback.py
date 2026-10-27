"""将指定 IANI 鼠标素材与地面光圈拼成完整画布，保留原始来源证据。"""

import argparse
import hashlib
import json
import struct
from pathlib import Path

from PIL import Image


def decode_iani(path):
    """顺序解码 IANI v1 的全部帧和图块，拒绝不完整或未知格式。"""
    data = path.read_bytes()
    magic, tag, version, header, tile_w, tile_h, width, height, count = struct.unpack_from('<9I', data)
    if (magic, tag, version, header) != (0x1A465151, 0x494E4149, 0x10000, 36):
        raise ValueError('不支持的 IANI 头部')
    if not all(0 < value <= 4096 for value in (tile_w, tile_h, width, height, count)):
        raise ValueError('无效的 IANI 尺寸或帧数')
    frames = []
    offset = header
    for _ in range(count):
        canvas = Image.new('RGBA', (width, height))
        for y in range(0, height, tile_h):
            for x in range(0, width, tile_w):
                w, h, stride, kind = struct.unpack_from('<4I', data, offset)
                offset += 16
                if (w, h, stride, kind) != (tile_w, tile_h, tile_w * 2, 1):
                    raise ValueError('不支持的 IANI 图块')
                end = offset + stride * h
                if end > len(data):
                    raise ValueError('IANI 图块不完整')
                rgba = bytearray()
                for (pixel,) in struct.iter_unpack('<H', data[offset:end]):
                    rgba.extend((((pixel >> 8) & 15) * 17, ((pixel >> 4) & 15) * 17,
                                 (pixel & 15) * 17, ((pixel >> 12) & 15) * 17))
                canvas.paste(Image.frombytes('RGBA', (w, h), bytes(rgba)), (x, y))
                offset = end
        frames.append(canvas)
    if offset != len(data):
        raise ValueError('IANI 文件存在未解析数据')
    return frames


def main():
    """从只读导出目录生成运行资源、源哈希与首帧播放约定。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    args = parser.parse_args()
    target = Path(__file__).resolve().parents[1] / 'assets/pointer'
    target.mkdir(parents=True, exist_ok=True)
    records = []
    for source, name in [('res/uires/cursor/Normal.gsa', 'normal'), ('res/effect/circle.gsa', 'circle'),
                         ('res/uires/cursor/Click.gsa', 'click'), ('res/uires/cursor/Fetch.gsa', 'fetch'),
                         ('res/uires/cursor/Edit.gsa', 'edit')]:
        path = args.source / 'raw' / source
        frames = decode_iani(path)
        outputs = [f'{name}_{index}.png' for index in range(len(frames))] if name in ('click', 'fetch') else [f'{name}.png']
        for frame, output in zip(frames, outputs):
            frame.save(target / output)
        records.append(dict(source=source, sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                            frames=len(frames), width=frames[0].width, height=frames[0].height,
                            output=outputs if name in ('click', 'fetch') else outputs[0], selected_frame='all' if name in ('click', 'fetch') else 0))
        if name == 'fetch':
            records[-1]['hotspot'] = [6, 6]
        if name == 'edit':
            records[-1]['hotspot'] = [5, 8]
    config = args.source / 'raw/res/uires/uiroot.lua'
    manifest = dict(resources=records, cursor_config=dict(source='res/uires/uiroot.lua',
                    sha256=hashlib.sha256(config.read_bytes()).hexdigest(), hotspot=[2, 2], holdframe=1),
                    circle_fade_seconds=0.5, cursor_display_scale=1.25, text_cursor_display_height=32, click_frame_seconds=0.08,
                    note='circle 为单帧完整画布；淡出由客户端实现，原版用途未确认。Click 按下播放两帧，释放恢复 Normal；Fetch 第一帧悬停，第二帧握住拖动，热点 (6,6)；Edit 首帧用于文本输入 I 形鼠标，热点 (5,8)。')
    (target / 'source.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
