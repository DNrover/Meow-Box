# FixBatteryControl — 充电上限修复明细

## 版本信息

- **分支**: `FixBatteryControl`
- **目标版本**: v3.1.1
- **目标机型**: Xiaomi Book Pro 14 2026
- **修复提交**:
  - `4cbc10c` — fix: update battery charge limit handling in performance mode
  - `f59dc2f` — fix: enhance battery charge limit restoration logic on startup

---

## 问题背景

小米笔记本固件在收到性能模式切换指令（WMI `fun2: 0x0800`）时，会连带将硬件充电上限寄存器重置为 100%。此前代码仅发送了性能模式命令，未补偿恢复充电上限，导致以下两个场景下充电上限丢失：

1. **运行时切换性能模式**：Fn+K 手动切换、AC 插拔触发的自动切换（每 2 秒轮询）都会导致充电上限被固件静默重置
2. **启动时**：启动过程中 `ApplyPerformanceMode` 被调用恢复性能模式，触发固件重置；且 `PreferredChargeLimitPercent` 配置从未在启动时被读取应用

---

## 修复一：性能模式切换后补偿恢复充电上限

**文件**：`src/MeowBox.Core/Services/BatteryControlService.cs`

### `SetPerformanceMode`（同步验证版，行 67-97）

```csharp
// 改动前：只发送性能模式命令，固件连带把充电上限重置为 100%
_ = Invoke(target, CreateBuffer(fun1: 0xFB00, fun2: 0x0800, fun3: rawCode, fun4: 0));
Thread.Sleep(SettleDelayMs);
var state = QueryState();
// ... 验证性能模式 ...

// 改动后：发送性能模式命令前保存充电上限，发送后若检测到充电上限被改动则补偿恢复
int? previousChargeLimit;
lock (_stateSync)
{
    previousChargeLimit = _cachedState?.ChargeLimitPercent;
}

_ = Invoke(target, CreateBuffer(fun1: 0xFB00, fun2: 0x0800, fun3: rawCode, fun4: 0));
Thread.Sleep(SettleDelayMs);
var state = QueryState();
// ... 验证性能模式 ...

if (previousChargeLimit.HasValue
    && previousChargeLimit.Value != BatteryControlCatalog.DefaultChargeLimitPercent
    && state.ChargeLimitPercent != previousChargeLimit.Value)
{
    var chargeLimitRawCode = BatteryControlCatalog.GetChargeLimitRawCode(previousChargeLimit.Value);
    _ = Invoke(target, CreateBuffer(fun1: 0xFB00, fun2: 0x1000, fun3: 0x0002, fun4: chargeLimitRawCode));
    Thread.Sleep(SettleDelayMs);
    state = QueryState();
}
```

### `SetPerformanceModeFast`（快速版，行 118-173）

```csharp
// 改动前：直接发送性能模式命令，不处理充电上限
_ = Invoke(target, CreateBuffer(fun1: 0xFB00, fun2: 0x0800, fun3: rawCode, fun4: 0));

BatteryControlState nextState;
lock (_stateSync)
{
    nextState = _cachedState is null
        ? new BatteryControlState { ... ChargeLimitPercent = BatteryControlCatalog.DefaultChargeLimitPercent }
        : CloneState(_cachedState);
    // ...
}

// 改动后：在发送性能模式命令后立即检查并恢复充电上限
_ = Invoke(target, CreateBuffer(fun1: 0xFB00, fun2: 0x0800, fun3: rawCode, fun4: 0));

int? previousChargeLimit = null;
lock (_stateSync)
{
    previousChargeLimit = _cachedState?.ChargeLimitPercent;
}

if (previousChargeLimit.HasValue && previousChargeLimit.Value != BatteryControlCatalog.DefaultChargeLimitPercent)
{
    var chargeLimitRawCode = BatteryControlCatalog.GetChargeLimitRawCode(previousChargeLimit.Value);
    _ = Invoke(target, CreateBuffer(fun1: 0xFB00, fun2: 0x1000, fun3: 0x0002, fun4: chargeLimitRawCode));
}

BatteryControlState nextState;
lock (_stateSync)
{
    nextState = _cachedState is null
        ? new BatteryControlState { ... ChargeLimitPercent = previousChargeLimit ?? BatteryControlCatalog.DefaultChargeLimitPercent }
        : CloneState(_cachedState);
    // ...
    if (previousChargeLimit.HasValue)
    {
        nextState.ChargeLimitPercent = previousChargeLimit.Value;
    }
    // ...
}
```

### 原理

WMI 命令序列（在同一 `_commandSync` 锁内连续发送）：
1. `fun2: 0x0800` → 切换性能模式（固件副作用：重置充电上限为 100%）
2. `fun2: 0x1000` → 立即恢复充电上限（覆盖固件的默认重置）

由于两条命令在同一锁内连续发送，固件串行处理，充电上限的恢复在硬件层面先生效，后续 `QueryState()` 读取到的即是正确值。

---

## 修复二：启动时恢复用户偏好的充电上限

**文件**：`src/MeowBox.Worker/WorkerHost.cs`

### `RestorePreferredBatteryStateOnStartupAsync`（行 396-460）

**改动前的逻辑**：
1. `shouldRestoreChargeLimit = ResetChargeLimitToFullOnStartup`
2. 若为 `true`：强制设置充电上限为 100%
3. 若为 `false`：什么都不做（`PreferredChargeLimitPercent` 从未被读取）

**改动后的逻辑**：
1. `shouldResetChargeLimitToFull = ResetChargeLimitToFullOnStartup`
2. 读取 `preferredChargeLimitPercent = NormalizeChargeLimitPercent(PreferredChargeLimitPercent)`
3. `shouldRestorePreferredChargeLimit = !shouldResetChargeLimitToFull && preferredChargeLimitPercent != DefaultChargeLimitPercent`
4. 若 `shouldResetChargeLimitToFull` 为 `true`：强制设置 100%
5. 若 `shouldRestorePreferredChargeLimit` 为 `true`：下发 `preferredChargeLimitPercent`

关键代码变更：
```csharp
// 改动前
var shouldRestoreChargeLimit = _configuration.Preferences.ResetChargeLimitToFullOnStartup;
if (!shouldRestorePerformanceMode && !shouldRestoreChargeLimit)
    return;

if (shouldRestoreChargeLimit)
    _batteryControlService.SetChargeLimitPercentFast(BatteryControlCatalog.DefaultChargeLimitPercent);

// 改动后
var shouldResetChargeLimitToFull = _configuration.Preferences.ResetChargeLimitToFullOnStartup;
var preferredChargeLimitPercent = BatteryControlCatalog.NormalizeChargeLimitPercent(
    _configuration.Preferences.PreferredChargeLimitPercent);
var shouldRestorePreferredChargeLimit =
    !shouldResetChargeLimitToFull && preferredChargeLimitPercent != BatteryControlCatalog.DefaultChargeLimitPercent;
if (!shouldRestorePerformanceMode && !shouldResetChargeLimitToFull && !shouldRestorePreferredChargeLimit)
    return;

if (shouldResetChargeLimitToFull)
    _batteryControlService.SetChargeLimitPercentFast(BatteryControlCatalog.DefaultChargeLimitPercent);
else if (shouldRestorePreferredChargeLimit)
    _batteryControlService.SetChargeLimitPercentFast(preferredChargeLimitPercent);
```

### 原理

修复一解决了运行时的问题（`_cachedState` 持有用户之前设置的正确充电上限）。但在启动场景中，`QueryState()` 先于 `ApplyPerformanceMode` 调用，若固件在启动时已将充电上限重置为 100%，则缓存的即是 100%（默认值），修复一的保护逻辑不会触发。

修复二在 `ApplyPerformanceMode` 完成后，直接从配置文件中读取 `PreferredChargeLimitPercent`（用户上次通过 UI 滑块设置的值，由 Controller 侧 [MeowBoxController.cs:611](src/MeowBox.Controller/Services/MeowBoxController.cs#L611) 持久化），主动下发到硬件，不依赖缓存状态。

---

## 补充：`global.json` SDK 版本固定

**文件**：`global.json`（新增）

```json
{
  "sdk": {
    "version": "8.0.421",
    "rollForward": "latestFeature"
  }
}
```

确保团队使用一致的 .NET SDK 版本构建。

---

## 触发路径回顾

```
AC 插拔 / 2 秒轮询 / Fn+K 快捷键
  → EvaluateBatteryAutomation / ExecuteCyclePerformanceModeAction
    → ApplyPerformanceMode
      → SetPerformanceModeFast
        → WMI: fun2=0x0800 (设置性能模式)
        → [固件副作用：充电上限→100%]
        → WMI: fun2=0x1000 (恢复充电上限) ← 修复一
  → RefreshBatteryStateAfterMutationAsync (700ms 后)
    → QueryState() → 读到正确值

应用启动
  → RestorePreferredBatteryStateOnStartupAsync
    → QueryState() → 缓存硬件状态
    → ApplyPerformanceMode → SetPerformanceModeFast (修复一防御)
    → SetChargeLimitPercentFast(preferredChargeLimitPercent) ← 修复二
```

---

## 配置默认值参考

| 配置项 | 默认值 | 说明 |
|-------|--------|------|
| `ResetChargeLimitToFullOnStartup` | `false` | 是否每次启动重置充电上限为 100% |
| `PreferredChargeLimitPercent` | `90` | 用户偏好的充电上限百分比（由 Controller 在用户调整滑块时保存） |
| `DefaultChargeLimitPercent` | `100` | 硬件默认充电上限（= 关闭充电限制） |