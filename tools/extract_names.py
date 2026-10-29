#!/usr/bin/env python3
"""从 ETS2 的游戏数据文件里提取显示名表（货物 / 品牌 / 城市 / 车型…）。

背景
----
游戏存档里存的全是 **token**（如 ``cargo.gummy_bears``、``company.permanent.acc``），
显示名在游戏数据文件里。本脚本把两者关联起来，产出可直接内嵌到程序里的
名称表。

数据来源
--------
* ``def.scs``   —— 定义表：``cargo_data``（货物）、``company_permanent``（品牌/公司）、
  以及 ``city`` / ``truck`` / ``trailer`` 等其它类型。
* ``locale.scs`` —— 本地化字符串：``key[]: "cn_acetylene"`` / ``val[]: "Acetylene"``。
  定义表里的 ``name`` 有时是占位符（``@@cn_acetylene@@``），需用这里的 key 换成实际名称。

用法
----
    python tools/extract_names.py "<ETS2 安装目录>" [输出 json]

输出是一个 json::

    {
      "cargo":   { "cargo.gummy_bears": "Gummy Bears", ... },
      "company": { "company.permanent.acc": "ACC", ... },
      ...
    }

**两个踩过的坑**（照抄代码时注意）：

1. SCS# v2 归档的目录表在**文件末尾**，整段 zlib 压缩：
   索引区大小 = 文件头 ``u32@0x18``；索引区起点 = ``filesize - 索引区大小``；
   解压后每条 20 字节，布局为
   ``+0`` 条目号、``+4`` 标志位（bit28 = 1 表示数据是 zlib 压缩的）、
   ``+8`` 压缩大小、``+12`` 解压大小（**可能为 0 = 未记录**）、``+16`` 数据偏移（实际偏移 = 值 × 16）。
2. 小型定义条目与本地化条目**不是**完整的 SII 文件，没有 ``SiiNunit`` 头，
   而且缩进用的是 **TAB**。按 ``SiiNunit`` 前缀过滤会把它们全部漏掉。
"""
import glob
import json
import os
import re
import struct
import sys
import zlib

TOC_ENTRY_SIZE = 20
TOC_INDEX_SIZE_OFFSET = 0x18
TOC_FLAG_ZLIB = 1 << 28

DEF_RE = re.compile(
    r'([a-z_0-9]+):\s*([a-z_0-9.]+)\s*\n\{\s*\n\s*name:\s*"([^"]*)"')
KV_RE = re.compile(r'key\[\]:\s*"([^"]*)"\s*\n\s*val\[\]:\s*"([^"]*)"', re.M)


def read_toc(path):
    """返回归档的 (文件对象, 目录表, 条目数, 文件大小)。"""
    handle = open(path, 'rb')
    size = os.path.getsize(path)
    handle.seek(0)
    header = handle.read(0x20)
    index_size = struct.unpack_from('<I', header, TOC_INDEX_SIZE_OFFSET)[0]
    if index_size == 0 or index_size >= size:
        handle.close()
        return None
    handle.seek(size - index_size)
    try:
        toc = zlib.decompress(handle.read(index_size))
    except Exception:
        handle.close()
        return None
    return handle, toc, len(toc) // TOC_ENTRY_SIZE, size


def iter_entries(handle, toc, count, size, max_size=8 * 1024 * 1024):
    """按目录表逐条产出解压后的内容。"""
    for i in range(count):
        entry = toc[i * TOC_ENTRY_SIZE:(i + 1) * TOC_ENTRY_SIZE]
        flags, csize, usize, offset_raw = struct.unpack_from('<4I', entry, 4)
        offset = offset_raw * 16
        if csize == 0 or csize > max_size or offset + csize > size:
            continue
        handle.seek(offset)
        raw = handle.read(csize)
        if flags & TOC_FLAG_ZLIB:
            try:
                raw = zlib.decompress(raw)
            except Exception:
                continue
        yield i, raw


def extract_definitions(path):
    """从 def.scs 提取所有定义类型：{类型: {标识: 名称}}。"""
    loaded = read_toc(path)
    if not loaded:
        print('  跳过（目录表不可用）: %s' % os.path.basename(path))
        return {}
    handle, toc, count, size = loaded

    defs = {}
    for _index, raw in iter_entries(handle, toc, count, size):
        if b'@include' in raw or b'name:' not in raw:
            continue
        text = raw.decode('utf-8', 'replace')
        for m in DEF_RE.finditer(text):
            kind, ident, name = m.group(1), m.group(2), m.group(3)
            defs.setdefault(kind, {})[ident] = name
    handle.close()
    return defs


def extract_locale(path):
    """从 locale.scs 提取英文本地化字符串：{key: 值}。"""
    loaded = read_toc(path)
    if not loaded:
        print('  跳过（目录表不可用）: %s' % os.path.basename(path))
        return {}
    handle, toc, count, size = loaded

    best = {}
    for _index, raw in iter_entries(handle, toc, count, size, max_size=24 * 1024 * 1024):
        if b'key[]:' not in raw:
            continue
        text = raw.decode('utf-8', 'replace')
        kv = {}
        for m in KV_RE.finditer(text):
            kv[m.group(1)] = m.group(2)
        if 'cn_acetylene' not in kv:
            continue
        # 用英文专有词识别语言（借词如 diesel/cement 会误判，不能只用它们）
        sample = ' '.join(kv.get(k, '') for k in
                          ('cn_acetylene', 'cn_beverages', 'cn_pallets',
                           'cn_electronics', 'cn_food')).lower()
        markers = ('acetylene', 'beverages', 'pallets', 'electronics',
                   'packaged food', 'machine', 'chemicals')
        if sum(1 for w in markers if w in sample) >= 3:
            best.update(kv)
    handle.close()
    return best


def resolve(name, locale):
    """把 @@key@@ 占位符换成实际名称。"""
    if name.startswith('@@') and name.endswith('@@'):
        key = name[2:-2]
        return locale.get(key, name)
    return name


def build_localized_cargo(locale):
    """从本地化条目里提取货物显示名的**兜底表**。

    规则很简单：``cn_<token 尾段>`` → 显示名，例如 ``cn_driller`` → "Driller D-50"。

    为什么需要它：``def.scs`` 的 ``cargo_data`` 只覆盖 397 个货物，而游戏当前
    使用的、以及老存档里还留着的 token 更多（``cargo.driller``、
    ``cargo.cable_reel``、``cargo.haulie`` …）。这些 token 在 ``cargo_data``
    里查不到，但它们的英文名仍在 ``locale.scs`` 的 ``cn_*`` 键里。

    键统一写成 ``cargo.<尾段>``，与 ``cargo_data`` 的 token 形式一致，方便
    查表时按 token 直接命中。
    """
    table = {}
    for key, value in locale.items():
        if not key.startswith('cn_'):
            continue
        if not value or value.startswith('@@'):
            continue
        table['cargo.' + key[3:]] = value
    return table


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2

    root = sys.argv[1]
    out_path = sys.argv[2] if len(sys.argv) > 2 else 'game_names.json'

    def_path = os.path.join(root, 'def.scs')
    locale_path = os.path.join(root, 'locale.scs')

    print('读取定义表：%s' % def_path)
    defs = extract_definitions(def_path)
    for kind, table in sorted(defs.items(), key=lambda x: -len(x[1])):
        print('  %-30s %d' % (kind, len(table)))

    print('读取本地化：%s' % locale_path)
    locale = extract_locale(locale_path)
    print('  英文条目 %d' % len(locale))

    # 把占位符换成实际名称
    resolved = {}
    for kind, table in defs.items():
        out = {}
        for ident, name in table.items():
            out[ident] = resolve(name, locale)
        resolved[kind] = out

    # 货物显示名的本地化兜底表（cn_<尾段> → 英文名）
    resolved['localized_cargo'] = build_localized_cargo(locale)

    with open(out_path, 'w', encoding='utf-8') as handle:
        json.dump(resolved, handle, ensure_ascii=False, indent=1, sort_keys=True)
    print()
    print('已保存 ->', out_path)
    print('  localized_cargo（本地化兜底）%d' % len(resolved['localized_cargo']))

    for kind in ('cargo_data', 'company_permanent'):
        if kind in resolved:
            table = resolved[kind]
            print()
            print('%s 前 6 项：' % kind)
            for ident, name in list(table.items())[:6]:
                print('  %-28s -> %s' % (ident, name))
    return 0


if __name__ == '__main__':
    sys.exit(main())
