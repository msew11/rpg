"""导入怪物选中的原始六帧红色圆环，保留来源和定位证据。"""

import argparse
import hashlib
import json
from pathlib import Path
import struct

from PIL import Image

from decode_actor_heads import preserve_source_pixels


def decode_ring(path):
    """顺序解码 DIMG v1 的连续帧，校验裁切块与末尾帧数，不扫描猜测像素块。"""
    data = path.read_bytes()
    if struct.unpack_from('<6I', data) != (0x1A465151, 0x474D4944, 0x10001, 36, 6, 1):
        raise ValueError('不支持的圆环头部')
    width, height, anchor_x, anchor_y = struct.unpack_from('<4i', data, 36)
    frames = []
    offset = 64
    for _ in range(6):
        x, y, flags, w, h, stride, kind = struct.unpack_from('<7i', data, offset)
        end = offset + 28 + stride * h
        if not (flags == 0 and kind == 1 and stride == w * 2 and
                0 <= x < width and 0 <= y < height and 0 < w <= width - x and
                0 < h <= height - y and end <= len(data)):
            raise ValueError('不支持或不完整的圆环帧')
        rgba = bytearray()
        for (pixel,) in struct.iter_unpack('<H', data[offset + 28:end]):
            rgba.extend((((pixel >> 8) & 15) * 17, ((pixel >> 4) & 15) * 17,
                         (pixel & 15) * 17, ((pixel >> 12) & 15) * 17))
        canvas = Image.new('RGBA', (width, height))
        canvas.paste(Image.frombytes('RGBA', (w, h), bytes(rgba)), (x, y))
        frames.append(canvas)
        offset = end
    if data[offset:] != struct.pack('<3I', 6, 0, 0):
        raise ValueError('圆环帧尾不符')
    return frames, (anchor_x, anchor_y)


def main():
    """从指定只读导出目录重建项目独立运行所需素材及来源清单。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    args = parser.parse_args()
    target = Path(__file__).resolve().parents[1] / 'assets/monsters/selection'
    target.mkdir(parents=True, exist_ok=True)
    ring_path = args.source / 'raw/magic/magic/magic9002.gso'
    frames, anchor = decode_ring(ring_path)
    outputs = []
    for index, frame in enumerate(frames):
        name = f'ring_{index}.png'
        frame.save(target / name)
        preserve_source_pixels(target / name)
        outputs.append(name)
    evidence = dict(ring=dict(source='magic/magic/magic9002.gso',
                             sha256=hashlib.sha256(ring_path.read_bytes()).hexdigest(),
                             outputs=outputs, width=frames[0].width, height=frames[0].height,
                             anchor=anchor, frames=len(frames), frame_seconds=0.1),
                    note='圆环保留原始公共锚点；0.1 秒帧时长为项目表现参数。鼠标素材由 assets/pointer/source.json 记录。')
    (target / 'source.json').write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
