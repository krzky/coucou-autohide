# Coucou auto-hide

A small Windows helper that makes the **Coucou** overlay behave like an auto-hiding
toolbar: it slides the window up behind the top edge of the screen and brings it
back when the pointer touches that edge. Version **1.0.0**.

## What this is — and what it is not

**Coucou is not my project.** It is a Claude Code companion app created by
**Louis Raillé** — [louisraille.fr](https://louisraille.fr/) ·
[github.com/Louis-CFM/coucou](https://github.com/Louis-CFM/coucou) — and it is
distributed under the MIT licence. All copyright in Coucou belongs to him.

This repository is an **independent helper written by me (Kay / krzky)**. It
contains **no Coucou source code**: nothing is copied, patched, forked or bundled.
It is one PowerShell script that calls `SetWindowPos()` on Coucou's own top-level
window — the same thing a window manager does. Coucou is not modified, injected or
hooked, and it does not need to be rebuilt.

So:

| | Coucou | This repository |
|---|---|---|
| Author | Louis Raillé | Kay (krzky) |
| Language | Swift / SwiftUI (macOS), Tauri + Rust/TypeScript (Windows) | PowerShell + inline C# P/Invoke |
| Licence | MIT for code; brand and artwork reserved | MIT |
| Files shared with the other | none | none |

It is not affiliated with or endorsed by Louis Raillé. If auto-hide belongs in the
app itself, upstream is the right place to propose it.

## Requirements

- Windows 10 or 11
- PowerShell 5.1 (built in) — Windows PowerShell is enough, PowerShell 7 is not required
- Coucou for Windows installed and running

## Use

```powershell
# run in the foreground (closing the window stops the helper)
powershell -ExecutionPolicy Bypass -File .\coucou-autohide.ps1
```

Or double-click `run-coucou-autohide.cmd`, which starts it hidden and detached from
a console window.

To stop it, end the `powershell` process running the script, or close the console
it was started from.

A single instance only: the script takes the `Global\CoucouAutoHide` mutex and
exits immediately if one is already running.

## How it works

`Add-Type` compiles a small C# class, then a 30 ms loop:

- finds the top-level window titled exactly `Coucou` whose process is `coucou.exe`
  (`EnumWindows` + `GetWindowText`, so a window titled `Settings — Coucou` is left
  alone);
- only manages that window while it is docked at the top of its monitor — if you
  move it somewhere else, the helper leaves it there;
- when the pointer leaves the window for 700 ms, slides it up with an ease-out
  cubic over 220 ms, leaving 3 px visible at the screen edge;
- when the pointer touches the top edge over that window, or the window gets focus,
  slides it back down;
- re-detects the window if Coucou restarts, and is per-monitor DPI aware
  (`SetProcessDpiAwarenessContext(-4)`), so the 3 px peek stays 3 px on scaled
  displays.

No mouse hook is installed — the pointer is read with `GetCursorPos` on each tick,
so nothing is injected into other processes.

Tuning constants live at the top of the C# class: `PEEK`, `ANIM_MS`,
`HIDE_DELAY_MS`, `TRIGGER_PX`.

## Package

Release assets are the packaged script: `coucou-autohide-1.0.0.zip` contains
`coucou-autohide.ps1`, `run-coucou-autohide.cmd` and this README. Unblock the zip
before extracting if Windows marks it as downloaded from the internet.

MIT licensed — see [LICENSE](LICENSE).
