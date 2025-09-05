# Silksong Death Counter 🎮💀

<img width="1919" height="1079" alt="image" src="https://github.com/user-attachments/assets/d088b74f-f941-4fa8-8268-cbbaf3f61f97" />

**TOP LEFT**

A [BepInEx](https://github.com/BepInEx/BepInEx) plugin for Hollow Knight: Silksong that tracks player deaths, supports per-save counters, and displays stats on screen.

## ✨ Features
- Counts player deaths (`PlayHeroDeath`).
- Per-save and global death counters.
- Run-based counter with hotkey reset (default **F10**).
- Automatic saving to config file.
- Simple UI overlay.
- Detailed logging for debugging.

## 📦 Installation
1. Install [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases).
2. Build the project as a `.dll` (target .NET Framework 4.x).
3. Copy `SilksongDeathCounter.dll` into: <game folder>/BepInEx/plugins/SilksongDeathCounter
4. Start the game. A config file will be created under: BepInEx/config/com.peacestudio.silksongdeathcounter.cfg

## ⚙️ Configuration
```ini
[General]
TotalDeaths = 12

[Hotkeys]
ResetRunDeaths = F10

[Save_0]
TotalDeaths = 8

[Save_1]
TotalDeaths = 4
```

## 🎮 Controls
- **F10** → Reset run death counter.
- Death counters are displayed in the top-left corner of the screen.
