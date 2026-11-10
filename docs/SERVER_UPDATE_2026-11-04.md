# 2026-11-04 联机服务器更新

## 房间清理

2026-11-04 按用户要求清理公网服务器当前全部房间。备份目录为 `/home/ubuntu/Bound_Shoot/backup-clear-rooms-XPTUa3tt`。已删除 3 个房间及其 7 条成员记录，结束 2 场进行中的对局并标记为 `Aborted`，清理 32 条连接票据。操作在服务停止期间以数据库事务完成，保留账号和历史战绩，外键检查通过。

服务恢复后公网健康检查通过。二次核对确认备份中的 3 个房间均已删除，此时玩家已经新建 1 个房间。临时 Bitvise 配置已删除。

本次发布目标为 `ubuntu@123.207.238.56:22` 上的 `/home/ubuntu/Bound_Shoot`，服务为 `bound-shoot.service`。更新内容包括房间准备和热身、热点胜负规则、击杀提示与 KDA、结算 MVP、救助和拖拽参数以及提速道具。

- Windows 客户端位于 `builds/Windows/BoundShoot.exe`。
- 服务器发布文件位于 `builds/UploadToServer/`，包括 Linux 战斗服、Node 后端及迁移、共享配置和运维脚本。
- 两端使用 Unity 2022.3.62f3，已完成构建。版本号为 `0.3.0`，游戏配置 SHA-256 均为 `5ed8b2f2b6356d52396a2d5661fdf8a8403edc61856cb4dd5667b6a0792ba6bd`。

部署脚本位于 `logs/deploy-123.207.238.56/update-20261104.ps1`。它使用指定的 Bitvise 配置上传到服务器暂存目录，校验 161 个文件，编译后端并检查活动对局。切换前备份旧程序和数据库，启动后检查本机健康端点，切换失败时回滚。Bash 语法已在本地检查通过。

已使用 Bitvise 配置部署并切换服务。远端文件校验和 TypeScript 编译通过，`bound-shoot.service` 为 `active`。回滚备份位于 `/home/ubuntu/Bound_Shoot/backup-20261104-L3mCayGI`。

公网健康检查通过。同一台 Windows 电脑上的两个测试客户端通过公网创建和加入同一房间，进入热身，由房主开启正式对战。两端通过 NGO/UDP 同步了同一局的四人阵容（两位真人和两名 AI）。验证日志位于 `logs/public-lobby-1793731877383/`。本轮冒烟验证到正式开局，完整结算、再战及跨客户端网络验证尚待进行。
