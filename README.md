# 游戏修改器启动器 (Game Trainer Launcher)

| [English Version](README.en.md)

受够了手动下载管理众多的游戏修改器.exe文件，又不想在 flyy.cn 和 wemod 这种平台上充会员？试试这款基于 [FlingTrainer](https://flingtrainer.com) 的 Windows 桌面应用：浏览、搜索、下载并启动游戏修改器，轻松管理你的游戏修改器。

**下载安装包**：[https://github.com/hwttop5/GameTrainerLauncher/releases](https://github.com/hwttop5/GameTrainerLauncher/releases)

---

## 功能概览

- **搜索**：按游戏英文名检索 FlingTrainer，结果可「下载并添加」到我的游戏；支持多任务同时添加、每张卡片独立状态。
- **热门游戏**：拉取 FlingTrainer 热门修改器列表，一键「下载并添加」到我的游戏，带进度条与超时提示。
- **我的游戏**：已添加的修改器列表，默认按添加时间倒序（最新在前），支持拖拽排序；支持启动、移除；封面会在「添加」时下载到本地，进入页面时会检查是否每个游戏都有本地封面，缺失则自动抓取并补全；可在此页对未下载项单独下载；当游戏库为空时显示友好的无数据提示。
- **设置**：语言（中文/英文）、主题（亮色/暗色）。

---

## 界面截图

| 搜索 | 热门游戏 |
|------|----------|
| ![搜索](Docs/Images/Search.png) | ![热门游戏](Docs/Images/PopularGames.png) |

| 我的游戏 | 设置 |
|----------|------|
| ![我的游戏](Docs/Images/MyLibrary.png) | ![设置](Docs/Images/Settings.png) |

---

## 技术栈

- **运行时**：.NET 8，仅支持 Windows（WPF）
- **UI**：WPF + [WPF-UI](https://github.com/lepo-co/wpf-ui)（Fluent 风格）+ [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- **数据**：SQLite + Entity Framework Core 8
- **爬虫**：HtmlAgilityPack，请求 FlingTrainer 列表/详情/下载
- **日志**：NLog（写入程序目录 `Data/Logs/log.txt`）

### 项目结构

- **GameTrainerLauncher.Core**：领域实体与接口
- **GameTrainerLauncher.Infrastructure**：爬虫、本地扫描、数据库、修改器下载与启动
- **GameTrainerLauncher.UI**：WPF 界面（MVVM）

---

## 环境与运行

- **要求**：.NET 8 SDK、Windows 10/11
- **还原与构建**：
  ```bash
  dotnet restore
  dotnet build
  ```
- **运行**：
  ```bash
  dotnet run --project GameTrainerLauncher.UI
  ```
  或直接运行输出目录中的 `GameTrainerLauncher.UI.exe`。

修改器与数据目录位于程序所在目录下的 `Data`（如 `Data/Trainers`、`Data/game_trainer_launcher.db`、`Data/Logs`）。

> 注：为避免安装到 Program Files 时无写权限，应用数据实际存放在 `%LocalAppData%\GameTrainerLauncher\Data`。
> 其中封面缓存目录为 `%LocalAppData%\GameTrainerLauncher\Data\Covers`（文件名形如 `game_{id}.png/jpg/...`）。

---

## 打包为 exe 安装包

本仓库仅支持通过 [Inno Setup](https://jrsoftware.org/isinfo.php) 生成单文件安装包（.exe）。**需先安装 Inno Setup 6**。

**一键打包**（在仓库根目录执行）：
```powershell
.\Installer\build-installer.ps1
```
安装包生成在 `Installer\Output\GameTrainerLauncher_Setup_1.0.1.exe`。

也可分步执行：先 `dotnet publish GameTrainerLauncher.UI -p:PublishProfile=FolderProfile`，再用 Inno Setup 打开 `Installer\GameTrainerLauncher.iss` 编译。

---

## 说明与免责

- 修改器内容与可用性依赖 FlingTrainer 站点；若站点结构调整，爬虫可能需要更新。
- 本工具仅供学习与个人使用，请遵守当地法律与游戏/平台条款。

---

## 开源协议

本项目采用 **[GNU General Public License v3.0 (GPL-3.0)](LICENSE)** 开源协议。使用、修改与分发须遵守该协议。
