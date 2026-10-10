# MuvluvMod

## [English](README_EN.md)

本仓库的文件适用于 **Windows 平台 DMM Game Player 版本**的游戏客户端

开发环境、Utility 源码引用和代码格式化见 [构建说明](https://github.com/anosu/ModEngineering/blob/main/docs/CONVENTIONS.md)。

---

## 功能特性

- 提供游戏内翻译（包括剧情和各类数据）
- 关闭游戏内动态添加的马赛克
- 战斗跳过按钮始终可用
- 剧情语音不中断播放
- 进入战斗时自动跳过战斗

---

## 使用方法

### 1. 准备工作

- 确保已安装游戏的客户端（DMM Game Player 版）
- 确认游戏可执行文件 `muv_luv_girlsgardenx_cl.exe` 所在目录

### 2. 下载插件

- 前往 [Releases 页面](https://github.com/anosu/MuvluvMod/releases) 下载最新版本（带绿色 `Latest` 标记）
- 展开 `Assets`，下载 `MuvluvMod.7z`（不要下载 `Source code`，那是源码）

### 3. 安装插件

- 将压缩包解压，得到 `winhttp.dll`、`BepInEx` 文件夹等内容
- 将它们复制到与 `muv_luv_girlsgardenx_cl.exe` 相同的目录下
- 你的`winhttp.dll`、`BepInEx`文件夹和`muv_luv_girlsgardenx_cl.exe`应当处于同一目录下
- 若已存在旧版本，可以先删除或直接覆盖

### 4. 启动游戏

- 正常启动游戏：我是指从 DMM Game Player 启动或者从第三方 DMM 启动器启动，**而不是直接双击`muv_luv_girlsgardenx_cl.exe`启动！！！**
- 第一次启动或游戏更新后，会出现控制台窗口并执行初始化
- 初始化过程中，BepInEx 会从官网获取对应 Unity 版本的补丁
- 若在第一次启动初始化时控制台出现红色报错（常见于无法直连 BepInEx 官网），请使用代理/梯子而不是加速器，确保你能够正常访问[https://unity.bepinex.dev/libraries/](https://unity.bepinex.dev/libraries/)
- 初始化完成后，游戏会正常启动

### 5. 配置文件

- 首次运行后会在 `BepInEx\config` 文件夹生成：
    - `BepInEx.cfg`（BepInEx 配置）
    - `MuvluvMod.cfg`（插件配置，可用于关闭翻译等）
- 可安装 [anosu/BepInEx.ConfigurationManager](https://github.com/anosu/BepInEx.ConfigurationManager) 的 IL2CPP 版本，将发行包中的 `ConfigurationManager.dll` 和 `BepInEx.KeyboardShortcut.dll` 一起放入 `BepInEx/plugins`，按 **F1** 打开配置窗口。
- 窗口中的文本在按 Enter、失去焦点或关闭窗口时提交，Escape 放弃当前草稿。资源地址、缓存目录、字体和缺失剧本提交位于高级设置。
- 配置键及默认值保持兼容。CDN 只接受 HTTP/HTTPS 地址；资源值去除首尾空格，无效地址或空路径回退默认值。所有变更记入日志，只有布尔开关弹出提示。
- 战斗和语音开关按配置说明即时或在下次触发时生效。开启翻译会在主线程加载字体并预加载数据，影响后续载入的剧情及主数据；已注入的译文需重启才能还原。CDN、语言、缓存策略、字体和更新检查需重启生效。直接修改配置文件时请重启游戏。
- 如需隐藏控制台窗口，请在 `BepInEx.cfg` 的 `[Logging.Console]` 中将 `Enabled` 设置为 `false`

### 6. 翻译缓存

- 翻译文件默认缓存在 `BepInEx\plugins\MuvluvMod\translation`，插件会根据翻译清单校验缓存并自动下载有更新的文件
- 下载失败时会尝试使用已有缓存；需要重新下载全部翻译时，关闭游戏后删除该目录即可
- 可通过 `MuvluvMod.cfg` 中的 `[Translation.Cache]` 修改缓存目录，或开启 `PreferLocalFiles` 优先使用本地翻译文件

---

## 快捷键

- `F3`: 开启/关闭始终启用跳过按钮
- `F4`: 开启/关闭语音中断
- `F5`: 开启/关闭自动跳过战斗

---

## 免责声明

- 本插件为 **第三方爱好者作品**，与官方开发商及发行商无任何关联
- 本插件仅供学习与技术研究使用，请在 **合法合规** 的前提下使用
- 使用本插件可能会影响游戏的正常运行，作者不对因使用本插件导致的任何问题（包括但不限于封号、数据丢失、程序崩溃）负责
- 下载与使用本插件即视为您已同意自行承担相关风险

## 开发

源码位于 `src/`，测试位于 `tests/`。项目配置由 `.csproj` 管理，依赖版本由 Git 子模块记录。构建、VS 联调和发布命令见[公共工程说明](https://github.com/anosu/ModEngineering/blob/main/docs/CONVENTIONS.md)。

`Core` 定义配置、装配依赖并管理字体和 HTTP 生命周期；配置校验器及 ConfigurationManager 标准标签留在此处。`Patches` 负责游戏对象、剧情状态和加载等待，`Services` 处理翻译缓存和缺失剧本提交，通过显式依赖获取配置。翻译管理不再持有字体或协程。游戏加载结果仍由协程转交时拒绝卸载，其余后台请求在卸载时统一取消。

运行 `python shared/ModEngineering/scripts/project.py test` 验证缓存策略、旧配置兼容、值校验、请求取消、提交开关及等待期间关闭翻译。主数据在实际写入前重新读取开关，关闭翻译仍会正常交回游戏加载结果。测试使用真实 BepInEx 配置类、模拟 HTTP 响应及游戏调度替身，不连接外部服务。游戏内字体、剧情加载和语音补丁需实际运行验证。
