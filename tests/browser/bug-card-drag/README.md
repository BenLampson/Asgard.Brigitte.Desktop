# Bug card drag regression check

This loopback preview renders the production `WorkspaceView` with fictional bugs in a new, dedicated temporary SQLite database. Its host rejects all external HTTP, credentials and sign-in. The tests use real mouse input, the production status command sequence and actual local persistence; native window opening and human confirmation use a recording host. Native MAUI/WebView2 acceptance remains a separate release check.

Start `dotnet run --project BugCardDragPreview.csproj` from this directory. From the desktop repository root:

```powershell
npx --yes --package @playwright/cli playwright-cli -s=bug-cards open http://127.0.0.1:8766 --headed
npx --yes --package @playwright/cli playwright-cli -s=bug-cards run-code --filename tests/browser/bug-card-drag/check.js
npx --yes --package @playwright/cli playwright-cli -s=bug-cards run-code --filename tests/browser/bug-card-drag/edges.js
npx --yes --package @playwright/cli playwright-cli -s=bug-cards close
```

Restart the preview host to get a fresh fixture before rerunning both scripts. Stop it with Ctrl+C when finished. Screenshots go under ignored `output/playwright/bug-card-drag/`.

Checks cover the dedicated detail button with mouse and keyboard, direct dragging, direct closure without human verification, threshold, target highlighting, cancellation, required reasons, human acceptance, foreign/manual locks, read-only viewing, prompt copying, lane-order preservation, edge scrolling, source removal, handler disposal/reinitialization, and English/light/dark layouts. Unit tests separately exercise failed claims, failed status writes, failed release, stale state and session changes over an offline HTTP transport.
