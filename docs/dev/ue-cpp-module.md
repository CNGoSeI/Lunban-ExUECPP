# Luban.UECpp — Unreal Engine C++ 代码生成模块

## 背景

用户使用 **Unreal Engine 5.3+** 开发游戏，需要 Luban 生成的 C++ 代码符合 UE 生态规范。现有 `Luban.Cpp` 模块生成的是标准 C++ 代码（STL 容器、自定义 Luban 类型），无法直接用于 UE 项目（缺少 UE 宏、UE 容器类型）。

## 本轮开发内容

新增 `Luban.UECpp` 模块，CodeTarget 名称 **`ue-cpp-bin`**，生成满足以下要求的 UE C++ 代码：

- 使用 **USTRUCT(BlueprintType)** / **UENUM(BlueprintType)** 宏标记类型
- 字段使用 **UPROPERTY(BlueprintReadOnly)** 暴露给蓝图（配置数据运行时只读）
- 容器映射到 UE 原生类型：`TArray<T>`、`TMap<K,V>`、`TSet<T>`、`TOptional<T>`
- 字符串默认映射为 `FString`
- 字段名以 `_fname` 结尾（不区分大小写）时自动映射为 `FName`（性能优于 FString）
- 通过 **FArchive** 原生 UE 序列化进行数据反序列化
- 遵循 UE 命名约定：结构体 `F` 前缀，枚举 `E` 前缀
- Table Manager 暴露 BlueprintReadOnly 数据，结构体可在蓝图中访问

### 新增文件清单

| 文件 | 说明 |
|---|---|
| `src/Luban.UECpp/Luban.UECpp.csproj` | .NET 8 项目，引用 Luban.Core |
| `src/Luban.UECpp/AssemblyInfo.cs` | `[assembly: RegisterBehaviour]` 发现标记 |
| `src/Luban.UECpp/CodeTarget/UECppBinCodeTarget.cs` | `[CodeTarget("ue-cpp-bin")]` 代码生成入口 |
| `src/Luban.UECpp/TemplateExtensions/UECppBinTemplateExtension.cs` | Scriban 模板函数：类型名/反序列化/命名辅助 |
| `src/Luban.UECpp/TypeVisitors/UECppDeclaringTypeNameVisitor.cs` | 顶层类型名（nullable→TOptional 包装） |
| `src/Luban.UECpp/TypeVisitors/UECppUnderlyingDeclaringTypeNameVisitor.cs` | Luban 类型到 UE 类型映射 |
| `src/Luban.UECpp/TypeVisitors/UECppDeserializeVisitor.cs` | 反序列化代码生成（nullable 装饰） |
| `src/Luban.UECpp/TypeVisitors/UECppUnderlyingDeserializeVisitor.cs` | 按类型生成 FArchive 反序列化逻辑 |
| `src/Luban.UECpp/Templates/common/uecpp/enum.sbn` | UE 枚举模板 |
| `src/Luban.UECpp/Templates/ue-cpp-bin/bean.sbn` | UE 结构体模板 |
| `src/Luban.UECpp/Templates/ue-cpp-bin/table.sbn` | 表管理器模板（TMap/TArray + Load） |
| `src/Luban.UECpp/Templates/ue-cpp-bin/tables.sbn` | 顶层 Tables 聚合模板 |
| `src/Luban.UECpp/Templates/ue-cpp-bin/schema_h.sbn` | 头文件汇合模板 |
| `src/Luban.UECpp/Templates/ue-cpp-bin/schema_cpp.sbn` | 实现文件（FLubanArchive operator<<） |

### 修改文件

| 文件 | 变更 |
|---|---|
| `src/Luban/Luban.csproj` | 添加 `<ProjectReference>` 引用 Luban.UECpp |

## 类型映射

| Luban 类型 | UE C++ 类型 |
|---|---|
| `bool` | `bool` |
| `byte` | `uint8` |
| `short` | `int32` |
| `int` | `int32` |
| `long` | `int64` |
| `float` | `float` |
| `double` | `double` |
| `string` | `FString` |
| `string` + `_fname` 命名 | `FName`（自动检测） |
| `datetime` | `int64` |
| `byte` + `_uint` 命名 | `uint8`（不变） |
| `short` + `_uint` 命名 | `uint32` |
| `int` + `_uint` 命名 | `uint32` |
| `long` + `_uint` 命名 | `uint64` |
| `long` + `_uint32` 命名 | `uint32`（突破 int32 上限） |
| `TArray` / `TList` | `TArray<T>` |
| `TSet` | `TSet<T>` |
| `TMap` | `TMap<K, V>` |
| nullable 非 Bean 类型 | `TOptional<T>` |
| enum | `E{Name}_{Namespace}` + `UENUM(BlueprintType)`，如 `EAccessFlag_test` |
| bean | `F{Name}_{Namespace}` + `USTRUCT(BlueprintType)`，如 `FCircle_test`、`FFaBaoAttr_XiuShiSys` |
| table manager | `F{Name}_{Namespace}` + `USTRUCT(BlueprintType)`，如 `FTbTest_test` |

## 使用方法

```bash
luban --conf game_config.xml -t default -c ue-cpp-bin -d bin
```

生成文件：
- `Schema.h` — 所有枚举、结构体、Table Manager 的声明
- `Schema_0.cpp` — 所有序列化实现（bean 较多时产生 `Schema_1.cpp` 等分片）

将生成的文件放入 UE 项目的 `Source/YourModule/` 目录即可。

## 设计决策

1. **FLubanArchive 序列化**：使用 `friend FLubanArchive& operator<<` 在 UE 原生 `Ar << value` 语法下兼容 Luban 的 varint 变长编码二进制格式
2. **值类型优先**：USTRUCT 按值持有，TArray/TMap 直接包含数据，不涉及手动内存管理
3. **BlueprintType 标记**：所有结构体标记为 BlueprintType，可在蓝图中作为变量类型使用
4. **UPROPERTY 暴露数据**：Table Manager 的 DataMap/DataList 通过 UPROPERTY 暴露，Blueprint 可直接读取
5. **FName 自动检测**：根据字段命名约定自动将 `string` + `_fname` 后缀字段映射为 `FName`（如 `item_fname`、`display_fname`），详见 [FName 支持](#fname-支持) 章节
6. **分片编译**：bean 数量超过阈值时自动拆分 .cpp，避免单文件过大
7. **全局作用域命名**：由于 UE 5.6 UHT 不支持命名空间内的 UENUM/USTRUCT，所有类型放在全局作用域，命名空间路径编码为类型名后缀（`F{Name}_{Namespace}`、`E{Name}_{Namespace}`），详见 [UE 5.6 UHT 命名空间限制](#ue-56-uht-命名空间限制)
8. **`.generated.h` 位置**：UE 5.6 要求 `#include "X.generated.h"` 放在头文件顶部（紧跟其他 include 之后），而非传统的文件末尾

## UE 5.6 UHT 命名空间限制

### 问题

UE 5.6 的 UnrealHeaderTool (UHT) **不支持** `namespace { }` 块内的 `UENUM()` / `USTRUCT()` 宏。如果类型的宏定义出现在 C++ 命名空间内部，UHT 会进入"跳过"状态，忽略所有后续的 `GENERATED_BODY()` 声明，导致 `Total of 0 written`——不生成任何反射数据，项目无法编译。

### 已验证事实

- `USTRUCT(BlueprintType)` 放在 `namespace cfg { namespace test { } }` 内 → **UHT 跳过，编译失败**
- `USTRUCT(BlueprintType)` 放在全局作用域 → **UHT 正常处理，编译通过**

### 解决方案

**所有生成类型放在全局作用域**，使用类型名后缀保留命名空间信息：

```
Luban 命名空间         →  生成的 UE 类型名
─────────────────────────────────────────
test.Circle           →  FCircle_test
test2.Rectangle       →  FRectangle_test2
XiuShiSys.FaBaoAttr   →  FFaBaoAttr_XiuShiSys
test.AccessFlag (枚举) →  EAccessFlag_test
```

命名规则：`F/E{Name}_{Namespace}`（UE 前缀在最前，命名空间路径用下划线连接、放在最后）。

### `.generated.h` 位置变化

UE 5.6 要求 `#include "Schema.generated.h"` 必须放在头文件**顶部**（紧跟业务 include 之后），而非传统 UE 惯例的文件末尾。这是 UHT 在此版本的行为变更。

## FName 支持

### 自动识别规则

生成代码时，`string` 字段类型在某些条件下自动变为 `FName`：

1. Luban 定义中字段类型为 **`string`**
2. 字段名以 **`_fname`** 结尾（不区分大小写）

即：只有字段名末尾为 `_fname` 的 string 字段才会映射为 `FName`，如 `item_fname`、`display_fname`、`ATTR_FNAME` 等。之前匹配 `*Name*` 的宽泛规则已被替换，以避免误匹配（如将 `DisplayName`、`Name` 等普通字段强制转为 FName）。

### 示例

| 字段名 | Luban 类型 | 生成的 UE 类型 | 原因 |
|---|---|---|---|
| `Text` | `string` | `FString` | 不以 `_fname` 结尾 |
| `item_fname` | `string` | `FName` | 以 `_fname` 结尾 |
| `display_fname` | `string` | `FName` | 以 `_fname` 结尾 |
| `ATTR_FNAME` | `string` | `FName` | 以 `_fname` 结尾（不区分大小写） |
| `DisplayName` | `string` | `FString` | 不以 `_fname` 结尾 |
| `AttrTag` | `string` | `FString` | 不以 `_fname` 结尾 |
| `item_fname` | `int` | `int32` | 非 string 类型，不触发 |

### 为什么用 FName

- **不可变的字符串标识符**：`FName` 是 UE 的全局字符串池索引，插入后不可变，适合做表主键
- **O(1) 比较**：`FName == FName` 是整数比较，比 `FString` 的逐字符比较快很多
- **内存效率**：相同字符串只存一次，Table Manager 的 Map 键用 `FName` 比 `FString` 省内存
- **自由使用**：不需要像 `FGameplayTag` 那样在项目设置中预注册

### 运行时读取流程

```
Excel: item_fname = "火球术"
   ↓ Luban -d bin 导出为二进制
bin文件: 变长前缀 + "火球术" (UTF-8 字节)
   ↓ FLubanArchive::operator<<(FName&)
     └─ 先读成 FString "火球术"
     └─ FName("火球术") → 注册到全局名称表
   ↓
USTRUCT 中: FName item_fname
```

### 自定义识别规则

识别逻辑在 `UECppBinTemplateExtension.IsNameField()` 中实现。如果项目有自己的命名约定，可以修改该方法来适配。

## 无符号整数支持

### 自动识别规则

生成代码时，整数类型字段在指定条件下自动变为对应的无符号（unsigned）类型：

1. Luban 定义中字段类型为 **整数类型**（`byte`、`short`、`int`、`long`）
2. 字段名以 **`_uint`** 结尾（不区分大小写）

### 类型映射

| Luban 类型 | 后缀 | UE 类型 | 默认 UE 类型 |
|---|---|---|---|
| `byte` | `_uint` | `uint8`（不变） | `uint8` |
| `short` | `_uint` | `uint32` | `int32` |
| `int` | `_uint` | `uint32` | `int32` |
| `long` | `_uint` | `uint64` | `int64` |
| `long` | **`_uint32`** | **`uint32`** | `int64` |

> **`_uint32` 特别说明**：由于 Luban 的 `int` 类型本质是 int32，无法在 Excel 中写入超过 21.4 亿的值。当需要 `uint32` 类型且值可能超过 int32 上限时，使用 **`long` 类型 + `_uint32` 后缀**。`_uint32` 的检测优先级高于 `_uint`。

### 示例

| 字段名 | Luban 类型 | 生成的 UE 类型 | 原因 |
|---|---|---|---|
| `Count` | `int` | `int32` | 不以 `_uint` 结尾 |
| `count_uint` | `int` | `uint32` | 以 `_uint` 结尾 |
| `LevelId` | `int` | `int32` | 不以 `_uint` 结尾 |
| `raw_score_uint` | `int` | `uint32` | 以 `_uint` 结尾 |
| `big_value_uint` | `long` | `uint64` | 以 `_uint` 结尾 |
| **`big_id_uint32`** | `long` | **`uint32`** | 以 `_uint32` 结尾（long→uint32） |
| `count_uint` | `float` | `float` | 非整数类型，不触发 |

### 使用场景

- **网络同步 ID**：服务器分发的唯一 ID 通常用无符号类型，避免负数
- **位掩码字段**：需要用无符号类型保证位移操作的正确性
- **协议兼容**：与外部系统对接时，无符号类型能更准确地表达数据语义
- **`_uint32` 场景**：Excel 中的数据超出 int32 范围（如 30 亿），但又不需要 uint64 那么大

### 自定义识别规则

识别逻辑在 `UECppBinTemplateExtension.IsUintField()` 和 `IsUint32Field()` 中实现。如果项目有自己的命名约定，可以修改这些方法来适配。

## 架构说明

本模块遵循 Luban 的标准扩展模式：

```
AssemblyInfo.cs  ───RegisterBehaviour──→  SimpleLauncher 扫描发现
                                                  ↓
CodeTargetManager ──CreateCodeTarget("ue-cpp-bin")──→ UECppBinCodeTarget
                                                              ↓
                                                  TemplateCodeTargetBase
                                                    ├─ GenerateEnum  → enum.sbn
                                                    ├─ GenerateBean  → bean.sbn
                                                    ├─ GenerateTable → table.sbn
                                                    ├─ GenerateTables → tables.sbn
                                                    └─ Handle → schema_h.sbn + schema_cpp.sbn
                                                              ↓
                                              Scriban 模板渲染
                                                ├─ UECppBinTemplateExtension (模板函数)
                                                └─ TypeVisitors (类型名/反序列化代码)
```

## 运行时依赖

生成的代码需要一个运行时文件：`LubanArchive.h`

### FLubanArchive

`LubanArchive.h` 包含 `FLubanArchive` 类（header-only），用 UE 原生类型封装了 Luban 变长编码（varint）的二进制读取：

- `operator<<(bool&)` — 1 字节直接读取
- `operator<<(int32&)` — varint 变长解码（1-5 字节）
- `operator<<(int64&)` — varint 变长解码（1-9 字节）
- `operator<<(float&)` — 4 字节定长
- `operator<<(double&)` — 8 字节定长
- `operator<<(FString&)` — 变长前缀 + UTF-8 字节
- `operator<<(FName&)` — 从 FString 转换
- `ReadSize()` — 读取变长编码的容器长度

与 Luban `-d bin` 产出的二进制格式完全兼容。

```cpp
// 使用
FLubanArchive Ar;
Ar.LoadFromFile(TEXT("Content/Data/LubanOutBin/xiushisys_tbfabaoattr"));
FTables Tables;
Tables.Load(Ar);  // Ar << value 语法
```

