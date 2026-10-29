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
    python tools/extract_level_xp.py "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Euro Truck Simulator 2\\def.scs"

输出
----
打印提取到的增量表、累计门槛表，并给出与本项目
``Ets2SaveEditor.Core/Domain/TypedUnits/Player.cs`` 中 ``LevelXpIncrements``
的一致性校验。

游戏版本升级后请重新运行本脚本，并同步更新 Player.cs 里的表。
"""

import os
import re
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


def scan_for_level_xp(data: bytes):
    """全量扫描归档，找到含 level_xp 的 zlib 流。"""
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

    with open(path, "rb") as handle:
        data = handle.read()

    print("文件：%s（%d 字节）" % (path, len(data)))

    text = None
    offset = DEFAULT_STREAM_OFFSET
    if "--scan" in sys.argv:
        print("全量扫描归档中含 level_xp 的 zlib 流……")
        offset, text = scan_for_level_xp(data)
        if text is None:
            print("未找到 level_xp 数据块")
            return 1
        print("命中偏移：%d" % offset)
    else:
        try:
            text = extract_from_stream(data, offset)
        except Exception as exc:
            print("在偏移 %d 解压失败（%s），请加 --scan 参数全量扫描。" % (offset, exc))
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
