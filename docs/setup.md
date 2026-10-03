# 环境搭建

## 基线（本仓库验证过的组合）

| 项目 | 值 | 说明 |
| --- | --- | --- |
| 系统 | Windows x64 | |
| 游戏 | Potion Craft: Alchemist Simulator（Steam，appid `1210320`） | |
| 游戏版本 | 2.0.2（buildid `18969694`） | 2.0.2 补丁发布于 2025-06-26 |
| 引擎 | Unity 2023.1.13f1 | 游戏自带 |
| Mod 框架 | BepInEx **5.4.23.5**（win x64） | 内含 Doorstop 4.5.0 |

## 一、安装 BepInEx

1. 下载 `BepInEx_win_x64_5.4.23.5.zip`（地址见 [../mods/manifest.md](../mods/manifest.md)）。
2. 解压，把压缩包**里面的内容**（不是外层文件夹）放进游戏根目录，即
   `<Steam>\steamapps\common\Potion Craft\`。
3. 确认根目录出现以下文件：

| 路径 | 作用 |
| --- | --- |
| `winhttp.dll` | Doorstop 注入器，BepInEx 的入口（游戏启动时被 Windows 加载） |
| `doorstop_config.ini` | 注入配置，指向 BepInEx 预加载器；`enabled = true` |
| `.doorstop_version` | Doorstop 版本标记（4.5.0） |
| `BepInEx\core\` | BepInEx / Harmony / Mono.Cecil / MonoMod 等运行库 |
| `BepInEx\plugins\` | **插件放这里** |
| `BepInEx\config\` | 配置文件，首次运行游戏后自动生成 |

4. 直接启动游戏（Steam 正常启动即可，不需要改启动项）。

## 二、安装插件

把每个 mod 压缩包里的 `.dll` 复制到 `BepInEx\plugins\`。本仓库当前的 4 个：

```
BepInEx\plugins\
├── PotionCraft_AutoHaggle.dll          # #28 自动讨价还价
├── PotionCraft_SortBookmark.dll        # #30 书签整理
├── PotionCraft_MoreInformation.dll     # #31 更多信息
└── PotionCraftAutoGarden.dll           # #39 自动花园
```

> 这些压缩包里**只有 dll**，没有额外依赖文件。若将来遇到带配置文件的 mod（例如已卸载的
> PlentifulHarvest 会带 `PlentifulHarvestConfig.txt`），按其 README 要求的位置放置，通常也是
> `BepInEx\plugins\`。

## 三、配置

- 首次运行游戏后，`BepInEx\config\` 会为需要配置的 mod 生成 `.cfg` 文件。
- 用文本编辑器改，**保存后重启游戏**才生效。
- 各配置项的当前取值与含义见 [mods.md](mods.md)。

## 四、卸载 / 回滚

| 想做什么 | 怎么做 |
| --- | --- |
| 卸载某个 mod | 删掉 `BepInEx\plugins\` 里对应的 dll（配置文件可留着，不影响） |
| 只留框架不装 mod | 清空 `BepInEx\plugins\` |
| 完全恢复原版 | 删除游戏根目录的 `winhttp.dll`、`doorstop_config.ini`、`.doorstop_version` 和整个 `BepInEx\` 文件夹；再用 Steam「验证游戏文件完整性」兜底 |

卸载是纯删除操作，游戏本体文件从未被修改。

## 版本选择说明

- **为什么用 5.4.23.5 而不是作者 README 写的 5.4.22**：4 个 mod 的 README 都写「安装
  BepInEx_x64_5.4.22」，但 GitHub 上 5.4.22 已不在发布列表中，5.4.x 分支当前最新为
  5.4.23.5（2026-02-08 发布）。5.4.23.x 是同一维护线，插件 API 没有变化——已实测本组合下
  4 个插件全部正常加载。
- **为什么不用 BepInEx 6**：6.0 仍处于 pre 阶段，且这些 mod 都是按 5.4.x 编译的。

## 校验安装是否成功

```powershell
pwsh -File scripts/verify-install.ps1 -GamePath "<游戏根目录>"
```

脚本只读：比对框架文件与 4 个插件 dll 是否存在、SHA256 是否与 [../mods/manifest.md](../mods/manifest.md)
记录一致，并列出多出来的（不在清单里的）dll。
