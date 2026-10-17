# Microsoft Store submission for Asgard Brigitte

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

Reserved and submitted for certification on October 17, 2026. The product is in certification, not yet published or certified. The assigned [Store URL](https://apps.microsoft.com/detail/9N5X2FD6C0XC) becomes useful after publication; do not advertise it as already available.

## Submission status / 提交状态

Updated October 17, 2026:

- **Price:** free (`0`) in all selected markets. Public discovery and publication after certification are selected.
- **Package:** `1.0.0.0`, x64, uploaded to Partner Center and accepted by package validation. The `runFullTrust` capability remains subject to Microsoft approval.
- **Store listings:** English (United States) and Chinese (Simplified), with descriptions, features, keywords, license information, and four 1600 × 900 PNG screenshots per language.
- **Privacy and support:** public GitHub URLs, with private privacy requests directed to the policy email.
- **Age ratings:** the completed questionnaire was saved after the publisher confirmed the IARC terms and legal-age declaration. The generated ratings include Microsoft/IARC 12+, ESRB Teen, and PEGI Parental Guidance. The certificate ID is pending in Partner Center.
- **Certification:** submitted on October 17, 2026. Partner Center shows **In certification**, with submission complete and pre-processing in progress. Publication is configured to start automatically after approval. The app is not yet certified or available in Microsoft Store.
- **Reviewer access:** an ordinary dedicated account and synthetic workspace are saved in Partner Center's private Credentials and Additional Testing Info sections. Service sign-in, project and bug creation, a reply, and a harmless attachment were exercised. Reviewer credentials must never be added to this repository.

已设置免费；正式 MSIX、中英文商品页、每种语言四张截图和年龄分级均已提交。发布者已确认 IARC 条款与法定成年声明。合作伙伴中心显示“正在认证”，当前处于预处理阶段；通过认证后自动发布，目前尚未上架。专用普通审核账号、演示项目与操作说明均已私密保存。

Screenshots are captured from the release's actual Razor components and production styles using fictional local fixture data. They do not expose a real customer's workspace. The screenshot renderer is a temporary development tool and is not included in the app. Browse [English screenshots](../../docs/screenshots/en-US) and [中文截图](../../docs/screenshots/zh-CN), or download the [bilingual screenshot ZIP](https://github.com/BenLampson/Asgard.Brigitte.Desktop/releases/download/v1.0.0/Asgard-Brigitte-1.0.0-store-screenshots.zip).

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
| System requirements / 系统要求 | Windows 10 version 1809 or later, x64; Microsoft Edge WebView2 Runtime; internet and a compatible service account |

Do not invent telephone numbers, company addresses, hardware requirements, or mandatory consumer-law declarations. Enter account/legal information privately in Partner Center when it is actually required.

The client copies prompts and manages MCP credentials; it does not itself generate AI output. Answer any generative-AI declaration according to the wording shown by Partner Center and the actual submitted behavior. Do not declare camera, microphone, location, mixed-reality, Xbox, or broadcast requirements that the app does not use. The desktop package declares `runFullTrust` to run the MAUI/WinUI desktop process and provide windows, secure storage, a tray, local cache, file selection/save, and browser sign-in.

## Submission steps / 提交顺序

1. In the **Asgard Brigitte** product, start a submission. The intended price is **free**, with public availability in all selected markets. A source license does not determine the Store selling price.
2. Complete Properties with the URLs above. Add **English (United States)** and **Chinese (Simplified)** Store listings, using the two listing files. The product name stays Asgard Brigitte in both.
3. Build with the registered identity: `pwsh -NoProfile -File packaging/windows/publish-store.ps1 -SettingsPath packaging/windows/store-settings.public.json`. Upload the resulting `.msix` from `publish/store/1.0.0.0/packages/`. Never upload a preview-identity package.
4. Supply real screenshots captured from this release in both languages using synthetic data. At least one desktop screenshot per listing is required; a useful set is board, details, editor, project invitations, and language settings. The current Microsoft documentation accepts desktop screenshots at 1366 × 768 or larger; follow the exact size and format validator in Partner Center. Do not upload mockups as if they were app screenshots.
5. Complete the age-rating questionnaire truthfully, including user-created text/images and communication features. The final rating is generated by the questionnaire; do not guess a rating from this document.
6. Provide a dedicated reviewer account and any invitation/password needed to reach a synthetic test project in the private **Notes for certification** field. Verify sign-in, project access, sync, attachments, and both languages before submitting. Never put these credentials in GitHub or public listing text.
7. Run the Windows App Certification Kit and installation/upgrade checks on a suitable test device. Review remaining Partner Center warnings, then choose the intended release timing and submit for certification.

The build script produces an unsigned upload package. Microsoft signs accepted Store packages; purchasing a signing certificate is not required just to submit MSIX to the Store. Separate signing is required for ordinary direct sideload distribution.

## Public reviewer instructions / 可公开的审核步骤模板

> Asgard Brigitte requires an online Brigitte service and a Heimdall account. Dedicated test credentials and any invitation are supplied separately in the private certification notes. Launch the app, choose “Sign in with Heimdall”, complete browser sign-in, and return to the app. Wait for synchronization. Open the supplied synthetic project, switch between Board and List, view a bug, add a reply, upload a harmless sample attachment, and check its history. Use Settings → General → Language to switch between English and 简体中文; the default is Follow system. The tray remains active after minimizing and stops after Quit. Copying a prompt only copies text and does not invoke an AI model. Privacy and GitHub feedback links are available before sign-in. `runFullTrust` is needed for the native desktop process and integrations described above.

> Asgard Brigitte 需要在线 Brigitte 服务与 Heimdall 账号。专用测试账号和邀请信息另行填写在私密审核备注中。启动后选择“使用 Heimdall 登录”，在浏览器完成登录并返回，等待同步。打开测试项目，切换看板和列表，查看 BUG、添加回复、上传无敏感内容的示例附件并查看历史。在“设置 → 普通设置 → 语言”切换 English 与简体中文，默认跟随系统。最小化后托盘继续运行，退出程序后停止。复制提示词只复制文本，不调用 AI 模型。登录前即可访问隐私说明和 GitHub 反馈入口。原生桌面进程及上述系统集成需要 `runFullTrust`。

## Current Microsoft references / 官方参考

- [Create an app submission](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/create-app-submission?pivots=store-installer-msix)
- [Support and privacy information](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/support-info)
- [MSIX package requirements](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements)
- [Screenshots and images](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images)
- [Microsoft Store policies](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)

Prepared October 17, 2026. Partner Center announces a policy update effective October 22, 2026; review the applicable version when submitting. Build success and these materials do not mean certification has passed.
