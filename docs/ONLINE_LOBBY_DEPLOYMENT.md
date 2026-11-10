# 联机大厅与服务器部署（2026-11-03）

本轮用户明确授权操作 Bitvise 完成部署，并更正目标服务器为 `123.207.238.56`、SSH/SFTP 22、用户 `ubuntu`。仍沿用 NGO 权威战斗服务器＋Node.js 大厅。该服务器此前只有课程服务，本次为 Bound Shoot 的首次安装；此前记录的 `134.175.153.218` 没有被修改，没有提交或推送 Git。

## 当前部署状态

- 服务器目录：`/home/ubuntu/Bound_Shoot`。完整上传 160 个文件并在远端逐一通过 SHA-256 校验；没有上传本机 `.env`、测试数据库或连接凭据。
- 项目使用独立 Node.js `v24.22.2`，位于 `tools/node-v24.22.2-linux-x64`，`tools/node` 为项目内链接。来自 Node.js 官方发布目录并校验官方 SHA-256，不替换系统 Node。
- 新建 `bound-shoot.service` 用户服务，已启用开机启动和 `Linger=yes`，退出 SSH 后继续运行。服务器内部 `/health` 返回 `0.3.0 / name-only / lobby`。
- Linux 战斗服务器已实际启动、向后端注册、绑定 UDP 7777，并加载 4 个席位（1 个诊断访客、3 个 AI）。诊断没有连接玩家客户端，按正常无玩家超时规则结束；这不是公网对局验收。
- 原有 Skynet `18888`、Redis `127.0.0.1:6380` 未更改。主机 UFW 未启用，未修改主机防火墙规则。
- Windows 客户端已重新构建，默认大厅地址为 `http://123.207.238.56:3000`。交付完整 `builds/Windows` 目录。
- **公网部署验收通过。** 用户已手动完成腾讯云端口放行，公网 `/health` 返回 `0.3.0 / lobby`。所需规则为入站允许来源 `0.0.0.0/0`，TCP `3000`，UDP `7777–7778`。
- 两个独立 Windows QA 客户端通过公网连接本机之外的 Linux 权威服务器，在「三路粮仓」完成两轮 2v2（2 客户端＋2 AI，每局 180 秒）。建房、房间号加入、地图同步、4 个席位及各自控制权、真实 UDP 连接、AI 活动、结算、返回房间再战、每人战绩恰好增加两局、返回大厅和退出会话全部通过。
- 验收结果：`docs/validation/online-lobby/public-two-clients.json`；截图：`public-room.png`、`public-battle.png`、`public-result.png`（同目录）。两个客户端位于同一台电脑；不同电脑／不同网络之间尚未单独验收。
- 此前电脑控制工具因无法可靠确认 Windows 浏览器网址而结束操作；该阻塞已由用户手动配置云防火墙解决。临时 Bitvise 连接配置已删除。

日常检查可直接执行：

```bash
bash /home/ubuntu/Bound_Shoot/deployment/check.sh
```

该脚本和安装／激活脚本会自动使用项目自带 Node，不要求修改用户全局 PATH。首次安装在远端生成新的随机服务密钥，未读取或输出该密钥。

本次公网验收的复现命令（会创建诊断账号和真实对局）：

```powershell
node tools/qa-public-lobby.mjs --base=http://123.207.238.56:3000
```

该命令使用已有独立 QA 构建，创建两个诊断昵称并通过正常 API 进行两轮 2v2（2 客户端＋2 AI），验证建房、加入、地图同步、真实 NGO/UDP、结算、再战、战绩恰好增加两局、返回大厅和主菜单。它不会启动本机后端或改变云端配置。通过后仍应让另一台电脑／另一网络进行一次互通检查。

## 玩家流程

标题 → 输入名字、八蔬果选角 → 联机对战 → 大厅 → 创建房间／房间列表加入／六位房间号加入 → 等待房 → 对战 → 结算 → 返回房间或大厅。

- 大厅每页 6 个房间，每 5 秒刷新，显示房主、房间号、地图、队伍人数、真人数量和状态。
- 默认 2v2；房主可改为 3v3／4v4，以及选择所有已开放地图。默认新房间为「果园交叉口」。
- 空位由服务器 AI 自动补齐；真人加入时优先交替安排到两队，可点击 AI 座位换位。
- 仅房主可修改人数、地图和开始对战。调整人数时若有人处于被删除的座位，需要先换位。
- 房主在等待阶段离开后自动交接；最后一名真人离开后删除房间。等待阶段可返回大厅或主菜单。
- 开局后锁定房间，新玩家不能中途加入；原有成员保留票据重连入口。结算后留在原房间，可再次开局。
- 同时最多两场战斗，沿用 UDP 7777–7778。房间数量与同时开战数量不同；资源已占满时显示服务器繁忙。
- 仍是昵称访客模式，战绩按昵称保存。同一昵称有活跃会话时不允许重复登录；超时后可重新进入。旧数据库保留，新增第 4 版迁移记录会话活跃时间。
- 训练、本地 AI、局域网入口保留。公网玩家只需要 Windows 客户端，不需要 Bitvise。

## 接口与文件

`GET /health` 返回版本 `0.3.0`、`mode: lobby`。`POST /api/auth/guest` 接受 `name`、`skin`，登录不再自动进入公共房间。登录后 `GET /api/rooms?page=0` 浏览大厅，其余创建、加入、换位、配置、启动接口延用原房间 API。旧 `/api/auth/quick` 返回 426，提示更新客户端。

房间通过 WebSocket 即时同步，HTTP 轮询兜底。`LOBBY_MODE=true` 是默认设置；`false` 仅用于历史快速房间兼容测试，不用于本次新客户端。

- 客户端大厅：`unity-client/Assets/BoundShoot/Scripts/UI/OnlineLobby.cs`
- 后端：`backend/src/service.ts`、`rooms.ts`、`quickplay.ts`
- 数据库迁移：`backend/migrations/004_lobby_presence.sql`
- 构建：`tools/build-game.ps1`
- 更新激活：`deployment/activate-game-server.sh`

构建会在输出目录写入 `release.json`，记录版本、Unity 版本及 GameConfig SHA-256。激活脚本核对服务属于当前项目，以及服务器构建与地图配置一致。不能只更新 Node.js 后端而继续运行旧战斗服务器。

本轮公网验收额外发现并修复了房间操作时序问题：在默认 2v2 房间再次点击「2v2」，会发出不必要的配置请求，紧接着选择地图的请求被客户端的忙碌保护忽略。现在重复选择当前人数不发请求，房间请求开始／结束时立即刷新按钮状态，选图入口和确认也检查请求状态。QA 在人数修改后等待选图按钮可用，继续通过正常界面操作；正式 Windows 客户端已重新构建，修复不改变网络协议或服务端对战规则。

## 构建

Unity 版本：2022.3.62f3。Windows 客户端与 Linux Dedicated Server Build Support 必须对应同一版本。官方安装说明：[添加模块](https://docs.unity.com/en-us/hub/add-modules)。

在项目根目录执行：

```powershell
.\tools\build-game.ps1 -Target Windows
.\tools\build-game.ps1 -Target LinuxServer
```

若 Unity 正在打开源工程，可将已同步的隔离工程传入 `-ProjectPath`。本轮使用 `logs/knockback/project`，避免占用用户工程。修改源码后必须同步后再构建。

完整 Linux 构建成功后，才会准备 `builds/UploadToServer`，包含 `backend`、`server`、`content`、`deployment` 四个目录。不生成压缩包，不包含 `.env` 或玩家数据库。客户端交付整个 `builds/Windows` 文件夹，不能只复制 exe。

## Bitvise 更新旧服务器

更新会中断进行中的对局，应先让玩家退出。确认 `builds/UploadToServer/server/release.json` 存在且版本为 `0.3.0` 后再操作。

1. Bitvise 连接 `123.207.238.56:22`，用户 `ubuntu`。以下流程用于本次安装完成后的版本升级。
2. 先在终端确认当前服务工作目录：

```bash
systemctl --user show bound-shoot.service --property=WorkingDirectory --value
```

必须为 `/home/ubuntu/Bound_Shoot/backend`。如果不同，先核对真实项目目录，不覆盖其他服务。

3. 停止本项目服务并备份旧程序、配置和数据库：

```bash
cd /home/ubuntu/Bound_Shoot
systemctl --user stop bound-shoot.service
backup_dir="$HOME/Bound_Shoot_backup_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$backup_dir"
cp -a backend server content deployment state "$backup_dir/"
printf 'Backup: %s\n' "$backup_dir"
```

停止后再复制 `state`，确保 SQLite 主文件和 WAL 日志一致。记住输出的备份目录。

4. SFTP 将本机 `builds/UploadToServer` **里面的四个文件夹**合并上传到 `/home/ubuntu/Bound_Shoot/`。允许覆盖这四个目录中的同名代码文件；保留服务器原有 `backend/.env`、`state/` 和 `runtime/`，不上传本地测试数据库。

5. 在 Bitvise 终端执行：

```bash
cd /home/ubuntu/Bound_Shoot
bash deployment/activate-game-server.sh
bash deployment/check.sh
```

激活脚本安装锁定依赖、编译 TypeScript、保留密钥并补齐本次配置，然后只重启 `bound-shoot.service`。它不会开放防火墙或修改其他项目。

6. 健康接口应显示 `version: 0.3.0`、`mode: lobby`。安全组与系统防火墙继续放行 TCP 3000 和 UDP 7777–7778。
7. 用新版 Windows 客户端测试：创建房间 → 列表／房间号加入 → 同步地图与人数 → 开始 → 两边都进入同一局 → 结算 → 返回大厅。实际连接公网战斗服务才能验收 UDP，健康接口本身不足以证明对局可用；另用两台电脑／不同网络测试可补充覆盖玩家网络差异。

如果更新失败：保持服务停止，把刚才备份的 `backend`、`server`、`content`、`deployment`、`state` 整套恢复到原项目目录，再启动原服务。不要仅恢复旧 exe 搭配新配置。

## 验证记录与当前状态

- 2026-11-03较早阶段只读访问旧地址 `134.175.153.218` 的 `/health`：在线，`0.2.0 / name-only / 2v2`。后续用户更正部署目标，旧地址未被修改。
- 后端 TypeScript 编译通过；6 项集成测试通过，覆盖 HTTP/WebSocket、不同房间、4v4 AI 名单、房主权限和转移、容量与分页、票据、结算、断线与重启恢复，兼容测试也通过。
- Unity EditMode 76 项通过；PlayMode 37 项均已有通过记录。整轮 36 项通过，8 连接测试在与四进程并发运行时遇到发送队列溢出；停止并行压力后单独复测通过，没有放宽断言或屏蔽日志。
- 四个独立 Windows QA 客户端、独立后端和权威服务器，4v4（4 真人＋4 AI）在「三路粮仓」连续两局完成；验证建房、房间号加入、选图和人数同步、开局、结果、每人战绩恰好增加两局、返回房间再次开局、返回大厅及退出会话。结果见 `docs/validation/online-lobby/four-clients.json`。
- 修复了独立服务端中运行时 `MapOverride` 被 Unity 序列化为空地图的问题，新增实际专用服务器名单初始化测试。训练和局域网依然使用各自的运行时地图。
- 原测试对狙击伤害 8、友伤关闭的固定假设已调整为读取当前伤害或显式隔离测试所需的友伤条件，测试后恢复。玩家当前的 100 生命、狙击伤害 15、友伤开启等配置没有被回退。
- 用户已安装对应 Linux 服务端模块；正式 Windows 与 Linux 服务端构建成功。版本和 GameConfig 摘要一致，上传目录没有 `.env`／数据库，正式客户端不包含 QA 自动操作代码。详见 `docs/validation/online-lobby/builds.json`。
- 大厅列表在本机临时后端中显示 7 个房间、每页 6 个；已通过列表加入 3v3 房间并返回大厅，非房主的地图／人数／开始按钮不可用。实际截图：`docs/validation/online-lobby/lobby-list.png`、`joined-room.png`。部署脚本通过 Bash 语法检查。
- 本轮新增 Linux 远端启动检查和真实公网双客户端两局验收，均通过。不同电脑／不同网络之间尚未单独验收。公网初测复现了默认 2v2 重复点击后选图请求被忽略的问题；修复客户端后重新执行整个公网流程通过，未降低断言或修改服务器比赛时长。

本机 QA 使用独立端口、数据库和随机测试昵称，不连接云端。执行入口：

```powershell
.\tools\build-game.ps1 -Target QA
node tools/qa-four-clients.mjs --humans=4 --size=4 --port=3193
```

QA 操作仅进入独立 QA 构建，正式 Windows 客户端不包含自动操作入口。
