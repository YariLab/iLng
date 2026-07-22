# iLng

<img src="assets/iLng-logo-128.png" width="128" height="128" align="right" alt="iLng logo">

OSD + sound indicator for Windows keyboard layout (EN / UA and others).

**Author:** Yaroslav Bohachuk ([@YariLab](https://github.com/YariLab))

**Download:** [iLng.exe (latest release)](https://github.com/YariLab/iLng/releases/latest/download/iLng.exe)

<br clear="all">

## Requirements

- Windows 10/11
- .NET Framework 4.x (built into Windows)

## Build & run

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\bin\iLng.exe
```

Or double-click `start.bat`.

## Tray menu

- **iLng info** — about window
- Volume 100% / 50% / Mute
- OSD / No OSD
- Start with Windows
- Exit

Menu language follows the Windows UI language (Ukrainian or English).

## Assets

Logo files live in `assets/` (SVG + PNG sizes). The published `bin/iLng.exe` is standalone (icon + 256px logo embedded).
