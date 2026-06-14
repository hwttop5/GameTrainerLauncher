const i18n = {
  "zh-CN": {
    "nav.features": "核心功能",
    "nav.workflow": "使用流程",
    "toggle.lang": "切换语言",
    "toggle.theme": "切换主题",
    "hero.title": "简洁高效的游戏修改器管理工具",
    "hero.subtitle": "基于 FlingTrainer 构建，支持中英文双语搜索、本地索引、版本选择与一键启动",
    "hero.download": "下载最新版",
    "hero.github": "Star on GitHub",
    "features.label": "核心功能",
    "features.title": "为玩家打造的实用功能",
    "features.description": "简单易用，功能完整",
    "features.search.title": "中英文搜索",
    "features.search.description": "本地标题索引优先返回结果，减少等待时间",
    "features.library.title": "本地库管理",
    "features.library.description": "添加、排序、移除和封面补全，统一管理",
    "features.version.title": "版本选择",
    "features.version.description": "下载前选择对应版本，降低不匹配问题",
    "features.update.title": "自动更新",
    "features.update.description": "基于 Velopack，检查、下载并重启安装",
    "workflow.label": "使用流程",
    "workflow.title": "三步即可开始",
    "workflow.step1.title": "搜索与浏览",
    "workflow.step1.description": "本地索引优先返回结果，后台同步补充内容",
    "workflow.step2.title": "下载与管理",
    "workflow.step2.description": "一键下载加入本地库，版本和封面统一管理",
    "workflow.step3.title": "启动与维护",
    "workflow.step3.description": "从库中直接启动，更新提示清晰易懂",
    "cta.title": "立即开始使用",
    "cta.description": "完全开源，直接从 GitHub Releases 下载",
    "cta.download": "下载最新版",
    "cta.github": "查看源代码",
    "footer.copy": "© 2024 Game Trainer Launcher · GPL-3.0",
    "footer.repo": "项目仓库",
    "footer.license": "开源协议"
  },
  "en": {
    "nav.features": "Features",
    "nav.workflow": "How It Works",
    "toggle.lang": "Switch language",
    "toggle.theme": "Switch theme",
    "hero.title": "Simple & Efficient Trainer Manager",
    "hero.subtitle": "Built on FlingTrainer with bilingual search, local indexing, version selection, and one-click launch",
    "hero.download": "Download Latest",
    "hero.github": "Star on GitHub",
    "features.label": "Features",
    "features.title": "Built for Gamers",
    "features.description": "Simple to use, feature complete",
    "features.search.title": "Bilingual Search",
    "features.search.description": "Local title index returns results faster",
    "features.library.title": "Local Library",
    "features.library.description": "Add, sort, remove, and manage covers in one place",
    "features.version.title": "Version Selection",
    "features.version.description": "Choose matching version before download",
    "features.update.title": "Auto Update",
    "features.update.description": "Powered by Velopack for seamless updates",
    "workflow.label": "How It Works",
    "workflow.title": "Get Started in 3 Steps",
    "workflow.step1.title": "Search & Browse",
    "workflow.step1.description": "Local index returns fast, background sync fills gaps",
    "workflow.step2.title": "Download & Organize",
    "workflow.step2.description": "One-click download to local library with version control",
    "workflow.step3.title": "Launch & Maintain",
    "workflow.step3.description": "Launch directly from library with clear update prompts",
    "cta.title": "Get Started Today",
    "cta.description": "Fully open source, download directly from GitHub Releases",
    "cta.download": "Download Latest",
    "cta.github": "View Source Code",
    "footer.copy": "© 2024 Game Trainer Launcher · GPL-3.0",
    "footer.repo": "Repository",
    "footer.license": "License"
  }
};

let currentLang = navigator.language.startsWith("zh") ? "zh-CN" : "en";
let currentTheme = window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";

function applyLanguage(lang) {
  currentLang = lang;
  const texts = i18n[lang];
  document.querySelectorAll("[data-i18n]").forEach(el => {
    const key = el.getAttribute("data-i18n");
    if (texts[key]) el.textContent = texts[key];
  });
  document.documentElement.lang = lang;
  
  const langToggle = document.getElementById("langToggle");
  const themeToggle = document.getElementById("themeToggle");
  if (langToggle) {
    langToggle.setAttribute("data-i18n-text", texts["toggle.lang"]);
  }
  if (themeToggle) {
    themeToggle.setAttribute("data-i18n-text", texts["toggle.theme"]);
  }
}

function applyTheme(theme) {
  currentTheme = theme;
  document.documentElement.setAttribute("data-theme", theme);
  const themeBtn = document.getElementById("themeToggle");
  if (themeBtn) {
    const icon = themeBtn.querySelector("svg");
    if (icon) {
      if (theme === "dark") {
        icon.innerHTML = '<circle cx="12" cy="12" r="5"></circle><path d="M12 1v2M12 21v2M4.22 4.22l1.42 1.42M18.36 18.36l1.42 1.42M1 12h2M21 12h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42"></path>';
      } else {
        icon.innerHTML = '<path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>';
      }
    }
  }
}

function initCarousel() {
  const slides = document.querySelectorAll(".preview-slide");
  const dots = document.querySelectorAll(".carousel-dot");
  const prevBtn = document.querySelector(".carousel-prev");
  const nextBtn = document.querySelector(".carousel-next");
  let current = 0;
  let timer;

  function showSlide(index) {
    current = (index + slides.length) % slides.length;
    slides.forEach((s, i) => {
      s.classList.toggle("active", i === current);
    });
    dots.forEach((d, i) => {
      d.classList.toggle("active", i === current);
    });
  }

  function nextSlide() {
    showSlide(current + 1);
  }

  function prevSlide() {
    showSlide(current - 1);
  }

  function startAuto() {
    stopAuto();
    if (!window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      timer = setInterval(nextSlide, 4000);
    }
  }

  function stopAuto() {
    if (timer) clearInterval(timer);
  }

  dots.forEach((dot, i) => {
    dot.addEventListener("click", () => {
      showSlide(i);
      startAuto();
    });
  });

  if (prevBtn) {
    prevBtn.addEventListener("click", () => {
      prevSlide();
      startAuto();
    });
  }

  if (nextBtn) {
    nextBtn.addEventListener("click", () => {
      nextSlide();
      startAuto();
    });
  }

  const preview = document.querySelector(".preview");
  if (preview) {
    preview.addEventListener("mouseenter", stopAuto);
    preview.addEventListener("mouseleave", startAuto);
  }

  startAuto();
}

function initMobileMenu() {
  const toggle = document.getElementById("mobileMenuToggle");
  const nav = document.querySelector(".nav");
  const navLinks = document.querySelectorAll(".nav-link");

  if (!toggle || !nav) return;

  toggle.addEventListener("click", () => {
    const isActive = nav.classList.toggle("active");
    toggle.classList.toggle("active", isActive);
  });

  navLinks.forEach(link => {
    link.addEventListener("click", () => {
      nav.classList.remove("active");
      toggle.classList.remove("active");
    });
  });

  window.addEventListener("resize", () => {
    if (window.innerWidth > 768) {
      nav.classList.remove("active");
      toggle.classList.remove("active");
    }
  });
}

document.getElementById("langToggle")?.addEventListener("click", () => {
  applyLanguage(currentLang === "zh-CN" ? "en" : "zh-CN");
});

document.getElementById("themeToggle")?.addEventListener("click", () => {
  applyTheme(currentTheme === "dark" ? "light" : "dark");
});

applyLanguage(currentLang);
applyTheme(currentTheme);
initCarousel();
initMobileMenu();
