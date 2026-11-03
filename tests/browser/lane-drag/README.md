# Lane drag regression check

This loopback-only host renders the production `LaneOrder` Razor component and CSS/JS using fictional records and an isolated preference file under ignored `output/playwright/lane-drag-tests/`. It makes no service or database requests. Browser checks exercise actual mouse input rather than calling `MoveLane` directly. Native MAUI/WebView2 acceptance remains a separate release check.

From this directory, start `dotnet run --project LaneDragPreview.csproj`. From the desktop repository root, run:

```powershell
npx --yes @playwright/cli -s=lane-drag open http://127.0.0.1:8765 --headed
npx --yes @playwright/cli -s=lane-drag run-code --filename tests/browser/lane-drag/check.js
npx --yes @playwright/cli -s=lane-drag run-code --filename tests/browser/lane-drag/scrollbar.js
npx --yes @playwright/cli -s=lane-drag run-code --filename tests/browser/lane-drag/width.js
npx --yes @playwright/cli -s=lane-drag close
```

Stop the host with Ctrl+C. The screenshot captures the complete translucent column shadow, original position and insertion marker. The check also covers reload persistence, click threshold, Escape/outside cancellation, card exclusion, edge scrolling, filtered hidden-lane preservation, keyboard movement and reset.

The scrollbar check adds simulated update and warning banners, verifies that the board and status bar remain inside the window, and drags the horizontal scrollbar with the mouse at multiple window sizes and in both themes.

The width check resizes the window and filters the board to one status, verifying that lanes stay 240 pixels wide and a wider window shows more lanes.
