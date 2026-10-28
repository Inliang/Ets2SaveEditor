# Ets2SaveEditor 系统架构设计（ARCHITECTURE）

> 文档版本：v0.1（架构草案）　|　作者：架构师 高见远（Gao）　|　状态：待评审
> 适用范围：v1（明文 SII + zlib 压缩 SII；二进制 / 3NK 后置 P2）
> 配套文档：`docs/PRD.md`（产品需求）、`README.md`（技术栈与目录占位）

---

## 1. 实现方案与框架选型

### 1.1 技术栈与理由

| 维度 | 选型 | 理由 |
| --- | --- | --- |
| 语言 / 运行时 | **C# / .NET 8** | LTS 长期支持、跨项目引用简单、原生 `System.IO.Compression.ZLibStream` 可直接做托管 zlib 解压，无需 P/Invoke。 |
| 桌面框架 | **WPF（Windows）** | ETS2 主平台为 Windows；WPF 的 XAML + 数据绑定 + 标签页（TabControl）天然契合 PRD 的多功能页签布局。 |
| 架构模式 | **MVVM（CommunityToolkit.Mvvm）** | 通过 `[ObservableProperty]` / `RelayCommand` / `AsyncRelayCommand` 源码生成器，减少样板代码；视图与逻辑解耦，便于社区贡献与单元测试。 |
| 压缩处理 | **`System.IO.Compression.ZLibStream`** | .NET 原生托管实现，零额外依赖，可安全解压 zlib 变体 SII；加载时解压为明文、保存时写回明文（游戏加载会自动重压缩）。 |
| 分层 | **Core 类库 + WPF 应用** | 解析 / 领域模型 / 游戏数据与 UI 彻底解耦；Core 可被测试、可被后续 CLI / MAUI 复用。 |

### 1.2 核心架构决策：通用模型 + 类型化包装（双层 SII 模型）

为**最大限度避免存档损坏**，采用双层结构：

1. **通用层（无损透传）**：`SiiDocument` / `SiiUnit` / `SiiAttribute` / `SiiValue` 把整个 SII 文件**无损**读入。解析器 / 序列化器对**任何不认识的字段一律原样保留（透传）**，确保即使我们未显式支持某字段也不会丢数据——这是存档安全性的根本保障。
2. **类型化包装层（便捷访问）**：针对已知实体（`Bank` / `Player` / `Garage` / `Vehicle` / `Trailer` / `JobOffer` / `Economy`）提供基于属性名访问的包装类，底层仍操作通用 `SiiUnit`。改金钱 / 等级 / 磨损等**只读写对应字段**，其余结构原样透传。

> 设计不变量：**修改 = 在通用模型上局部 SetAttribute；保存 = 通用模型 → 明文 SII**。绝不直接拼字符串改值，避免破坏语法。

### 1.3 格式检测策略

`SiiFormatDetector` 按首字节判定：
- 以 `SiiNunit` 开头 → 明文（`Plaintext`）。
- 否则尝试 `ZLibStream` 解压；解压结果以 `SiiNunit` 开头 → `Zlib` 成功。
- 仍失败 → 标注 `Binary/3NK`（P2 扩展点，v1 抛明确异常不破坏原文件）。

---

## 2. 文件清单及相对路径

> 仓库根：`Ets2SaveEditor/`。两个项目位于 `src/`。下列文件为 v1 全部需创建的源文件（`.cs` / `.csproj` / `.xaml`）。

### 2.1 解决方案与项目文件

| 相对路径 | 职责 |
| --- | --- |
| `Ets2SaveEditor.sln` | 解决方案，引用下两个项目 |
| `src/Ets2SaveEditor.Core/Ets2SaveEditor.Core.csproj` | .NET 8 类库项目文件（TargetFramework `net8.0`，无额外 NuGet 依赖） |
| `src/Ets2SaveEditor/Ets2SaveEditor.csproj` | .NET 8 WPF 应用项目文件（`<UseWPF>true</UseWPF>`，引用 Core + CommunityToolkit.Mvvm） |

### 2.2 `Ets2SaveEditor.Core`（类库）

| 相对路径 | 职责 |
| --- | --- |
| `src/Ets2SaveEditor.Core/Common/Constants.cs` | 命名空间常量、SII 头字符串 `SiiNunit`、关键 unit 类型名（`bank` / `game` / `garage` / `vehicle` / `trailer` / `job_offer_data` / `economy`）、备份扩展名等共享常量 |
| `src/Ets2SaveEditor.Core/Sii/Model/SiiValue.cs` | 抽象值类型 `SiiValue` 及子类 `IntValue` / `FloatValue` / `StringValue` / `BoolValue` / `VectorValue` / `ReferenceValue` / `ArrayValue`（含 `SiiValueKind` 枚举） |
| `src/Ets2SaveEditor.Core/Sii/Model/SiiAttribute.cs` | 通用属性（Name / Value / IsArray / Order / RawText 透传原文） |
| `src/Ets2SaveEditor.Core/Sii/Model/SiiUnit.cs` | 通用 unit（Type / Id / 有序 Attributes，Get/Set/GetOrCreate 方法） |
| `src/Ets2SaveEditor.Core/Sii/Model/SiiDocument.cs` | 顶层文档（Header / 有序 Units，按 Id / Type 查询） |
| `src/Ets2SaveEditor.Core/Sii/SiiFormatDetector.cs` | 格式枚举 `SiiFormat`、首字节检测、zlib 解压 |
| `src/Ets2SaveEditor.Core/Sii/SiiParser.cs` | 明文 SII 文本 → `SiiDocument`（逐 unit / 逐 attribute 解析，未知结构原样保留） |
| `src/Ets2SaveEditor.Core/Sii/SiiSerializer.cs` | `SiiDocument` → 明文 SII 文本（无损写回） |
| `src/Ets2SaveEditor.Core/Sii/SiiFormatException.cs` | 解析 / 格式相关自定义异常 |
| `src/Ets2SaveEditor.Core/Domain/IUnitCollection.cs` | 类型化访问接口：按 type / id 取 `SiiUnit`，供包装类复用 |
| `src/Ets2SaveEditor.Core/Domain/TypedUnits/Bank.cs` | 类型化包装：`MoneyAccount` / `BankLoan` |
| `src/Ets2SaveEditor.Core/Domain/TypedUnits/Player.cs` | 类型化包装：`Experience` / `Level`（由 XP 阈值推导）/ 技能（`LongDist` / `Heavy` / `Fragile` / `Urgent` / `EcoDriving` 等，属性名待校验） |
| `src/Ets2SaveEditor.Core/Domain/TypedUnits/Garage.cs` | 类型化包装：`Status` / `Location` / `Upgrade` |
| `src/Ets2SaveEditor.Core/Domain/TypedUnits/Vehicle.cs` | 类型化包装：`EngineWear` / `ChassisWear` / ... / `Fuel` / `FuelCapacity`（修理→置 0，加油→置满） |
| `src/Ets2SaveEditor.Core/Domain/TypedUnits/Trailer.cs` | 类型化包装：磨损系列属性 |
| `src/Ets2SaveEditor.Core/Domain/TypedUnits/JobOffer.cs` | 类型化包装（`job_offer_data`）：`SourceCity` / `TargetCity` / `Cargo` / `Reward` |
| `src/Ets2SaveEditor.Core/Domain/TypedUnits/Economy.cs` | 类型化包装：`VisitedCities` 等 |
| `src/Ets2SaveEditor.Core/Domain/SaveFile.cs` | 聚合单一存档：Path / Format / Document / IsLoaded；`Load()` / `Save()`；暴露 `GetBank()` / `GetPlayer()` / `GetGarages()` 等类型化入口 |
| `src/Ets2SaveEditor.Core/Persistence/SaveLocationFinder.cs` | 枚举本地（`文档/Euro Truck Simulator 2/...`）与 Steam（`userdata/<uid>/227300/remote/...`）存档路径，返回 `SaveSlot` 列表 |
| `src/Ets2SaveEditor.Core/Persistence/BackupService.cs` | 保存前备份（`Backup(path, enabled)`），生成 `.bak.<timestamp>` |
| `src/Ets2SaveEditor.Core/GameData/GameDataModel.cs` | 静态实体 `City` / `Cargo` / `Company` |
| `src/Ets2SaveEditor.Core/GameData/IGameDataProvider.cs` | 游戏数据提供器接口 |
| `src/Ets2SaveEditor.Core/GameData/BuiltInGameData.cs` | 内置少量城市 / 货物 / 公司（注明待扩充） |

### 2.3 `Ets2SaveEditor`（WPF 应用）

| 相对路径 | 职责 |
| --- | --- |
| `src/Ets2SaveEditor/App.xaml` + `App.xaml.cs` | 应用入口、全局资源与样式 |
| `src/Ets2SaveEditor/MainWindow.xaml` + `MainWindow.xaml.cs` | 主窗口：菜单栏（文件 / 编辑 / 帮助）+ 左侧导航 + 右侧 `TabControl` 标签容器，绑定 `MainViewModel` |
| `src/Ets2SaveEditor/ViewModels/MainViewModel.cs` | 聚合各功能 VM、当前 `SaveFile`、打开 / 保存 / 备份命令 |
| `src/Ets2SaveEditor/ViewModels/ProfileViewModel.cs` | 档案页：路径 / 等级 / 技能 / 金钱（P0-3~P0-5） |
| `src/Ets2SaveEditor/ViewModels/GaragesViewModel.cs` | 车库页：列表 / 购买 / 升级（P0-6） |
| `src/Ets2SaveEditor/ViewModels/TruckViewModel.cs` | 卡车页：修理 / 加油 / 颜色 / 导出导入 / 位置（P0-7, P1-1, P2-1） |
| `src/Ets2SaveEditor/ViewModels/TrailerViewModel.cs` | 挂车页：列表 / 修理 / 颜色（P1-6） |
| `src/Ets2SaveEditor/ViewModels/FreightMarketViewModel.cs` | 货运市场页：自定义任务创建 / 管理（P1-4） |
| `src/Ets2SaveEditor/ViewModels/CargoViewModel.cs` | 货物页：市场浏览 / 编辑 / 已访问城市抓货（P1-2, P1-3, P1-5） |
| `src/Ets2SaveEditor/ViewModels/SettingsViewModel.cs` | 设置页：默认路径 / Steam 探测开关 / 自动备份开关 / 语言 / 关于 |
| `src/Ets2SaveEditor/Views/ProfileView.xaml` + `.xaml.cs` | 档案页视图 |
| `src/Ets2SaveEditor/Views/GaragesView.xaml` + `.xaml.cs` | 车库页视图 |
| `src/Ets2SaveEditor/Views/TruckView.xaml` + `.xaml.cs` | 卡车页视图 |
| `src/Ets2SaveEditor/Views/TrailerView.xaml` + `.xaml.cs` | 挂车页视图 |
| `src/Ets2SaveEditor/Views/FreightMarketView.xaml` + `.xaml.cs` | 货运市场页视图 |
| `src/Ets2SaveEditor/Views/CargoView.xaml` + `.xaml.cs` | 货物页视图 |
| `src/Ets2SaveEditor/Views/SettingsView.xaml` + `.xaml.cs` | 设置页视图 |
| `src/Ets2SaveEditor/Services/DialogService.cs` | 文件打开 / 保存对话框、消息提示、路径选择（`OpenFileDialog` / `SaveFileDialog` / `FolderBrowserDialog`） |
| `src/Ets2SaveEditor/Converters/*.cs` | 必要的 WPF 值转换器（如 bool→可见性、磨损→进度色） |

---

## 3. 数据结构与接口（类图）

### 3.1 通用 SII 模型 + 值类型

```mermaid
classDiagram
    class SiiValue {
        <<abstract>>
        +SiiValueKind Kind
    }
    class IntValue {
        +long Value
    }
    class FloatValue {
        +double Value
    }
    class StringValue {
        +string Value
    }
    class BoolValue {
        +bool Value
    }
    class VectorValue {
        +double X
        +double Y
        +double Z
    }
    class ReferenceValue {
        +string TargetId
    }
    class ArrayValue {
        +List~SiiValue~ Items
    }
    SiiValue <|-- IntValue
    SiiValue <|-- FloatValue
    SiiValue <|-- StringValue
    SiiValue <|-- BoolValue
    SiiValue <|-- VectorValue
    SiiValue <|-- ReferenceValue
    SiiValue <|-- ArrayValue

    class SiiAttribute {
        +string Name
        +SiiValue Value
        +bool IsArray
        +int Order
        +string RawText
    }
    class SiiUnit {
        +string Type
        +string Id
        +List~SiiAttribute~ Attributes
        +SiiAttribute GetAttribute(string name)
        +void SetAttribute(string name, SiiValue value)
        +T GetValue~T~(string name)
        +SiiAttribute GetOrCreate(string name, SiiValue def)
    }
    class SiiDocument {
        +string Header
        +List~SiiUnit~ Units
        +SiiUnit GetUnitById(string id)
        +List~SiiUnit~ GetUnitsByType(string type)
    }
    SiiDocument "1" *-- "0..*" SiiUnit : contains
    SiiUnit "1" *-- "0..*" SiiAttribute : has
    SiiAttribute "1" *-- "1" SiiValue : holds
```

### 3.2 解析 / 序列化 / 存档服务

```mermaid
classDiagram
    class SiiFormatDetector {
        <<enum>> SiiFormat { Plaintext, Zlib, Binary, Unknown }
        +SiiFormat Detect(byte[] raw)
        +string DecompressZlib(byte[] raw)
    }
    class SiiParser {
        +SiiDocument Parse(string text)
        -SiiUnit ParseUnit(...)
        -SiiAttribute ParseAttribute(...)
    }
    class SiiSerializer {
        +string Serialize(SiiDocument doc)
    }
    class SaveFile {
        +string Path
        +SiiFormat Format
        +SiiDocument Document
        +bool IsLoaded
        +void Load(string path)
        +void Save(string path)
        +Bank GetBank()
        +Player GetPlayer()
        +List~Garage~ GetGarages()
        +List~Vehicle~ GetVehicles()
        +List~Trailer~ GetTrailers()
    }
    class SaveLocationFinder {
        +List~SaveSlot~ FindLocalProfiles()
        +List~SaveSlot~ FindSteamProfiles()
    }
    class BackupService {
        +string Backup(string path, bool enabled)
    }
    class SaveSlot {
        +string ProfileId
        +string SlotName
        +string FullPath
    }
    SaveFile ..> SiiFormatDetector : uses
    SaveFile ..> SiiParser : uses
    SaveFile ..> SiiSerializer : uses
    SaveFile ..> BackupService : uses
    SiiParser ..> SiiDocument : builds
    SiiSerializer ..> SiiDocument : reads
    SaveLocationFinder ..> SaveSlot : returns
```

### 3.3 类型化包装层 + 游戏数据

```mermaid
classDiagram
    class SiiUnitWrapper {
        <<abstract>>
        #SiiUnit Unit
        #SiiDocument Document
        +SiiUnitWrapper(SiiUnit unit, SiiDocument doc)
    }
    class Bank {
        +long MoneyAccount
        +long BankLoan
    }
    class Player {
        +long Experience
        +int Level
        +int LongDist
        +int Heavy
        +int Fragile
        +int Urgent
        +int EcoDriving
    }
    class Garage {
        +string Status
        +string Location
        +int Upgrade
    }
    class Vehicle {
        +double EngineWear
        +double ChassisWear
        +double Fuel
        +double FuelCapacity
        +void Repair()
        +void Refuel()
    }
    class Trailer {
        +double Wear
        +void Repair()
    }
    class JobOffer {
        +string SourceCity
        +string TargetCity
        +string Cargo
        +long Reward
    }
    class Economy {
        +List~string~ VisitedCities
    }
    SiiUnitWrapper <|-- Bank
    SiiUnitWrapper <|-- Player
    SiiUnitWrapper <|-- Garage
    SiiUnitWrapper <|-- Vehicle
    SiiUnitWrapper <|-- Trailer
    SiiUnitWrapper <|-- JobOffer
    SiiUnitWrapper <|-- Economy
    SiiUnitWrapper ..> SiiUnit : wraps
    SiiUnitWrapper ..> SiiDocument : queries

    class City { +string Token +string Name +double X +double Y +double Z }
    class Cargo { +string Token +string Name }
    class Company { +string Token +string Name }
    class IGameDataProvider {
        +List~City~ Cities()
        +List~Cargo~ Cargoes()
        +List~Company~ Companies()
    }
    class BuiltInGameData {
        +List~City~ Cities()
    }
    BuiltInGameData ..|> IGameDataProvider
```

### 3.4 ViewModel 层（MVVM）

```mermaid
classDiagram
    class MainViewModel {
        +SaveFile CurrentSave
        +ProfileViewModel Profile
        +GaragesViewModel Garages
        +TruckViewModel Truck
        +TrailerViewModel Trailer
        +FreightMarketViewModel Freight
        +CargoViewModel Cargo
        +SettingsViewModel Settings
        +AsyncRelayCommand OpenCommand
        +AsyncRelayCommand SaveCommand
        +AsyncRelayCommand BackupCommand
    }
    class ProfileViewModel {
        +string SavePath
        +long MoneyAccount
        +int Level
        +bool SkillLongDist
        +AsyncRelayCommand ApplyProfileCommand
    }
    class GaragesViewModel {
        +ObservableCollection~GarageItem~ Garages
        +AsyncRelayCommand BuyUpgradeCommand
    }
    class TruckViewModel {
        +ObservableCollection~VehicleItem~ Trucks
        +AsyncRelayCommand RepairCommand
        +AsyncRelayCommand RefuelCommand
    }
    class TrailerViewModel {
        +ObservableCollection~TrailerItem~ Trailers
        +AsyncRelayCommand RepairTrailerCommand
    }
    class FreightMarketViewModel {
        +ObservableCollection~JobOfferItem~ Jobs
        +AsyncRelayCommand CreateJobCommand
    }
    class CargoViewModel {
        +ObservableCollection~CargoItem~ Market
        +AsyncRelayCommand MarkVisitedCommand
    }
    class SettingsViewModel {
        +bool AutoBackup
        +bool SteamDetection
        +string DefaultSavePath
    }
    MainViewModel *-- ProfileViewModel
    MainViewModel *-- GaragesViewModel
    MainViewModel *-- TruckViewModel
    MainViewModel *-- TrailerViewModel
    MainViewModel *-- FreightMarketViewModel
    MainViewModel *-- CargoViewModel
    MainViewModel *-- SettingsViewModel
```

---

## 4. 程序调用流程（时序图）

打开存档 → 探测格式 → 解压(zlib) → 解析为通用模型 → 类型化包装供 UI 编辑 → 序列化回明文 → 写回文件（含备份）。

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant UI as MainWindow/MainViewModel
    participant Finder as SaveLocationFinder
    participant Det as SiiFormatDetector
    participant Parser as SiiParser
    participant Doc as SiiDocument
    participant Wrap as Typed Wrappers(Bank/Player/...)
    participant Ser as SiiSerializer
    participant Backup as BackupService
    participant FS as FileSystem

    User->>UI: 选择存档路径 / 浏览
    UI->>Finder: FindLocalProfiles()/FindSteamProfiles()
    Finder-->>UI: List<SaveSlot>
    User->>UI: 点击"打开"
    UI->>FS: ReadAllBytes(path)
    FS-->>UI: byte[]
    UI->>Det: Detect(byte[])
    alt 明文 SII
        Det-->>UI: SiiFormat.Plaintext
        UI->>UI: text = Encoding.UTF8.GetString(byte[])
    else zlib 压缩
        Det-->>UI: SiiFormat.Zlib
        UI->>Det: DecompressZlib(byte[])
        Det-->>UI: string(明文 SII)
    end
    UI->>Parser: Parse(text)
    Parser->>Doc: new SiiDocument(); AddUnit(...)
    Doc-->>UI: SiiDocument
    UI->>Wrap: new Bank(doc.GetUnitsByType("bank")[0], doc)
    Wrap-->>UI: 类型化对象(其余字段透传)
    UI->>UI: 绑定到 ViewModel 属性(金钱/等级/技能/...)
    User->>UI: 编辑字段并点击"应用/保存"
    UI->>Wrap: SetAttribute(...)/Repair()/Refuel()
    Wrap->>Doc: 修改底层 SiiUnit 属性
    UI->>Ser: Serialize(Doc)
    Ser-->>UI: string(明文 SII 文本)
    UI->>Backup: Backup(originalPath, settings.AutoBackup)
    Backup->>FS: 写入 .bak.<timestamp>
    UI->>FS: WriteAllText(path, serialized)
    FS-->>UI: 保存成功
    UI-->>User: 提示保存成功(游戏加载时自动重压缩)
```

---

## 5. 任务列表（有序、含依赖、按实现顺序）

> 优先级：P0=核心必须，P1=重要，P2=可选增强。工程师应按编号顺序执行；「所属」标注负责项目/主要文件。

| 编号 | 任务名称 | 所属 / 主要文件 | 依赖 | 优先级 |
| --- | --- | --- | --- | --- |
| **T01** | 解决方案与项目骨架 | `Ets2SaveEditor.sln`、`src/Ets2SaveEditor.Core/*.csproj`、`src/Ets2SaveEditor/*.csproj`、`App.xaml`、`MainWindow.xaml`、`Common/Constants.cs` | 无 | P0 |
| **T02** | 通用 SII 模型与格式检测器 | `Sii/Model/*`、`Sii/SiiFormatDetector.cs`、`Sii/SiiFormatException.cs` | T01 | P0 |
| **T03** | SII 解析器与序列化器（无损透传） | `Sii/SiiParser.cs`、`Sii/SiiSerializer.cs` | T02 | P0 |
| **T04** | 存档加载 / 保存服务 | `Domain/SaveFile.cs`、`Persistence/SaveLocationFinder.cs`、`Persistence/BackupService.cs` | T02, T03 | P0 |
| **T05** | 类型化领域包装层 | `Domain/IUnitCollection.cs`、`Domain/TypedUnits/*`（`Bank`/`Player`/`Garage`/`Vehicle`/`Trailer`/`JobOffer`/`Economy`） | T02 | P0/P1 |
| **T06** | 游戏静态数据提供器 | `GameData/GameDataModel.cs`、`GameData/IGameDataProvider.cs`、`GameData/BuiltInGameData.cs` | T01 | P1 |
| **T07** | MVVM ViewModel 层 | `ViewModels/MainViewModel.cs` 及各功能 VM | T04, T05, T06 | P0/P1 |
| **T08** | WPF 视图与标签页 | `Views/*.xaml` + `.xaml.cs`、`MainWindow.xaml` 标签容器 | T07 | P0/P1 |
| **T09** | 对话框服务与集成联调 | `Services/DialogService.cs`、`Converters/*`、`SettingsView` 设置持久化、关于 / 免责声明、整体联调 | T07, T08 | P0/P1 |
| **T10** | P2 扩展预留接口（二进制/3NK、Mod 扫描、分享导入导出框架） | `SiiFormatDetector` 扩展点、`IGameDataProvider` Mod 扩展、分享序列化工具骨架 | T04, T05 | P2 |

### 5.1 任务依赖图

```mermaid
graph TD
    T01[T01 解决方案与项目骨架 P0]
    T02[T02 通用SII模型+检测器 P0]
    T03[T03 解析器+序列化器 P0]
    T04[T04 存档加载/保存服务 P0]
    T05[T05 类型化领域包装层 P0/P1]
    T06[T06 游戏静态数据提供器 P1]
    T07[T07 MVVM ViewModel层 P0/P1]
    T08[T08 WPF视图与标签页 P0/P1]
    T09[T09 对话框服务与集成联调 P0/P1]
    T10[T10 P2扩展预留接口 P2]

    T01 --> T02
    T01 --> T06
    T02 --> T03
    T02 --> T05
    T03 --> T04
    T04 --> T07
    T05 --> T07
    T06 --> T07
    T07 --> T08
    T07 --> T09
    T08 --> T09
    T04 --> T10
    T05 --> T10
```

---

## 6. 依赖包列表（NuGet）

| 包 | 版本 | 用途 | 备注 |
| --- | --- | --- | --- |
| `CommunityToolkit.Mvvm` | `^8.2.2` | MVVM 源码生成器（`[ObservableProperty]` / `RelayCommand` / `AsyncRelayCommand`） | WPF 项目引用，Core 不引用 |
| （无） | — | zlib 解压 | 使用 .NET 8 内置 `System.IO.Compression.ZLibStream`，**无需额外 NuGet 包** |
| （无） | — | WPF UI | 使用 .NET 8 SDK 内置 WPF（`Microsoft.NET.Sdk`，`<UseWPF>true</UseWPF>`） |

> 设计原则：Core 项目 **零第三方依赖**，便于复用与测试；所有外部能力均由 BCL 提供。

---

## 7. 共享知识（跨文件约定）

- **命名空间**：Core 采用 `Ets2SaveEditor.Core.{Sii|Domain|Persistence|GameData}`；WPF 采用 `Ets2SaveEditor.{ViewModels|Views|Services|Converters}`。
- **SII 数值规则**：整数统一 `long`（避免大金额溢出）；浮点 / 磨损统一 `double`；向量 `(x, y, z)`；引用为**无引号**的 nameless id。
- **字符串转义**：属性值双引号包裹，内部 `\"`、换行等按 SII 规则转义；序列化时优先使用解析阶段保留的 `RawText` 兜底，确保字节级无损。
- **透传约定（关键）**：解析器对任意未识别的 unit / attribute / value **原样保留**；类型化包装只读写已知属性，绝不删除或重排未知字段——这是存档不损坏的根本保证。
- **单位约定**：金钱 / 经验使用游戏内原生单位；坐标为米制（与 SII 一致）；磨损范围 0.0（全新）~1.0（报废），修理即写 0、加油即写 `fuel_capacity`。
- **等级推导**：`Level` 由 `Experience`（XP）经内置阈值表推导；阈值表内置为 `Player` 静态字典，待真实存档校验。
- **MVVM 约定**：VM 通过 `CommunityToolkit.Mvvm` 生成属性 / 命令；**VM 绝不引用 View**；`SaveFile` 作为唯一可绑定模型源；长耗时操作（加载 / 保存）使用 `AsyncRelayCommand` 并避免阻塞 UI 线程。
- **备份命名**：`<name>.sii.bak.<yyyyMMddHHmmss>`；默认开关由 `SettingsViewModel.AutoBackup` 控制，保留最近 N 份（N 待定，默认 5）。
- **错误策略**：任何解析 / 解压失败都**抛明确异常、不写入原文件**，UI 给予友好提示，原存档保持只读未动。

---

## 8. 待明确事项（需在真实存档上校验）

1. **字段名校验（高优先）**：以下属性名以真实 `game.sii` / `economy.sii` / `info.sii` 为准，v1 须抽样校验：
   - 金钱：`bank` unit 的 `bank` 还是 `money_account`（PRD 两处表述不一致，需统一）。
   - 贷款：`bank_loan`。
   - 等级 / 经验：`game`（或 `player`）unit 的 `experience`；等级是否直接存字段还是需要 XP 推导。
   - 技能：`long_dist` / `heavy` / `fragile` / `urgent` / `ecodriving` 等属性名与取值范围（0–6）。
   - 车库：`garage` unit 的 `status` / `location` / `upgrade` 取值含义。
   - 卡车磨损：`vehicle` unit 中 `wear_chassis` / `wear_engine` / `wear_transmission` / `wear_cabin` 等具体键。
2. **二进制 / 3NK 格式（P2）**：v1 仅明文 + zlib；`SiiFormatDetector` 已预留 `Binary` / `Unknown` 分支，后续接入参考项目 `SII_Decrypt.dll` 的另两种格式解析。
3. **Steam 路径探测方式**：注册表（`HKEY_CURRENT_USER\Software\Valve\Steam` 的 `SteamPath`）vs 常见路径规则；建议「注册表优先 + 手动指定兜底」。
4. **备份策略默认开关与保留数量**：默认是否开启、保留份数、命名规则（见 §7）。
5. **数值边界与校验**：金钱 / 等级 / 技能是否对齐游戏内上限；超限策略（截断 / 警告 / 拒绝）。
6. **Mod 数据来源（P2-5）**：创意工坊订阅目录解析优先级是否高于编队控制包（P2-3），需产品确认。
7. **城市 / 货物 / 公司清单**：`BuiltInGameData` v1 仅内置少量（如基础 20+ 城市），需随 DLC / 版本扩充策略。

---

## 9. 架构要点汇报（给主理人）

本方案采用 **.NET 8 + WPF + MVVM**，双项目分层（`Core` 类库 + `WPF` 应用），核心为**通用 SII 模型（无损透传）+ 类型化包装层**双层结构，从根上杜绝存档损坏；zlib 用 .NET 原生 `ZLibStream` 零额外依赖。共规划 **10 个有序任务**（T01 骨架 → T02/T03 模型与解析 → T04 存档服务 → T05 包装层 → T06 游戏数据 → T07/T08/T09 MVVM 与视图联调 → T10 P2 扩展），P0 覆盖加载/保存/金钱/等级/技能/车库/卡车修理加油，P1 覆盖涂装/城市/货运市场，P2 预留二进制/3NK 与 Mod 扫描。所有未识别字段原样保留，确保安全性；待真实存档校验字段名与备份策略。
