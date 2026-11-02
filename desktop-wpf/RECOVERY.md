# v2026.11.02.7 源码恢复记录

原 .3—.7 临时源码及本地提交未推送，清理后无法取回原文件。已从保存的 .7 完整运行包恢复 C# 实现和界面 XAML，并对照仓库的 .2 源码修复项目引用。恢复工作仅在 recovery/v2026.11.02.7 分支进行，未合并主分支。

## 来源与可追溯性

- 基线源码：e46e0c7ab7f184d4d3b5ac9e33aeb8377ad45696。
- 原运行包：NanjingPlanner_v2026.11.02.7_win-x64.zip，SHA-256 为 `0d50c305935c2f868a16b1a9dbde3006df25b4f64558335207d046296400bbf2`。
- 工具：ILSpy CLI 11.2.0.10050，SDK 10.0.201。
- 原始反编译快照：仓库 recovery-snapshots/v2026.11.02.7；先行保存快照的提交为 4f222bb4e94d7904d4a5ec55682567ffc9adffd2。
- 可编译恢复源码：本 desktop-wpf 目录。已将三个程序集中的类型按文件拆分，保留反编译生成的辅助集合类型。
- 使用基线项目文件和 NuGet 依赖，恢复原嵌入 SQL 名称；恢复版标记为 v2026.11.02.7-recovered。唯一必要的代码编译修正是去掉反编译器输出的 `object._002Ector()`，由 C# 隐式基类构造调用实现相同含义。

## 包含的改动

草稿局部修改/恢复与路线比较、可关闭的中间页面与会话标签、原位重命名和批量关闭、文字选择复制、简短连接错误、必填标记、图标和弹层定位、直接高德地点查询、后台独立会话、万圣节主题和彩蛋、牧濑红莉栖可选角色模式均存在于恢复的实现中。

## 验证

- 核心和数据项目编译通过。反编译源码存在 nullable 流分析警告（完整核心构建143条）；未通过禁用警告掩盖问题。
- Windows应用交叉编译通过（首次完整构建127条 nullable 等警告、0错误）；既有 Planner.BoardChecks 项目也编译通过。原生检查未在 Linux 执行。
- 基线149项测试中148项通过。唯一未通过为 Linux 不支持 Windows 密钥保护的旧用例。地图HTTP fixture改用已有provider注入，绕开非必要的Windows加密；断言按 .7 直接地点搜索更新为3次HTTP请求，未跳过此测试。
- 新增 Planner.RecoveryChecks：11/11通过。其中与原运行包对比了Core/Data公开API、完整工具/角色提示词、全部主题与强调色组合、地点关键词解析、批量关闭范围及高德地点响应/拒绝结果；另检查后台回复归属、独立取消、自动铺满和草稿局部修改。
- 对比仅覆盖列明的接口和场景，不证明所有输入下的字节级或行为完全等价。没有真实地图/模型调用，没有Windows实机DPI、鼠标交互和视觉验收。

## 复现

```sh
dotnet build desktop-wpf/Planner.Desktop/Planner.Desktop.csproj -p:EnableWindowsTargeting=true
dotnet run --project desktop-wpf/Planner.Tests/Planner.Tests.csproj
dotnet run --project desktop-wpf/Planner.RecoveryChecks/Planner.RecoveryChecks.csproj -- /path/to/original/NanjingPlanner
```

原始运行包无需替换。恢复分支提供Windows构建workflow，可在Actions生成明确标记为recovered的包；发布结果以实际CI状态为准。

## 尚未恢复的内容

原始注释、精确文件组织/局部变量名、.3—.7提交历史及这些版本当时新增的完整测试文件不能从程序集还原。本分支提供可编译的恢复实现与明确标注的新核对，不冒充原始源码或宣称恢复了之前的198项测试结果。
