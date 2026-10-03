# 排障

## 一、日志在哪

| 日志 | 路径 | 用途 |
| --- | --- | --- |
| BepInEx 日志 | `<游戏根目录>\BepInEx\LogOutput.log` | 插件是否被加载、插件自身报错 |
| Unity 玩家日志 | `%USERPROFILE%\AppData\LocalLow\niceplay games\Potion Craft\Player.log` | 注入后的运行期异常、崩溃调用栈 |
| 上一次运行的玩家日志 | 同目录的 `Player-prev.log` | 重启游戏会覆盖 `Player.log`，上一次的内容在这里 |
| 崩溃转储 | `%TEMP%\niceplay games\Potion Craft\Crashes\` | 原生崩溃（APPCRASH）时的模块与内存信息 |

## 二、一次健康的启动长什么样

`BepInEx\LogOutput.log`：

```
[Message:   BepInEx] BepInEx 5.4.23.5 - Potion Craft (...)
[Info   :   BepInEx] Running under Unity v2023.1.13.16771760
[Info   :   BepInEx] 4 plugins to load
[Info   :   BepInEx] Loading [PotionCraftAutoGarden 1.1.5]
[Info   :   BepInEx] Loading [AutoHaggle 2.0.0]
[Info   :   BepInEx] Loading [MoreInformation 2.0.2]
[Info   :   BepInEx] Loading [SortBookmark 2.0.3]
[Message:   BepInEx] Chainloader startup complete
```

对照检查：

| 现象 | 含义 | 处置 |
| --- | --- | --- |
| 压根没有 `BepInEx 5.4.23.5` 字样 | 注入没生效 | 确认 `winhttp.dll` 在游戏根目录；确认 `doorstop_config.ini` 里 `enabled = true` |
| `N plugins to load` 的 N 小于你放的 dll 数 | 有 dll 没被当成插件识别 | 检查该文件是不是 BepInEx 插件、是否为 .NET 程序集 |
| 某个 `Loading [...]` 之后紧跟异常 | 该 mod 有问题 | 见下一节 |
| 有 `Loading [...]` 但游戏内没效果 | 可能是按键或配置问题 | 查 [mods.md](mods.md) 的按键与配置项 |

## 三、症状对照表

| 症状 | 大概原因 | 处置 |
| --- | --- | --- |
| 启动即崩 / 弹 Unity 崩溃报告 | 某个 mod 的类型与 2.0.2 不兼容 | 在 `Player.log` 里搜 `TypeLoadException` 或 `invalid vtable`，按**类型名的命名空间前缀**定位 mod，删掉对应 dll |
| 游戏能开，但某个功能没反应 | mod 未加载 / 按键不对 | 先看 `LogOutput.log` 有没有 `Loading [...]`；笔记本试 `Fn` + `F1` / `Fn` + `F2` |
| 改了配置没效果 | 没重启游戏 / 键名写错 | 保存后重启游戏；键名以配置文件中现成的那一行为准 |
| 日志出现 `LoadingQueue.GetQueueIndexByName: No index found for ...` | 已知无害 | 见下节 |

## 四、已知无害的日志

```
[Info   :PotionCraftAutoGarden] uk自动花园插件正在加载..
LoadingQueue.GetQueueIndexByName: No index found for 'RecipeBook.DMD<PotionCraft.ObjectBased.UIElements.Books.RecipeBook.RecipeBook::Awake>'. Returning current index (9)
```

第一行只是 AutoGarden 自己的启动输出，中文提示、无实际含义。

第二行从字面看是游戏的加载队列没有为该条目找到索引、于是回退到当前索引。它在本组合下**每次启动
都会出现**，而 4 个 mod 均能正常加载、实际游玩未发现问题，因此归类为无害。

## 五、二分法定位崩溃的 mod

一次装多个 mod 后游戏崩溃时：

1. 记下列表：`Get-ChildItem "<游戏根目录>\BepInEx\plugins" -Filter *.dll`
2. 移走**一半** dll（先放游戏目录外，不要删），启动测试。
3. 崩 → 问题在剩下那一半里；不崩 → 问题在被移走的那一半里。
4. 对有问题的那一半重复第 2 步，直到只剩一个 dll。
5. 定位后按 [compatibility.md](compatibility.md) 的方式确认它引用了哪个已变化的游戏类型。

## 六、快速校验脚本

```powershell
pwsh -File scripts/verify-install.ps1 -GamePath "<游戏根目录>"
```

只读脚本：逐项检查框架文件与 4 个插件 dll 是否存在、SHA256 是否与 [../mods/manifest.md](../mods/manifest.md) 一致，
并列出不在清单里的额外 dll（例如新装的 mod）。发现不一致时退出码为 `1`。

## 七、还是不行？

按顺序收集这三样，基本能定位到具体 mod：

1. `BepInEx\LogOutput.log` 的最后 50 行
2. `Player.log` 里第一处 `Exception` 前后各 15 行
3. `BepInEx\plugins\` 的文件列表

判断原则：**日志里出现的类型名，其命名空间前缀几乎就是出问题的 mod**。
