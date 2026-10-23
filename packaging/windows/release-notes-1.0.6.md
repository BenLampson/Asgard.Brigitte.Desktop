# 1.0.6.0 商店更新说明

## 简体中文

- 直接拖动看板泳道标题调整顺序，移除独立的上下箭头排序面板。
- 拖拽时显示插入位置，靠近看板左右边缘时自动滚动。
- 标题支持 Alt＋左右方向键调整顺序，工具栏可恢复默认顺序。
- 沿用本机、账号与项目的已有排序偏好；状态筛选不丢失其他泳道的顺序。
- 历史状态迁移后隐藏空历史泳道和筛选项；仍有本机历史记录时继续显示。

## English (United States)

- Reorder board lanes by dragging their headers, replacing the separate arrow-based panel.
- See the insertion position while dragging and scroll automatically near the board edges.
- Use Alt + Left / Right on a lane header, or reset the order from the toolbar.
- Keep existing device, account and project preferences, including lanes hidden by status filters.
- Hide empty legacy lanes and filters after migration while retaining access to existing local records.

## 发布范围

本次为桌面客户端界面更新，不需要服务部署或本机 SQLite 结构迁移。客户端不自动转换或删除历史 BUG；单独授权的线上数据迁移由管理员另行执行。
版本号为 1.0.6 / 1.0.6.0，已正式提交商店认证，通过后立即发布；当前尚未确认上架。

本地验证与发布记录见 [1.0.6 记录](../../docs/releases/v1.0.6.md)：195 项客户端测试通过，真实组件拖拽和偏好保存验证通过。商店状态以该记录的最终提交结果为准。
