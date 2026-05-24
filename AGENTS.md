# AGENTS.md

本文件面向在此仓库内协作的 coding agent。`README.md` 和 `README.en.md` 面向用户与贡献者；需要理解仓库结构、构建验证、发布边界与文档约束时，优先阅读本文件。

## 项目概览

- 这是一个基于 `.NET 8` 和 `WPF` 的 Windows 桌面项目。
- 主程序入口是 `GameTrainerLauncher.UI`。
- 当前仓库重点是桌面客户端、官网静态页源码、以及本地与 GitHub Releases 发布链路。
- 当前不使用嵌套 `AGENTS.md`；默认由仓库根本文件统一约束。

## 仓库结构

- `GameTrainerLauncher.Core`: 领域实体、模型、接口与共享抽象。
- `GameTrainerLauncher.Infrastructure`: 数据访问、SQLite、抓取、扫描、下载、启动与集成实现。
- `GameTrainerLauncher.UI`: WPF 应用、MVVM 界面、资源与主程序入口。
- `Docs/site`: 官网静态页源码，使用纯 `HTML + CSS + JS`，由 GitHub Pages 工作流发布。
- `installer`: 本地打包与发布辅助脚本，包括 Velopack 打包、发布说明生成与 UI 校验脚本。
- `designs`: 本地设计资料、提示词、草图与参考图目录；默认不提交，仅作本地参考。

## 常用命令

在仓库根目录执行:

```powershell
dotnet restore
dotnet build GameTrainerLauncher.UI/GameTrainerLauncher.UI.csproj
dotnet run --project GameTrainerLauncher.UI
dotnet tool restore
npm install
npm run commitlint:history -- HEAD
./installer/build-velopack.ps1
```

补充说明:

- 常规改动完成后，优先执行 `dotnet build GameTrainerLauncher.UI/GameTrainerLauncher.UI.csproj`。
- 当前仓库没有独立测试项目；验证基线以构建通过和脚本/路径一致性为主。
- 提交信息遵循 Conventional Commits 1.0.0；首次克隆后运行 `npm install` 安装 Husky commit-msg hook。
- 可使用 `npm run commitlint:history -- HEAD` 校验当前分支历史；新增提交应使用 `feat:`、`fix:`、`docs:`、`chore:` 等小写类型。
- 如果任务涉及打包或发布文档，检查 `installer/build-velopack.ps1`、`installer/generate-release-notes.ps1`、`.github/workflows/release.yml` 是否仍与文档一致。

## 验证要求

- 文档改动后，确认文中命令、目录、脚本路径都真实存在。
- 应用相关改动后，至少运行一次 `dotnet build GameTrainerLauncher.UI/GameTrainerLauncher.UI.csproj`。
- 提交规范或历史改写相关改动后，运行 `npm ci`、`npm run commitlint:history -- HEAD`，并抽查 `.github/workflows/commitlint.yml`。
- 官网相关改动后，确认 `Docs/site` 仍可作为静态站点源码使用。
- 发布链路相关改动后，抽查以下文件的一致性:
  - `Directory.Build.props`
  - `installer/build-velopack.ps1`
  - `.github/workflows/release.yml`
  - `.github/workflows/pages-site.yml`

## 文档边界

- `README.md` / `README.en.md`: 面向用户与贡献者的项目入口，不承载 agent 专用执行规则。
- `AGENTS.md`: 面向 coding agent 的仓库维护说明。
- `Docs/README.md`: 仓库文档地图与边界说明。
- `Docs/site`: 官网源码，不是开发手册，也不是桌面 UI 规范文档。
- `designs/`: 历史设计资料目录；如果与当前代码、根文档或发布流程冲突，以当前代码和根文档为准。

## 约束与注意事项

- 这是 Windows-only 项目；`WPF` 和部分实现不以跨平台为目标。
- 发布版本号来源于 `Directory.Build.props`。
- 官网部署由 `.github/workflows/pages-site.yml` 驱动。
- Velopack 发布链路由 `.github/workflows/release.yml` 与 `installer/*.ps1` 驱动。
- 修改文档时，不要把归档设计资料误写成当前实现规范。
- 如果任务只涉及官网文案或界面展示，不要默认改动桌面应用逻辑。
