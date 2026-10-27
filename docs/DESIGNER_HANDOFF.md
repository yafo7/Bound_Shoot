# 给枪械、道具、地图设计同学

当前任务是可迭代的基础逻辑和占位画面。参考图仍保留在 example/，没有拷贝商业游戏美术到客户端。

## 日常改动入口

Unity → Assets/BoundShoot/Resources/GameConfig.json。
客户端和专用服务器共用这份配置；后端也读取同一份配置中的可用地图列表。修改后需重新构建客户端与游戏服务器，后端重启。
同一场比赛的所有参与者应使用同一版本。发布时同时更新三端。

- weapons：name、maxHealth、damage、range、cooldown（秒）、bulletSpeed（单位/秒）。kind 固定：1狙击、2手枪、3AK、4匕首；0为空手。
- 正式初始值：狙击1血4伤；手枪3血1伤；AK3血2伤；匕首5血3伤。首次捡取直接填满对应生命。锁定直到死亡，不能替换。
- items：kind 固定1收缩/2延长/3护盾/4冲刺/5炸弹/6弹性强化/7血包。weight 是问号盒抽中权重，enabled 是是否启用，duration 是临时效果秒数。
- 问号盒 kind=8。状态立即执行；冲刺和炸弹进入一个槽，后来者覆盖，E消费，死亡清空。
- ropeLength、ropeSpring、ropeDamping、ropeMaxForce；shortenScale=.65、extendScale=1.5、elasticScale=3。重复同类刷新截止时间，效果结束后用基础值重新计算，双方弹性效果共用截止时间。
- moveSpeed、dashSpeed、dashSeconds、bombFuse=3、bombDamage=2、bombRadius。
- reviveSeconds=5、protectionSeconds=2、roundSeconds=180、baseHealth=3。

## 地图

maps 每条：id、name、available、halfWidth、halfHeight、blueSpawns、redSpawns、supplies、revivals、blocks。
坐标单位为 Unity 世界单位，俯视平面 XY，Z仅绘图排序。出生数组必须各有4个位置，分别对应队内座位0–3。
blocks 用 center(x,y)、size(x,y) 定义轴对齐矩形。绳索可见性图、角色与子弹碰撞都由这些数据生成；不要仅修改美术位置而不修改碰撞数据。
补给位置初始左右各4类武器和问号盒；刷新后随机武器/道具。复活点以中心±(2,1.5)矩形判定。
只将可玩的地图设 available=true。房间地图按钮会轮换已注册地图；未开放地图不会出现。

当前地图是参考图的结构占位版，使用基本几何体，不是最终视觉。角色、拾取物、障碍、绳子绘制入口：
Scripts/Gameplay/ArenaView.cs（Draw2D/ArenaView）
Scripts/Gameplay/NetPlayer.cs（人物与名字）
Scripts/Gameplay/BattleWorld.cs（拾取物、弹丸、爆炸）

## 必须保留的规则

外观无属性差异；同队相邻座位连成链，不是网状；尸体仍在链里；无默认友伤；计时平局比较存活人数再比较存活成员血量（不含盾）。
所有玩法数值由服务器判定；不要在客户端加“扣血”“获取道具”等直接改状态的逻辑。
添加全新武器攻击形式或新道具效果需要程序增加对应枚举与服务器分支；修改现有数值、名称和地图布局无需改网络代码。

## 联调时优先反馈

绳索在哪个柱角换边/抖动、冲刺时最大拉伸、复活点的进退判定、空手到补给的耗时、不同血量的存活时间。
记录地图ID、队伍座位、操作、预期、实际，附两端截图/日志即可。当前真实性验证范围见 STATUS.md。
