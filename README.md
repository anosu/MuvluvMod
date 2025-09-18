# MuvluvMod

## [English](README_EN.md)

本仓库的文件适用于 **Windows 平台 DMM Game Player 版本**的游戏客户端

---

## 功能特性

-   提供剧情翻译（包括主线与活动角色剧情）
-   去除游戏内动态添加的马赛克
-   始终启用跳过按钮
-   剧情语音不中断播放
-   自动跳过战斗

---

## 使用方法

### 1. 准备工作

-   确保已安装游戏的客户端（DMM Game Player 版）
-   确认游戏可执行文件 `muv_luv_girlsgardenx_cl.exe` 所在目录

### 2. 下载插件

-   前往 [Releases 页面](https://github.com/anosu/MuvluvMod/releases) 下载最新版本（带绿色 `Latest` 标记）
-   展开 `Assets`，下载 `MuvluvMod.7z`（不要下载 `Source code`，那是源码）

### 3. 安装插件

-   将压缩包解压，得到 `winhttp.dll`、`BepInEx` 文件夹等内容
-   将它们复制到与 `muv_luv_girlsgardenx_cl.exe` 相同的目录下
-   你的`winhttp.dll`、`BepInEx`文件夹和`muv_luv_girlsgardenx_cl.exe`应当处于同一目录下
-   若已存在旧版本，可以先删除或直接覆盖

### 4. 启动游戏

-   正常启动游戏：我是指从 DMM Game Player 启动或者从第三方 DMM 启动器启动，**而不是直接双击`muv_luv_girlsgardenx_cl.exe`启动！！！**
-   第一次启动或游戏更新后，会出现控制台窗口并执行初始化
-   初始化过程中，BepInEx 会从官网获取对应 Unity 版本的补丁
-   若在第一次启动初始化时控制台出现红色报错（常见于无法直连 BepInEx 官网），请使用代理/梯子而不是加速器，确保你能够正常访问[https://unity.bepinex.dev/libraries/](https://unity.bepinex.dev/libraries/)
-   初始化完成后，游戏会正常启动

### 5. 配置文件

-   首次运行后会在 `BepInEx\config` 文件夹生成：
    -   `BepInEx.cfg`（BepInEx 配置）
    -   `MuvluvMod.cfg`（插件配置，可用于关闭翻译等）
-   修改配置后需重启游戏生效
-   如需隐藏控制台窗口，请在 `BepInEx.cfg` 的 `[Logging.Console]` 中将 `Enabled` 设置为 `false`

---

## 快捷键

-   `F2`: 开启/关闭翻译
-   `F3`: 开启/关闭始终启用跳过按钮
-   `F4`: 开启/关闭语音中断
-   `F5`: 开启/关闭自动跳过战斗

---

## 交流群

-   QQ 群 1: [660247178](https://qm.qq.com/q/N1GMXxIBCG)（已满）
-   QQ 群 2: [485328718](https://qm.qq.com/q/rCHcfhnW6G)

如有问题，请在群内反馈

---

## 免责声明

-   本插件为 **第三方爱好者作品**，与官方开发商及发行商无任何关联
-   本插件仅供学习与技术研究使用，请在 **合法合规** 的前提下使用
-   使用本插件可能会影响游戏的正常运行，作者不对因使用本插件导致的任何问题（包括但不限于封号、数据丢失、程序崩溃）负责
-   下载与使用本插件即视为您已同意自行承担相关风险
