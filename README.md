# Coucou auto-hide

A small Windows helper that makes the **Coucou** overlay behave like an auto-hiding
toolbar: it slides the window up behind the top edge of the screen and brings it
back when the pointer touches that edge. Version **1.1.0** — now with an installer.

## Install (the normal way)

1. Download **`CoucouAutoHideSetup.exe`** from the [latest release](../../releases/latest).
2. Run it — it installs for the current user, no administrator prompt.
3. Leave *Start with Windows* ticked if you want it to run after a reboot.

That is all. The installer puts the app in
`%LOCALAPPDATA%\Programs\CoucouAutoHide`, adds a Start Menu shortcut, registers an
entry in *Settings → Apps* and starts the helper.

**Requirements:** Windows 10 or 11 (the app targets .NET Framework 4.8, which ships
with both — nothing to install) and **Coucou for Windows installed and running**.
Without Coucou the helper sits idle waiting for the overlay, which is expected.

If Windows SmartScreen warns about an unknown publisher, that is because the exe is
not code-signed: *More info → Run anyway*.

### Uninstall

*Settings → Apps → Coucou auto-hide → Uninstall*, or run
`Uninstall.exe --uninstall` from the install folder. It removes the files, the
shortcut, the autostart entry and the registry key, and stops the helper.

### Without installing

`CoucouAutoHide.exe` is the same program as a single portable exe — put it anywhere
and run it. The PowerShell script `coucou-autohide.ps1` (and `run-coucou-autohide.cmd`,
which starts it hidden) does the same job if you prefer not to run a compiled binary.

## What this is — and what it is not

**Coucou is not my project.** It is a Claude Code companion app created by
**Louis Raillé** — [louisraille.fr](https://louisraille.fr/) ·
[github.com/Louis-CFM/coucou](https://github.com/Louis-CFM/coucou) — and it is
distributed under the MIT licence. All copyright in Coucou belongs to him.

This repository is an **independent helper written by me (Kay / krzky)**. It
contains **no Coucou source code**: nothing is copied, patched, forked or bundled.
It calls `SetWindowPos()` on Coucou's own top-level window — the same thing a window
manager does. Coucou is not modified, injected or hooked, and it does not need to be
rebuilt.

| | Coucou | This repository |
|---|---|---|
| Author | Louis Raillé | Kay (krzky) |
| Language | Swift / SwiftUI (macOS), Tauri + Rust/TypeScript (Windows) | C# (.NET Framework 4.8) and PowerShell |
| Licence | MIT for code; brand and artwork reserved | MIT |
| Files shared with the other | none | none |

It is not affiliated with or endorsed by Louis Raillé. If auto-hide belongs in the
app itself, upstream is the right place to propose it.

## How it works

A 30 ms loop:

- finds the top-level window titled exactly `Coucou` whose process is `coucou.exe`
  (`EnumWindows` + `GetWindowText`, so a window titled `Settings — Coucou` is left
  alone);
- only manages that window while it is docked at the top of its monitor — if you
  move it somewhere else, the helper leaves it there;
- when the pointer leaves the window for 700 ms, slides it up with an ease-out cubic
  over 220 ms, leaving 3 px visible at the screen edge;
- when the pointer touches the top edge over that window, or the window gets focus,
  slides it back down;
- re-detects the window if Coucou restarts, and is per-monitor DPI aware, so the
  3 px peek stays 3 px on scaled displays.

No mouse hook is installed — the pointer is read with `GetCursorPos` on each tick,
so nothing is injected into other processes. A single instance runs at a time
(`Global\CoucouAutoHide` mutex).

Tuning constants live at the top of `src/app/Program.cs`: `PEEK`, `ANIM_MS`,
`HIDE_DELAY_MS`, `TRIGGER_PX`.

## Build from source

```
build.cmd            # builds the app, embeds it in the installer, both land in dist\
```

Or by hand: `dotnet build src/app -c Release`, copy
`src/app/bin/Release/net48/CoucouAutoHide.exe` to `src/setup/payload/`, then
`dotnet build src/setup -c Release`. Requires the .NET SDK; both projects target
`net48` so the output runs on a stock Windows 10/11 with no runtime download.

MIT licensed — see [LICENSE](LICENSE).
