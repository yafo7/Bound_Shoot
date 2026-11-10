# macOS 客户端

Unity 菜单入口为「Bound Shoot → Build → macOS Client」。命令行入口如下。

```powershell
./tools/build-game.ps1 -Target MacOS
```

构建使用当前游戏配置和公网入口，输出到 `builds/MacOS/BoundShoot.app`。目标架构为 Intel 64-bit + Apple silicon，使用 Mono 后端。同目录的 `release.json` 记录游戏配置校验值，需与服务器配置匹配。

Windows 上的 Unity 2022.3.62f3 需要安装「Mac Build Support (Mono)」。本机已通过 Unity Hub 安装，已成功生成客户端。

2026-11-04 已生成 `builds/MacOS/BoundShoot-MacOS.zip`，大小为 63,818,078 字节，包含应用和 `release.json`。将完整压缩包传到 Mac，使用 macOS 自带的归档工具解压，然后打开 `BoundShoot.app`。包内执行权限已设置并核验，主程序、UnityPlayer 和 Burst 原生插件均保留执行权限。Unity 生成的应用签名文件随包保留。

Unity 构建成功。主程序和 UnityPlayer 的 Mach-O 头均确认包含 Intel x86_64、ARM64 两种架构，压缩包中的两个二进制与构建原件的 SHA-256 一致。游戏配置校验值为 `0396dd56fca8d37276bfffbe1745f5e548bf90fe13a1b268fe37146e3e399dd5`，与已部署服务器一致，团灭复活等待为 20 秒，默认公网入口为 `http://123.207.238.56:3000`。

压缩包 SHA-256 为 `cc554136f68487644504dfd3c8e1e6510db149004b623b3066159abff6542b30`。本机为 Windows，尚未在 Mac 上实机运行或验证跨平台联机。Apple Developer ID 签名和公证尚未完成，macOS 可能拦截首次打开，需要在 Mac 上进一步完成分发验证。

参考 [Unity 2022.3 macOS 构建说明](https://docs.unity3d.com/2022.3/Documentation/Manual/macos-building.html)。
