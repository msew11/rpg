"""解码角色原素材的五方向头部、身体像素块边界及挂接坐标。"""

import struct

from PIL import Image


def read_header(data):
    """校验已支持的 DIMG 版本并返回画布与帧数。"""
    magic, tag, version, header, count, directions = struct.unpack_from("<6I", data)
    if (magic, tag, version, header, directions) != (0x1A465151, 0x474D4944, 0x10002, 36, 5):
        raise ValueError("不支持的 DIMG 头部")
    width, height = struct.unpack_from("<2i", data, header)
    return width, height, count


def frame_end(data, offset, width, height):
    """校验 ARGB4444 像素块边界，返回块尾。"""
    x, y, flags, w, h, stride, kind = struct.unpack_from("<7i", data, offset)
    end = offset + 28 + stride * h
    if not (0 <= x < width and 0 <= y < height and flags in (0, 256)
            and 0 < w <= width - x and 0 < h <= height - y
            and stride == w * 2 and kind == 1 and end + 12 <= len(data)):
        raise ValueError(f"无效像素块：{offset}")
    return end


def read_heads(path):
    """解码五方向头部，并读取各帧脖颈连接点。"""
    data = path.read_bytes()
    width, height, count = read_header(data)
    if count != 5:
        raise ValueError("头部必须每方向一帧")
    frames = []
    offset = 64
    while offset + 40 <= len(data):
        try:
            end = frame_end(data, offset, width, height)
        except ValueError:
            offset += 2
            continue
        x, y, _, w, h, _, _ = struct.unpack_from("<7i", data, offset)
        rgba = bytearray()
        for (pixel,) in struct.iter_unpack("<H", data[offset + 28:end]):
            rgba.extend((((pixel >> 8) & 15) * 17, ((pixel >> 4) & 15) * 17,
                         (pixel & 15) * 17, ((pixel >> 12) & 15) * 17))
        image = Image.new("RGBA", (width, height))
        image.paste(Image.frombytes("RGBA", (w, h), bytes(rgba)), (x, y))
        anchor_x, anchor_y = struct.unpack_from("<2i", data, end)
        frames.append((image, (x + anchor_x, y + anchor_y)))
        # 跳过像素块，后续染色副本不符合原画布坐标约束，不作为动画帧。
        offset = end
    if len(frames) != count:
        raise ValueError(f"头部解码帧数不符：{path}")
    return frames
