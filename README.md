# 派对射击 / Bound Shoot

Unity 2022.3.62f3c1 的 2D 绳索射击基础版。Windows 客户端与独立游戏服务器共用 Unity 工程；Node.js 24 + TypeScript + Express + ws + SQLite 提供账号、房间与战绩。优先交付基础逻辑和可修改配置，当前使用程序生成的占位几何图形。

**验证范围与待办请看 [docs/STATUS.md](docs/STATUS.md)。** 不把本机四进程测试当作跨电脑验收。

## 本机目录

- Unity 工程：D:\workshop\developer_learn\Unity_Hub\Project\Bound_Shoot
- Git/后端/文档：C:\Users\yafo777\Desktop\2026Autumn\IMDT\game_develop\Bound_Shoot
- 仓库中的 unity-client 是指向上述 Unity 工程的 NTFS 目录联接，Git 实际跟踪目录内文件。
- 其他同学正常 git clone 后，unity-client 是普通文件夹，直接用 Unity Hub 打开即可；不需要重建这台电脑的路径。
- example 是用户的本地参考图，保留原文件并忽略提交；未使用参考商业截图作为游戏美术。
- builds/、logs/、数据库、运行票据、.env、Library/ 均不提交 Git。

## 第一次启动（PowerShell，在仓库根目录）

已安装 Unity 2022.3、Node.js 24、Git。Unity 包固定为 NGO 1.8.1 / Transport 1.4.0，首次打开会下载依赖。
官方包注册表声明 NGO 1.8.1 最低 Unity 2021.3，并依赖 Transport 1.4.0；本机已以2022.3编译验证：
[包元数据](https://packages.unity.com/com.unity.netcode.gameobjects)。

1. 构建（如已有 builds/Windows，可跳过）：

~~~powershell
.\tools\build.ps1 Windows
~~~

2. 启动后端并让它自动拉起独立游戏服务器：

~~~powershell
.\tools\start-backend.ps1 -ServerPath "$PWD\builds\Windows\BoundShoot.exe"
~~~

脚本首次生成本地私密 .env；默认监听 TCP 3000、游戏 UDP 7777起，最多2场。保持终端运行。数据库自动迁移到 backend/data/。
同一个 Windows 可执行文件带 -batchmode -nographics -matchFile 参数时充当无界面的独立服务器，不是客户端房主。

3. 启动四个客户端：

~~~powershell
.\tools\start-clients.ps1 -Count 4
~~~

每个客户端注册不同账号（用户名3–24字符，密码至少8字符），选择外观。一人创建房间，把6位房间号发给其他人。进入四个座位（A1—A2、B1—B2），全员准备，房主开始。
服务器 Ready 后客户端自动获取一次性票据并连接，统一3秒倒计时。
WASD移动；鼠标瞄准；左键攻击；E使用携带道具。先经过补给拾武器，死亡前无法换枪。
整队全灭或180秒结束后结算；点击返回房间，重新准备即可再开一局。

## 在 Unity Editor 中运行

Unity Hub → Add → 选择 D盘 Bound_Shoot 文件夹。
首次如未生成启动场景，菜单 Bound Shoot → 1. Prepare Project；打开 Assets/BoundShoot/Scenes/Boot.unity，点击 Play。
客户端界面里的“后端地址”可修改为 http://服务器IP:3000（仅开发内网）或线上 https://域名。
后台必须已启动；Editor 本身不会模拟数据库或四人假联机。
菜单 Bound Shoot → 2. Build Windows Client + Local Server 可构建Windows。
命令行 tools/build.ps1 会把构建统一输出到本仓库 builds/。

## 手动服务器模式

不带 -ServerPath 启动后端，或设置 SERVER_MODE=manual。四人准备并开始后：

~~~powershell
.\tools\start-server.ps1
~~~

脚本读取最新 backend/runtime/<matchId>.json 并启动独立服务器。必须在90秒启动窗口内执行。
也可以显式 -MatchFile 完整路径，避免多房间选错。每场比赛必须独占自己的配置文件和端口。

## 两台或更多电脑

服务端机器启动：

~~~powershell
.\tools\start-backend.ps1 -ServerPath "$PWD\builds\Windows\BoundShoot.exe" -GameHost "你的局域网IP"
~~~

其他电脑复制整个 builds/Windows 文件夹（不能只复制 exe），启动后填写后端地址 http://服务端局域网IP:3000。
在服务器防火墙放行 TCP3000 和 UDP7777–7778。PUBLIC_GAME_HOST 必须是客户端能访问的地址，不能填127.0.0.1。
业务地址和战斗UDP地址分开配置：前者在客户端登录页，后者由后端返回票据时下发。
公网部署账号接口使用 HTTPS/WSS；UDP战斗端口还需云安全组开放。

## Linux Dedicated Server 与 Ubuntu 部署

本机尚缺 Linux Dedicated Server 构建模块，不能声称已生成Linux二进制。
在 Unity Hub 为相同版本安装 Linux Build Support / Dedicated Server（Mono），然后：

~~~powershell
.\tools\build.ps1 LinuxServer
~~~

或菜单 Bound Shoot → 3. Build Linux Dedicated Server。
上传构建目录到 /opt/bound-shoot/server/，上传源码到 /opt/bound-shoot/。
Ubuntu安装 Node.js 24；创建运行用户 boundshoot 与可写目录 /var/lib/bound-shoot/runtime；backend/ 执行 npm ci && npm run build。
复制 deployment/production.env.example 到 /etc/bound-shoot.env，填写随机 ACCESS_SECRET、公网IP、服务器程序路径；权限600，归运行用户读取。
给服务端程序可执行权限；部署 deployment/bound-shoot.service 到 systemd，加载并启用服务。
deployment/nginx.conf 是 HTTPS/WSS 反向代理模板，需要替换域名并安装证书。/internal/ 在公网代理层禁止访问，只允许本机游戏进程调用。
开放 TCP443 与 UDP7777–7778，不向公网开放内部API。
systemd 日志使用 journalctl -u bound-shoot -f；每局游戏日志写入 RUNTIME_DIR。
停止服务会停止同一 cgroup 的游戏进程；重启后端会安全中止旧局，不计正常战绩。
备份 SQLite 应使用一致性备份；不要只复制正在写入的 .sqlite 而忽略WAL。

## 验证与交接

~~~powershell
cd backend
npm ci
npm run build
npm test
cd ..
.\tools\build.ps1 Test
node tools/validate-content.mjs
node tools/smoke.mjs
~~~

smoke 创建隔离数据库、四个真实账号、四个独立客户端，每局自动启动独立游戏服务器，通过真实HTTP/UDP链路，使用相同输入RPC自动操作，连续完成两局。
日志与截图在 logs/smoke-*/；不调用客户端“直接扣血”接口。它不替代人工手感、网络延迟和跨电脑验证。

- [设计同学交接](docs/DESIGNER_HANDOFF.md)：修改武器、道具、地图的入口。
- [接口说明](docs/API.md)：账号、房间、票据、WebSocket、内部结算。
- [架构与限制](docs/ARCHITECTURE.md)。
- [实施清单](docs/PLAN.md) / [验证状态](docs/STATUS.md)。

Git远端：git@github.com:yafo7/Bound_Shoot.git。源码可正常提交，生成文件与凭证已忽略。
