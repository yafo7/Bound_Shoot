# 四个 exe 联机验收与云端运行

2026-10-27。本轮按用户最新“四个 exe 开局并结算”的要求准备可执行程序，不制作压缩包。本文替代早期 C2S/SSH 隧道步骤。玩家无需 Bitvise，开发者仅用它上传云端文件。

## 玩家操作

1. 打开 Windows 文件夹中的 BoundShoot.exe，直接显示登录页。
2. 四个窗口分别注册或登录四个不同账号。用户名 3–24 字符，密码 8–72 字节。
3. 一人创建房间，其余三人输入房间号加入；默认 2v2，可选择外观和空座位。
4. 四人点击准备，房主点击开始对战。客户端自动领取连接票据、进入云端对战，不填写 IP、不点击连接服务器。
5. 团灭或 180 秒结束后显示结算，战绩由服务器保存；返回原房间可以继续准备下一局。

同一份 exe 可以运行四次，也可以将整个 Windows 文件夹复制到四台电脑。必须保留 exe 同目录中的 Data、MonoBleedingEdge、UnityPlayer.dll 等依赖。每个窗口使用独立内存会话；关闭后重新登录，不共用 Windows PlayerPrefs 中的账号。

同机测试也可以双击 Windows/OpenFourClients.cmd，一次打开四个正常客户端窗口；该脚本不设置服务器地址、不自动填写账号。

## 本轮交付目录

仓库根目录为 `C:/Users/yafo777/Desktop/2026Autumn/IMDT/game_develop/Bound_Shoot`。

| 路径 | 用途 |
|---|---|
| builds/Windows/BoundShoot.exe | 玩家客户端，默认公网地址已写入 |
| builds/LinuxServer/ | Linux 战斗程序原始输出 |
| builds/UploadToServer/ | 只需上传的内容：server、content、deployment |
| builds/QA/ | 独立自动化测试程序，不发给玩家、不上传云端 |
| logs/four-clients-*/ | 测试报告、客户端/服务端日志、界面截图 |

默认账号/房间地址为 `http://134.175.153.218:3000`，战斗地址由后端分配为 `134.175.153.218:7777–7778/UDP`。发布客户端不接受 `-backend` 参数；只有 Editor、Development 和独立 QA 版本支持本机调试覆盖。游戏界面没有服务器地址输入或手动连接步骤。

## 你在云服务器上只做这些

公网 `/health` 已实际访问成功；已有 Node 服务不需要重装。保留远端 backend/.env、state 数据库以及现有账号。

用 SFTP 将 **builds/UploadToServer 里面的三个文件夹** 复制到 `/home/ubuntu/Bound_Shoot/`，合并目录：

~~~text
/home/ubuntu/Bound_Shoot/
  backend/                         已有账号/房间服务
  state/                           已有账号数据库，保留
  server/
    BoundShootServer.x86_64
    UnityPlayer.so
    BoundShootServer_Data/         完整目录，不能只复制 exe
  content/GameConfig.json
  deployment/activate-game-server.sh
~~~

然后在已登录的云服务器终端执行：

~~~bash
cd /home/ubuntu/Bound_Shoot
bash deployment/activate-game-server.sh
~~~

脚本会检查完整性和 Linux 依赖，设置执行权限，配置当前项目的战斗程序路径，重启现有 bound-shoot 用户服务。它保留账号密钥、数据库和其他服务。看到 health 返回 `ok:true` 后，直接用四个 Windows 客户端开局。

腾讯云安全组需允许 TCP3000 和 UDP7777–7778，已有规则保留。若 Ubuntu 的 UFW 已启用，也需要允许相同端口。开放端口本身不会产生对战进程：房主开始后，Node 自动启动一局对应的 Linux Unity 程序，结束后自动退出；不需要手动为每局运行命令。

如果开战失败，在远端执行以下命令并保留输出：

~~~bash
systemctl --user status bound-shoot.service --no-pager -l
journalctl --user -u bound-shoot.service -n 50 --no-pager
ls -lt runtime/*.log | head
~~~

runtime 中最新的日志记录 Unity 程序的启动、玩家接入和结算保存。没有游戏程序或不可执行时，房间会报“对战服务尚未就绪”，不会假装已开始。

## 这次改动

- 客户端直接显示注册/登录页，请求默认发往公网服务器；移除共享的记住登录，避免同机四个窗口继承同一账号。
- 修复注册成功再退出账号后仍显示注册表单的问题，退出后回到正常登录页。
- 明确允许多开并在后台保持网络更新；窗口失去焦点时发送零移动、不射击，避免后台角色继续响应键鼠。
- 保留自动房间同步、开战票据核销、服务器权威物理与命中、持久化结算、原房间再战。断线后的重新连接按钮只用于异常恢复，不是开局必需步骤。
- 增加显式 Windows/Linux 构建方法与手动激活脚本；没有修改参考课程项目，没有自动登录远端。
- 独立 QA 通过真实注册/登录/房间按钮驱动四个客户端，移动射击仍走正常 NGO 输入 RPC。自动化代码仅在 BOUND_SHOOT_QA 编译条件下存在，正式客户端不包含它。

## 验证与边界

具体结果见 [STATUS.md](STATUS.md) 及日志报告。公网 health 成功仅证明账号/房间服务可访问；远端 Unity 战斗尚需你上传后实际验收。本机测试不等同于腾讯云 UDP 防火墙、跨运营商网络和远端系统依赖均已验证。

当前公网 HTTP 使用测试账号。后续正式发布账号时再配置 HTTPS/WSS 入口。道具、枪械和地图仍由 GameConfig.json 配置，交给设计同学填写；不需要修改网络代码。

## 后续修改后的重新构建

关闭本项目 Unity Editor 后，在仓库 PowerShell 中执行：

~~~powershell
./tools/build-game.ps1 -Target Windows
./tools/build-game.ps1 -Target LinuxServer
~~~

这是显式构建操作，不会自动压缩或部署。也可在 Unity 的 Bound Shoot → Build 菜单中分别构建；菜单默认输出到工程 Builds 文件夹，脚本统一输出到仓库 builds 文件夹。
