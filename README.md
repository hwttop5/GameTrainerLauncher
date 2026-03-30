# 游戏修改器启动器 (Game Trainer Launcher)

| [English Version](README.en.md)

受够了手动下载管理众多的游戏修改器.exe文件，又不想在 flyy.cn 和 wemod 这种平台上充会员？试试这款基于 [FlingTrainer](https://flingtrainer.com) 的 Windows 桌面应用：浏览、搜索、下载并启动游戏修改器，轻松管理你的游戏修改器。

**下载安装包**：[https://github.com/hwttop5/GameTrainerLauncher/releases](https://github.com/hwttop5/GameTrainerLauncher/releases)

**项目官网**：[https://hwttop5.github.io/GameTrainerLauncher/](https://hwttop5.github.io/GameTrainerLauncher/)

---

## 功能概览

- **搜索**：支持中文或英文游戏名检索；优先走本地标题索引秒回结果，并在后台做增量同步与补齐；结果可「下载并添加」到我的游戏，支持每张卡片独立状态。
- **热门游戏**：拉取 FlingTrainer 热门修改器列表，一键「下载并添加」到我的游戏，带进度条与超时提示。
- **版本选择**：下载前可弹窗选择对应版本，适配不同游戏版本，避免版本不匹配导致无法使用。
- **我的游戏**：已添加的修改器列表；新添加项目会默认置顶，兼容历史排序数据并保持顺序稳定；支持拖拽排序、启动、移除；封面会在「添加」时下载到本地，进入页面时会检查是否每个游戏都有本地封面，缺失则自动抓取并补全；可在此页对未下载项单独下载；当游戏库为空时显示友好的无数据提示。
- **设置**：语言（中文/英文）、主题（亮色/暗色）、检查更新，以及项目 GitHub 仓库入口。
- **软件更新**：支持手动检查更新、查看版本状态与发布说明，发现新版本后可下载并重启安装。

---

## 功能演示

![功能演示](Docs/Images/demo.gif)

---

## 技术栈

- **运行时**：.NET 8，仅支持 Windows（WPF）
- **UI**：WPF + [WPF-UI](https://github.com/lepo-co/wpf-ui)（Fluent 风格）+ [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- **数据**：SQLite + Entity Framework Core 8
- **爬虫**：HtmlAgilityPack，请求 FlingTrainer/Gamersky/Steam 元数据（按策略后台同步）
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
> 标题索引快照位于 `%LocalAppData%\GameTrainerLauncher\Data\title-index.snapshot.json`，首装会使用内置种子 `GameTrainerLauncher.UI/Assets/title-index.seed.snapshot.json` 初始化后再做增量更新。

---

## 打包与发布

项目现在使用 **Velopack + GitHub Releases** 作为安装包和自动更新主链路。

**本地打包**（在仓库根目录执行）：
```powershell
dotnet tool restore
.\installer\build-velopack.ps1
```
打包产物输出到 `artifacts/velopack`。
其中会额外生成 `checksums.txt`（SHA256 清单），可用于下载后完整性校验。

**自动发布 GitHub Release**：
- 版本号统一定义在 `Directory.Build.props`
- 推送与版本匹配的标签 `vX.Y.Z`
- GitHub Actions 会自动构建、校验版本、执行 Velopack 打包并上传 Release 资产
- Release 资产包含 `checksums.txt`，可与本地文件哈希比对

**Legacy Inno Setup**：
```powershell
.\installer\build-installer.ps1
```
该路径仅作为兼容/遗留安装器保留，不再作为主要更新链路。

**校验与误报申诉建议**：
- 下载后先校验 SHA256（参考 Release 中 `checksums.txt`），确保文件未被篡改。
- 若杀毒软件误报，可将校验通过的安装包提交到官方误报申诉渠道：
  - Microsoft Defender 误报提交：[https://www.microsoft.com/en-us/wdsi/filesubmission](https://www.microsoft.com/en-us/wdsi/filesubmission)
  - 也可先用 VirusTotal 做多引擎交叉检查：[https://www.virustotal.com/](https://www.virustotal.com/)

---

## 说明与免责

- 修改器内容与可用性依赖 FlingTrainer 站点；若站点结构调整，爬虫可能需要更新。
- 本工具仅供学习与个人使用，请遵守当地法律与游戏/平台条款。

---

## 开源协议

本项目采用 **[GNU General Public License v3.0 (GPL-3.0)](LICENSE)** 开源协议。使用、修改与分发须遵守该协议。
