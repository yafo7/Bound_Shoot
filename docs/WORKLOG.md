# 本轮工作与手动上传步骤

日期：2026-10-27。最新要求：只改源码，用户在 Unity Editor 中测试，不生成游戏构建或压缩包；所有工作说明写在 Markdown 中。当前等待用户操作远端，不进行自动登录或部署。

## 一、已做的工作

1. 构建目录 builds/ 和 Windows 压缩包已从项目移除；移除了自定义 Windows/Linux 打包菜单、打包脚本、依赖 exe 的客户端启动/冒烟脚本及游戏内自动测试入口。没有重新生成这些文件。
2. 在仓库根目录与 D 盘 Unity 工程中加入 AGENTS.md，明确禁止在游戏内堆放说明、技术细节和开发标语。
3. 清理登录页宣传文案、外观属性解释、房间操作教程、地图介绍、底部 WASD 长文、版本号、服务器地址输入和内部状态名。界面保留登录/注册、选外观、房间、准备、战斗 HUD、结算及必要错误提示。
4. 保留 NGO + Unity 权威战斗服务器，Node.js/SQLite 管理账号、房间、票据与战绩。参考课程 unity_skynet_adv 的连接生命周期、输入序号、服务器时钟和快照缓冲思路；没有复制课程业务或修改课程项目。
5. 增加服务器时间戳快照缓冲。远端玩家使用服务器时间轴插值，旧/重复快照不覆盖新状态，缺包时只作有限外推；绳索显示端点跟随渲染后的角色，物理判定仍完全在服务器。
6. 输入增加 epoch，死亡、复活、连接切换时递增，防止旧生命/旧连接的输入被新状态接受。输入仍验证所有权、顺序、有效向量，并有超时停止。
7. WebSocket 增加连接代际隔离和重连，避免旧连接排队的房间消息污染新会话；HTTP 轮询保留作恢复途径。Access Token 并发过期时只执行一份刷新，失败返回登录。
8. 修正异步请求在退出/过期后回写旧房间或重新发起战斗连接的问题；断线按账号恢复原实体；服务端核销票据期间发生断线时丢弃批准；结算开始后不再让心跳错误打断正常结算。
9. 后端在缺少可执行战斗服务器时拒绝开战，不创建假的 Starting 对局。用户可先测试账号和房间；未上传 Unity Linux 服务端时不能进入战斗。
10. 增加 Bitvise 手动上传用的用户级 systemd 安装/检查脚本。脚本只在用户执行后生效；本轮没有操作服务器。

## 二、双方的操作分工

- 我负责：源码、Unity 网络配置、部署脚本、检查与操作文档。
- 你负责：用 Bitvise 登录、用 SFTP 上传文件、在远端运行命令、在 Unity Editor 中操作测试。
- 每一阶段执行后，把结果告诉我，再处理下一阶段。不要把“代码已准备”理解为“远端已经部署成功”。
- 后续需要真正远端战斗时，Unity Dedicated Server 必须有 Linux 可执行程序；仅将 C# 源文件复制到 Ubuntu 不能启动 Unity Physics2D。本轮不会为此构建。

## 三、当前服务器与端口

| 用途 | 配置 |
|---|---|
| Bitvise SSH/SFTP | 134.175.153.218，端口22，用户ubuntu |
| 独立项目目录 | /home/ubuntu/Bound_Shoot |
| Node账号/房间服务 | 远端127.0.0.1:3000，仅本机监听 |
| Bitvise C2S本地入口 | 本机127.0.0.1:13000 → 远端127.0.0.1:3000 |
| Unity游戏服务器 | 134.175.153.218，UDP7777–7778，每局独立端口 |

不改课程的 Skynet 目录、服务和8888端口。若3000已被其他服务占用，安装脚本会退出，先反馈给我再换端口；不停止已有服务。
前期账号请求通过SSH加密隧道，省去当前尚未准备的域名/TLS配置。后续给普通玩家公开访问时再配置HTTPS/WSS入口，届时修改同一处Unity配置即可，不要求玩家持有服务器SSH账号。

## 四、你现在只需执行这一步

在 Bitvise 保持截图中的 Host / Port / Username，自行输入原有密码并点击 Log in。不需要把密码发给我。

打开 New terminal console，执行：

~~~bash
whoami
uname -m
node --version
npm --version
~~~

把这四项输出告诉我。这一步仅确认账号、CPU架构和Node环境，不会改远端文件、端口或服务。
后端要求 Node.js 24或更新版本；缺少Node、版本过旧或机器不是x86_64时，先反馈，我们调整后再继续。不要先按旧README打包或运行课程服务器。

## 五、环境确认后再上传

先在远端终端创建独立目录：

~~~bash
mkdir -p /home/ubuntu/Bound_Shoot/{backend,deployment,content,server}
~~~

打开 Bitvise 的 New SFTP window，按下表逐项复制：

| 本机文件/目录 | 远端目标 |
|---|---|
| 仓库 backend/src 整个目录 | /home/ubuntu/Bound_Shoot/backend/src |
| 仓库 backend/migrations 整个目录 | /home/ubuntu/Bound_Shoot/backend/migrations |
| backend/package.json | /home/ubuntu/Bound_Shoot/backend/package.json |
| backend/package-lock.json | /home/ubuntu/Bound_Shoot/backend/package-lock.json |
| backend/tsconfig.json | /home/ubuntu/Bound_Shoot/backend/tsconfig.json |
| 仓库 deployment 整个目录 | /home/ubuntu/Bound_Shoot/deployment |
| D盘Unity工程 Assets/BoundShoot/Resources/GameConfig.json | /home/ubuntu/Bound_Shoot/content/GameConfig.json |

不要上传本机 node_modules、Library、Temp、logs、测试数据库、本机.env或整个Unity缓存。远端不需要Unity客户端工程来运行Node后端。
SFTP更新时不要选择“删除远端多余文件”，不要覆盖已存在的 backend/.env、state/、runtime/；数据库与密钥保留在服务器。
目录复制后确认没有形成 deployment/deployment 或 backend/src/src 两层重复目录。

上传完成后，在远端终端运行：

~~~bash
cd /home/ubuntu/Bound_Shoot
bash deployment/install-user-service.sh
sudo loginctl enable-linger ubuntu
bash deployment/check.sh
~~~

安装脚本：检查Node与文件，首次生成私密.env，安装依赖、编译TypeScript、安装并启动用户级服务。已有.env不覆盖；已有其他项目的同名服务不替换。
这里的 TypeScript 编译只生成后台的JS，不会生成Unity程序或压缩包。
sudo那一行只让该用户的服务在关闭Bitvise后继续运行，不开启或修改防火墙。

若检查显示 gameServerUploaded:false，表示账号/房间服务可用，但战斗尚不可用，这是本阶段的预期结果。

服务日常操作：

~~~bash
systemctl --user status bound-shoot.service --no-pager
journalctl --user -u bound-shoot.service -n 60 --no-pager
systemctl --user restart bound-shoot.service
systemctl --user stop bound-shoot.service
~~~

重启会中止仍在进行的对局，更新前先结束测试。

## 六、设置Bitvise C2S与Unity

Bitvise → C2S → Add，配置：

- Listen Interface：127.0.0.1
- Listen Port：13000
- Destination Host：127.0.0.1
- Destination Port：3000

保存后保持SSH连接；按Bitvise提示使这条转发生效。该转发是TCP，只用于账号HTTP与房间WebSocket，不承载Unity战斗UDP。

本机PowerShell可检查：

~~~powershell
Invoke-RestMethod http://127.0.0.1:13000/health
~~~

Unity打开 D:/workshop/developer_learn/Unity_Hub/Project/Bound_Shoot。
菜单 Bound Shoot → Network Settings，或选中 Assets/BoundShoot/Resources/NetworkSettings.asset：

| Inspector字段 | 当前值 |
|---|---|
| Backend Url | http://127.0.0.1:13000 |
| Connect Timeout Seconds | 20 |
| Transport Disconnect Ms | 5000 |
| Interpolation Ms | 100 |
| Extrapolation Ms | 100 |

这个本机地址是SSH隧道入口，不是让游戏使用本机假后端。账号数据实际存到远端SQLite。
打开 Assets/BoundShoot/Scenes/Boot.unity 并Play，可测试注册、登录、外观、建房和入房。
注册用户名3–24字符，密码至少8字符；这些规则写在文档中，游戏内用必要错误提示处理。
若要在不同电脑测试，每台开发电脑都要有自己的有效隧道；后续公开HTTPS入口配置好后不再需要隧道。

## 七、真正开战前的第二阶段

需要等你确认“现在可以准备Linux游戏服务端”后，再操作Unity的Linux Dedicated Server目标。本轮不构建。

未来上传的是完整服务端输出目录，结构应包含：

~~~text
/home/ubuntu/Bound_Shoot/server/
  BoundShootServer.x86_64
  BoundShootServer_Data/
  UnityPlayer.so
  以及该次Unity输出的其余依赖
~~~

不能只上传一个x86_64文件。客户端与服务器应使用同一份源码、网络协议和GameConfig。
服务端上传后，执行 chmod u+x server/BoundShootServer.x86_64；检查脚本会报告它是否存在。确认可运行后再允许客户端准备开战。

云安全组需要放行 UDP7777–7778；服务器若已有启用的UFW，也需放行这段UDP。不要因为教程而直接启用一个尚未启用的防火墙，避免影响现有SSH/课程服务。
SSH的TCP22保持原有配置。账号3000不需要对公网开放，13000只存在于你本机；没有必要开放TCP7777或把UDP转成SSH的TCP转发。

PUBLIC_GAME_HOST 已在远端.env模板中写为134.175.153.218。它是后端下发的战斗地址，不能填写127.0.0.1。该地址与Unity的账号隧道地址用途不同。

## 八、检查范围与未完成项

本轮仅做C#编辑器编译/EditMode、后端编译/集成测试、配置检查和Bash语法检查，不启动Windows客户端，不生成构建包。
快照缓冲有独立测试覆盖乱序、重复、插值、有限外推和重连清空；现有规则测试保留。
以前版本的四客户端两局记录仅是历史证据。本轮改了输入协议和连接处理，仍需你在部署后完成真实跨电脑测试。
远端的系统版本、Node环境、可用内存、端口占用、Linux游戏运行依赖与云安全组尚未确认。未登录远端，未声称这些条件已通过。
当前仍未实现完整本地移动预测与射击延迟补偿；复杂绕柱的持续缠绕拓扑也待优化。
