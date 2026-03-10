开发一个 Windows 平台专用的游戏修改器启动器桌面应用，核心功能分为三大模块：顶部全局搜索框、“热门游戏”页、“我的游戏”页，所有数据均从 `https://flingtrainer.com/`  爬取并本地缓存。  
 具体需求如下： 
 
 1. 界面框架  
    - 采用单窗口多标签（或 NavigationView）布局，顶部固定搜索框实时响应输入。  
    - 支持深色/浅色主题切换，最小窗口尺寸 1024×768，DPI 感知 Per-Monitor V2。  
 
 2. 热门游戏页  
    - 启动时异步爬取 `https://flingtrainer.com/trainer/slay-the-spire-2-trainer/`  的 Trainer 列表，解析游戏名称、封面图、最后更新日期、Trainer 版本号。  
    - 以卡片流方式展示，默认 12条，下滑自动分页。  
    - 卡片提供“添加到我的游戏”按钮，点击后写入本地 SQLite 库并弹窗提示成功；若已存在则置灰。  
    - 异常处理：网络超时 20 s、HTTP 非 200 时显示重试按钮并记录日志。  
 
 3. 我的游戏页  
    - 首次进入扫描本地已安装游戏：  
      – Steam：解析注册表 `HKCU\Software\Valve\Steam` 及 `steamapps\libraryfolders.vdf`。  
      – Epic：解析 `C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests\*.item`。  
      – Xbox（PC Game Pass）：调用 `winget` 或解析 `XboxGameCallableUI` 本地缓存。  
    - 将扫描到的游戏列表与 SQLite 中“已添加”条目做左关联，展示图标、名称、安装路径。  
    - 点击游戏图标：  
      – 实时爬取 `https://flingtrainer.com/trainer/slay-the-spire-2-trainer/`  对应页面，提取“Standalone Versions”区块第一个下载链接（直接 EXE）。  
      – 下载到 `%LOCALAPPDATA%\FlingLauncher\Trainers\<GameName>\<Version>.exe`，显示进度条，支持暂停/取消。  
      – 下载完成后自动校验文件大小与页面标注是否一致，并立即启动该 EXE；若已存在相同版本则直接启动。  
    - 右键菜单：删除——同步移除列表项并删除本地 `%LOCALAPPDATA%\FlingLauncher\Trainers\<GameName>` 整个目录，给出二次确认。  
 
 4. 全局搜索  
    - 输入≥2 字符时，实时爬取 `https://flingtrainer.com/all-trainers/`  的“All Trainers”页面，按标题模糊匹配，下拉列表展示前 10 条结果。  
    - 选中条目后行为与“热门游戏”页“添加”逻辑一致。  
 
 5. 数据与缓存  
    - 使用 Entity Framework Core + SQLite 本地库，表结构：Game(Id, Name, CoverUrl, TrainerUrl, InstalledPath, AddedDate, Version)。  
    - 封面图与 Trainer 文件均做本地缓存，缓存上限 10 @GB 
 
 6. 性能与体验  
    - 爬虫采用 HttpClient + HtmlAgilityPack，并发度≤3，遵守 robots.txt，设置 User-Agent 与 1 s 延迟。  
    - 所有网络/磁盘 IO 放在后台线程，UI 不卡顿；提供全局 Loading 遮罩与任务栏进度合并。  
 
 7. 异常与日志  
    - NLog 写入 `%LOCALAPPDATA%\FlingLauncher\Logs`，保留 30 天。  
    - 网络失败、文件校验不一致、启动器崩溃均记录堆栈并弹友好提示，支持一键打包日志反馈。  
 
 8. 测试与交付  
    - 单元测试覆盖爬虫解析、数据库 CRUD、本地文件清理逻辑，覆盖率≥80%。  
    - 提供 x64 安装包（MSIX 或 MSI），签名证书，自动检查更新。  
    - 交付物：源码、编译脚本、安装包、使用文档、测试报告。