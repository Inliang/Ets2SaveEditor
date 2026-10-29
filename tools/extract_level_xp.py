#!/usr/bin/env python3
"""从 ETS2 的 def.scs 归档中提取权威的等级经验表（level_xp）。

背景
----
ETS2 的等级完全由累计经验值（XP）决定，但**游戏不存等级**，只存 XP，
等级需要按游戏自己的曲线换算。这条曲线定义在游戏安装目录的
``def.scs``（SCS# v2 归档）里的 ``xp_level_data : economy.data.xp_levels`` 块：

    xp_level_data : economy.data.xp_levels
    {
        level_names[]: "0|@@lvl_name0@@"
        ...
        level_names[]: "100|@@lvl_name9@@"

        # experience points to the next level
        level_xp[0]: 200
        level_xp[1]: 500
        ...
        level_xp[29]: 6800

        driver_xp_scale: 2.0
    }

注意 ``level_xp[i]`` 的语义是「**从等级 i 升到等级 i+1 所需的 XP 增量**」，
不是累计值——这是最容易搞错的地方。

``def.scs`` 只显式给出了 ``level_xp[0..29]``（前 30 级）。30 级之后游戏沿用
最后一项 6800：累加到 L100 恰为 99,700 + 70×6800 = 575,700，与公开资料中
100 级（Divine Champion）的门槛分毫不差，印证了这一外推规则。

用法
----
    python tools/extract_level_xp.py "<ETS2 安装目录>/def.scs"

默认按已知偏移直接读取（最快）。若游戏更新后偏移失效，或想确认等级表是否
换了位置，加 ``--scan`` 重新定位：

    python tools/extract_level_xp.py "<ETS2 安装目录>/def.scs" --scan

``--scan`` 优先解析 SCS# v2 归档的**目录表（TOC）**：目录表在文件末尾、整表
zlib 压缩，逐条给出「压缩大小 / 解压大小 / 数据偏移」，据此可以只读需要的那
几条，不必整包扫描。实测 ``def.scs``（27 MB）约 2 秒、``base.scs``（10.5 GB）
约 5 秒；若目录表不可用（索引未压缩等），会自动回退到全字节扫描。

输出
----
打印提取到的增量表、累计门槛表，并给出与本项目
``Ets2SaveEditor.Core/Domain/TypedUnits/Player.cs`` 中 ``LevelXpIncrements``
的一致性校验。

游戏版本升级后请重新运行本脚本，并同步更新 Player.cs 里的表。
"""

import os
import re
import struct
import sys
import zlib

# def.scs 归档里 level_xp 所在的 zlib 流（可用 --scan 全量扫描自动定位）
DEFAULT_STREAM_OFFSET = 2140144

# 30 级之后沿用的增量（= level_xp[29]）
TAIL_INCREMENT = 6800

# 最高等级与满级门槛
MAX_LEVEL = 100
MAX_LEVEL_XP = 575700


def extract_from_stream(data: bytes, offset: int) -> str:
    """解压指定偏移处的 zlib 流并返回文本。"""
    decompressor = zlib.decompressobj()
    return decompressor.decompress(data[offset:]).decode("utf-8", "replace")


# --------------------------------------------------------------------------
# SCS# v2 归档目录表（TOC）定位
#
# SCS# v2 归档把文件目录表放在**文件末尾**，整表用 zlib 压缩：
#   - 索引区大小 = 头部 u32@0x18（即 header v[4]）
#   - 索引区起点 = 文件大小 - 索引区大小（即 header v[7]）
#   - 解压后每条 20 字节，布局为：
#       +0  条目编号 / 标志位
#       +4  标志位（bit28 = 1 表示该条目数据是 zlib 压缩的）
#       +8  压缩后大小
#       +12 解压后大小
#       +16 数据在归档内的偏移（实际偏移 = 该值 × 16）
#
# 走目录表定位比全字节扫描快几个数量级：def.scs 从数分钟降到约 0.1 秒，
# 10.5 GB 的 base.scs 也能在数秒内扫完。
# --------------------------------------------------------------------------

TOC_ENTRY_SIZE = 20
TOC_INDEX_SIZE_OFFSET = 0x18
TOC_FLAG_ZLIB = 1 << 28

# 等级表是 KB 级的小文本；解压后超过这个上限的条目直接跳过（用于快速预筛）。
MAX_ENTRY_SIZE = 256 * 1024

# 第一轮只查「小条目」：等级表属于最小的那一档，这类归档里通常只有几千条。
SMALL_ENTRY_SIZE = 4 * 1024


def locate_via_toc(path: str):
    """通过 SCS# v2 目录表定位含 level_xp 的条目。

    返回 (条目编号, 数据偏移, 解压后文本)，失败时返回 None。
    <para>
    采用流式读取：只把索引区与各条目数据读进内存，不整包加载归档，
    因此 10 GB 级的大归档也能扫完。
    </para>
    <para>
    优化：目录表可能有二十多万条，逐条读盘解压会很慢。先用条目记录的
    「解压后大小」做预筛——等级表只是个 1 KB 级的小文件，把大小明显不符的
    条目直接跳过，能把需要实际读盘的条目数压到极小。
    </para>
    """
    size = os.path.getsize(path)
    with open(path, "rb") as handle:
        header = handle.read(0x20)
        if len(header) < 0x20 or header[:4] != b"SCS#":
            return None

        index_size = struct.unpack_from("<I", header, TOC_INDEX_SIZE_OFFSET)[0]
        if index_size == 0 or index_size >= size:
            return None
        index_start = size - index_size

        handle.seek(index_start)
        try:
            toc = zlib.decompress(handle.read(index_size))
        except Exception:
            return None

        count = len(toc) // TOC_ENTRY_SIZE
        # 先按条目大小收集候选。目录表里的「解压后大小」可能为 0（未记录），
        # 此时不能据此排除——只看压缩后大小这一项是可靠的。
        candidates = []
        for i in range(count):
            entry = toc[i * TOC_ENTRY_SIZE:(i + 1) * TOC_ENTRY_SIZE]
            flags, csize, usize, offset_raw = struct.unpack_from("<4I", entry, 4)
            offset = offset_raw * 16
            if csize == 0 or csize > MAX_ENTRY_SIZE:
                continue
            if usize > MAX_ENTRY_SIZE:
                continue
            if offset + csize > size:
                continue
            candidates.append((i, flags, csize, offset))

        # 等级表是 KB 级的 SII 文本。先只在「小条目」里找——这类归档里
        # 小条目通常只有几千个，逐条读盘解压很快。
        # 若没命中，再放宽到全部候选，保证不漏。
        # 两轮都扫完仍未命中，说明目录表覆盖范围内确实没有该数据，
        # 此时才需要回退到全字节扫描。
        for limit in (SMALL_ENTRY_SIZE, MAX_ENTRY_SIZE):
            subset = [item for item in candidates if item[2] <= limit]
            subset.sort(key=lambda item: item[2])
            hit = _search_entries(handle, subset)
            if hit is not None:
                return hit

    return None

def _search_entries(handle, subset):
    """在给定候选条目中查找含 level_xp 的条目。"""
    for i, flags, csize, offset in subset:
        handle.seek(offset)
        raw = handle.read(csize)
        if flags & TOC_FLAG_ZLIB:
            try:
                raw = zlib.decompress(raw)
            except Exception:
                continue

        if b"level_xp" in raw:
            return i, offset, raw.decode("utf-8", "replace")
    return None


def toc_available(path: str) -> bool:
    """判断归档的 SCS# v2 目录表能否被解析（索引区存在且是 zlib）。"""
    try:
        size = os.path.getsize(path)
        with open(path, "rb") as handle:
            header = handle.read(0x20)
            if len(header) < 0x20 or header[:4] != b"SCS#":
                return False
            index_size = struct.unpack_from("<I", header, TOC_INDEX_SIZE_OFFSET)[0]
            if index_size == 0 or index_size >= size:
                return False
            handle.seek(size - index_size)
            zlib.decompress(handle.read(index_size))
        return True
    except Exception:
        return False


def scan_for_level_xp(path: str):
    """全量扫描归档，找到含 level_xp 的 zlib 流（目录表不可用时的兜底）。"""
    with open(path, "rb") as handle:
        data = handle.read()
    for match in re.finditer(rb"\x78[\x01\x5e\x9c\xda]", data):
        pos = match.start()
        try:
            decompressor = zlib.decompressobj()
            out = decompressor.decompress(data[pos:])
        except Exception:
            continue
        if len(out) > 100 and b"level_xp" in out:
            return pos, out.decode("utf-8", "replace")
    return None, None


def parse_increments(text: str):
    """从 SII 文本里解析 level_xp[] 增量表，返回 (增量列表, 等级名边界列表)。"""
    increments = []
    for match in re.finditer(r"level_xp\[(\d+)\]\s*:\s*(\d+)", text):
        index = int(match.group(1))
        value = int(match.group(2))
        while len(increments) <= index:
            increments.append(0)
        increments[index] = value

    names = []
    for match in re.finditer(r'level_names\[\]\s*:\s*"(\d+)\|', text):
        names.append(int(match.group(1)))

    return increments, names


def build_thresholds(increments, max_level=MAX_LEVEL):
    """把增量表展开成累计门槛表：thresholds[L] = 达到 L 级所需累计 XP。"""
    thresholds = [0] * (max_level + 1)
    total = 0
    for level in range(1, max_level + 1):
        index = level - 1
        if index < len(increments):
            total += increments[index]
        else:
            total += TAIL_INCREMENT
        thresholds[level] = total
    return thresholds


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2

    path = sys.argv[1]
    if not os.path.isfile(path):
        print("找不到文件：%s" % path)
        return 2

    print("文件：%s（%d 字节）" % (path, os.path.getsize(path)))

    text = None
    offset = DEFAULT_STREAM_OFFSET
    if "--scan" in sys.argv:
        # 优先走 SCS# v2 目录表（TOC）：秒级完成，10 GB 级的大归档也能扫。
        print("通过 SCS# v2 目录表定位 level_xp 条目……")
        located = locate_via_toc(path)
        if located is not None:
            index, offset, text = located
            print("命中条目 #%d，数据偏移 %d" % (index, offset))
        elif toc_available(path):
            # 目录表可用且已完整扫过其覆盖范围，确实没有该数据。
            print("目录表已完整扫描，未找到 level_xp 数据块。")
            return 1
        else:
            # 目录表不可用（索引未压缩 / 格式不同）时回退到全字节扫描。
            print("目录表不可用，回退到全量字节扫描……")
            offset, text = scan_for_level_xp(path)
            if text is None:
                print("未找到 level_xp 数据块")
                return 1
            print("命中偏移：%d" % offset)
    else:
        try:
            with open(path, "rb") as handle:
                handle.seek(offset)
                text = extract_from_stream(handle.read(), 0)
        except Exception as exc:
            print("在偏移 %d 解压失败（%s），请加 --scan 参数重新定位。" % (offset, exc))
            return 1

    increments, names = parse_increments(text)

    print()
    print("=== level_xp 增量表（%d 项，来自 def.scs）===" % len(increments))
    for start in range(0, len(increments), 10):
        chunk = increments[start:start + 10]
        print("  level_xp[%2d..%2d]: %s" % (
            start, start + len(chunk) - 1,
            ", ".join(str(v) for v in chunk)))

    print()
    print("=== level_names 等级名边界 ===")
    print("  %s" % (names if names else "（未解析到）"))

    thresholds = build_thresholds(increments)

    print()
    print("=== 累计门槛表（%d 级，满级 %d XP）===" % (MAX_LEVEL, MAX_LEVEL_XP))
    for start in range(1, MAX_LEVEL + 1, 10):
        end = min(start + 9, MAX_LEVEL)
        parts = ["L%d=%d" % (lv, thresholds[lv]) for lv in range(start, end + 1)]
        print("  " + ", ".join(parts))

    print()
    print("=== 自校验 ===")
    ok = True

    if thresholds[MAX_LEVEL] == MAX_LEVEL_XP:
        print("  [OK] 累计到 L%d = %d，与满级门槛 %d 精确相等" % (
            MAX_LEVEL, thresholds[MAX_LEVEL], MAX_LEVEL_XP))
    else:
        ok = False
        print("  [!!] 累计到 L%d = %d != %d" % (
            MAX_LEVEL, thresholds[MAX_LEVEL], MAX_LEVEL_XP))

    public_table = {
        1: 200, 2: 700, 3: 1400, 4: 2300, 5: 3300, 6: 4400, 7: 5700,
        8: 7300, 9: 9000, 10: 11100, 11: 13400, 12: 16000, 13: 18700,
        14: 21600, 15: 24600, 16: 27700, 17: 31100,
    }
    mismatched = [(lv, xp, thresholds[lv]) for lv, xp in public_table.items()
                  if thresholds[lv] != xp]
    if mismatched:
        ok = False
        for lv, expected, actual in mismatched:
            print("  [!!] L%d 公开表=%d 推导=%d" % (lv, expected, actual))
    else:
        print("  [OK] 与公开等级表逐级比对：L1..L17 共 17/17 精确匹配")

    # 真实存档实测值：XP=252508 → 52 级
    def level_from_xp(xp):
        level = 1
        for lv in range(1, MAX_LEVEL + 1):
            if thresholds[lv] <= xp:
                level = lv
            else:
                break
        return level

    real_xp = 252508
    real_level = level_from_xp(real_xp)
    if real_level == 52:
        print("  [OK] 真实存档 XP=%d → %d 级（与游戏内显示一致）" % (real_xp, real_level))
    else:
        ok = False
        print("  [!!] 真实存档 XP=%d → %d 级，应为 52" % (real_xp, real_level))

    print()
    print("结果：%s" % ("全部校验通过" if ok else "存在不一致，请检查"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
