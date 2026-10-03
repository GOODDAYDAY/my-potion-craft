# my-potion-craft

《Potion Craft: Alchemist Simulator》的 **mod 使用说明与个人工作台**。

这个仓库回答三个问题：**现在装了什么**、**怎么装 / 怎么调**、**哪些 mod 被淘汰以及为什么**。
以后如果现有 mod 覆盖不到某个需求，自研的插件代码也会放在这里。

> **优先级原则**：能复用现成 mod 就复用——上游源码以 git submodule 钉在**与已装二进制对应的 release tag** 上引入；
> 只有上游确实没有的功能，才自己写插件。见 [docs/roadmap.md](docs/roadmap.md)。

## 当前状态

| 项目 | 值 |
| --- | --- |
| 游戏 | Potion Craft: Alchemist Simulator（Steam，appid `1210320`） |
| 游戏版本 | **2.0.2**（游戏内显示 `ST2.0.2.0`；buildid `18969694`，对应 2025-06-26 的 2.0.2 补丁） |
| 引擎 | Unity 2023.1.13f1 |
| Mod 框架 | **BepInEx 5.4.23.5**（win x64，内置 Doorstop 4.5.0） |
| 运行时验证 | 4 个插件全部加载成功，日志出现 `Chainloader startup complete`，无异常 |

游戏本体文件零改动——所有 mod 都通过 BepInEx 从外部注入，卸载时删干净即可。

## 已启用的 mod（5 个）

| Nexus | Mod | 版本 | 作用 | 对应我的需求 |
| --- | --- | --- | --- | --- |
| [#28](https://www.nexusmods.com/potioncraftalchemistsimulator/mods/28) | Auto Haggle | 2.0.0 | 自动讨价还价 | ① 自动讨价还价 |
| [#30](https://www.nexusmods.com/potioncraftalchemistsimulator/mods/30) | Sort Bookmark | 2.0.3 | 配方书书签一键整理（配方书界面按 `空格`） | ② 整理配方书书签 |
| [#31](https://www.nexusmods.com/potioncraftalchemistsimulator/mods/31) | MoreInformation | 2.0.2 | 物品价值 / 药水成本 / NPC 需求属性 / 研磨进度等提示 | 附加 |
| [#39](https://www.nexusmods.com/potioncraftalchemistsimulator/mods/39) | PotionCraftAutoGarden | 1.1.5 | 每日自动收获 + 浇水、水晶收获、一键施肥 | ③ 自动浇水、自动收获 |
| —（GitHub，非 Nexus） | [Ukersn's Tweak Wizard](https://github.com/ukersn/Potion-Craft-Ukersn-s-TweakWizard) | 1.3.2 | **花园无限种植**（另含一键研磨、快速选药、药水边缘吸附） | 附加 |

另有一个本仓库自研的插件 **MagicShelf**（魔法架子原型），见下方专门章节。

每个 mod 的功能细节、按键与配置项见 [docs/mods.md](docs/mods.md)。

### 三个需求的覆盖情况

1. **自动讨价还价** → Auto Haggle 覆盖 ✓（装好即生效，无可调项）
2. **整理配方书书签** → Sort Bookmark 覆盖 ✓
3. **自动浇水、自动收获** → PotionCraftAutoGarden 覆盖 ✓
   - 已开启：每天醒来自动收获植物 + 浇水 + 收水晶（快速模式）
   - **唯一缺口**：施肥没法「每天自动」，只能按 `F2` 手动触发（已把范围改成全部房间，一次施完）

## 自研插件：MagicShelf（魔法架子 · 阶段 1 原型）

需求是「**架子上放一瓶药 → 一键把背包里同种的药水都放到那一格**」。Nexus / Thunderstore / GitHub
都没有现成实现（最接近的 `PotionCraftShelves` 是给房间加置物架，`General Utils` 是作弊合集），
所以按「没有现成的才自己写」的原则动手，源码在 [plugins/MagicShelf](plugins/MagicShelf/)。

| 项 | 值 |
| --- | --- |
| 当前阶段 | **阶段 1 原型**：挂在游戏现有货架上，非建造模式点击 → 把背包里同种的药水全部搬上去（每次点击搬一次）。行为已通过端到端实测，待手感确认 |
| 技术栈 | BepInEx 5.4.x + Harmony，目标框架 `netstandard2.1` |
| 已装文件 | `BepInEx\plugins\MagicShelf.dll` |
| 运行时验证 | ✅ 插件加载无异常（`5 plugins to load`）+ **功能实测通过**：自检在真实货架「Set 1 Table」上完成搬运，背包 3 → 0 瓶、架上 1 → 4 瓶，耗时 0.5 秒 |
| 尚未做 | 独立物品、颜色区分、商人出售、存档兼容（阶段 2，等阶段 1 手感确认） |

「同种」直接用游戏自己的 `Potion.IsSame()` 判定——它比较效果数组、药水基底、**实际用掉的材料**、
瓶子/贴纸/图标颜色、自定义名称与描述，已经是最严格的现成判定。相关逆向结论见
[docs/game-api-notes.md](docs/game-api-notes.md)。

## 目录结构

```
.
├── README.md                   # 本文件：总览与当前状态
├── docs/
│   ├── setup.md                # BepInEx 安装步骤、版本选择、卸载方法
│   ├── mods.md                 # 每个 mod 的功能、按键、配置项
│   ├── compatibility.md        # 兼容性矩阵、淘汰记录、报错原文与根因
│   ├── troubleshooting.md      # 出事怎么查：日志位置、症状对照表、二分法
│   ├── roadmap.md              # 决策原则、已知缺口、自研插件的来龙去脉
│   ├── stage2-design.md        # 阶段 2（独立魔法架子）的设计、里程碑与风险
│   └── game-api-notes.md       # 游戏内部 API 调查笔记（自研插件的地基）
├── mods/
│   └── manifest.md             # 下载清单：来源 URL + 版本 + 大小 + SHA256
├── plugins/
│   └── MagicShelf/             # 自研插件（阶段 1 原型）：点击货架补齐同种药水
├── scripts/
│   └── verify-install.ps1      # 只读校验：比对已装插件与清单
└── upstream/                   # 上游源码（git submodule，钉在对应 release tag）
    ├── PotionCraft_AutoHaggle       @ 2.0.0
    ├── PotionCraft_SortBookmark     @ 2.0.3
    ├── PotionCraft_MoreInformation  @ 2.0.2
    └── PotionCraftAutoGarden        @ 1.1.5
```

## 快速上手

```powershell
# 1) 装框架：把 BepInEx_win_x64_5.4.23.5.zip 解压到游戏根目录
#    成功标志：根目录出现 winhttp.dll、doorstop_config.ini、.doorstop_version、BepInEx\ 文件夹

# 2) 装插件：把各 mod 的 dll 放进 BepInEx\plugins\

# 3) 启动一次游戏 —— 配置文件会自动生成到 BepInEx\config\，改完重启游戏生效

# 4) 校验（只读，不改动任何文件）
pwsh -File scripts/verify-install.ps1 -GamePath "<你的游戏根目录>"
```

详细步骤与文件清单见 [docs/setup.md](docs/setup.md)，下载地址与哈希见 [mods/manifest.md](mods/manifest.md)。

## 一条重要的兼容性红线

游戏 **2.0.2 补丁改动过内部类型**：有的类型被改名（`Bookmark.MovingState` → 顶层 `BookmarkMovingState`），
有的类型虚表槽位发生变化（`StackItem`）。因此为旧版本编译的 mod 会在启动时抛
`TypeLoadException` / `ReflectionTypeLoadException`，严重时直接让游戏原生崩溃。

**某个 mod 是否受影响，和它的发布时间没有必然关系**，取决于它 patch 了哪些游戏内部类型：

- 2024-12 发布的 Auto Haggle → 正常
- 同样是 2024-12 发布的 Pour Back In → 崩溃（虚表槽位不匹配）
- 2025-02 发布的 Brew From Here → 崩溃（引用的类型已被改名）

所以判断标准只有一条：**装完看日志**。报错原文、根因分析与已淘汰清单见
[docs/compatibility.md](docs/compatibility.md)。
