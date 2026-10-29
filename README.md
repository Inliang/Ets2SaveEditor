# Ets2SaveEditor

> 《欧洲卡车模拟 2》（Euro Truck Simulator 2，简称 **ETS2**）开源存档修改器（Save Editor）。

Ets2SaveEditor 是一款用于**查看与修改 ETS2 游戏存档**的开源工具，灵感与功能对齐成熟的参考项目 **TS-SE-Tool**（C#/.NET Framework WPF 实现的 ETS2/ATS 存档修改器）。本项目采用现代化的 **C# / .NET 8 + WPF** 技术栈重写，目标是提供一个功能完整、结构清晰、易于社区贡献的存档编辑方案。

> ⚠️ **修改游戏存档存在风险，请在操作前务必备份原存档！**（详见文末「使用须知与免责声明」。）

---

## 📌 项目简介

ETS2 的存档以 **SII 格式** 存储，常见有四种变体：

1. **明文 SII**：以 `SiiNunit` 开头的纯文本格式。
2. **zlib 压缩 SII**：对明文 SII 进行 zlib 压缩；解压后即得到明文 SII 文本。
3. **加密 SII（现代版本默认）**：`ScsC` 容器 = 56 字节头（魔数 / 32 字节 HMAC / 16 字节 IV / 明文长度）+ AES-256-CBC 密文；解密后再 zlib 解压。
4. **二进制 SII**：`BSII` 载荷（结构定义块 + 数据块），是加密存档解密解压后的实际内容。

Ets2SaveEditor 会自动识别上述格式：`ScsC` → AES-256-CBC 解密 → zlib 解压 → `BSII` 二进制解码 → 明文 SII 文本；全程使用 .NET 托管代码（`System.Security.Cryptography.Aes` / `System.IO.Compression.ZLibStream`），**零额外依赖**。

保存时一律写回**明文 SII**（游戏引擎可直接读取）。由于 `ScsC` 头部的 HMAC 密钥未公开、无法重新生成加密容器，覆盖加密源文件前会**强制备份**为 `<name>.sii.bak.<yyyyMMddHHmmssfff>`（毫秒精度 + 重名自动追加序号，**绝不覆盖已有备份** —— 原始加密存档不可再生，备份是唯一安全网）。写入采用「同目录临时文件 + 原子替换」，中途崩溃不会留下半截的 `game.sii`。

> 说明：参考项目的原生 `SII_Decrypt.dll` 共支持 4 种格式（明文 / 加密-zlib / 二进制 / 3NK）。本项目现已覆盖 **明文 / zlib / 加密(ScsC) / 二进制(BSII)** 四种；**3NK** 格式解析作为后续工作，已在「路线图」中注明。

---

## 🎯 功能列表与实现状态

以下功能归纳自参考项目 TS-SE-Tool。下表标注了 **v1 已实现** 与 **规划中（P2）** 状态：

| 分类 | 功能 | 实现状态 | 说明 |
| --- | --- | --- | --- |
| **存档管理** | 自定义 / 浏览存档路径 | ✅ 已实现 | 支持手动指定任意位置的存档目录 |
| | 自动探测本地与 Steam 存档 | ✅ 已实现 | 右侧「档案与存档」面板按 **游戏 → 档案 → 存档槽** 三级下拉选择 |
| | 打开存档 / 保存 / 另存为 | ✅ 已实现 | 保存写回**明文 SII**；加密源文件**强制备份**为 `.bak.<时间戳>` |
| **玩家档案** | 修改账户金钱 | ✅ 已实现 | 调整 `bank.money_account` |
| | 修改玩家经验 / 等级 | ✅ 已实现 | 经验值 → 自动推导等级，等级表取自游戏自身 `def.scs` 的 `level_xp[]`（权威数据，见下文） |
| | 修改驾驶技能 | ✅ 已实现 | 长距 / 重载 / 易碎 / 加急 / 经济驾驶 |
| **卡车 / 挂车** | 卡车修理 / 加油 | ✅ 已实现 | 一键清零磨损、加满燃油 |
| | 挂车修理 | ✅ 已实现 | 一键清零挂车磨损 |
| | 编辑 / 分享用户颜色 | 🔲 规划中 | 编辑卡车与挂车涂装颜色（P2） |
| | 分享卡车涂装 | 🔲 规划中 | 导出 / 导入卡车涂装配置（P2） |
| **资产** | 购买 / 升级车库 | ⚠️ 部分实现 | 可查看车库状态 / 车辆 / 司机 / 挂车；**暂不能真正购买新车库**（`garage.status` 语义未经证实） |
| | 标记城市为已访问 | ✅ 已实现 | 「城市」页签可解锁 / 取消访问 334 座城市（写入 `economy.visited_cities`） |
| | 从已探索城市抓取货物 | ✅ 已实现 | 解锁城市后，其货运市场即可在游戏内使用；工具内可直接浏览该城市的任务 |
| **货运市场** | 创建自定义货运任务 | ✅ 已实现 | 「货运市场」页签可新建任务：创建 `job_offer_data` 单元**并注册到公司**的 `job_offer` 数组（缺一不可，游戏才认） |
| | 任务报酬估算 | ✅ 已实现 | 实时显示「预估报酬」与「每公里报酬」；费率取自真实存档的 `delivery_log_entry` 实测数据 |
| | 货物市场浏览 / 编辑 | ✅ 已实现 | 浏览内置城市与货物数据 |
| **分享 / 协作** | 分享卡车位置 | 🔲 规划中 | 导出 / 导入卡车坐标（P2） |
| | 分享 GPS 路径 | 🔲 规划中 | 导出 / 导入导航路径（P2） |
| | 编队控制包 | 🔲 规划中 | 多卡车位置打包为 Convoy Control 包（P2） |

---

## 🛠 技术栈

| 类别 | 选型 |
| --- | --- |
| 语言 / 运行时 | C# / .NET 8 |
| 桌面框架 | WPF（Windows，需 `net8.0-windows`） |
| 架构模式 | MVVM（使用 `CommunityToolkit.Mvvm` 源码生成器） |
| 压缩处理 | `System.IO.Compression.ZLibStream`（托管 zlib 解压） |
| 第三方依赖 | 仅 `CommunityToolkit.Mvvm`（UI 层）；Core 类库零依赖 |
| 代码托管 | GitHub（开源，MIT 许可证） |

---

## 📁 目录结构

```text
Ets2SaveEditor/
├── .gitignore
├── LICENSE
├── README.md
├── Ets2SaveEditor.sln                  # 解决方案（含 Core / WPF / Tests 三个项目）
├── docs/
│   ├── PRD.md                          # 产品需求文档（目标、用户故事、需求池）
│   └── ARCHITECTURE.md                 # 架构设计（类图、时序图、任务分解）
├── src/
│   ├── Ets2SaveEditor.Core/            # 类库（net8.0，零第三方依赖）
│   │   ├── Common/Constants.cs         # SII 头、各 unit 类型名常量
│   │   ├── Sii/                        # SII 解析 / 序列化（无损透传）
│   │   │   ├── Model/                  # SiiValue / SiiAttribute / SiiUnit / SiiDocument
│   │   │   ├── SiiFormatDetector.cs    # 明文 / zlib 自动识别与解压
│   │   │   ├── SiiParser.cs            # 行解析（未知字段原样保留）
│   │   │   └── SiiSerializer.cs        # 无损写回
│   │   ├── Domain/                     # 领域模型
│   │   │   ├── SaveFile.cs             # 加载 / 保存（含备份）、聚合访问器
│   │   │   ├── IUnitCollection.cs
│   │   │   └── TypedUnits/             # 类型化便捷读写层
│   │   │       ├── Bank.cs             # 金钱 / 贷款
│   │   │       ├── Player.cs           # 经验 / 等级 / 技能（等级表取自游戏 def.scs）
│   │   │       ├── Garage.cs           # 状态 / 位置 / 升级 / 购买
│   │   │       ├── Vehicle.cs          # 磨损 / 燃油 / 修理 / 加油
│   │   │       ├── Trailer.cs          # 磨损 / 修理
│   │   │       ├── Company.cs          # 货运公司（持有 job_offer 数组）
│   │   │       ├── JobOffer.cs         # 货运任务（目标公司、货物、报酬估算）
│   │   │       └── Economy.cs          # 已访问城市
│   │   ├── GameData/                   # 内置游戏数据（城市 / 货物 / 公司）
│   │   │   ├── CityCatalogue.cs       # 334 座城市（中英文对照）
│   │   │   ├── CargoCatalogue.cs      # 350 种货物 + 实测单位费率
│   │   │   └── JobIncomeEstimator.cs  # 报酬 / 每公里报酬估算
│   │   └── Persistence/                # 存档位置探测 / 备份服务
│   └── Ets2SaveEditor/                 # WPF 应用（net8.0-windows，MVVM）
│       ├── App.xaml(.cs)               # 应用入口 + 全局异常处理
│       ├── MainWindow.xaml(.cs)        # 菜单 / 右侧「档案与存档」面板 / 8 标签页 / 状态栏
│       ├── ViewModels/                 # Main / Profile / Garages / Truck / Trailer
│       │                               #   / FreightMarket / Cargo / Cities / Settings
│       │                               #   + SaveProfileItem（档案分组、友好档案名）
│       ├── Views/                      # 对应 8 个标签页的 XAML 视图
│       ├── Services/DialogService.cs   # 文件/文件夹选择、消息框
│       └── Converters/WearToColorConverter.cs  # 磨损 → 颜色画刷
├── tests/
│   └── Ets2SaveEditor.Tests/           # 端到端集成测试（合成存档全链路验证）
└── tools/
    └── extract_level_xp.py             # 从游戏 def.scs 提取权威等级表（游戏升级后重跑）
```

---

## 🚧 构建与运行

> **前置要求**：Windows 操作系统 + [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。WPF 项目依赖 Windows 桌面运行时（`net8.0-windows`），因此**仅支持 Windows 构建与运行**。

```text
# 1. 克隆仓库
git clone https://github.com/Inliang/Ets2SaveEditor.git
cd Ets2SaveEditor

# 2. 还原依赖并构建整个解决方案
dotnet restore
dotnet build Ets2SaveEditor.sln -c Release

# 3. 运行 WPF 应用（GUI）
dotnet run --project src/Ets2SaveEditor

# 4.（可选）运行端到端集成测试
dotnet run --project tests/Ets2SaveEditor.Tests
```

构建成功后，在「文件」菜单中选择「打开文件」或「打开文件夹」，定位到 ETS2 的存档目录（见下文），即可开始编辑。

### 存档位置

- **本地存档**：`文档\Euro Truck Simulator 2\profiles\<profile_id>\save\<slot>\`
- **Steam 存档**：由注册表 `HKEY_CURRENT_USER\Software\Valve\Steam` 的 `SteamPath` 定位到
  `Steam\userdata\<steam_id>\227300\remote\profiles\<profile_id>\save\<slot>\`
  （ETS2 的 Steam AppId 为 `227300`）

> 工具也内置了「存档位置探测」功能，可在「设置」页启用 Steam 自动探测。

---

## 📐 等级换算表的数据来源

游戏**不存等级**，存档里只有累计经验值 `economy.experience_points`，等级需要按游戏自己的曲线换算。
这条曲线定义在游戏安装目录 `def.scs` 内的 `xp_level_data` 块：

```
# experience points to the next level
level_xp[0]: 200      # ← 增量语义：从等级 i 升到 i+1 所需 XP，不是累计值
...
level_xp[29]: 6800
```

本工具内置的等级表即由此提取（前 30 项用 `def.scs` 原值，30 级之后游戏沿用最后一项 6800，
累加到 100 级恰为 575,700 XP）。三重交叉验证：

1. 增量累加后与公开等级表逐级比对，L1..L17 共 **17/17 精确匹配**；
2. 累加到 L100 = 99,700 + 70 × 6,800 = **575,700**，与 100 级（Divine Champion）门槛精确相等；
3. 真实存档 `experience_points = 252,508` 推导为 **52 级**，与游戏内显示一致。

> ⚠️ 网上流传的**分段**等级门槛表（如「50 级需 249,700 XP」）是**旧版本数据**，与当前游戏曲线
> 存在 100~14000 XP 的偏差，不要据以校准。本工具以游戏自身 `def.scs` 为准。

游戏版本升级后，用下面的命令重新提取并同步到 `Player.cs`：

```bash
python tools/extract_level_xp.py "<ETS2 安装目录>/def.scs"
```

脚本会解压归档、解析 `level_xp[]`、展开累计门槛表，并跑三项自校验（满级门槛、公开表逐级比对、
真实存档实测值），输出「全部校验通过」方可用于更新。

---

## 🗺️ 路线图（P2 后续工作）

- [ ] **3NK 格式**解析（ATS 变体；`BSII` 二进制格式已于 v0.0.5 支持）
- [ ] 卡车 / 挂车**颜色与涂装**的编辑与分享
- [ ] **卡车位置 / GPS 路径 / 编队控制包**的分享
- [ ] **Mod 扫描**（加载 Mod 提供的城市 / 货物数据）
- [ ] 跨平台支持探索（Avalonia / MAUI 替代 WPF）

---

## ⚠️ 使用须知与免责声明

1. **务必先备份存档。** 修改存档属于对游戏文件的写操作，任何错误都可能导致存档损坏、无法加载甚至游戏崩溃。在编辑前，请完整复制 `save` 目录到安全位置。
2. **保存的是明文 SII，游戏可直接读取。** `ScsC` 头部的 HMAC 密钥未公开，本工具**无法**重新生成加密容器（强行加密会被游戏判为无效并回退到备份），因此保存时写出**明文** `SiiNunit`——游戏引擎能正常读取（游戏自己就把 `game_backup.sii` 写成明文）。原加密存档会被自动备份为同目录的 `game.sii.bak.<时间戳>`。**若游戏出现异常，改名回滚即可：删除 `game.sii`，把备份文件改名为 `game.sii`。**
3. **首次使用建议先在副本上试手。** 先把整个 `save\1` 目录复制一份，用副本走一遍「加载 → 改钱 → 保存 → 启动游戏读档 → 退出再进一次」，确认无误后再动正本。
4. **仅用于单机 / 离线体验。** 本工具旨在帮助玩家在个人游戏中获得更自由的体验，请勿用于在线 / 联机（如 Convoy、TruckersMP 等）作弊或破坏他人体验。
5. **版本兼容性。** ETS2 不同版本（含 DLC、Mod）的 SII 结构可能存在差异，修改后请确认游戏可正常加载；如遇无法加载，请恢复备份存档。
6. **非官方工具。** 本项目与 SCS Software 无任何官方关联，不保证对全部游戏版本 / Mod 的兼容性。使用本软件所产生的任何后果由使用者自行承担。
7. **遵守许可证。** 本项目以 MIT 许可证开源，二次分发与修改请保留版权声明。

---

## 📄 许可证

本项目基于 [MIT License](./LICENSE) 开源，版权所有者为 **Ets2SaveEditor Contributors**（2026）。
