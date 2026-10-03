# 下载清单

每个包的确切来源、版本、大小与 SHA256。**本仓库不存放第三方二进制文件**，只记录来源与校验值，
需要时按 URL 重新下载并核对哈希即可。

> 快照时间：**2026-10-03**（来源 URL 与发布日期均于当日核对）
> 说明：本地留存的文件名（右列"本地文件名"）与上游资产名不完全一致——下载时加了版本号后缀，便于区分多个版本。

## 一、正在使用：框架

| 本地文件名 | 版本 | 上游资产名 | 大小 (B) | SHA256 |
| --- | --- | --- | --- | --- |
| `BepInEx_win_x64_5.4.23.5.zip` | 5.4.23.5 | `BepInEx_win_x64_5.4.23.5.zip` | 639118 | `82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4` |

来源：<https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip>
（发布于 2026-02-08；内含 Doorstop 4.5.0）

## 二、正在使用：4 个 mod

| Nexus | 本地文件名 | 版本 | 上游资产名 | 大小 (B) | SHA256 |
| --- | --- | --- | --- | --- | --- |
| #28 | `PotionCraft_AutoHaggle_2.0.0.zip` | 2.0.0 | `PotionCraft_AutoHaggle.zip` | 2849 | `3F90DC0F3BC4113AD8C875F054DE8B9C03830089537D030CA4E4DBACF6CC622B` |
| #30 | `PotionCraft_SortBookmark_2.0.3.zip` | 2.0.3 | `PotionCraft_SortBookmark.zip` | 5831 | `D31F804C4B7AEE564436B88E737B86502B99258EC9BA9290D1F53E3FB9DFE404` |
| #31 | `PotionCraft_MoreInformation_2.0.2.zip` | 2.0.2 | `PotionCraft_MoreInformation.zip` | 20545 | `DA6968B7EA02D9530865C80EEB05EFE2C5B9AD01FFFF040CFEDA061A063FE768` |
| #39 | `PotionCraftAutoGarden_1.1.5.zip` | 1.1.5 | `PotionCraftAutoGarden.zip` | 11149 | `3848DDB645A78BF821D21794F4663D59E8BAF934C812B26B123EC1343FEFD640` |

来源：

- #28 <https://github.com/xiaoye97/PotionCraft_AutoHaggle/releases/download/2.0.0/PotionCraft_AutoHaggle.zip>
- #30 <https://github.com/xiaoye97/PotionCraft_SortBookmark/releases/download/2.0.3/PotionCraft_SortBookmark.zip>
- #31 <https://github.com/xiaoye97/PotionCraft_MoreInformation/releases/download/2.0.2/PotionCraft_MoreInformation.zip>
- #39 <https://github.com/ukersn/PotionCraftAutoGarden/releases/download/1.1.5/PotionCraftAutoGarden.zip>

> 这 4 个包内**只有一个 dll**，没有额外依赖文件。

## 三、已退役（保留记录，便于回溯）

| Nexus | Mod | 本地文件名 | 版本 | 来源 | 大小 (B) | SHA256 |
| --- | --- | --- | --- | --- | --- | --- |
| #4 | Plentiful Harvest | `PlentifulHarvest_2.0.1.zip` | 2.0.1 | GitHub release（tag `2.0.1`，资产 `PlentifulHarvest.zip`） | 6679 | `8668527A9DF47B18E6686E7068AB65D1948C400BF9996851B3CBFA0B764C45CE` |
| #25 | Pour Back In | `Pour_Back_In_2.0.1.zip` | 2.0.1 | Thunderstore 构建（源码：PotionCraftPourBackIn） | 154191 | `672D0A8AB5005E5D3E1F23E0028921608BA8521FF7397F9BFC1B499B9EC90421` |
| #32 | Brew From Here | `Brew_From_Here_2.0.1.zip` | 2.0.1 | Thunderstore 构建（源码：PotionCraftUsefulRecipeMarks） | 144100 | `D798D07F7B1276141BBB9013A2FB9224E749A24CDC9D02C0CE1F34672062D735` |
| #23 | Alchemy Machine Recipes | —（未下载） | 1.1.0.0 | GitHub release（tag `v1.1.0.0`） | — | — |

来源与退役原因：

- #4 <https://github.com/TommySoucy/PlentifulHarvest/releases/tag/2.0.1> —— 主动卸载；且上游停更于 2023-06
- #25 <https://thunderstore.io/package/download/AndrewFahlgren/Pour_Back_In/2.0.1/> —— 与 2.0.2 不兼容，崩溃
- #32 <https://thunderstore.io/package/download/AndrewFahlgren/Brew_From_Here/2.0.1/> —— 与 2.0.2 不兼容，崩溃
- #23 <https://github.com/AndrewFahlgren/PotionCraftAlchemyMachineRecipes/releases/tag/v1.1.0.0> —— 作者声明游戏 v2.0 起废弃，未安装

> #25 / #32 取自 Thunderstore 的构建包，所以体积比 GitHub release 里的小压缩包大
> （Thunderstore 包内含 `manifest.json`、图标与说明文件）。已装 dll 与下表哈希一致。

## 四、已安装 dll 的校验值

`scripts/verify-install.ps1` 使用的对照表：

| 安装路径（相对游戏根目录） | 大小 (B) | SHA256 |
| --- | --- | --- |
| `BepInEx\plugins\PotionCraft_AutoHaggle.dll` | 6144 | `BB2D66D2928621578BEBBA99DB66F1A238DD04D0A788983C721BF6ECE36890EC` |
| `BepInEx\plugins\PotionCraft_SortBookmark.dll` | 11776 | `16D36EDEFB5021EC8E4B98C3DAC48235AE9AC9B6F3C32591CAEC7132DFC18500` |
| `BepInEx\plugins\PotionCraft_MoreInformation.dll` | 30720 | `9A3FC4FB2B3124BDAD8C187502DC6697E2622DF0DDEC91C50F41CCD0BFEBBFBE` |
| `BepInEx\plugins\PotionCraftAutoGarden.dll` | 24576 | `31A3FA3167A447DF478DBB3875A5D39E84B7872A4D51A18DEB60DF8BB7CC9ECB` |
| `winhttp.dll` | — | `8C6CDBC38836DEE87E3368F5DE1994D7C0CCEBF29E4CE7ABA3C0981F9375412C` |
| `BepInEx\core\BepInEx.Preloader.dll` | — | `55D3895351A9D16B63B6F35F1C01B44AC650979E853D0BD3A442B92A082AF64F` |
| `BepInEx\core\BepInEx.dll` | — | `8255B28902886085C578B9E427D3073C97002DB85176D2090CDEDA90EF14CE70` |

## 五、重新安装的最短路径

```powershell
# 1) 下载（示例：全部 4 个 mod + 框架，存到 C:\Temp\potion-mods）
#    直接用上面的 URL 逐个下载即可

# 2) 解压框架到游戏根目录
Expand-Archive C:\Temp\potion-mods\BepInEx_win_x64_5.4.23.5.zip -DestinationPath "<游戏根目录>" -Force

# 3) 解压 4 个 mod，把里面的 dll 放到 BepInEx\plugins\
#    （每个包内只有一个 dll）

# 4) 校验
pwsh -File scripts/verify-install.ps1 -GamePath "<游戏根目录>"
```
