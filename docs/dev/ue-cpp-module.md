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

**推荐目录结构：**

```
Source/YourModule/
├── Public/Luban/Gen/
│   └── Schema.h          ← 头文件放 Public 下，对外可见
└── Private/Luban/Gen/
    └── Schema_0.cpp      ← 实现文件放 Private 下
```

> **包含路径**：UE 模块默认将 `Public/` 加入头文件搜索路径，无需额外配置。如果生成目录不在 `Public/` 下方，需在 `YourModule.Build.cs` 中添加 `PublicIncludePaths.Add("...");`，确保 `#include "Schema.h"` 能被找到，同时 `.generated.h` 也能被 UHT 正确扫描。

## 设计决策

1. **FLubanArchive 序列化**：使用 `friend FLubanArchive& operator<<` 在 UE 原生 `Ar << value` 语法下兼容 Luban 的 varint 变长编码二进制格式
2. **值类型优先**：USTRUCT 按值持有，TArray/TMap 直接包含数据，不涉及手动内存管理
3. **BlueprintType 标记**：所有结构体标记为 BlueprintType，可在蓝图中作为变量类型使用
4. **UPROPERTY 暴露数据**：Table Manager 的 DataList 通过 UPROPERTY 暴露，可直接绑定到 Blueprint 的 ListView 等控件
5. **FName 自动检测**：根据字段命名约定自动将 `string` + `_fname` 后缀字段映射为 `FName`（如 `item_fname`、`display_fname`），详见 [FName 支持](#fname-支持) 章节
6. **TMap 索引存储**：Map 类型表不重复存储数据 —— TArray 存数据本体，TMap 只存 `Key → int32下标`（详见 [表存储架构](#表存储架构)）
8. **分片编译**：bean 数量超过阈值时自动拆分 .cpp，避免单文件过大
9. **全局作用域命名**：由于 UE 5.6 UHT 不支持命名空间内的 UENUM/USTRUCT，所有类型放在全局作用域，命名空间路径编码为类型名后缀（`F{Name}_{Namespace}`、`E{Name}_{Namespace}`），详见 [UE 5.6 UHT 命名空间限制](#ue-56-uht-命名空间限制)
10. **`.generated.h` 位置**：UE 5.6 要求 `#include "X.generated.h"` 放在头文件顶部（紧跟其他 include 之后），而非传统的文件末尾

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

## 表存储架构

### Map 表（有主键）

数据**只存一份**在 TArray 中，TMap 仅存储 `Key → int32下标`：

```cpp
USTRUCT(BlueprintType)
struct FTbTest_test
{
    /** 数据本体 — 直接绑定 Blueprint ListView */
    UPROPERTY(BlueprintReadOnly, Category = "Luban")
    TArray<FTest_test> DataList;

    /** Key → 下标索引（不暴露蓝图，用 Get/Contains 查询） */
    TMap<uint32, int32> IndexMap;

    bool Load(FLubanArchive& Ar)
    {
        // ...
        IndexMap.Add(_v.Id_uint32, DataList.Num());  // 存下标
        DataList.Add(MoveTemp(_v));                    // Move 不拷贝
        // ...
    }

    FTest_test Get(uint32 Key) const
    {
        const int32* Idx = IndexMap.Find(Key);  // O(1) 哈希
        return Idx ? DataList[*Idx] : FTest_test{}; // O(1) 下标取数据
    }
};
```

**内存对比：**

| 方案 | 数据存储 | 查找结构 | 蓝图遍历 |
|---|---|---|---|
| 旧版（双份） | TMap<K,V> + TArray<V> | DataMap 直接存 V | DataList（OK） |
| **新版（索引）** | **TArray<V> 单份** | TMap<K,**int32**> 4字节下标 | DataList（OK） |

对于大结构体或大表，索引方案可节省近 50% 内存；对于小表（几十行 × 十几个字段），差异可忽略。

### List 表（无主键 / 联合主键）

List 表仅保留 TArray，不做索引。联合主键表仍使用 TMap 存数据（按联合 key 查找，不暴露 DataList）。

## Blueprint 函数库

### 背景

USTRUCT 不支持 `UFUNCTION` 宏，Map 表的 `Get()` / `Contains()` 方法无法被蓝图直接调用。通过生成一个 `UBlueprintFunctionLibrary` 类，以静态 `UFUNCTION(BlueprintPure)` 方法为每个 Map 表提供蓝图可用的查找接口。

### 生成规则

为每个 **Map 表**（存在唯一主键）生成 `Find` 和 `Contains` 两个静态蓝图函数：

| 方法 | 返回 | 说明 |
|---|---|---|
| `Find{表名}` | `bool` | 按键查找，通过 `OutValue` 参数输出结果 |
| `Contains{表名}` | `bool` | 检查 Key 是否存在 |

### 蓝图使用

```
FindTbTest 节点（BlueprintPure）
  ├─ Table    (FTables)        ← 传入根配置结构体
  ├─ Key      (int32/FName/FString)
  ├─ OutValue (FTest_test&)    ← 输出找到的数据行
  └─ Return   (bool)           ← true=找到, false=未找到
```

蓝图调用流程：
1. 拖入 `FindTbTest` 节点
2. `Table` 引脚连到根 `FTables` 变量
3. 填入查找的 `Key` 值
4. 通过 `Return Value` 判断是否找到
5. 从 `OutValue` 引脚读取数据

### 生成代码示例

```cpp
UCLASS()
class ULubanBPFunctionLibrary : public UBlueprintFunctionLibrary
{
    GENERATED_BODY()

public:
    UFUNCTION(BlueprintPure, Category = "Luban|TbTest")
    static bool FindTbTest(
        const FTables& Table,
        int32 Key,
        FTest_test& OutValue)
    {
        if (const FTest_test* Ptr = Table.TbTest.Get(Key))
        {
            OutValue = *Ptr;
            return true;
        }
        return false;
    }

    UFUNCTION(BlueprintPure, Category = "Luban|TbTest")
    static bool ContainsTbTest(
        const FTables& Table,
        int32 Key)
    {
        return Table.TbTest.Contains(Key);
    }
};
```

> **C++ 调用**：直接使用 `Tables.TbTest.Get(Key)` 返回 `const T*` 零拷贝指针；蓝图通过函数库间接访问，内部发生一次值拷贝（`OutValue = *Ptr`）。

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

