# 南京市内智能行程表 · Windows 桌面版

## 下载并运行

**[下载最新 Windows 运行包](https://github.com/YuuKldesu/Trip-Agent/releases/latest)**

进入发布页，下载 Assets 中的 Windows ZIP（不要下载 Source code）。完整解压到可写目录后，双击 **NanjingPlanner.exe**。无需安装 Python 或 .NET，请保留同目录的组件文件。

如果只点击仓库右上角 Code → Download ZIP，得到的是源码，不含编译后的 exe。

## 当前版本

- 暖色圆角看板、可展开侧边栏、独立设置与本地个人资料。
- 月历、纵向列表和简洁模式；多份行程表、12种归属颜色、字号调整。
- 左滑完成、右滑收藏；手动编辑、日期弹层和可跨日循环的时间选择。
- 多轮智能引导、编辑旧消息创建分支、核对后生成与按批次撤回。
- 地点搜索显示地址候选；交通参与排程，自动回填时间并单独显示交通卡片。

实际地图与模型服务需在设置中配置；无地图配置时的本地估算会明确标注。提醒在程序运行时有效。

## 数据与升级

个人行程、对话和服务设置保存在程序旁的 Data 目录。升级前退出程序并备份该目录，再将原 Data 复制到新版程序旁。不要上传个人 Data 或密钥。

## 源码与构建

当前正式程序的源码位于 [desktop-wpf](desktop-wpf/)。开发构建需要 .NET 10 SDK，使用 [构建脚本](desktop-wpf/build.ps1) 生成自包含 Windows x64 运行包。

[完整使用说明](desktop-wpf/README.md) · [验证记录](desktop-wpf/VERIFICATION.md) · [独立审阅](desktop-wpf/ROUTE_FIX_REVIEW.md)

主分支只保留当前 Windows 版本。旧 Python 版本在备份分支 backup/pre-release-cleanup-20261029 中保留，不再作为主分支启动入口。
