---
title: 框架组转交摘要——G7b（Tier 1.5 DateTime? nullable TypeHandler 未注册）
date: 2026-09-13
source: XiaoShuTong 框架问题单（docs/草稿/框架问题单-Tier1.5-SQLite内存库-v4.10.8不可用.md，G7b 节）
转交状态: 待框架组处理（完整问题单已推送 XiaoShuTong main）
---

# G7b 转交摘要：DateTimeUtcHandler 未覆盖 `DateTime?`（nullable）

> 本摘要供框架组快速定位；完整上下文见 XiaoShuTong 问题单 G7b 节（含源码级修复代码）。

## 一、现象

**v4.10.18（G7 修复已含）下，XiaoShuTong 7 个 DateTime 断言仍失败 +8h**（Kind=Unspecified）：

```
期望 2026-09-19T22:33:24.5813690Z
实际 2026-09-20T06:33:24.5813690   ← +8h（System.Data.SQLite 原生路径 Kind 丢失）
```

涉及 7 用例：ListSubscriptions×1 / CancelSubscription×1 / ListMyTasks×2 / ExportRosterCsv×2 / GenerateInviteCodes×1——全部为 **`DateTime?`（nullable）字段**（TrialEndAt/PeriodEndAt/DeadlineAt/CsvExpiresAt/ExpiresAt）。

## 二、根因

`SqliteTypeHandlerRegistrar.EnsureRegistered()` 只注册：

```csharp
TypeHandlers[typeof(DateTime)] = new DateTimeUtcHandler();   // 非空 DateTime 命中 ✓
```

**未注册 `typeof(DateTime?)`** → FreeSql 对 nullable 属性查字典不命中 → 走原生路径 +8h。

## 三、机制证据（FreeSql 3.5.311 源码审计）

FreeSql 对 `Nullable<T>` **不 unwrap**，TypeHandler 查找两条路径均直接类型比较：
- 写入/建表：`Aop.ConfigEntityProperty` 中 `e.Property.PropertyType == typeHandler.Type`（`typeof(DateTime?) == typeof(DateTime)` → false）
- 读取：`TypeHandlers.TryGetValue(type2, ...)`（以 `typeof(DateTime?)` 为 key 查字典 → 无此键）
- `NullableTypeOrThis()` 存在但不用于 TypeHandler 查找

⚠️ 官方 G7 回归测试实体 `Tier15DateTimeEntity.CreatedAt` 是**非空 DateTime**——nullable 场景框架未覆盖（测试盲区）。

## 四、最小修复方案（可直接实施）

`EnsureRegistered()` 追加，用**独立 handler 类**（⚠️ 不能复用 `DateTimeUtcHandler` 实例——其 `ITypeHandler.Type` 固定返回 `typeof(DateTime)`，`PropertyType == Type` 比较会失败）：

```csharp
private sealed class DateTimeUtcNullableHandler : ITypeHandler
{
    public Type Type => typeof(DateTime?);
    public object Deserialize(object value)
    {
        if (value == null) return null;
        return new DateTimeUtcHandler().Deserialize(value);
    }
    public object Serialize(object value)
    {
        if (value == null) return null;
        return new DateTimeUtcHandler().Serialize(value);
    }
    public void FluentApi(ColumnFluent column) => column.MapType(typeof(string));
}
```

注册行：

```csharp
global::FreeSql.Internal.Utils.TypeHandlers[typeof(DateTime?)] = new DateTimeUtcNullableHandler();
```

## 五、回归测试建议

官方 `G7_DateTimeUtcRoundTrip` 加 nullable 变体：`Tier15DateTimeEntity` 补 `DateTime? NullableCreatedAt` 列 + 写 UtcNow → 读回 `Assert.Equal` 断言（修复前应 FAIL +8h，修复后 PASS）。

## 六、验收标准（XiaoShuTong 侧）

框架修复 + 部署 v4.10.19+ 后，XiaoShuTong 全套件预期 **345 通过 / 0 失败 / 2 跳过**（7 用例清零）。
