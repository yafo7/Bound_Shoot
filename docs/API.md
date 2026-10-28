# 后端接口 v0.2：名字直进公共2v2房间

2026-10-28更新。默认入口为 `POST /api/auth/quick`，请求 `{"name":"yafo777"}`，返回 `accessToken,refreshToken,user,room`。名字1–24个汉字、字母、数字或下划线；忽略首尾空格，ASCII大小写视为同名，AI_前缀保留给机器人。无密码，不需要先创建账号。每次成功进入都会撤销这个调试ID的旧会话；名字仍在线时返回409 NAME_IN_USE。退出或等待离线超时后可再次使用同名，保留该调试ID的战绩。

仅有一个公共房间 `bound-shoot-quick-2v2`，固定2v2。进入自动分配真人空位，机器人补齐到4席，成员字段新增 `isBot:0/1`。机器人不占真人容量；1–4个真人对应3–0个机器人。新玩家只在等待阶段进入；对局中返回409 ROOM_BUSY；四个真人时第五人返回409 ROOM_FULL。进入失败事务回滚，不留下多余ID或会话。

客户端不再调用注册/密码登录、建房、房号加入、准备或切换人数。默认这些旧注册/登录/建房入口返回410；历史账号数据保留，名字入口使用独立ID映射，不能直接接管历史密码账号。原有账号/多房间代码只供内部回归测试，部署默认不开放。这个名字入口用于本项目调试，不提供名字所有权验证。

公共房间内任意真人可调用 `POST /api/rooms/:id/start`，不要求额外准备。地图配置仍只有房主可改且人数必须为2；进入、座位、外观、开局、结算仍由服务器决定。对局名单固化当时的isBot，AI由Unity服务器控制，不领取客户端票据。结果必须含四个唯一参与者，原有结算幂等与战绩保存规则保留。

数据库自动执行001和新增002迁移，给match_players增加is_bot并建立quick_identities，不删历史账号和数据库。`GET /health`现在返回version=0.2.0、entry=name-only、mode=2v2。

以下为保留的底层接口参考；与上述最新公共房间规则冲突时以上述为准。

Node.js 24，Express 5，SQLite（node:sqlite）。启动位置必须为 backend/，或设置 GAME_CONFIG_PATH 指向共享 GameConfig.json。数据库启动时自动执行 migrations/001_initial.sql，启用 WAL、外键和事务。

所有响应：成功 {"ok":true,"data":...}；失败 {"ok":false,"error":{"code":"...","message":"中文提示"}}。
业务认证：Authorization: Bearer <accessToken>。Access Token 是 HMAC-SHA256 签名的短期凭证（15 分钟），不是第三方账号 Token。
密码 bcrypt cost 12；refresh token 30 天，仅 SHA256 摘要入库，每次刷新撤销旧 session 并创建新 session。退出撤销 session。勿记录凭证。

| 方法 / 路径 | 请求体 / 返回 data |
|---|---|
| GET /health | service, version，无需登录 |
| POST /api/auth/register | username,password → accessToken,refreshToken,user |
| POST /api/auth/login | username,password → 同上 |
| POST /api/auth/refresh | refreshToken → 同上；旧刷新凭证立即失效 |
| POST /api/auth/logout | {} → {}；需要 Access Token |
| GET /api/users/me | 用户资料与 roomId |
| PATCH /api/users/me/cosmetic | skin: 0–3 → 用户资料 |
| GET /api/users/me/matches | matches: 最近20场 |
| GET /api/catalog | 可用 maps 与 modes |
| POST /api/rooms | {} → 房间 |
| POST /api/rooms/join | code:6位字符串 → 房间 |
| GET /api/rooms/:id | 房间；仅成员 |
| POST /api/rooms/:id/leave | {}；仅等待状态 |
| PUT /api/rooms/:id/seat | team:0/1,seat:0–3 → 房间 |
| PUT /api/rooms/:id/ready | ready:bool → 房间 |
| PUT /api/rooms/:id/config | size:2/3/4,mapId → 房间；仅房主 |
| POST /api/rooms/:id/start | {} → Match；仅房主、满员且全部准备 |
| GET /api/matches/:id | Match；仅本局成员 |
| POST /api/matches/:id/ticket | {} → matchId,ticket,host,port |
| GET /api/matches/:id/result | status,mapId,result；仅本局成员 |

房间字段：id,code,hostId,size,mapId,status,matchId,members[]。成员：userId,username,skin,team,seat,ready(0/1)。
Match：id,roomId,mapId,status,host,port,players[]。状态 Starting → Ready → Playing → Finished，异常 Aborted。
结算：winner(-1平局/0蓝/1红),duration,players[{userId,kills,deaths,damage}]。

WebSocket：/ws。连接后5秒内发送 {"type":"auth","accessToken":"..."}；不要把 Token 放 URL。
服务器推送 {"event":"room.changed 等","room":{...}}，支持 member.joined/member.left/seat.changed/ready.changed/config.changed/cosmetic.changed/match.starting/match.ready/match.finished/match.aborted。
客户端也有1秒 HTTP 房间轮询作为断线回退。HTTP 请求或有效 WebSocket 维持房间在线状态；等待房间失联约20秒后释放座位并转移房主。

内部接口仅接收每局独有的 X-Match-Secret，客户端业务凭证不能调用：

| POST /internal/matches/:id/... | 行为 |
|---|---|
| register | 返回本局只读 roster,mapId,port |
| ready | 确认监听完成，通知客户端 |
| heartbeat | {playing:bool}，每5秒；20秒无心跳中止 |
| consume-ticket | {ticket} → 权威 userId,team,seat,skin,username；一次使用 |
| result | 权威结算；完整名单校验，事务累计战绩，重复提交无重复统计 |
| abort | 中止，不计胜负 |

启动超时90秒；手动模式从 runtime/<matchId>.json 读取启动配置。该文件含服务间密钥，属于临时私密运行数据，不提交 Git。托管模式启动同一机器的独立游戏进程，端口从 GAME_PORT_START 分配；最多 MAX_MATCHES 个。启动、端口占用、心跳异常均释放对局状态。
端口池固定为 GAME_PORT_START 到 GAME_PORT_START+MAX_MATCHES-1。托管进程完全退出后才复用其端口；手动模式结算后保留10秒释放冷却。池中端口尚未释放时明确返回 SERVER_BUSY。
游戏服务器在连接批准时在线核销票据，不接受客户端声明的用户或队伍。正在连接/已连接的同账号被服务器拒绝；断线后可获取新票据接管原实体，旧客户端失去所有权。
后端重启会将旧的未结算对局标为 Aborted，重置房间，不写入正常胜负。

常见错误：400 INVALID_INPUT；401 LOGIN_FAILED/TOKEN_EXPIRED/SESSION_REVOKED；403 HOST_ONLY/NOT_MEMBER/TICKET_INVALID；404 ROOM_MISSING/MATCH_MISSING；409 SEAT_TAKEN/NOT_READY/ROOM_BUSY；503 SERVER_BUSY/SERVER_START_FAILED。
