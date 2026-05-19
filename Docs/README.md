# 文档地图

本目录和仓库根文档一起构成项目的主要说明入口。阅读顺序建议先看根 `README`，再按任务类型进入对应文档。

## 当前权威入口

- [../README.md](../README.md): 中文项目入口，面向用户与贡献者。
- [../README.en.md](../README.en.md): 英文项目入口。
- [../AGENTS.md](../AGENTS.md): 面向 coding agent 的仓库结构、命令、验证与边界说明。

## 仓库内文档与源码边界

- `Docs/site`: 官网静态页源码，使用纯 `HTML + CSS + JS`。
- `installer/`: 本地打包、发布说明生成与相关校验脚本。
- `.github/workflows/release.yml`: GitHub Releases + Velopack 发布工作流。
- `.github/workflows/pages-site.yml`: GitHub Pages 官网发布工作流。

## 设计资料边界

- `designs/` 是本地设计归档目录，默认不提交。
- `designs/` 下的设计稿、提示词、参考图默认不是当前实现规范。
- 如果本地设计资料与当前代码、根 `README`、根 `AGENTS.md` 冲突，以当前代码和根文档为准。

## 使用建议

- 了解产品与快速启动: 优先看根 `README.md` 或 `README.en.md`。
- 理解构建、验证、发布与协作约束: 优先看根 `AGENTS.md`。
- 修改官网: 进入 `Docs/site`，并同步检查 `.github/workflows/pages-site.yml`。
- 参考过往设计方向: 可查看本地 `designs/`，但默认仅作归档参考且不提交。
