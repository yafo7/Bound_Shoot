# 森林v3素材与设计资料清单

核对日期：2026-10-30。下表保留策划阶段候选调研。后续已选择Ninja Adventure并取得三个原始PNG接入Unity，其余包没有购买、下载或导入；实际来源、许可、固定版本和文件校验见[已用素材记录](FOREST_V3_ASSETS.md)。

## 推荐素材

| 候选与作者 | 页面确认内容 | 对本项目的判断 | 条款与取得状态 |
|---|---|---|---|
| [Tiny Swords / Pixel Frog](https://pixelfrog-assets.itch.io/tiny-swords) | 64×64网格；免费包有地形、装饰、UI；地形介绍明确包含高度、连接台阶、树石、阴影和多种草色 | 首选视觉样板，直接匹配用户森林高台需求；只取环境和适用UI，保留蔬果 | 页面允许个人/商业项目及修改，禁止资源再分发、转售或重新打包。未下载；原始素材不得直接随公共源码仓库分发，团队各自取得或采用合适独立资源流程 |
| [Ninja Adventure / Pixel-Boy、AAA](https://pixel-boy.itch.io/ninja-adventure-asset-pack) | 俯视环境tileset、地面自动衔接、UI、音效等；页面标记16×16和CC0 | 首选可公开协作候选；森林树/地块作主包，具体崖壁/台阶覆盖需包内核对，不假定介绍里没列的内容已经齐备 | CC0，可用于商业项目；作者欢迎署名。未下载；记录作者和来源后可用于项目协作 |
| [Tiny Town / Kenney](https://kenney.nl/assets/tiny-town) | 16×16、130项、CC0 | 简洁轮廓和备用环境参考；城镇取向，未确认完整高台地块，不单独保证满足新地图 | CC0，未下载 |
| [Pixel Art Top Down - Basic / Cainos](https://cainos.itch.io/pixel-art-top-down-basic) | 32×32；草地、石地、墙、树、道具、独立阴影，含Unity相关内容 | 质量清楚但比目标更精细，可作落地阴影/遮挡参考；不和其他包随意混搭 | 允许商业使用和修改，禁止再分发/转售。未下载 |
| [Cozy Farm / shubibubi](https://shubibubi.itch.io/cozy-farm) | 16×16，介绍含崖壁/台阶、树石、道路、阴影感明确；免费版内容较少 | 柔和森林/农场配色参考；候选完整版本，不把免费版当全功能包 | 免费版限非商用；完整版本有商业使用条件，均限制资源再分发。未购买或下载 |
| [Sprout Lands / Cup Nooble](https://cupnooble.itch.io/sprout-lands-asset-pack) | 可爱柔和色彩、草水地块等，区分Basic与Premium | 配色参考，可能过于柔软，蔬果轮廓需实际比较；不作为默认免费商用包 | 免费版限非商用；付费版商业使用条件及署名要求见作者页，限制资源再分发。未购买或下载 |

建议先做Tiny Swords风格样板对照Ninja Adventure环境样板，再固定一个主包；如采用自定义条款资源，先按实际条款安排资源协作，不把“免费可下载”与“可公开提交原图”视为同一件事。不因上述区别中断策划，本轮所有条目保持候选状态。

## 设计和实现参考

- [Boomerang Fu 开发者官网](https://www.boomerangfu.com/)：食物角色派对、简单输入、AI与强化。借鉴清楚的角色和游戏入口层级，不移植其武器/玩法或素材。
- [Bread & Fred 官方商店](https://store.steampowered.com/app/1607680/Bread__Fred/)：连接角色通过协作、动量跨越障碍。用来定义“能把同伴带着走”的体验；不是借用其内部物理参数。
- [Unity 2022.3 Tilemap文档](https://docs.unity3d.com/2022.3/Documentation/Manual/class-Tilemap.html)：组织Tile资源及关联显示/碰撞组件。与当前2022.3编辑器版本匹配；本轮没有安装新包。俯视高台的连接、射击和绳索规则是本项目设计，文档没有替我们自动实现这些内容。

## 用户参考图

- 来源：用户于本轮提供的森林像素地图截图。
- 保存：`art-proposals/2026-10-30/forest-v3-plan/user-forest-reference.png`。
- SHA256：`0764438ABDC085EB74270AB8E4656A4EB80CBC31317AA8E23F07B1FD859D71A4`。
- 用途：分析草地密度、树墙、崖壁、台阶、配色与空间层级；不声称知道具体游戏/作者，不从图中提取可发布地块。

## 下一实施阶段的素材记录要求

对最终选中包记录作者、原始链接、取得日期、原包版本/校验、许可证副本、使用文件、调整内容、署名位置和Git协作方式。对最终新增图标/Logo记录生成或绘制来源。环境候选清单与美术规则已写入 `generated/50-art/forest-ui-v3`，都保持draft/researched状态，未批准为最终资产。
