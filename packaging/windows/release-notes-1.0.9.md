# 1.0.9.0 商店更新说明

## 简体中文

- 无需切换模式，按住 BUG 卡片即可拖到目标状态泳道；支持落点高亮、卡片预览和边缘自动滚动。
- 每张卡片新增“查看详情”按钮，查看与拖动各有明确入口；复制 ID 和修复提示词操作继续可用。
- 拖动提交自动领取临时锁、修改状态并释放；已有的本人手工锁保留，他人持锁或只读时禁止拖动。
- 部署、验收、关闭等必填说明与人工验收确认沿用现有流程；取消拖动或填写说明不会领取锁。
- 状态提交失败仍尝试释放自动锁；释放失败提示持续保留，临时锁最长一分钟到期。

## English (United States)

- Drag bug cards directly into status lanes, with target highlights, a card preview and automatic edge scrolling.
- Open a bug through its dedicated View details button. Copying bug IDs and repair prompts remains separate from dragging.
- Status moves automatically claim, update and release a temporary lock. Existing manual claims stay held; foreign locks and read-only mode prevent dragging.
- Required explanations, deployment steps and explicit human verification remain in place. Cancelling a drag or explanation does not claim a lock.
- Failed updates still attempt to release automatic locks. Release errors remain visible, and temporary locks expire within one minute.

## 发布记录

版本为 `1.0.9` / `1.0.9.0`。221 项客户端测试、Windows Release 构建和真实组件浏览器交互检查通过。商店状态以 [提交记录](store-submission.md) 的最终结果为准。
