# KeepAwake Monitor

A lightweight C# tray application that simulates F15 key presses when the system has been idle for a configurable time, keeping the computer "awake". It features real-time monitoring, optional balloon notifications, and an overlay showing current idle time.

---

## 🧰 Features

- Simulates F15 key every `N` seconds after idle threshold is reached
- Displays a real-time overlay with inactivity time
- Custom tray icon changes based on active/idle state
- Tray menu showing current state and allowing graceful exit
- Optional: balloon tips for state changes
- Full configuration via `config.ini`
- Events log in ./logs

---

## 🚀 Usage

- Run `KeepAwakeMonitor.exe`
- App minimizes to tray and monitors user inactivity
- Left-click tray icon to toggle the real-time status menu
- Right-click tray icon to access "Exit" and "Reset Settings"
- `config.ini` in the same folder allows tuning settings

---

## ⚙️ Configuration (`config.ini`)

Place in the same directory as the executable. Example:

```ini
idleThresholdSeconds = 10
f15IntervalSeconds = 60
showNotifications = true
notificationDuration = 1000
showIdleOverlay = true
iconIdle = idle.ico
iconActive = active.ico
```

All values are optional and have defaults.

---

## 🏗️ Build Instructions

1. Target: **.NET Framework 4.8**
2. Compile `KeepAwakeMonitor.cs` using Visual Studio or from command line:

```bash
csc /target:winexe /platform:x64 /out:KeepAwakeMonitor.exe KeepAwakeMonitor.cs
```

3. Place optional `idle.ico`, `active.ico`, and `config.ini` in the same folder.
4. Run the executable.

---

## 🗂️ File Structure

```
/KeepAwakeMonitor/
├── config.ini         (optional)
├── idle.ico           (optional)
├── active.ico         (optional)
├── KeepAwakeMonitor.exe
├── KeepAwakeMonitor.cs
└── compile.bat
```

---

## 📝 Notes

- The `F15` key is rarely used and safe for idle simulation
- The overlay is positioned near the system tray
- Tray menu resizes dynamically based on content
- Only **mouse movement** resets inactivity tracking

---

