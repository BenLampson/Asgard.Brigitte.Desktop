# Using Asgard Brigitte

English · [简体中文](usage.md)

## Use locally by default

Every launch shows the welcome page with **Use on this computer** as its primary action and online sign-in below. Local mode needs no account or network. Create and edit projects and bugs, search and filter, change status, add replies and work logs, claim locks, add and preview or download attachments, delete bugs, and copy repair prompts. Records and attachment bodies persist in local SQLite.

Choose **Back to welcome** from the **Local user** menu to return to the sign-in entry without deleting local data. Local and online workspaces stay separate; nothing is automatically uploaded or merged. Invitations, online account management, remote MCP keys, and LAN sharing are unavailable locally. Local prompts contain issue snapshots without directing tools to an online MCP service.

## Online sign-in and projects

Select **Sign in with Heimdall** to restore saved online credentials or sign in through your system browser and return through a local callback. If the service offers registration, **Create an account** opens its registration page. Online mode needs network access and an account on the configured service.

After sign-in, wait for workspace sync to finish. Create a project from the empty workspace or project management window, or use **Join with invitation** and paste the full link shared by its owner. Enter an invitation password if requested. Joining a project gives collaboration access according to server permissions; it does not make you an administrator.

## Work with bugs

Choose a project in the sidebar. Switch between board and list views, search, and filter by status, priority, assignee, or source. Bug details, editing, replies, status changes, projects, invitations, and keys open in separate windows.

Use **Report bug** to enter a title, description, issue details, priority, assignee, and affected services. Markdown is supported. You can select attachments or paste and drag images and files. Each attachment is limited to 50 MB. Downloaded files are saved to the location you choose.

Claiming a bug takes a temporary work lock. Another worker's active lock prevents changes while keeping reading, copying, and downloading available. The server makes the final authorization decision. Deleting a bug requires two confirmations and also removes associated comments, history, and attachments.

## Sync, updates, and the tray

In online mode, workspace views read synchronized local data. The first sync must complete before opening the online workspace. During a network interruption, synchronized content is read-only. The online client does not queue offline edits or automatically repeat successful write requests. Local mode reads and writes its own database directly and does not wait for synchronization.

Unread updates remain in the tray view until marked as read. System notifications display update counts; detailed summaries appear inside the app. When you close the main window, choose whether to quit or continue in the tray. You can remember the choice and change it later in **Settings → General**. Quitting stops background synchronization and notifications.

## Language and appearance

**Settings → General → Language** offers **Follow system**, **简体中文**, and **English**. The default follows your operating system: Chinese language preferences use Simplified Chinese, and other languages use English. Switching language updates open windows without discarding form drafts. Project names, bug text, comments, and server-provided prompt content stay in their original language.

Appearance can be light, dark, or follow the system. Language, theme, and close preferences are saved on this device and remain after restarting.

## Prompts and MCP

Prompt buttons copy text generated from your workspace's synchronized templates. They do not call an AI model or send content to an AI provider. Review the text before pasting it into an external tool.

The MCP key window creates scoped access for external clients. Save a new key immediately: its full value is shown only once. Share it only with trusted tools, use the minimum scopes required, and revoke unused keys. Client source access does not grant access to anyone else's workspace.

## Privacy and support

**Privacy and data** is available before sign-in. Read the [privacy policy](../PRIVACY.md) for accounts, local cache, attachments, and integrations. Signing out removes current saved credentials but does not delete every cache file, saved attachment, or server record.

For problems and suggestions, [open a GitHub Issue](https://github.com/BenLampson/Asgard.Brigitte.Desktop/issues/new/choose). Issues are public, so remove credentials and private content. Use the [support guide](../SUPPORT.md) for privacy and security reporting channels.
