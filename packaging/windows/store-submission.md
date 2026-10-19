# Microsoft Store submission for Asgard Brigitte

## October 19, 2026 — 1.0.2 update submitted

Partner Center confirms the existing product is available in Microsoft Store. Version `1.0.2.0` was uploaded, validated and formally submitted as Submission 2 (`1152921505702171873`) on October 19, 2026. At 23:20 Asia/Shanghai, the update is **In certification / pre-processing**: submission complete, pre-processing in progress, certification and publication not started. The UI confirms publication begins immediately after certification. This does not mean 1.0.2 is already available to customers. English and Simplified Chinese release notes describe the complete 15-status board and verified claimant identities. Existing screenshots, age ratings, runFullTrust justification and private reviewer credentials were retained; older packages were retained rather than deleted.

English listing: [store-listing.en.md](store-listing.en.md) · 简体中文商品页：[store-listing.zh-CN.md](store-listing.zh-CN.md)

This file contains public release information only. Never commit reviewer credentials, personal addresses, certificates, or access tokens. The service backend stays in its private repository.

## Registered product / 已登记产品

| Field / 字段 | Value / 值 |
| --- | --- |
| Name / 名称 | Asgard Brigitte |
| Type / 类型 | MSIX or PWA app / MSIX 或 PWA 应用 |
| Store ID | `9N5X2FD6C0XC` |
| Package/Identity/Name | `32960BenLampson.AsgardBrigitte` |
| Package/Identity/Publisher | `CN=7BDCFD81-1900-42BB-9976-ED316E4C3A15` |
| Package/Properties/PublisherDisplayName | `BenLampson` |
| Package family name | `32960BenLampson.AsgardBrigitte_jdr936f9yvnyt` |
| Initial package version / 首包版本 | `1.0.0.0` |
| Languages / 语言 | English (United States), Chinese (Simplified) / `en-US`, `zh-CN` |
| Architecture / 架构 | x64 |

Reserved and first submitted on October 17, 2026. That submission failed certification: the reviewer received `discovery_unavailable` before the credential form, leaving the primary workspace inaccessible (10.1.2.10 and 10.3.1). Version `1.0.1.0` adds complete local use without sign-in and was resubmitted on October 18, 2026. Partner Center now shows **In certification / pre-processing**. It is not yet certified or published. The assigned [Store URL](https://apps.microsoft.com/detail/9N5X2FD6C0XC) becomes useful after publication.

## Submission status / 提交状态

Updated October 18, 2026:

- **Price:** free (`0`) in all selected markets. Public discovery and publication after certification are selected.
- **Package:** `1.0.1.0`, x64, was uploaded and shows **Validated** in Partner Center. Package settings were saved with the new version ranked first. The rejected `1.0.0.0` package is retained but is not distributed because the newer package supports the same customers. Build, unpacked runtime acceptance and Partner Center validation are separate results; none establishes certification success. The `runFullTrust` capability remains subject to Microsoft approval.
- **Store listings:** English (United States) and Chinese (Simplified) descriptions, features and release notes were saved as local-first. Each listing now has six PNG screenshots: the original four 1600 × 900 component captures plus two actual native 1426 × 893 local-mode captures.
- **Privacy and support:** the updated English/Chinese privacy policies were published in public desktop commit `d7a051e`; both public documents returned HTTP 200 and contained the local-mode disclosure. Other desktop source changes remain in the local working tree. Private privacy requests use the policy email.
- **Age ratings:** the completed questionnaire was saved after the publisher confirmed the IARC terms and legal-age declaration. The generated ratings include Microsoft/IARC 12+, ESRB Teen, and PEGI Parental Guidance. The certificate ID is pending in Partner Center.
- **Certification:** the prior review completed October 17 with **Attention needed** because discovery failed before the credential form. New local-first instructions were saved in Additional Testing Info and checked after reopening; existing private credentials were retained. **1.0.1.0 was formally resubmitted on October 18 at approximately 14:35 Asia/Shanghai.** The returned UI shows **In certification**, submission complete, pre-processing in progress, certification/publication not started. Automatic publication after certification remains selected. Hourly follow-up in the current chat tracks important changes until publication.
- **Reviewer access:** an ordinary dedicated account and synthetic workspace are saved in Partner Center's private Credentials and Additional Testing Info sections. Service sign-in, project and bug creation, a reply, and a harmless attachment were exercised. Reviewer credentials must never be added to this repository.

已设置免费。`1.0.0.0` 的认证因 discovery 失败阻断工作区体验而失败。`1.0.1.0` 已通过包验证，双语介绍、每种语言六张截图及本机审核步骤已保存，原私密账号保留。两份公开隐私政策已更新。10 月 18 日约 14:35（北京时间）已正式重新提交，后台显示“正在认证 / 预处理”，通过后立即发布；当前仍未上架。

Runtime acceptance and online findings are recorded in [1.0.1 candidate notes](../../docs/releases/v1.0.1.md). The user chose unpacked execution without system security changes. Installation/upgrade and WACK remain unperformed. Updated local-mode privacy and data documentation accompanies this candidate.

The original four screenshots per language are captured from actual Razor components and production styles using fictional local fixtures. The two new local-mode images per language are native captures of the unpacked 1.0.1.0 executable using synthetic acceptance records. Neither set exposes a customer's workspace. Browse [English screenshots](../../docs/screenshots/en-US) and [中文截图](../../docs/screenshots/zh-CN). The published [1.0.0 screenshot ZIP](https://github.com/BenLampson/Asgard.Brigitte.Desktop/releases/download/v1.0.0/Asgard-Brigitte-1.0.0-store-screenshots.zip) contains only the original set.

## Properties / 属性

| Partner Center field | Suggested value / 建议填写 |
| --- | --- |
| Category / 类别 | Developer tools / 开发人员工具 |
| Subcategory / 子类别 | Utilities, if offered / 实用工具（如有此选项） |
| Accesses personal information / 是否访问个人信息 | Yes / 是 |
| Privacy policy URL / 隐私政策 URL | https://github.com/BenLampson/Asgard.Brigitte.Desktop/blob/main/PRIVACY.md |
| Chinese privacy policy / 中文政策 | https://github.com/BenLampson/Asgard.Brigitte.Desktop/blob/main/PRIVACY.zh-CN.md |
| App website / 网站 | https://github.com/BenLampson/Asgard.Brigitte.Desktop |
| Support URL / 支持 URL | https://github.com/BenLampson/Asgard.Brigitte.Desktop/issues/new/choose |
| Support documentation / 支持说明 | https://github.com/BenLampson/Asgard.Brigitte.Desktop/blob/main/SUPPORT.md |
| Privacy contact / 隐私邮箱 | cn.benlampson@gmail.com |
| System requirements / 系统要求 | Windows 10 version 1809 or later, x64; Microsoft Edge WebView2 Runtime. No account or internet is required for local use. Optional online collaboration requires a compatible service, account, and internet access. |

Do not invent telephone numbers, company addresses, hardware requirements, or mandatory consumer-law declarations. Enter account/legal information privately in Partner Center when it is actually required.

The client copies prompts and manages MCP credentials; it does not itself generate AI output. Answer any generative-AI declaration according to the wording shown by Partner Center and the actual submitted behavior. Do not declare camera, microphone, location, mixed-reality, Xbox, or broadcast requirements that the app does not use. The desktop package declares `runFullTrust` to run the MAUI/WinUI desktop process and provide windows, secure storage, a tray, local cache, file selection/save, and browser sign-in.

## Submission steps / 提交顺序

1. In the **Asgard Brigitte** product, start a submission. The intended price is **free**, with public availability in all selected markets. A source license does not determine the Store selling price.
2. Complete Properties with the URLs above. Add **English (United States)** and **Chinese (Simplified)** Store listings, using the two listing files. The product name stays Asgard Brigitte in both.
3. Build with the registered identity: `pwsh -NoProfile -File packaging/windows/publish-store.ps1 -SettingsPath packaging/windows/store-settings.public.json`. Upload the resulting `.msix` from `publish/store/1.0.1.0/packages/`. Never upload a preview-identity package. Preserve the old package and its report until the replacement is verified.
4. Supply real screenshots captured from this release in both languages using synthetic data. At least one desktop screenshot per listing is required; a useful set is board, details, editor, project invitations, and language settings. The current Microsoft documentation accepts desktop screenshots at 1366 × 768 or larger; follow the exact size and format validator in Partner Center. Do not upload mockups as if they were app screenshots.
5. Complete the age-rating questionnaire truthfully, including user-created text/images and communication features. The final rating is generated by the questionnaire; do not guess a rating from this document.
6. Use the local-first instructions below in **Notes for certification**. Local functionality needs no credentials. Keep dedicated online reviewer credentials only in the existing private fields, and verify optional online sign-in, access, synchronization, and attachments separately. Never put credentials in GitHub or public listings. Do not claim the previous discovery failure was resolved unless evidence establishes that.
7. Run the Windows App Certification Kit and installation/upgrade checks on a suitable test device. Review remaining Partner Center warnings, then choose the intended release timing and submit for certification.

The build script produces an unsigned upload package. Microsoft signs accepted Store packages; purchasing a signing certificate is not required just to submit MSIX to the Store. Separate signing is required for ordinary direct sideload distribution.

## Public reviewer instructions / 可公开的审核步骤模板

> Version 1.0.1.0 addresses the blocked primary workflow in the prior review by providing complete local use without an account, network access, or a server. On launch, select the primary “Use on this computer” button (Chinese: “本机使用”), not the optional “Sign in with Heimdall” action. Create a project, report a bug with Markdown, switch Board/List and filters, edit it, change status, add a reply/work log, add and preview or download a harmless attachment, and inspect history. Quit and reopen: the welcome page remains available, and choosing local use again restores the local data. Local records and attachment bodies remain on the device, separate from online workspaces, without automatic upload or merging. Settings → General → Language switches between English and 简体中文. Closing may minimize to the tray according to the saved preference; use Quit to end the process. Copying a repair prompt only copies text, without calling an AI model. Online collaboration is optional, requires network/service access, and is independently testable using the dedicated credentials supplied privately. The native desktop process, windows, tray, file pick/save and secure storage need `runFullTrust`. See `reviewer-notes.en.txt` for the paste-ready notes; do not add credentials to public files.

> 1.0.1.0 通过完整本机模式解除上次审核中核心功能被登录阻断的问题。启动后选择主按钮“本机使用”，无需账号、联网或部署服务；“使用 Heimdall 登录”为可选在线入口。创建项目和 BUG，检查 Markdown、看板/列表、搜索筛选、编辑、状态流转、回复/处理记录、附件添加及预览下载、操作历史。完全退出并重启后仍显示欢迎页，再选本机使用可继续读取记录和附件。本机数据与在线工作区分离，不自动上传或合并。设置中可切换中英文，托盘与退出行为依保存偏好执行。复制提示词仅复制文本，不调用 AI。可选在线协作需联网及服务可用，专用账号另存私密字段，独立验证。原生窗口、托盘、文件选择保存和安全存储需要 `runFullTrust`。

## Current Microsoft references / 官方参考

- [Create an app submission](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/create-app-submission?pivots=store-installer-msix)
- [Support and privacy information](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/support-info)
- [MSIX package requirements](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements)
- [Screenshots and images](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images)
- [Microsoft Store policies](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)

Prepared October 17, 2026. Partner Center announces a policy update effective October 22, 2026; review the applicable version when submitting. Build success and these materials do not mean certification has passed.
