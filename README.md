# 绳紧兵大作战 / Bound Shoot

蔬果主题的 2D 团队射击游戏。队友由绳索相连，通过协作移动、拾取武器与道具争夺热点。支持训练、本地 AI、局域网和公网联机，队伍规模为 2v2、3v3、4v4。

本次 main 交付整理于 2026-11-10，项目版本为 0.3.0。功能变更、验证范围和已知限制见[本次交付说明](docs/RELEASE_NOTES_2026-11-10.md)。较早策划和实施文档中的服务器地址、生命规则、复活点和构建限制属于历史记录，当前运行方式以本文及最新专题说明为准。

## 运行项目

使用 Unity 2022.3.62f3，在 Unity Hub 中打开仓库的 unity-client 目录。打开 Assets/BoundShoot/Scenes/Boot.unity，点击 Play，输入名字并选择角色。

- 训练房提供全部武器和道具。
- 本地 AI 对战支持选择地图、调整人数并补齐 AI。
- 局域网联机由一名玩家创建房间，其他玩家填写房主的局域网 IPv4 地址加入，默认 UDP 端口为 7779。同机测试使用 127.0.0.1，跨机连接需允许房主客户端接收入站 UDP。只有房主可以选图、调整人数、补齐 AI 和开始对战，房主退出会中止房间。
- 公网联机进入大厅后创建或加入房间，准备后进入热身，由房主开始正式对战。

WASD／方向键移动，鼠标瞄准，左键攻击或使用所选道具，E 切换武器／道具，长按 F 救助倒地队友，按住 Tab 查看战绩。

## 当前玩法与配置

双方争夺热点，各队在所有热点的平均热度达到目标值获胜。保留开场倒计时，对局没有限时。角色生命上限固定，换枪不改变生命。枪械弹药耗尽后恢复默认小刀，另有三个道具位。

Unity 菜单 Bound Shoot → 数值编辑器提供生命、武器、弹簧、提速、投掷物等配置。Bound Shoot → Level Editor 关卡编辑器用于地图和物资点。停止 Play 后保存，重新 Play 生效。当前配置启用统一团灭复活等待 20 秒，可在数值编辑器修改，关闭覆盖后沿用各地图配置。

配置主文件为 unity-client/Assets/BoundShoot/Resources/GameConfig.json。公网玩法或共享配置修改后，需要使用匹配配置更新客户端和 Linux 战斗服。

## 联机服务

使用 NGO + Unity 独立权威战斗服务器运行公网对局，Node.js 24+ 负责身份、房间和战绩，SQLite 保存数据。局域网模式由玩家客户端担任主机。

当前配置的公网入口为 http://123.207.238.56:3000，战斗端口为 UDP 7777–7778。玩家运行客户端即可连接。通过 Unity 菜单 Bound Shoot → Network Settings 修改入口。公开正式分发的 HTTPS 配置尚待完成。

部署操作见[大厅与部署说明](docs/ONLINE_LOBBY_DEPLOYMENT.md)、[部署及房间清理记录](docs/SERVER_UPDATE_2026-11-04.md)。源码中的配置样例为 deployment/production.env.example，部署前应按目标环境填写独立的 .env。

## 构建

在仓库根目录使用 PowerShell 执行对应命令。构建目标分别需要 Windows、Linux Dedicated Server 和 Mac Build Support (Mono) 模块。

```powershell
./tools/build-game.ps1 -Target Windows
./tools/build-game.ps1 -Target LinuxServer
./tools/build-game.ps1 -Target MacOS
```

输出分别为 builds/Windows/、builds/LinuxServer/、builds/MacOS/。Linux 构建另准备 builds/UploadToServer/ 上传目录。脚本支持 -Unity 指定编辑器路径，默认 D:/UnityEditor/2022.3.62f3/Editor/Unity.exe。客户端交付需包含完整目录。

Mac 目标为 Intel + Apple silicon 通用应用，执行权限、分发和验证状态见[Mac 构建说明](docs/MACOS_BUILD.md)。构建产物和本地日志位于 Git 忽略目录，新 clone 需要自行构建。

## 源码检查

```powershell
node tools/validate-content.mjs
cd backend
npm ci
npm run build
npm test
```

Unity EditMode／PlayMode 测试位于 Assets/BoundShoot/Tests/。历史验证结果见各专题文档，新增修改需按影响范围重新验证。

## 设计和专题说明

- [完整游戏策划案](docs/GAME_DESIGN_DOCUMENT_2026-11-01.md)
- [热点、物资与团灭复活](docs/CORE_GAMEPLAY_UPDATE_2026-11-04.md)
- [击杀提示、KDA 和 MVP](docs/MATCH_STATS_AND_MVP.md)
- [队伍颜色、救助与提速](docs/TEAM_KILL_RESCUE_SPEED_BOOST.md)
- [公网延迟排查与已知限制](docs/ONLINE_NETWORK_DIAGNOSTICS.md)
- [三道具背包](docs/ITEM_INVENTORY_E_CYCLE.md)
- [地图选择](docs/OBJECTIVE_MAPS_AND_SELECTION.md)
- [战斗 UI](docs/BATTLE_HUD_REFERENCE.md)
