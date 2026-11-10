# 背景音乐与中文名

标题、选角、菜单、联机大厅和热身场连续循环播放用户提供的《Happy End》。正式战斗倒计时结束后切换到原创的《Rope Rush》，结算后切回《Happy End》。训练场使用《Happy End》。设置中的「声音」控制音乐和音效。

两首音频位于 `unity-client/Assets/BoundShoot/Music/Resources`。《Rope Rush》以 144 BPM 合成，16 小节约 27 秒，由 `node tools/generate-battle-music.mjs` 生成，未使用外部采样。

中文名已统一为「绳紧兵大作战」，包括游戏内标题、Unity 产品名、构建设置和项目文档。Unity 批处理验证了音频资源导入，菜单与战斗退出相关的 3 项 PlayMode 测试通过。实际听感与音乐切换可在编辑器打开 Boot 场景后试听。本次交付为本地源码和音频资源。
