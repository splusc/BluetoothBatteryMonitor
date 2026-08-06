# BluetoothBatteryMonitor 完整改名设计

## 目标

将应用从 `TrayMonitor` 完整改名为 `BluetoothBatteryMonitor`，中文显示名称统一为“蓝牙电量监视器”。改名覆盖项目结构、程序集、命名空间、运行时标识、文档和发布产物，不改变现有蓝牙设备监控功能。

工作区根目录 `D:\Projects Code\traymonitor` 保持不变，历史设计和实施计划继续保留原名称，作为项目演进记录。

## 命名范围

- `src/TrayMonitor` 改为 `src/BluetoothBatteryMonitor`。
- `tests/TrayMonitor.Checks` 改为 `tests/BluetoothBatteryMonitor.Checks`。
- 项目文件、程序集、命名空间和友元程序集统一使用 `BluetoothBatteryMonitor`。
- 图标资源改名为 `BluetoothBatteryMonitor.ico`，继续使用现有半电量电池图标。
- 发布产物改为 `artifacts/win-x64/BluetoothBatteryMonitor.exe`。
- 托盘界面、错误提示和当前使用文档中的产品名改为“蓝牙电量监视器”或 `BluetoothBatteryMonitor`。
- 更新 `README.md` 和 `CLAUDE.md`，不批量改写 `docs/superpowers` 下的历史文档。

现有 `BluetoothMonitor.cs` 中尚未提交的 GATT 电量读取改动必须保留，除目录移动和命名空间调整外不改变其行为。

## 旧数据清理

新应用使用以下独立标识：

- 设置文件：`%LocalAppData%\BluetoothBatteryMonitor\settings.json`
- 当前用户启动项：`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下的 `BluetoothBatteryMonitor`
- 单实例互斥锁：`Local\BluetoothBatteryMonitor`

新程序启动时执行尽力而为的旧数据清理：

1. 删除 `%LocalAppData%\TrayMonitor` 目录及其中的旧设置。
2. 删除当前用户启动项 `TrayMonitor`。

不读取或迁移旧设置。清理失败不得阻止应用启动，后续启动继续尝试。新应用不持有旧互斥锁，也不兼容旧程序的单实例状态。

## 实现约束

- 复用现有 `SettingsStore`、`StartupRegistration` 和启动流程，不引入迁移框架或第三方依赖。
- 将旧数据清理集中在一个最小的启动清理入口，避免分散到托盘界面逻辑。
- 删除目录和注册表值时只操作上述精确路径及名称，不扫描或修改其他应用数据。
- 保留现有自包含、单文件 `win-x64` 发布方式，因此 EXE 体积仍约为 100 MB。

## 验证

- 更新现有检查程序，验证新项目路径、程序集访问、图标配置、设置路径和启动项名称。
- 使用临时目录和隔离的注册表测试键验证旧数据清理，不触碰真实用户数据。
- 运行 Release 检查和 Release 构建。
- 发布 `artifacts/win-x64/BluetoothBatteryMonitor.exe`，并删除旧的 `artifacts/win-x64/TrayMonitor.exe`。
- 验证新 EXE 存在、嵌入半电量电池图标，且不再生成旧名称 EXE。

## 非目标

- 不迁移或保留旧配置。
- 不重命名仓库根目录。
- 不改写历史设计和计划文档。
- 不改变蓝牙枚举、电量读取、设备选择、轮播或托盘图标绘制逻辑。
- 不调整发布模型或压缩 EXE 体积。
