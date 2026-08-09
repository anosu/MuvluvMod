# MuvluvMod

This repository contains files intended for the **Windows DMM Game Player version** of the game client.

---

## Features

- Provides in-game translation (including story and various data)
- Removes dynamically added mosaics/censorship in-game
- Skip button is always available during battles
- Story voice playback is not interrupted
- Automatically skips battles upon entry

---

## Usage

### 1. Preparation

- Ensure the game client (DMM Game Player version) is installed
- Locate the directory where the game executable `muv_luv_girlsgardenx_cl.exe` resides

### 2. Download the plugin

- Go to the [Releases page](https://github.com/anosu/MuvluvMod/releases) and download the latest version (marked with the green `Latest` badge)
- Expand `Assets` and download `MuvluvMod.7z` (do **not** download `Source code`, as that is the source code)

### 3. Install the plugin

- Extract the archive to obtain `winhttp.dll`, the `BepInEx` folder, and other contents
- Copy them to the same directory as `muv_luv_girlsgardenx_cl.exe`
- Your `winhttp.dll`, `BepInEx` folder, and `muv_luv_girlsgardenx_cl.exe` should all be in the same directory
- If an older version already exists, you may delete it first or simply overwrite

### 4. Launch the game

- Start the game normally: **I mean launch it from DMM Game Player or a third-party DMM launcher, NOT by double-clicking `muv_luv_girlsgardenx_cl.exe` directly!!!**
- On the first launch or after a game update, a console window will appear and perform initialization
- During initialization, BepInEx will download the corresponding Unity version patch from the official website
- If you see red error messages in the console during the first initialization (commonly due to inability to connect directly to the BepInEx website), use a proxy/VPN (not a game accelerator) and make sure you can access [https://unity.bepinex.dev/libraries/](https://unity.bepinex.dev/libraries/)
- After initialization completes, the game will start normally

### 5. Configuration file

- After the first run, the following files will be generated in the `BepInEx\config` folder:
    - `BepInEx.cfg` (BepInEx configuration)
    - `MuvluvMod.cfg` (plugin configuration, can be used to disable translation, etc.)
- Changes to the configuration require a game restart to take effect
- See the relevant descriptions within the configuration files for specific options
- To hide the console window, set `Enabled` to `false` under `[Logging.Console]` in `BepInEx.cfg`

### 6. Translation cache

- Translation files are cached in `BepInEx\plugins\MuvluvMod\translation` by default. The plugin verifies cached files against the translation manifest and downloads updates automatically
- If a download fails, the plugin tries to use the existing cache. To download all translations again, close the game and delete this directory
- The cache directory can be changed under `[Translation.Cache]` in `MuvluvMod.cfg`; enable `PreferLocalFiles` to prioritize local translation files

---

## Shortcut Keys

- `F3`: Toggle always-enable skip button
- `F4`: Toggle voice interruption
- `F5`: Toggle auto-skip battle

---

## Disclaimer

- This plugin is a **third-party fan-made work** and is not affiliated with the official developers or publishers in any way
- This plugin is intended for learning and technical research purposes only. Please use it **in compliance with all applicable laws and regulations**
- Using this plugin may affect normal game operation. The author assumes no responsibility for any issues arising from its use (including but not limited to account bans, data loss, or program crashes)
- By downloading and using this plugin, you agree that you do so at your own risk
