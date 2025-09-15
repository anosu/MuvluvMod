# MuvluvMod

本仓库的文件主要用于 **Windows 平台 DMM Game Player 端**

### 插件功能

-   为游戏提供剧情翻译，包括主线活动角色剧情
-   去除游戏内动态添加的马赛克
-   总是启用跳过按钮
-   剧情语音不中断
-   自动跳过战斗

### 使用方法

-   首先确保你已经安装了游戏的客户端（DMM Game Player 版）并且知道游戏的可执行文件（`muv_luv_girlsgardenx_cl.exe`）所在的文件夹路径

-   从本仓库的[Releases](https://github.com/anosu/MuvluvMod/releases)页面（← 如果你不知道在哪儿那就直接点这里）找到最新发布的版本（带有绿色的`Latest`标识），展开`Assets`选项卡（默认应该就是展开的），下载名为`MuvluvMod.7z`或类似的压缩包，不要下载`Source code`，那是源码

-   将下载的压缩包解压你会得到`winhttp.dll`，`BepInEx`等文件和文件夹，将所有这些文件复制（或直接解压）到与游戏的可执行文件（`muv_luv_girlsgardenx_cl.exe`）相同的目录，你的`BepInEx`文件夹、`winhttp.dll`文件以及`muv_luv_girlsgardenx_cl.exe`应该在同一个目录下。如果你之前下载过旧版本可以先将旧版本删除或者直接全部覆盖（如果后面没问题的话）

-   正常启动游戏。注意：首次启动或者游戏更新之后，插件会有一个初始化的过程，此时你只会看到一个控制台窗口，等待其初始化完成游戏才会正常启动，此过程中 BepInEx 会从其官网下载对应游戏 Unity 版本的补丁来对游戏进行修改以支持插件的运行，如果你使用 ACGP 之类的加速器并且在此过程中看到了控制台窗口出现了红色的报错那么说明你可能无法直连其官网，请打开梯子来解决此问题。

-   当插件初始化完成并且游戏正常启动后（控制台窗口没有出现红色的报错），那么此时应该已经可以正常使用了。插件首次运行之后会在`BepInEx\config`目录下生成 BepInEx 和 mod 本身的配置文件，分别为`BepInEx.cfg`和`MuvluvMod.cfg`，如果你需要修改插件的设置（如关闭翻译），请修改`MuvluvMod.cfg`之后重新启动游戏。如果你需要隐藏控制台窗口，请在`BepInEx.cfg`中找到`[Logging.Console]`选项，并将`Enabled`的值设置为`false`

### 快捷键

-   `F2`: 开启/关闭翻译
-   `F3`: 开启/关闭始终启用跳过按钮
-   `F4`: 开启/关闭语音中断
-   `F5`: 开启/关闭自动跳过战斗

### 群聊

-   QQ 群: [660247178](https://qm.qq.com/q/N1GMXxIBCG)（已满）
-   QQ 群 2: [485328718](https://qm.qq.com/q/rCHcfhnW6G)

有问题在群里反馈
