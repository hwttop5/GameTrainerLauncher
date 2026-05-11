const STORAGE_KEYS = {
  lang: "gtl.site.lang",
  theme: "gtl.site.theme"
};

const TEXT = {
  "zh-CN": {
    "meta.title": "Game Trainer Launcher",
    "meta.description": "基于 FlingTrainer 的 Windows 游戏修改器启动器，支持中英文搜索、一键下载与本地管理。",
    "nav.features": "核心能力",
    "nav.workflow": "使用流程",
    "nav.trust": "开源与更新",
    "nav.releases": "Releases",
    "nav.github": "GitHub",
    "hero.eyebrow": "Windows 桌面应用",
    "hero.title": "更轻松地管理游戏修改器",
    "hero.subtitle": "浏览、搜索、下载并启动修改器。支持中英文搜索与本地索引，启动快，管理稳。",
    "hero.download": "下载最新版",
    "hero.platform": "Windows 10/11 (64-bit)",
    "hero.github": "查看 GitHub",
    "hero.opensource": "开源项目",
    "hero.badge.windows": "Windows 专属",
    "hero.badge.bilingual": "中英双语",
    "hero.badge.opensource": "开源免费",
    "hero.badge.releases": "GitHub Releases",
    "hero.metric1.value": "本地索引",
    "hero.metric1.label": "优先返回搜索结果",
    "hero.metric2.value": "版本匹配",
    "hero.metric2.label": "下载前选择适配版本",
    "hero.metric3.value": "本地库",
    "hero.metric3.label": "统一管理封面与启动入口",
    "hero.panelBadge": "桌面工作流",
    "hero.panelTitle": "真实界面预览",
    "hero.panelDesc": "当前桌面应用的实际操作演示",
    "workflow.kicker": "How it works",
    "workflow.title": "三步即可开始",
    "workflow.step1.title": "搜索与浏览",
    "workflow.step1.desc": "先走本地索引，再做后台同步，快速找到你要的修改器。",
    "workflow.step2.title": "下载与管理",
    "workflow.step2.desc": "一键下载并加入本地库，版本状态和封面统一管理。",
    "workflow.step3.title": "启动与维护",
    "workflow.step3.desc": "从库中直接启动游戏或修改器，更新检查与版本提示保持清晰。",
    "features.kicker": "Core features",
    "features.title": "为玩家打造的高效工具",
    "features.search.title": "中英文搜索",
    "features.search.desc": "本地标题索引优先返回结果，减少等待时间。",
    "features.library.title": "本地库管理",
    "features.library.desc": "添加、拖拽排序、移除和封面补全都在一个界面完成。",
    "features.version.title": "版本选择",
    "features.version.desc": "下载前选择对应版本，降低版本不匹配带来的问题。",
    "features.update.title": "自带更新流程",
    "features.update.desc": "基于 Velopack 和 GitHub Releases，检查、下载并重启安装。",
    "trust.kicker": "Open source and updates",
    "trust.title": "开源、透明、可验证",
    "trust.desc": "完全开源，发布链路清晰，下载来源可追踪。",
    "trust.point1.title": "GPL-3.0",
    "trust.point1.desc": "代码公开，欢迎审阅与贡献。",
    "trust.point2.title": "GitHub Releases",
    "trust.point2.desc": "安装包与版本记录统一托管。",
    "trust.point3.title": "SHA256 校验",
    "trust.point3.desc": "发布产物附带校验清单，便于核验完整性。",
    "trust.point4.title": "Windows 桌面定位",
    "trust.point4.desc": "专注 Windows 10/11，不做多余平台包装。",
    "trust.note.title": "关于来源与使用",
    "trust.note.desc": "修改器内容依赖 FlingTrainer；本工具仅供学习与个人使用，请遵守当地法律与平台条款。",
    "cta.kicker": "Download",
    "cta.title": "立即下载 Game Trainer Launcher",
    "cta.desc": "为 Windows 打造的修改器管理器，直接从 GitHub Releases 获取最新版。",
    "cta.download": "下载最新版",
    "cta.releases": "前往 Releases 查看全部版本",
    "footer.copy": "Game Trainer Launcher · GPL-3.0",
    "footer.repo": "项目仓库",
    "footer.license": "开源协议",
    "toggle.theme.light": "浅色",
    "toggle.theme.dark": "深色",
    "toggle.lang": "EN",
    "toggle.menu": "菜单"
  },
  en: {
    "meta.title": "Game Trainer Launcher",
    "meta.description": "A Windows game trainer launcher based on FlingTrainer with bilingual search, one-click download, and local library management.",
    "nav.features": "Features",
    "nav.workflow": "How It Works",
    "nav.trust": "Open Source",
    "nav.releases": "Releases",
    "nav.github": "GitHub",
    "hero.eyebrow": "Windows Desktop App",
    "hero.title": "Manage Game Trainers with Less Friction",
    "hero.subtitle": "Browse, search, download, and launch trainers with bilingual search, local index speed, and stable library management.",
    "hero.download": "Download Latest",
    "hero.platform": "Windows 10/11 (64-bit)",
    "hero.github": "View GitHub",
    "hero.opensource": "Open source project",
    "hero.badge.windows": "Windows focused",
    "hero.badge.bilingual": "Bilingual search",
    "hero.badge.opensource": "Open source",
    "hero.badge.releases": "GitHub Releases",
    "hero.metric1.value": "Local index",
    "hero.metric1.label": "Search results return fast",
    "hero.metric2.value": "Version fit",
    "hero.metric2.label": "Pick the matching trainer build",
    "hero.metric3.value": "Local library",
    "hero.metric3.label": "Covers and launch entry stay together",
    "hero.panelBadge": "Desktop workflow",
    "hero.panelTitle": "Real product preview",
    "hero.panelDesc": "Actual workflow captured from the desktop app",
    "workflow.kicker": "How it works",
    "workflow.title": "Start in Three Steps",
    "workflow.step1.title": "Search and Browse",
    "workflow.step1.desc": "Local index results come back first, then background sync fills in coverage.",
    "workflow.step2.title": "Download and Organize",
    "workflow.step2.desc": "Add trainers to a local library with one click and keep versions and covers in one place.",
    "workflow.step3.title": "Launch and Maintain",
    "workflow.step3.desc": "Start trainers from the library directly and keep update prompts and status readable.",
    "features.kicker": "Core features",
    "features.title": "A Practical Tool for Players",
    "features.search.title": "Chinese + English Search",
    "features.search.desc": "Prioritizes local title index results to cut waiting time.",
    "features.library.title": "Local Library Management",
    "features.library.desc": "Add, reorder, remove, and backfill covers from a single interface.",
    "features.version.title": "Version Selection",
    "features.version.desc": "Choose the matching trainer version before download to reduce mismatch issues.",
    "features.update.title": "Built-in Update Flow",
    "features.update.desc": "Powered by Velopack and GitHub Releases for checks, download, and restart-to-install.",
    "trust.kicker": "Open source and updates",
    "trust.title": "Open, Transparent, and Verifiable",
    "trust.desc": "The code is public, the release path is clear, and download sources are easy to inspect.",
    "trust.point1.title": "GPL-3.0",
    "trust.point1.desc": "Source code is public and open for review and contribution.",
    "trust.point2.title": "GitHub Releases",
    "trust.point2.desc": "Installers and version history are published through a single channel.",
    "trust.point3.title": "SHA256 Checksums",
    "trust.point3.desc": "Release artifacts include a checksum manifest for integrity verification.",
    "trust.point4.title": "Windows Desktop Scope",
    "trust.point4.desc": "Focused on Windows 10/11 without unnecessary cross-platform packaging.",
    "trust.note.title": "Source and usage notes",
    "trust.note.desc": "Trainer content depends on FlingTrainer. This tool is for learning and personal use only; follow local laws and platform terms.",
    "cta.kicker": "Download",
    "cta.title": "Download Game Trainer Launcher",
    "cta.desc": "A Windows trainer manager built for direct installs from GitHub Releases.",
    "cta.download": "Download Latest",
    "cta.releases": "Browse all releases on GitHub",
    "footer.copy": "Game Trainer Launcher · GPL-3.0",
    "footer.repo": "Repository",
    "footer.license": "License",
    "toggle.theme.light": "Light",
    "toggle.theme.dark": "Dark",
    "toggle.lang": "简体中文",
    "toggle.menu": "Menu"
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

const siteHeader = document.querySelector(".site-header");
const langToggle = document.getElementById("langToggle");
const themeToggle = document.getElementById("themeToggle");
const menuToggle = document.getElementById("menuToggle");
const siteNavLinks = Array.from(document.querySelectorAll(".site-nav a"));

function applyLanguage(lang) {
  currentLang = TEXT[lang] ? lang : "zh-CN";
  const dict = TEXT[currentLang];

  document.documentElement.lang = currentLang;
  document.querySelectorAll("[data-i18n]").forEach((el) => {
    const key = el.getAttribute("data-i18n");
    if (key && dict[key]) {
      el.textContent = dict[key];
    }
  });

  document.title = dict["meta.title"];
  const description = document.querySelector('meta[name="description"]');
  if (description) {
    description.setAttribute("content", dict["meta.description"]);
  }

  if (langToggle) {
    langToggle.textContent = dict["toggle.lang"];
  }

  if (menuToggle) {
    menuToggle.setAttribute("aria-label", dict["toggle.menu"]);
  }

  applyTheme(currentTheme);
  localStorage.setItem(STORAGE_KEYS.lang, currentLang);
}

function applyTheme(theme) {
  currentTheme = theme === "light" ? "light" : "dark";
  document.documentElement.setAttribute("data-theme", currentTheme);

  const dict = TEXT[currentLang] || TEXT["zh-CN"];
  if (themeToggle) {
    themeToggle.textContent =
      currentTheme === "dark" ? dict["toggle.theme.light"] : dict["toggle.theme.dark"];
  }

  localStorage.setItem(STORAGE_KEYS.theme, currentTheme);
}

function setMenuOpen(open) {
  if (!siteHeader || !menuToggle) return;
  siteHeader.setAttribute("data-menu-open", open ? "true" : "false");
  menuToggle.setAttribute("aria-expanded", open ? "true" : "false");
}

langToggle?.addEventListener("click", () => {
  applyLanguage(currentLang === "zh-CN" ? "en" : "zh-CN");
});

themeToggle?.addEventListener("click", () => {
  applyTheme(currentTheme === "dark" ? "light" : "dark");
});

menuToggle?.addEventListener("click", () => {
  const isOpen = siteHeader?.getAttribute("data-menu-open") === "true";
  setMenuOpen(!isOpen);
});

siteNavLinks.forEach((link) => {
  link.addEventListener("click", () => {
    setMenuOpen(false);
  });
});

window.addEventListener("resize", () => {
  if (window.innerWidth > 980) {
    setMenuOpen(false);
  }
});

function setupDemoCarousel() {
  const carousel = document.querySelector("[data-demo-carousel]");
  if (!carousel) return;

  const slides = Array.from(carousel.querySelectorAll("[data-demo-slide]"));
  const dots = Array.from(carousel.querySelectorAll("[data-carousel-dot]"));
  const prev = carousel.querySelector("[data-carousel-prev]");
  const next = carousel.querySelector("[data-carousel-next]");
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
  const intervalMs = 3000;
  let activeIndex = 0;
  let timer = 0;

  function showSlide(index) {
    activeIndex = (index + slides.length) % slides.length;
    slides.forEach((slide, slideIndex) => {
      slide.classList.toggle("is-active", slideIndex === activeIndex);
    });
    dots.forEach((dot, dotIndex) => {
      const isActive = dotIndex === activeIndex;
      dot.classList.toggle("is-active", isActive);
      dot.setAttribute("aria-selected", isActive ? "true" : "false");
    });
  }

  function stopAutoPlay() {
    if (timer) {
      window.clearInterval(timer);
      timer = 0;
    }
  }

  function startAutoPlay() {
    stopAutoPlay();
    if (reduceMotion.matches || slides.length < 2) return;
    timer = window.setInterval(() => showSlide(activeIndex + 1), intervalMs);
  }

  prev?.addEventListener("click", () => {
    showSlide(activeIndex - 1);
    startAutoPlay();
  });

  next?.addEventListener("click", () => {
    showSlide(activeIndex + 1);
    startAutoPlay();
  });

  dots.forEach((dot, index) => {
    dot.addEventListener("click", () => {
      showSlide(index);
      startAutoPlay();
    });
  });

  carousel.addEventListener("mouseenter", stopAutoPlay);
  carousel.addEventListener("mouseleave", startAutoPlay);
  carousel.addEventListener("focusin", stopAutoPlay);
  carousel.addEventListener("focusout", startAutoPlay);
  reduceMotion.addEventListener?.("change", startAutoPlay);

  showSlide(0);
  startAutoPlay();
}

applyLanguage(currentLang);
setupDemoCarousel();
setMenuOpen(false);
