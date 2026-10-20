# Asgard Brigitte Privacy Policy

English · [简体中文](PRIVACY.zh-CN.md)

Effective date: October 20, 2026

Operator: **benlampson**

Privacy contact: **[cn.benlampson@gmail.com](mailto:cn.benlampson@gmail.com)**

This policy describes how Asgard Brigitte's desktop application and its default Brigitte service handle information. The desktop client is open source at [Asgard.Brigitte.Desktop](https://github.com/BenLampson/Asgard.Brigitte.Desktop). The default services use `brigitte.mudou.tech`, `idp.mudou.tech`, and `heimdall.mudou.tech` for collaboration, authentication, and account management. If you use a separately operated or modified deployment, ask that deployment's operator about its handling of server data. External websites, Microsoft Store, GitHub, and AI tools have their own policies.

## Information used

**Local mode:** every launch offers **Use on this computer** without registration or sign-in. Projects, bugs, replies, work logs, history, and attachment bodies stay in a separate local SQLite database. Local mode does not read online credentials, upload or merge records with online workspaces, or open a network service port. Returning to welcome keeps local data. Protect access to your device and backups; the local database is not encrypted. User-supplied external images and links can still contact their destinations when displayed or opened, as described below. Account, collaboration sharing, online cache, invitation, and MCP key descriptions in this policy apply to online mode.

- **Account and access information.** Account ID, username, display name, email when supplied, tenant, roles, permissions, and authentication tokens are used to sign you in, determine access, and identify actions. Sign-in takes place through Heimdall in your system browser using an authorization flow; the desktop application does not ask you to enter your account password into the desktop interface. If registration is available, the registration website sends the username, email, display name, and password you provide through Brigitte to Heimdall to create an account. Brigitte does not store the registration password. Registration alone does not verify your email address.
- **Collaboration content.** Projects, project membership, bug titles and descriptions, issue details, priorities, statuses, services, source references, replies, work logs, status history, timestamps, and attachments that you submit or are authorized to access. Actions can be associated with your account or an authorized MCP worker. You control the information you put in these fields; avoid including passwords or unnecessary personal information.
- **Invitations and integration credentials.** Invitation links, their expiration and optional passwords, and MCP keys and scopes support the sharing and integrations you request. Invitation tokens are shown when created; the server stores token digests and optional password hashes. Full MCP keys are shown once after creation and are not included in the workspace sync cache.
- **Connection and local technical information.** A service necessarily receives connection information such as an IP address and request timing. Service operation, authentication, security, and troubleshooting can involve request results, errors, and audit records. The desktop also stores local preferences, an installation worker ID, and WebView data needed to run the application. It has no built-in advertising or third-party analytics SDK.

## Purposes and sharing

We use this information to provide accounts and collaboration, enforce project permissions, synchronize authorized records, restore sessions, deliver updates, support integrations you enable, troubleshoot failures, and protect the service. We do not sell personal information or use desktop data for advertising profiles.

Content and associated names, actions, and history are visible to the people and authorized tools allowed by a project's permissions, including its owner and relevant administrators. Identity services and infrastructure providers process information needed to deliver and secure the service. Information may also be disclosed when required by applicable law or necessary to investigate abuse and protect legitimate rights. Creating a project, submitting content, or inviting members can make that content available to authorized collaborators; an invitation does not grant project management permissions.

The application uses HTTPS and authenticated connections to its configured services. Your browser and the identity provider may store their own sign-in cookies under their policies. The local sign-in callback listens only on the loopback interface and is used to complete the authorization flow.

## Storage, retention, and security

- **Credentials:** the desktop uses the platform's secure storage for session credentials. Credentials are not written into the business SQLite database or JavaScript browser storage. Temporary authorization state and one-time integration results are held in memory for the relevant operation.
- **Workspace cache:** authorized projects, bugs, replies, history, attachment metadata, and unread update summaries are saved in a local SQLite database, separated by service, tenant, and verified account. Attachment bodies are retrieved when you preview or download them; files you save remain in the location you choose. Workspace cache is not represented as an encrypted vault, so protect access to your device and its backups.
- **Preferences and WebView data:** theme, language, and close behavior are saved on the device. The application also uses WebView cache. Preferences can remain across updates and account changes. Exact file locations depend on the operating system and installation type; implementation details are in [data handling notes](docs/data.md).
- **Retention:** service data is kept while needed to provide collaboration, maintain security and auditability, resolve requests, or meet applicable obligations. Retention can depend on the workspace and deployment. Local cache can remain until removed through application-data management. Read updates are removed from the local unread list. Files you saved yourself remain until you remove them.

Signing out removes the current saved sign-in credentials and closes work windows. **It does not automatically erase cached workspace files, previously saved attachments, browser cookies, or server collaboration records.** Uninstalling the app also does not delete server records, exported files, or every separately stored preference. Contact us about account or service-data deletion; do not rely on signing out or uninstalling for that purpose. Where deletion applies, legal or security records and applicable backup copies may need separate handling. We will explain relevant limitations when responding to a request.

We use access controls, authenticated connections, and platform credential protection, but no storage or transmission system can guarantee absolute security.

## Attachments, notifications, and external content

The app accesses attachments when you select, drag, or paste them into the app, preview them, or choose to save them. It does not continuously monitor the clipboard. Check attachments before uploading them; other authorized project members may access them.

System notifications include project names, bug titles, and action summaries, including while the app is in the background. Long Windows notifications are split into parts; the operating system may still collapse banner text. Full summaries remain available inside the app. Notifications can be controlled in your operating system settings. If you choose to minimize to the system tray or menu bar, the application continues background session maintenance and synchronization until you quit it.

Markdown content may contain external HTTP(S) image links. Displaying such an image can contact its host, which receives normal network request information. Opening an external link launches your browser and subjects that visit to the destination's policy. Avatar initials are generated locally rather than fetched from an avatar service.

## AI tools and MCP

The desktop does not bundle an AI model or automatically send copied prompts to an AI provider. Copying a prompt places text on your clipboard. If you paste it into another application, that application's operator handles it under its own policy.

Providing an external MCP client with a key allows it to access information and perform actions within the key's scopes and your server permissions. Choose trustworthy clients, grant only the scopes you need, and revoke keys you no longer use. External AI and MCP tools' storage, processing, and retention are governed by their operators and your arrangements with them.

## Your choices and requests

You can review authorized content in the app, edit or delete items where your role allows, manage project members and invitations where permitted, revoke MCP keys, change local preferences, control system notifications, and sign out. Account-management functions open the configured Heimdall website. Some actions require a project owner or administrator.

For access, correction, export, deletion, or other applicable privacy requests, email **[cn.benlampson@gmail.com](mailto:cn.benlampson@gmail.com)**. State the service and account concerned and the request; do not send passwords, tokens, or full MCP keys. We may need to verify your identity and authority before acting. Available rights and any retention exceptions depend on applicable law and the relevant deployment.

This is a project collaboration tool, not a service specifically directed at children. If you believe a child has provided personal information without appropriate authorization, contact the privacy email so it can be reviewed.

## Support and policy changes

Use [GitHub Issues](https://github.com/BenLampson/Asgard.Brigitte.Desktop/issues/new/choose) for ordinary bugs and feature requests; English and Chinese are welcome. Issues are public. Do not post personal account information, private project content, invitation links, passwords, tokens, or keys. Use the privacy email for privacy requests and [private security reporting](https://github.com/BenLampson/Asgard.Brigitte.Desktop/security/advisories/new) for vulnerabilities. See [SUPPORT.md](SUPPORT.md).

This policy is hosted on GitHub, which handles visits under the [GitHub Privacy Statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement). We will update this page and its effective date when our practices change. Material changes will be communicated as appropriate. Both language versions describe the same practices.
