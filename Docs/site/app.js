const STORAGE_KEYS = {
  lang: "gtl.site.lang",
  theme: "gtl.site.theme"
};

const TEXT = {
  "zh-CN": {
    "hero.badge": "Windows 桌面应用",
    "hero.title": "更轻松地管理游戏修改器",
    "hero.desc": "浏览、搜索、下载并启动修改器。支持中英文搜索与本地索引，启动快、管理稳。",
    "hero.download": "下载最新版",
    "hero.github": "查看 GitHub",
    "features.title": "核心功能",
    "features.search.title": "中英文搜索",
    "features.search.desc": "优先本地索引秒回，后台增量同步，减少等待。",
    "features.download.title": "一键下载添加",
    "features.download.desc": "在热门或搜索页直接下载并添加到我的游戏。",
    "features.library.title": "我的游戏管理",
    "features.library.desc": "支持启动、移除、拖拽排序，新添加项目默认置顶。",
    "features.update.title": "自动更新",
    "features.update.desc": "基于 Velopack 和 GitHub Releases，支持检查更新与升级。",
    "demo.title": "功能演示",
    "demo.desc": "下面是应用实际使用演示。",
    "footer.copy": "Game Trainer Launcher · GPL-3.0",
    "footer.repo": "项目仓库",
    "meta.description": "基于 FlingTrainer 的 Windows 游戏修改器启动器，支持中英文搜索、一键下载与本地管理。",
    "toggle.theme.light": "浅色",
    "toggle.theme.dark": "深色",
    "toggle.lang": "EN"
  },
  en: {
    "hero.badge": "Windows Desktop App",
    "hero.title": "Manage Game Trainers with Ease",
    "hero.desc": "Browse, search, download, and launch trainers with local index speed and stable library management.",
    "hero.download": "Download Latest",
    "hero.github": "View GitHub",
    "features.title": "Core Features",
    "features.search.title": "Chinese + English Search",
    "features.search.desc": "Fast local index results first, with background incremental sync.",
    "features.download.title": "One-click Download & Add",
    "features.download.desc": "Download and add trainers directly from popular and search pages.",
    "features.library.title": "My Library",
    "features.library.desc": "Launch, remove, reorder by drag-and-drop, and keep newly added items on top.",
    "features.update.title": "Auto Update",
    "features.update.desc": "Powered by Velopack and GitHub Releases with built-in update checks.",
    "demo.title": "Demo",
    "demo.desc": "A quick look at the app in action.",
    "footer.copy": "Game Trainer Launcher · GPL-3.0",
    "footer.repo": "Repository",
    "meta.description": "A Windows trainer launcher based on FlingTrainer with bilingual search, one-click download, and local management.",
    "toggle.theme.light": "Light",
    "toggle.theme.dark": "Dark",
    "toggle.lang": "中文"
  }
};

function pickInitialLanguage() {
  const saved = localStorage.getItem(STORAGE_KEYS.lang);
  if (saved && TEXT[saved]) return saved;
  return navigator.language.toLowerCase().startsWith("zh") ? "zh-CN" : "en";
}

function pickInitialTheme() {
  const saved = localStorage.getItem(STORAGE_KEYS.theme);
  if (saved === "light" || saved === "dark") return saved;
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

let currentLang = pickInitialLanguage();
let currentTheme = pickInitialTheme();

function applyLanguage(lang) {
  const dict = TEXT[lang] || TEXT["zh-CN"];
  document.documentElement.lang = lang;
  document.querySelectorAll("[data-i18n]").forEach((el) => {
    const key = el.getAttribute("data-i18n");
    if (key && dict[key]) el.textContent = dict[key];
  });
  document.title = "Game Trainer Launcher";
  const desc = document.querySelector('meta[name="description"]');
  if (desc) desc.setAttribute("content", dict["meta.description"]);

  const langBtn = document.getElementById("langToggle");
  if (langBtn) langBtn.textContent = dict["toggle.lang"];

  applyTheme(currentTheme);
  localStorage.setItem(STORAGE_KEYS.lang, lang);
}

function applyTheme(theme) {
  currentTheme = theme === "light" ? "light" : "dark";
  document.documentElement.setAttribute("data-theme", currentTheme);
  const dict = TEXT[currentLang] || TEXT["zh-CN"];
  const themeBtn = document.getElementById("themeToggle");
  if (themeBtn) {
    themeBtn.textContent =
      currentTheme === "dark" ? dict["toggle.theme.light"] : dict["toggle.theme.dark"];
  }
  localStorage.setItem(STORAGE_KEYS.theme, currentTheme);
}

document.getElementById("langToggle")?.addEventListener("click", () => {
  currentLang = currentLang === "zh-CN" ? "en" : "zh-CN";
  applyLanguage(currentLang);
});

document.getElementById("themeToggle")?.addEventListener("click", () => {
  applyTheme(currentTheme === "dark" ? "light" : "dark");
});

applyLanguage(currentLang);
