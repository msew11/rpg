"""从只读来源独立重建角色参考，生成C#黄金摘要并核对正式GPU逐帧输出。"""

import argparse
from functools import lru_cache
import hashlib
import json
from pathlib import Path
import re
import struct

import numpy as np
from PIL import Image

# 独立记录源客户端动作编号及图标对应系列，不导入生产导入器或表现算法。
MOTIONS = ('stand', 'run', 'sit', 'die', 'attack1', 'attack2', 'attack3', 'magic',
           'prepare1', 'prepare2', 'prepare3', 'hurt', 'ride')
BINDINGS = {1012: ('weaponr', 1, 5), 1013: ('weaponr', 41, 5), 1014: ('weaponl', 11, 0),
            1015: ('weaponr', 21, 6), 1016: ('weaponr', 31, 6),
            1017: ('weaponr', 51, 7), 1018: ('weaponl', 105, 7)}
# 原客户端0x5FFD60的15部件完整顺序由客户端只读提取并在运行时再次核验。
CLIENT_HASH = '2135e6295a4c4d75cfb96d5641c41d2180465e4882550eca9ac52f95a9485b11'
SLOTS = (0, 3, 5, 6)


@lru_cache(maxsize=None)
def decode(path):
    """独立解析原始DIMG像素及尾部挂点，不读取项目纹理或生产解码函数。"""
    data = path.read_bytes()
    magic, tag, version, header, count, directions = struct.unpack_from('<6I', data)
    assert (magic, tag, version, header, directions) == (0x1A465151, 0x474D4944, 0x10002, 36, 5)
    width, height = struct.unpack_from('<2i', data, 36)
    frames = []
    position = 64
    while position + 40 <= len(data):
        x, y, flags, w, h, stride, kind = struct.unpack_from('<7i', data, position)
        end = position + 28 + stride * h
        if not (0 <= x < width and 0 <= y < height and flags in (0, 256) and
                0 < w <= width-x and 0 < h <= height-y and stride == w*2 and kind == 1 and end+12 <= len(data)):
            position += 2
            continue
        pixels = np.frombuffer(data, dtype='<u2', count=w*h, offset=position+28)
        rgba = np.stack((((pixels >> 8) & 15)*17, ((pixels >> 4) & 15)*17,
                         (pixels & 15)*17, ((pixels >> 12) & 15)*17), axis=1).astype(np.uint8).reshape(h, w, 4)
        image = Image.new('RGBA', (width, height))
        image.paste(Image.fromarray(rgba), (x, y))
        anchor = struct.unpack_from('<2i', data, end)
        points = [tuple(v + d for v, d in zip(struct.unpack_from('<2i', data, end+offset), (x, y)))
                  for offset in (12, 28, 36)] if path.parent.name == 'npc' else []
        frames.append((image, (x+anchor[0], y+anchor[1]), points, position))
        position = end
    assert len(frames) == count, (path, len(frames), count)
    return frames


def layers(raw):
    """独立读取普通/g段全部层次，键按性别、身体动作、部件、方向、身体帧排列。"""
    result = {}
    for part, (folder, filename) in enumerate((('npc', 'Npc.ini'), ('head', 'Head.ini'),
                                              ('weaponl', 'Weaponl.ini'), ('weaponr', 'Weaponr.ini'))):
        sections = {}
        current = None
        for line in (raw / 'avatar' / folder / filename).read_text(encoding='ascii').splitlines():
            if line.startswith('['):
                current = line.strip()[1:-1]
                sections[current] = []
            elif line.startswith('50,'):
                row = [int(value) for value in line.strip().split(',') if value.strip()]
                assert row[0] == 50 and len(row) == 51
                sections[current].append(row[1:])
        for gender, suffix in enumerate(('', 'g')):
            for motion, name in enumerate(MOTIONS):
                assert len(sections[name+suffix]) == 5
                for direction, row in enumerate(sections[name+suffix]):
                    for frame, value in enumerate(row):
                        if value:
                            result[f'{gender}/{motion}/{part}/{direction}/{frame}'] = value
    return result


def weapon_source(raw, item, gender, motion, main=None):
    """按源系列及明确源手解析原生动作，未提供的一律回退站立。"""
    hand, series, _ = BINDINGS[item]
    if main is not None and item in (1017, 1018):
        hand = 'weaponr' if main else 'weaponl'
    number = series*100 + MOTIONS.index(motion)+1 + (50 if gender == 'female' else 0)
    source = raw / 'avatar' / hand / f'{hand}{number:04}.gsn'
    if not source.exists():
        number = series*100+1+(50 if gender == 'female' else 0)
        source = raw / 'avatar' / hand / f'{hand}{number:04}.gsn'
    return source, number % 100 - (50 if gender == 'female' else 0)


def fixture(raw, output):
    """从原始像素与连接点生成独立C#黄金记录，保留全部入口及原始文件哈希。"""
    records = []
    project = Path(__file__).resolve().parents[1]
    for item, (hand, _, _) in BINDINGS.items():
        variants = [('', None)]
        if item == 1017: variants.append(('left/', False))
        if item == 1018: variants.append(('right/', True))
        for subfolder, main in variants:
            for gender in ('male', 'female'):
                for motion in MOTIONS:
                    source, action = weapon_source(raw, item, gender, motion, main)
                    frames = decode(source)
                    digest = hashlib.sha256()
                    resource = f'assets/characters/weapons/{item}/{subfolder}{gender}/{motion}'
                    text = (project / (resource+'.tres')).read_text(encoding='utf-8')
                    actual_anchors = list(map(int, re.search(r'Anchors = PackedVector2Array\(([^)]*)', text)[1].split(',')))
                    assert actual_anchors == [value for _, point, _, _ in frames for value in point], resource
                    texture = re.search(r'type="Texture2D" path="res://([^"]+)"', text)[1]
                    atlas = Image.open(project / texture).convert('RGBA')
                    count = len(frames)//5
                    for index, (image, anchor, _, _) in enumerate(frames):
                        direction, frame = divmod(index, count)
                        rect = (frame*image.width, direction*image.height, (frame+1)*image.width, (direction+1)*image.height)
                        assert atlas.crop(rect).tobytes() == image.tobytes(), (resource, index, '原像素改变')
                        pixels = np.array(image)
                        digest.update(pixels.tobytes())
                        digest.update(struct.pack('<4i', *anchor, image.width, image.height))
                    records.append(dict(path='res://'+resource+'.tres', source=source.relative_to(raw).as_posix(),
                                        sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(), sourceAction=action,
                                        frames=len(frames), sha256=digest.hexdigest().upper()))
    output.write_text(json.dumps(dict(description='独立原始DIMG像素、连接点、尺寸黄金；层次取男女完整身体动作，不取武器回退动作。',
                                      resources=records, layers=layers(raw)), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(f'Independent source fixture passed: {len(records)} resources, {sum(r["frames"] for r in records)} frame entries.', flush=True)


def client_orders(client):
    """校验指定客户端版本，从PE节表定位0x5FFD60的完整基础顺序。"""
    data = client.read_bytes()
    assert hashlib.sha256(data).hexdigest() == CLIENT_HASH, '客户端版本与取证不符'
    pe = struct.unpack_from('<I', data, 0x3c)[0]
    sections, optional_size = struct.unpack_from('<H', data, pe+6)[0], struct.unpack_from('<H', data, pe+20)[0]
    section_table = pe+24+optional_size
    rva = 0x1FFD60
    for index in range(sections):
        size, address, raw_size, raw_offset = struct.unpack_from('<4I', data, section_table+40*index+8)
        if address <= rva < address+max(size, raw_size):
            values = struct.unpack_from('<120i', data, raw_offset+rva-address)
            return [[SLOTS.index(v) for v in values[row*15:(row+1)*15] if v in SLOTS] for row in range(8)]
    raise ValueError('找不到来源次序常量')


def verify_review(raw, body_manifest, client, review):
    """逐组核对正式GPU输出，参考完全由外部原始素材、层次及客户端常量生成。"""
    manifest = json.loads((review/'manifest.json').read_text(encoding='utf-8'))
    expected_groups = {(gender, main, off, motion) for gender in ('male', 'female')
                       for main in (0, 1012, 1013, 1015, 1016, 1017) for off in (0, 1014, 1017, 1018) for motion in MOTIONS}
    actual_groups = {(group['gender'], group['main'], group['off'], group['motion']) for group in manifest['groups']}
    assert actual_groups == expected_groups and len(manifest['groups']) == len(expected_groups), '来源合成覆盖缺失或重复'
    bodies = {(('male' if entry['gender']=='男性' else 'female'), entry['name']): entry
              for entry in json.loads(body_manifest.read_text(encoding='utf-8'))}
    table = layers(raw)
    orders = client_orders(client)
    cell = manifest['cell']
    width, height = cell['width'], cell['height']
    origin = (int(cell['anchorX']), int(cell['anchorY']))
    errors, frame_total, maximum, maximum_alpha = [], 0, 0, 0
    for number, group in enumerate(manifest['groups'], 1):
        gender, main, off = group['gender'], group['main'], group['off']
        motion = group['motion']
        category = BINDINGS.get(main or off, ('', 0, 0))[2]
        if category and motion in ('attack1', 'attack2', 'attack3'):
            motion = MOTIONS[category-1]
        if category and motion in ('prepare1', 'prepare2', 'prepare3'):
            motion = MOTIONS[category+3]
        assert group['actualMotion'] == motion
        body_info = bodies[gender, motion]
        body_frames = decode(raw/'avatar/npc'/body_info['source'])
        assert len(body_frames)//5 == group['count'] == body_info['framesPerDirection']
        head_paths = sorted(p for p in (raw/'avatar/head').glob('head*.gsn')
                            if (int(p.stem[4:]) % 100 == 51) == (gender=='female'))
        head_frames = decode(head_paths[manifest['headId']-1])
        gpu = Image.open(review/group['sheet']).convert('RGBA')
        expected_sheet = Image.new('RGBA', gpu.size)
        for direction in range(8):
            native_direction = (4, 0, 1, 2, 3, 2, 1, 0)[direction]
            mirrored = direction >= 5
            for frame in range(group['count']):
                body, _, points, offset = body_frames[native_direction*group['count']+frame]
                assert offset == body_info['frames'][native_direction*group['count']+frame]['sourceOffset']
                anchor = (body_info['anchorX'], body_info['anchorY'])
                head, head_anchor, _, _ = head_frames[native_direction]
                parts = {0: (body, (-anchor[0], -anchor[1])),
                         1: (head, (points[0][0]-anchor[0]-head_anchor[0], points[0][1]-anchor[1]-head_anchor[1]))}
                for item, part, hand_point in ((main, 3, 2), (off, 2, 1)):
                    if not item: continue
                    source, action = weapon_source(raw, item, gender, motion, part==3)
                    weapon_frames = decode(source)
                    count = len(weapon_frames)//5
                    image, weapon_anchor, _, _ = weapon_frames[native_direction*count+frame % count]
                    sample = group['samples'][direction]['frames'][frame]['main' if part==3 else 'off']
                    assert sample['sourceAction'] == action and sample['sourceFrame'] == frame % count
                    parts[part] = image, tuple(points[hand_point][i]-anchor[i]-weapon_anchor[i] for i in (0, 1))
                order = sorted(orders[(direction+7) % 8], key=lambda part:
                    table.get(f'{int(gender=="female")}/{MOTIONS.index(motion)}/{part}/{native_direction}/{frame}', 0))
                expected = Image.new('RGBA', (width, height))
                for part in order:
                    if part not in parts: continue
                    image, (x, y) = parts[part]
                    if mirrored:
                        image = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                        x = -x-image.width
                    expected.alpha_composite(image, (origin[0]+x, origin[1]+y))
                expected_sheet.paste(expected, (frame*width, direction*height))
                frame_total += 1
        # 用预乘颜色比较，避免透明边缘RGB未定义；alpha与几何仍单独严格核验。
        actual = np.array(gpu).astype(np.int16)
        reference = np.array(expected_sheet).astype(np.int16)
        alpha_error = np.abs(actual[:, :, 3]-reference[:, :, 3])
        premul_a = actual[:, :, :3].astype(np.int32)
        if manifest.get('alphaEncoding') == 'straight':
            premul_a = premul_a*actual[:, :, 3:4]//255
        premul_b = reference[:, :, :3].astype(np.int32)*reference[:, :, 3:4]//255
        color_error = np.max(np.abs(premul_a-premul_b), axis=2)
        bad = (alpha_error > 1) | (color_error > 2)
        maximum = max(maximum, int(color_error.max()))
        maximum_alpha = max(maximum_alpha, int(alpha_error.max()))
        if bad.any():
            diff_dir = review/'reference-diffs'/group['key']
            diff_dir.parent.mkdir(parents=True, exist_ok=True)
            expected_sheet.save(diff_dir.with_suffix('.reference.png'))
            diff = np.zeros_like(actual, dtype=np.uint8)
            diff[bad] = (255, 30, 30, 255)
            Image.fromarray(diff).save(diff_dir.with_suffix('.diff.png'))
            errors.append(dict(key=group['key'], pixels=int(bad.sum()), maxAlpha=int(alpha_error.max()), maxColor=int(color_error.max())))
        if number % 26 == 0: print(f'Reference compared {number}/{len(manifest["groups"])} groups, {frame_total} frames, failures={len(errors)}', flush=True)
    assert frame_total == manifest['frameCount'], '来源合成总帧数与清单不符'
    report = dict(clientSha256=CLIENT_HASH, bodyManifestSha256=hashlib.sha256(body_manifest.read_bytes()).hexdigest(),
                  groups=len(manifest['groups']), frames=frame_total, maxAlphaDifference=maximum_alpha,
                  combinations=len(manifest['equipment']), maxPremultipliedColorDifference=maximum,
                  alphaTolerance=1, colorTolerance=2, failures=errors)
    (review/'source-reference-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    assert not errors, f'{len(errors)} groups differ; see source-reference-report.json and reference-diffs/'
    print(f'Independent GPU reference passed: {frame_total} frames, {len(manifest["groups"])} groups.', flush=True)


def main():
    """仅显式指定时写黄金，其余模式只读来源并输出build审查报告。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, required=True)
    parser.add_argument('--write-fixture', type=Path)
    parser.add_argument('--body-manifest', type=Path)
    parser.add_argument('--client', type=Path)
    parser.add_argument('--review', type=Path)
    args = parser.parse_args()
    if args.write_fixture: fixture(args.raw, args.write_fixture)
    if args.review:
        if not args.client or not args.body_manifest: parser.error('GPU对照需要--client与--body-manifest')
        verify_review(args.raw, args.body_manifest, args.client, args.review)


if __name__ == '__main__':
    main()
