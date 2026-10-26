"""整理正式角色实绘，交付离线逐帧浏览页、中文联系表、独立PNG与审查ZIP。"""

import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import zipfile

from PIL import Image, ImageDraw, ImageFont


def fonts():
    """选择可显示中文和方向箭头的本机字体，缺失时明确报错。"""
    for path in [Path('C:/Windows/Fonts/msyh.ttc'), Path('/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc')]:
        if path.exists():
            return ImageFont.truetype(str(path), 18), ImageFont.truetype(str(path), 14)
    raise RuntimeError('缺少中文字体，请安装微软雅黑或Noto Sans CJK')


def build(root, replace_archive=False):
    """逐组裁切并核验每帧，整理完整清单，最后验证ZIP中的全部文件。"""
    manifest = json.loads((root / 'manifest.json').read_text(encoding='utf-8'))
    (root / '.gdignore').touch()
    archive = root.with_suffix('.zip')
    if archive.exists() and not replace_archive:
        raise FileExistsError(f'压缩包已存在：{archive}')
    groups = manifest['groups']
    expected_groups = len(manifest['equipment']) * len(manifest['genders']) * len(manifest['motions'])
    if len(groups) != expected_groups or len({g['key'] for g in groups}) != expected_groups:
        raise ValueError('审查分组缺失或重复')
    equipment = {entry['key']: entry['name'] for entry in manifest['equipment']}
    motions = {entry['key']: entry['label'] for entry in manifest['motions']}
    genders = {entry['key']: entry['label'] for entry in manifest['genders']}
    width, height = manifest['cell']['width'], manifest['cell']['height']
    title_font, label_font = fonts()
    total = 0
    coverage = []
    browser_groups = []
    for number, group in enumerate(groups, 1):
        image = Image.open(root / group['sheet']).convert('RGBA')
        count = group['count']
        if image.size != (width * count, height * 8):
            raise ValueError(f'联系表尺寸错误：{group["key"]}')
        frame_dir = root / 'frames' / group['key']
        frame_dir.mkdir(parents=True, exist_ok=True)
        labelled_path = Path('labelled') / (group['key'] + '.png')
        (root / labelled_path).parent.mkdir(parents=True, exist_ok=True)
        labelled = Image.new('RGB', (88 + width * count, 76 + height * 8), '#202b36')
        draw = ImageDraw.Draw(labelled)
        title = f'{genders[group["gender"]]} / {motions[group["motion"]] + "→" + motions[group["actualMotion"]]} / {equipment[group["equipment"]]} / {count}帧'
        fitted_title = title_font
        while draw.textlength(title, font=fitted_title) > labelled.width - 28 and fitted_title.size > 10:
            fitted_title = title_font.font_variant(size=fitted_title.size - 1)
        if draw.textlength(title, font=fitted_title) > labelled.width - 28:
            raise ValueError(f'联系表标题过长：{title}')
        draw.text((14, 10), title, font=fitted_title, fill='#f3f6fa')
        draw.text((14, 39), f'统一定位点 · 原始1倍像素 · 帧号从0开始 · 头型{manifest["headId"]}', font=label_font, fill='#9eb0c3')
        for frame in range(count):
            draw.text((88 + frame * width + 10, 57), f'帧 {frame:03}', font=label_font, fill='#c7d5e4')
        for direction in manifest['directions']:
            index = direction['index']
            draw.text((10, 76 + index * height + 12), direction['label'], font=label_font, fill='#b9d9f4')
            for frame in range(count):
                rect = (frame * width, index * height, (frame + 1) * width, (index + 1) * height)
                cell = image.crop(rect)
                alpha = cell.getchannel('A')
                used = alpha.getbbox()
                if not used or min(used[0], used[1], width - used[2], height - used[3]) < 2:
                    raise ValueError(f'帧为空或可见像素接近边界：{group["key"]}/{index}/{frame}')
                frame_path = frame_dir / f'd{index:02}-{direction["key"]}-f{frame:03}.png'
                cell.save(frame_path, compress_level=2)
                with Image.open(frame_path) as saved:
                    if saved.convert('RGBA').tobytes() != cell.tobytes():
                        raise ValueError(f'独立帧像素与实绘联系表不一致：{frame_path}')
                x, y = 88 + frame * width, 76 + index * height
                labelled.paste(cell, (x, y), cell)
                draw.rectangle((x, y, x + width - 1, y + height - 1), outline='#384757')
                total += 1
        labelled.save(root / labelled_path, compress_level=3)
        group['labelled'] = labelled_path.as_posix()
        coverage.append([group['gender'], group['equipment'], group['main'], group['off'], group['motion'], group['actualMotion'], count, 8, count * 8, group['fps'], group['sheet'], group['labelled']])
        # 浏览页只保留每方向资源和逐帧采样号；完整源证据仍留在manifest.json。
        light = {key: group[key] for key in ['key', 'gender', 'equipment', 'main', 'off', 'motion', 'actualMotion', 'count', 'fps', 'loop', 'sheet', 'labelled']}
        light['durations'] = [entry['duration'] for entry in group['samples'][0]['frames']]
        light['sources'] = []
        for direction in group['samples']:
            hands = {}
            for hand in ['main', 'off']:
                entries = [entry[hand] for entry in direction['frames']]
                hands[hand] = None if entries[0] is None else {
                    'sourceAction': entries[0]['sourceAction'], 'sourceRightHand': entries[0]['sourceRightHand'], 'resource': entries[0]['resource'], 'count': entries[0]['framesPerDirection'],
                    'frames': [entry['sourceFrame'] for entry in entries]}
            light['sources'].append(hands)
        browser_groups.append(light)
        if number % 26 == 0:
            print(f'整理 {number}/{len(groups)} 组，{total} 张单帧', flush=True)
    if total != manifest['frameCount']:
        raise ValueError(f'帧数不符：{total} != {manifest["frameCount"]}')
    manifest['validation'] = {'allGroupsPresent': True, 'uniqueKeys': True, 'allFramesNonEmpty': True,
                              'allFramesWithinCanvas': True, 'allSavedFramePixelsMatchSheets': True,
                              'frameFiles': total, 'labelledSheets': len(groups)}
    (root / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    browser = {key: manifest[key] for key in ['generatedAt', 'revision', 'headId', 'cell', 'directions', 'equipment', 'motions', 'genders', 'groupCount', 'frameCount']}
    browser['groups'] = browser_groups
    (root / 'manifest.js').write_text('window.ACTOR_REVIEW=' + json.dumps(browser, ensure_ascii=False, separators=(',', ':')) + ';\n', encoding='utf-8')
    (root / 'index.html').write_text((Path(__file__).parent / 'actor_review.html').read_text(encoding='utf-8'), encoding='utf-8')
    with (root / 'coverage.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['性别', '装备组合', '主手ID', '副手ID', '请求动作', '实际身体动作', '身体帧数', '方向数', '单帧图数', 'FPS', '原始联系表', '中文联系表'])
        writer.writerows(coverage)
    (root / '使用说明.txt').write_text(
        f'角色完整逐帧审查包\n\n双击index.html，可离线筛选性别、装备及动作，逐帧查看8方向并播放、放大。\n'
        f'覆盖：男女 × {len(manifest["equipment"])}种槽位允许组合 × {len(manifest["motions"])}个动作；'
        f'{len(groups)}组，{total}张独立PNG。默认头型：{manifest["headId"]}。\n'
        '方向从0开始：↓、↙、←、↖、↑、↗、→、↘。帧号从0开始。\n'
        'frames/：透明独立帧，目录按性别/主副手组合/动作排列。\n'
        'labelled/：带中文方向与帧号的总览图，列为帧，行为方向。\n'
        'sheets/：透明原始1倍实绘联系表。coverage.csv：完整覆盖表。\n'
        'manifest.json：版本、画布定位点、帧时长、实际左右手资源与采样帧。\n'
        '复用说明：盾牌等部分武器缺少独立动作，按原客户端回退站立；攻击和准备按装备类别解析身体姿态；'
        '浏览页显示实际资源和采样帧，保留游戏当前结果供审查。\n'
        '当前槽位规则未限定双手武器占用副手，因此包中也保留这些当前允许的组合。\n'
        f'来源版本：{manifest["revision"]}\n生成时间：{manifest["generatedAt"]}\n', encoding='utf-8')
    files = sorted(path for path in root.rglob('*') if path.is_file() and not path.name.endswith('.import'))
    temporary = archive.with_suffix('.zip.tmp')
    with zipfile.ZipFile(temporary, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as output:
        for path in files:
            output.write(path, path.relative_to(root).as_posix())
    with zipfile.ZipFile(temporary) as output:
        if len(output.infolist()) != len(files) or output.testzip() is not None:
            raise ValueError('ZIP完整性校验失败')
    os.replace(temporary, archive)
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    archive.with_suffix('.zip.sha256').write_text(f'{digest}  {archive.name}\n', encoding='ascii')
    print(f'审查包验证通过：{len(groups)}组，{total}张独立PNG，{len(files)}个文件；ZIP {archive.stat().st_size / 1024 / 1024:.1f} MiB', flush=True)


def main():
    """整理指定导出目录；仅显式重建时原子替换已生成的ZIP。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--rebuild', action='store_true', help='重新整理本工具已有的输出并原子替换ZIP')
    args = parser.parse_args()
    build(args.directory.resolve(), args.rebuild)


if __name__ == '__main__':
    main()
