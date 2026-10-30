# 派对射击 / Bound Shoot

Unity 2022.3.62f3c1 的 2D 绳索射击项目。使用 NGO + Unity 独立权威战斗服务器，Node.js 负责账号、房间和战绩。

当前源码版（2026-10-30）：**蔬果主题本地Demo，产品提升首批M0/M1已接入**。修复手动房间等待超时，加入连续弹丸、准确开火归属、统一枪口与近战表现、绳索张力/拐点反馈。保留八角色、2v2/4v4、AI补位、踩枪换枪和结算再战。Unity菜单 **Bound Shoot → Play Vegetable Demo**。详见[首批实施说明](docs/PRODUCT_UPGRADE_M0_M1.md)和[基础运行说明](docs/VEGETABLE_DEMO.md)。没有生成新exe或更新云端，旧构建不代表当前源码。

## 先阅读

**[蔬果本地Demo说明](docs/VEGETABLE_DEMO.md)**：日常测试直接在Unity本地运行，不需要Node、Bitvise或部署云端。云端旧版暂保留，[此前的公网调试说明](docs/QUICK_PLAY.md)仅用于历史版本，不作为本轮部署指令。

最新方向：[产品提升策划v2](docs/PRODUCT_UPGRADE_PLAN_2026-10-30.md)及[执行任务状态](docs/PRODUCT_UPGRADE_BACKLOG_2026-10-30.csv)，保留[蔬果玩法策划v1.1](docs/UPGRADE_PLAN_VEGETABLES.md)；此前[美术讨论](docs/ART_STYLE_OPTIONS.md)保留存档。

关卡设计入口：**Unity → Bound Shoot → Level Editor 关卡编辑器**，操作见 [关卡编辑器说明](docs/LEVEL_EDITOR.md)。可视化摆放地面、障碍、武器、道具、出生点和复活区，直接保存游戏地图配置。

- [设计同学交接](docs/DESIGNER_HANDOFF.md)
- [接口说明](docs/API.md)
- [验证状态与限制](docs/STATUS.md)

## 本机位置

- Unity 工程：D:/workshop/developer_learn/Unity_Hub/Project/Bound_Shoot
- 后端、文档、Git：C:/Users/yafo777/Desktop/2026Autumn/IMDT/game_develop/Bound_Shoot
- unity-client 为指向 Unity 工程的 NTFS 目录联接；其他同学正常 clone 后直接打开 unity-client 即可。
- 参考课程项目 unity_skynet_adv 不作修改，参考其连接生命周期、输入序号和快照插值思想。

## Unity Editor

打开 Assets/BoundShoot/Scenes/Boot.unity，点击 Play。需要准备资源时使用菜单 Bound Shoot → Prepare Project。

菜单 Bound Shoot → Network Settings 可选择服务器配置；默认 Backend URL 为 http://134.175.153.218:3000，用于公网联调。玩家直接连接云端，不需要Bitvise或SSH；正式账号入口配置HTTPS后修改此处。登录界面不显示技术配置。

## 源码检查

在 backend 目录运行 npm ci、npm run build、npm test；这里的 build 仅编译 TypeScript 后端。
在仓库根目录运行 node tools/validate-content.mjs。
关闭本项目 Editor 后，可运行 tools/check-editor.ps1 检查 C# 编译与 EditMode 测试；不会生成 Unity 客户端、游戏服务器或压缩包。

远端服务器 134.175.153.218 由用户通过 Bitvise 手动操作，本轮未登录、修改或部署远端。
