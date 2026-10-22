# Coucou auto-hide

A PowerShell helper that makes the Coucou overlay behave like an auto-hiding
toolbar: it slides the window up behind the top edge of the screen and brings it
back when the pointer touches the top edge.

It moves the window only — Coucou itself is not modified, patched or hooked.

```powershell
# run once in the current session
powershell -ExecutionPolicy Bypass -File .\coucou-autohide.ps1
```

Uses `user32.dll` through P/Invoke (`SetWindowPos`, `GetCursorPos`, a low-level
mouse hook) to track the pointer.

MIT licensed — see [LICENSE](LICENSE).
