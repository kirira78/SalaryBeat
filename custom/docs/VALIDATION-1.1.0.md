# SalaryBeat 薪跳 1.1.0 验证记录

日期：2026-11-07。开发主机 macOS ARM64，SDK 10.0.403，目标 Windows 10/11 x64。

## 已执行

### 核心回归

```
dotnet run --project custom/Tests/SalaryBeat.Tests.csproj -c Release
```

**exit 0，12 组、72 项断言通过**。保留原 8 组、49 项断言，新增 4 组：

- 内嵌年度数据：2026/2027 可直接从程序集读取，国庆为休息日；缺失年度链接指向其对应 JSON 页面。
- 弹窗布局：640×400、工作区右下角、负坐标屏幕、较小工作区保持 16:10 并缩放适配。
- 更名迁移：旧收入设置与自定义日历保留，原文件不删除；新配置已经存在时不覆盖。
- 同窗口翻面状态：初始看板、中点换面、连续点击保护、返回看板与重复打开幂等。

新功能先执行失败测试：年度数据缺失、布局/迁移未实现时 3 组失败；翻面状态未实现时相应组失败。实现后全部通过。

针对内嵌日历问题另外执行撤回验证：仅将 LoadBundled 恢复为空实现时，`2026 must work without sidecar JSON` 失败（exit 1）；恢复后再次 72 项通过（exit 0）。没有删除或弱化已有测试。

### 构建与发布

```
dotnet build custom/Desktop/SalaryBeat.Desktop.csproj -c Release -p:UseAppHost=false -p:NuGetAudit=false --source /tmp/lucyna-offline-nuget

dotnet publish custom/Desktop/SalaryBeat.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:NuGetAudit=false --source /tmp/lucyna-offline-nuget -o custom/dist/1.1.0/win-x64
```

最终结果均为 exit 0，0 warnings / 0 errors。启用单文件内部压缩，不裁剪 WPF 运行时。

`file SalaryBeat.exe` 确认 `PE32+ executable (GUI) x86-64, for MS Windows`。

Theme.xaml 及项目 XML 语法解析通过。**XML 语法与交叉编译不代表 WPF 模板已实际渲染。**

2026/2027 JSON 既作为 Core 程序集的 EmbeddedResource，又保留在运行包 data 目录供核对。启动使用 LoadBundled，不再依赖 EXE 旁的 JSON 文件。

### 设计参考核查

已通过浏览器查看用户指定 Figma 文件的公开预览：薄荷绿／深绿、橙色、淡紫卡片、大圆角。独立适配为桌面横向卡片和同窗口设置背面，未复制插画或其他素材。

## 尚未执行／不能宣称通过

当前没有可用 Windows 节点，未在 Windows 上启动新 EXE：

- 640×400 实际布局是否有裁切，字体与 DPI 表现；
- 主看板和设置背面的翻面视觉效果、交互焦点与局部滚动；
- 深浅主题下输入框、日期选择、下拉和表格的实际呈现；
- 白色加粗黑体与深色轮廓／薄荷光晕在各种壁纸上的可读性；
- 托盘、鼠标穿透、原生置顶与启动项迁移；
- 真正只复制 EXE 后启动（已验证程序集资源，不等同于 Windows 单文件运行测试）。

附 Windows 专用渲染自检入口：

```
SalaryBeat.exe --render-preview ui-preview
```

该入口不迁移或读取正式工资，不创建托盘、不写启动项，用独立示例数据渲染 PNG。当前未执行，不提供伪造的 Windows 截图。

## 交付边界

本版本是可测试构建，不是已完成 Windows 原生交互验收的正式版。没有公开发布、付费、上传工资或修改开发机的 Windows 配置。
