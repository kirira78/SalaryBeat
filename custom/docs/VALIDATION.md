# 验证记录 · 2026-11-07

## 环境与边界

- 开发主机：macOS 27 ARM64；.NET SDK 10.0.403，运行时 10.0.14。
- SDK 从 Microsoft 官方源取得并与其发布元数据的 SHA-512 校验一致。Windows 目标包、宿主与运行时包从 Microsoft 官方 Blob / NuGet CDN 获取，校验与服务器提供的摘要一致。
- NuGet 默认索引和 Azure DevOps 备用索引均遇到连接失败；本地官方包源 `/tmp/lucyna-offline-nuget` 用于完成恢复，没有修改全局 NuGet 配置。
- 没有 Windows 节点。**没有运行 Windows EXE、没有真实窗口截图、没有验证 Windows 自启或鼠标穿透。**
- 目标为 Windows 10/11 x64。ARM64 构建入口存在，但本次未构建／验证；不提供 x86 包。

## 自动化测试

命令（仓库 custom 目录）：

```
dotnet run --project Tests/SalaryGlow.Tests.csproj -c Release
```

实际结果：exit 0，**8 组、49 项断言通过**。

1. 计薪与上下班、午休边界，月末封顶、未来月份不预赚。
2. 三个年度日历读取，国家放假/补班、手工覆盖及连续大小周。
3. 不等长班次、补班周一时段回退、自然日多段班次、零分母及无效时段。
4. 已保存月份冻结官方日历，不受后续数据改动影响。
5. 仅从此前月份继承，提醒保持到确认，不复制日期覆盖，不改变历史或已录未来月。
6. 原子保存、往返加载及损坏文件备份恢复。
7. 现有行为刻画：收入/日期验证、各日收入合计等于月收入、历史进度整月重算、未来数据不倒灌。
8. 日历更正会移除已经取消的补班/放假日期。

测试变更：新增 6 组核心规格、1 组现有行为刻画和 1 组日历更新回归；未删除或弱化断言。

### RED / GREEN 证据摘要

- 首次在契约桩上运行：exit 1，6 组分别因未实现的功能失败，随后才实现核心逻辑。
- 日历目录扫描初次实际运行误读 `.deps.json` / `.runtimeconfig.json`，exit 134；限制年度文件名后通过。撤回筛选修复再次 exit 134；恢复后 exit 0。
- 更正版日历的旧日期回归：`expected False, got True`，exit 1；修复后 exit 0；仅撤回修复后再次 exit 1；恢复后 49 项全部通过。

## 构建与发布

框架依赖交叉构建曾执行：

```
dotnet build Desktop/SalaryGlow.Desktop.csproj -c Release -p:UseAppHost=false -p:NuGetAudit=false --source /tmp/lucyna-offline-nuget
```

结果：exit 0，0 warnings / 0 errors。

最终 x64 自包含发布命令：

```
dotnet publish Desktop/SalaryGlow.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:NuGetAudit=false --source /tmp/lucyna-offline-nuget -o dist/win-x64
```

早期一次发布提示单文件 `Assembly.Location` 警告；已经改为基目录路径。最终发布无警告。`file SalaryGlow.Leo.exe` 确认是 `PE32+ executable (GUI) x86-64, for MS Windows`，这只是二进制格式检查，不是运行验证。

## Windows 实机验收清单（待执行）

- 在 Windows 10 和 Windows 11 解压整个运行包，启动后可保存收入并刷新真实时间。
- 点击今日/本月切换，数字滚动无跳位；关闭系统动画后不滚动。
- 午休、下班、休息日暂停，下一工作时段自动恢复；重开补算。
- 跨月提醒持续直到保存，提前录入未来月不会提前重置。
- 主看板与迷你窗互斥、均默认置顶；鼠标穿透可从托盘解锁/重新锁定。
- 明/暗系统主题和手动主题；不同 DPI、多显示器位置与数字可读性。
- 关闭窗口留在托盘，退出后无残留进程；重启 Windows 自启动成功，关闭选项后停止自启。
- 日历导入错误不覆盖原配置，主设置损坏从备份恢复。

本次交付为**可测试构建**，不宣称已经完成上述原生交互验收。
