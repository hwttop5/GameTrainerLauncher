# 游戏修改器启动器 (Game Trainer Launcher)

[English Version](README.en.md)

一个基于 `FlingTrainer` 体验优化的 Windows 桌面启动器，用来搜索、下载、整理并启动游戏修改器。

## 核心功能

- 支持中英文游戏名搜索，并结合本地标题索引与后台增量补全。
- 支持热门修改器浏览、下载并加入本地库。
- 支持按版本选择修改器，降低版本不匹配问题。
- 支持本地库管理，包括排序、启动、移除、封面补全与状态反馈。
- 支持中英双语、亮暗主题和应用内更新检查。

## 技术栈与结构

- 运行时: `.NET 8`、`WPF`、Windows only
- UI: `WPF-UI` + `CommunityToolkit.Mvvm`
- 数据: `SQLite` + `Entity Framework Core 8`
- 抓取与集成: `HtmlAgilityPack`、本地扫描、下载与启动服务

主要目录:

- `GameTrainerLauncher.Core`: 领域实体与接口
- `GameTrainerLauncher.Infrastructure`: 数据、抓取、扫描、下载与启动实现
- `GameTrainerLauncher.UI`: WPF 界面与应用入口
- `Docs/site`: 官网静态页源码
- `installer`: 本地打包与发布辅助脚本

## 快速开始

要求: Windows 10/11，`.NET 8 SDK`

```powershell
dotnet restore
dotnet build GameTrainerLauncher.UI/GameTrainerLauncher.UI.csproj
dotnet run --project GameTrainerLauncher.UI
```

应用数据默认位于 `%LocalAppData%\GameTrainerLauncher\Data`。

## 提交规范

本仓库使用 [Conventional Commits 1.0.0](https://www.conventionalcommits.org/zh-hans/v1.0.0/)。

首次克隆后运行以下命令安装 commitlint 与 Husky hook:

```powershell
npm install
```

手动验证当前历史:

```powershell
npm run commitlint:history -- HEAD
```

## 文档导航

- [AGENTS.md](AGENTS.md): 面向 coding agent 的仓库协作与维护说明
- [README.en.md](README.en.md): 英文版项目说明
- [Docs/README.md](Docs/README.md): 文档地图与边界说明
- `Docs/site`: 官网源码，不是开发手册
- `designs/`: 本地设计归档目录，默认不提交

## License

本项目采用 [GNU General Public License v3.0 (GPL-3.0)](LICENSE)。
