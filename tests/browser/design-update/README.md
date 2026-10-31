# Settings and record design verification

This loopback-only preview renders the production lane settings and record components with fictional projects in a temporary local workspace. It never accesses online services or credentials.

Run `dotnet run --project DesignUpdatePreview.csproj` from this directory, then use Playwright CLI against `http://127.0.0.1:8767`. Verify drag and keyboard reorder, project switching, copy preview and confirmation, persistence after reload, independent record collapse, and light/dark layouts. The native Windows host remains a separate acceptance step.
