# 1.0.10.0 商店更新说明

## 简体中文

- 优化修复提示词：先用简短易懂的摘要说明处理进展，再保留完整的技术交接材料，方便用户阅读和后续人员接手。
- 修复 Markdown 普通回车被合并显示的问题；问题正文、回复、处理记录、状态历史和编辑预览均保留换行，标题、列表、表格和代码块继续正常显示。
- 支持将 BUG 卡片从任意状态直接拖入“已关闭”泳道，状态窗口也可直接关闭；仍需填写关闭原因并遵守处理锁和访问权限。关闭不会自动标记人工验收通过。

## English (United States)

- Improved repair prompts request a short, plain-language summary followed by complete technical handoff details, making progress easier to read while preserving the information needed by the next person.
- Markdown now preserves ordinary line breaks in bug descriptions, replies, work logs, status history and editor previews, while keeping headings, lists, tables and code blocks intact.
- Drag bug cards directly into the Closed lane from any status, or close them from the status window, with a required explanation and the existing work-lock and access checks. Closing does not automatically mark human verification as passed.

## 发布记录

246 项客户端测试、Windows 商店包与便携版构建、包身份及程序集校验通过，源码 CI 通过。`1.0.10.0` 已通过 Partner Center 包验证并正式提交 Submission 10，当前正在认证／预处理，通过后立即发布；尚不代表已上架。商店详细状态见[提交记录](store-submission.md)。
