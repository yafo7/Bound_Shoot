# 森林 v3 第三方素材记录

取得与核对日期：2026-10-30。

主包：[Ninja Adventure — Pixel-Boy、AAA](https://pixel-boy.itch.io/ninja-adventure-asset-pack)。作者页面明确声明CC0，可用于商业游戏，无强制署名。官方页面直接链接到[Godot示例仓库](https://github.com/pixel-boy/NinjaAdventure)。本项目只使用下列PNG，没有引入Godot程序或整包压缩文件。

许可：[CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/)。本文件保留来源与作者署名，不在战斗UI中增加技术/许可说明。没有使用Tiny Swords等限制再分发的候选素材。

固定上游提交：`6ac78232d5aedcc85ce5f27d060ea92366f7c24a`。资源位置：`unity-client/Assets/BoundShoot/Resources/Forest/`。

| 本机文件 | 上游原文件（固定提交） | 尺寸 | SHA256 |
|---|---|---|---|
| tileset_floor.png | [content/map/tileset_floor.png](https://github.com/pixel-boy/NinjaAdventure/blob/6ac78232d5aedcc85ce5f27d060ea92366f7c24a/content/map/tileset_floor.png) | 336×448 | E111065065EDF806E7E893330086E68EFC8755175D92F14D087B42D40A331E16 |
| tileset_village_abandoned.png | [content/map/tileset_village_abandoned.png](https://github.com/pixel-boy/NinjaAdventure/blob/6ac78232d5aedcc85ce5f27d060ea92366f7c24a/content/map/tileset_village_abandoned.png) | 320×192 | 6787C6E22A4D44CEEE4F158309B2519707BDB70C59ACA00B3D49006CADCCA06E |
| heart.png | [content/ui/heart.png](https://github.com/pixel-boy/NinjaAdventure/blob/6ac78232d5aedcc85ce5f27d060ea92366f7c24a/content/ui/heart.png) | 80×16 | F1163B91560C5097AC1DE4F26470ABAA9E3461F3BC4A8685FD7DAAE2D8CABB61 |

PNG保持上游字节。Unity导入使用Point、无Mip、无压缩、保持尺寸与透明。运行时Sprite.Create读取切片，树/石块可着色，空心变灰；没有改写PNG像素。切片坐标集中在ForestArt.cs，以原图左上为原点。

道路、台阶、平台底色、草簇、接触阴影和UI底板由本项目几何绘制。已有蔬果、枪械和道具维持原来源记录，未冒称Ninja Adventure角色/武器。草地使用低对比底色与少量草簇，避免将不透明地形图块当透明装饰。

## 视觉核对

实际Unity Camera/Canvas截图位于 `art-proposals/2026-10-30/forest-v3/`，覆盖标题、身份、模式、训练等待房、训练HUD、4v4/3v3房间、台阶战斗、结算与地图总览。截图中的站位与血量由Editor审查脚本安排。红/灰心、树石切片和台阶已经肉眼检查；风格最终认可仍由用户试玩决定。
