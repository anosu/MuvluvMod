This repository provides files for the **Windows DMM Game Player version** of the game.

---

## Features

-   Story chinese translation (including main and event character scenarios)
-   Remove in-game dynamic mosaics
-   Always enable the skip button
-   Prevent scenario voice interruptions
-   Auto-skip battles

---

## Installation

### 1. Preparation

-   Install the game via **DMM Game Player**.
-   Locate the game executable file: `muv_luv_girlsgardenx_cl.exe`.

### 2. Download

-   Go to the [Releases page](https://github.com/anosu/MuvluvMod/releases).
-   Download the latest release marked with `Latest`.
-   In the `Assets` section, download `MuvluvMod.7z` (do **not** download `Source code`).

### 3. Installation

-   Extract the archive; you will get `winhttp.dll`, `BepInEx`, and other files.
-   Copy all files to the same folder as `muv_luv_girlsgardenx_cl.exe`.
-   If an older version exists, delete or overwrite it.

### 4. Launching

-   On the first run (or after a game update), a console window will appear for initialization.
-   BepInEx will download the proper Unity patch from its official website.
-   After initialization, the game will start normally.

### 5. Configuration

-   After the first run, two config files will be created under `BepInEx\config`:
    -   `BepInEx.cfg` (general settings)
    -   `MuvluvMod.cfg` (mod settings, e.g. disable translation)
-   Restart the game after editing configs.
-   To hide the console window, set `Enabled = false` under `[Logging.Console]` in `BepInEx.cfg`.

---

## Hotkeys

-   `F2`: Toggle translation
-   `F3`: Toggle always-enabled skip button
-   `F4`: Toggle scenario voice interruption
-   `F5`: Toggle auto battle skip

---

## Disclaimer

-   This mod is a **fan-made third-party project** and has no affiliation with the official developers or publishers.
-   It is intended for educational and technical research purposes only.
-   Use of this mod may affect the normal operation of the game. The author is **not responsible** for any consequences (including but not limited to account bans, data loss, or crashes).
-   By downloading and using this mod, you agree to bear all risks yourself.
