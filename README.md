# 派对射击 / Bound Shoot

Unity 2022.3.62f3c1 的 2D 绳索射击项目。使用 NGO + Unity 独立权威战斗服务器，Node.js 负责账号、房间和战绩。

当前仅交付源码，由用户在 Unity Editor 中测试；不生成客户端、服务端构建或压缩包。游戏内只保留必要操作与状态，工作说明统一放在 Markdown 文档中。

## 先阅读

**[本轮工作与公网部署步骤](docs/WORKLOG.md)**：最新公网方案、远端目录、上传内容、端口开放、Unity 配置和待用户执行的步骤。

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
