# SalaryBeat · 薪跳

让工作中的每一秒收入都看得见。基于 [SalaryGlow](https://github.com/BarryD-777/SalaryGlow) 二次开发的 Windows 桌面收入看板。

**当前版本：v1.1.1 测试版 · Windows 10 / 11 x64 · 本地运行 · 无需服务器**

## 主要功能

- **今日已赚 / 本月累计**同时展示，点击可切换主次；收入保留两位小数，辅助显示六位本月秒薪。
- 里程表式数字滚动，按工作进度计算；关机、休眠或退出后再次打开会补算。
- **640×400（16:10）**桌面卡片，默认右下角置顶；设置是同窗口背面，可原地翻面。
- 透明迷你悬浮窗：白色加粗黑体、深色轮廓与薄荷绿光晕；托盘菜单可锁定 / 解锁鼠标穿透。
- 按星期分别配置多个工作时段，间隔视为午休或 break；午夜按自然日拆分。
- 双休、固定周六上班的单休、带基准周的大小周，以及逐日额外工作 / 调休覆盖。
- **内嵌 holiday-cn 2026–2027 年日历**，无需用户下载；另保留 2025 年历史数据。缺少其他年份时提供对应年度页面链接，可导入 JSON。
- 月份快照、未来月份预录、新月份继承与持续确认提醒；本地备份、系统明暗主题及开机自启。

## 下载安装

在本仓库的 [Releases](https://github.com/kirira78/SalaryBeat/releases) 下载 `SalaryBeat-1.1.1-win-x64.zip`，解压后运行 `SalaryBeat.exe`，自包含版本不需要额外安装 .NET。

升级前先从托盘退出旧版。程序沿用 `%LOCALAPPDATA%\SalaryBeat\` 中的设置；首次从旧 SalaryGlow 测试版升级时，会导入旧 `SalaryGlowLeo` 设置，保留原文件，不覆盖已存在的新配置。

源码目录与运行包目录并非同一套启动入口：**运行 SalaryBeat，不是根目录保留的原版 SalaryGlow 程序。**

## 计算规则

```text
本月秒薪 = 本月应得收入 ÷ 本月全部计薪时段总秒数
今日已赚 = 本月应得收入 × 今日已过计薪秒数 ÷ 本月计薪总秒数
本月累计 = 本月应得收入 × 本月已过计薪秒数 ÷ 本月计薪总秒数
```

- 收入按**所属月份**录入，实际到账日不会重置进度。
- 排班优先级：手动日期覆盖 > 国家放假 / 调休补班 > 每周单双休规则；同一天不重复计算。
- 所有计薪时段使用同一秒薪；午休、下班与休息日暂停。
- 修改所选月份会整月重新分摊，历史月份和已预录月份保留各自设置。
- 新月份未录入时继承此前最近月份，不复制具体日期覆盖，主界面提醒直到确认。
- 内部计算使用 decimal，显示四舍五入不影响内部精度。它是收入进度估算，不是银行到账或税务结算。

## 本地开发与构建

需要 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。在 Windows PowerShell 中：

```powershell
git clone https://github.com/kirira78/SalaryBeat.git
cd SalaryBeat\custom
dotnet run --project Tests/SalaryBeat.Tests.csproj -c Release
powershell -ExecutionPolicy Bypass -File build.ps1
```

输出：`custom/dist/1.1.1/win-x64/SalaryBeat.exe`。核心测试可在 macOS / Linux 运行；WPF 原生界面需要 Windows。

可选 Windows 图形回归入口（使用示例数据，不读写正式工资或启动项）：

```powershell
.\SalaryBeat.exe --render-preview .\ui-preview
.\SalaryBeat.exe --ui-regression .\ui-regression
```

## 当前验证状态

- 13 组、81 项核心断言通过。
- Windows x64 交叉构建和自包含发布成功。
- **最新界面修复尚未完成 Windows 实机验收**；构建成功不等于动画、输入框、穿透、置顶和自启都已验证。
- 详细结果见 [1.1.1 验证记录](custom/docs/VALIDATION-1.1.1.md)。

## 源码结构

| 路径 | 内容 |
|---|---|
| `custom/Core/` | 日历、计薪、月份快照、本地存储、金额格式化 |
| `custom/Desktop/` | WPF 看板、翻面设置、滚动数字、托盘与原生集成 |
| `custom/Tests/` | 可跨平台执行的核心回归 |
| `custom/data/` | 内嵌年度日历与 holiday-cn 许可 |
| `custom/assets/` | 薪跳图标与生成脚本 |
| `custom/build.ps1` / `custom/package.py` | 构建与本地打包 |
| `src/` / 根目录 `build.ps1` | 保留的上游 SalaryGlow 原始实现，不是定制版入口 |

## 文档

- [完整操作与升级说明](custom/README.md)
- [需求规格](custom/docs/REQUIREMENTS.md)
- [版本变更](custom/docs/CHANGELOG.md)
- [1.1.1 实际验证记录](custom/docs/VALIDATION-1.1.1.md)
- [第三方来源与许可](custom/THIRD_PARTY_NOTICES.md)
- [上游项目原说明](docs/UPSTREAM_README.md)

## 隐私与许可

工资与排班只保存在本机，不上传 GitHub。仓库不包含个人设置、密钥、缓存或构建中间文件。

本项目基于 MIT 许可的 SalaryGlow，保留其版权与 [LICENSE](LICENSE)；holiday-cn 采用 MIT，许可保存在 [data/LICENSE.holiday-cn](custom/data/LICENSE.holiday-cn)。PayDance 和指定 Figma 仅作视觉 / 交互参考，未复制其代码、插画或素材。
