# 给枪械、道具、地图设计同学

最新数值调整：用户要求全体生命×3，配置现为空手9、狙击3、手枪9、AK9、匕首15，已是最终数值，勿重复乘三；伤害/攻速/回血1点/盾1点保留。右下武器卡拾枪后显示数值3秒。[修正说明](FACING_HEALTH_PICKUP_FIX.md)。下文早期1/3/3/5生命基线已被替代。

**2026-10-30 v3更新**：当前本地默认地图为Forest_01/林间高台，房间支持2v2、3v3、4v4。复活区玩法已废止：只要队伍有人存活，个人倒地五秒后在身体当前位置复活；团灭先结算。绳索基础90/12/200，静止队友制动降低。关卡编辑器不再提供新放置复活区，旧字段只保留读取兼容；后文复活区描述为历史格式。森林高台和崖壁调整方式见[实施说明](FOREST_V3_IMPLEMENTATION.md)，武器/道具数值保留。

现行规则更新：2026-10-30。当前为八蔬果主题、本地2v2/4v4完整对局Demo。外观仅装饰；本文件取代早期“拾枪满血且锁定”说明。参考图与资源来源记录仍保留，没有拷贝商业游戏美术到客户端。

## 日常改动入口

关卡优先使用 **Bound Shoot → Level Editor 关卡编辑器**，详细操作见 [LEVEL_EDITOR.md](LEVEL_EDITOR.md)。支持直接摆放地面、障碍、四类武器、七种道具和问号盒、出生点及可调整尺寸的复活区；保存到游戏配置后由运行时读取。旧地图补给顺序规则仅适用于尚未转为明确点位配置的地图。

Unity → Assets/BoundShoot/Resources/GameConfig.json。
本地Editor测试修改后停止并重新Play即可，不用每次更新云端。客户端和专用服务器共用这份配置；后端也读取同一份配置中的可用地图列表。未来发布构建时集中更新客户端、游戏服务器及配置；需要时重启后端。同一场比赛使用同一版本。当前云端仍为旧版，入口禁用，本地通过后另行集中升级。

- weapons：name、maxHealth、damage、range、cooldown（秒）、bulletSpeed（单位/秒）。kind 固定：1狙击、2手枪、3AK、4匕首；0为空手。
- 当前基线：狙击1最大血4伤；手枪3最大血1伤；AK3最大血2伤；匕首5最大血3伤，以最新GameConfig为准。踩不同枪立即自动替换；同枪不消费；不回血。当前生命取旧当前生命与新最大生命的较小值，盾保留；开火时间取旧冷却与新武器冷却截止的较晚者，不能靠换枪刷冷却。
- items：kind 固定1收缩/2延长/3护盾/4冲刺/5炸弹/6弹性强化/7血包。weight 是问号盒抽中权重，enabled 是是否启用，duration 是临时效果秒数。
- 问号盒 kind=8。状态立即执行；冲刺和炸弹进入一个槽，后来者覆盖，E消费，死亡清空。
- ropeLength、ropeSpring、ropeDamping、ropeMaxForce；shortenScale=.65、extendScale=1.5、elasticScale=3。重复同类刷新截止时间，效果结束后用基础值重新计算，双方弹性效果共用截止时间。
- moveSpeed、dashSpeed、dashSeconds、bombFuse=3、bombDamage=2、bombRadius。
- reviveSeconds=5、protectionSeconds=2、roundSeconds=180、baseHealth=3。

## 地图

maps 每条：id、name、available、halfWidth、halfHeight、blueSpawns、redSpawns、supplies、revivals、blocks。
坐标单位为 Unity 世界单位，俯视平面 XY，Z仅绘图排序。出生数组必须各有4个位置，分别对应队内座位0–3。
blocks 用 center(x,y)、size(x,y) 定义轴对齐矩形。绳索可见性图、角色与子弹碰撞都由这些数据生成；不要仅修改美术位置而不修改碰撞数据。
layoutVersion=0是旧supplies/revivals兼容图；当前Field_01及编辑器新图使用layoutVersion=1的pickups/floors/revivalZones。每个拾取点明确武器或道具、刷新秒数和是否随机；复活区按明确center/size矩形判定，不依靠supplies数组顺序推断。
只将可玩的地图设available=true。当前本地入口默认Field_01，任意地图选择/直接测试属于升级计划的后续任务，不能把旧云端地图按钮当成本地已经支持。

当前Field_01为草土大图，碰撞仍为轴对齐矩形；theme/visual控制场景风格与障碍外观。设计同学继续负责地图和武器道具数值，程序/美术负责挂点、动画与反馈，不直接覆盖同伴调参。绘制入口：
Scripts/Gameplay/ArenaView.cs（Draw2D/ArenaView）
Scripts/Gameplay/NetPlayer.cs、CyberPlayerView.cs（人物、名字与武器姿态）
Scripts/Gameplay/BattleWorld.cs（拾取物、弹丸、爆炸）

## 必须保留的规则

八外观可重复选且无属性差异；默认2v2最多4v4；同队相邻座位连成链，不是网状；尸体仍在链里；无默认友伤；计时平局比较存活人数再比较存活成员血量（不含盾）。保留原消灭/计时玩法，不做搜打撤。训练增加的队友无自主AI，正式空位AI补齐。
所有玩法数值由服务器判定；不要在客户端加“扣血”“获取道具”等直接改状态的逻辑。
添加全新武器攻击形式或新道具效果需要程序增加对应枚举与服务器分支；修改现有数值、名称和地图布局无需改网络代码。

## 联调时优先反馈

绳索在哪个柱角换边/抖动、冲刺时最大拉伸、复活点的进退判定、空手到补给的耗时、不同血量的存活时间。
记录地图ID、队伍座位、操作、预期、实际，附两端截图/日志即可。当前真实性验证范围见 STATUS.md。
