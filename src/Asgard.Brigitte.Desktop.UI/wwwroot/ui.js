window.brigitte = {
  attachPaste(element, receiver) {
    element.addEventListener("paste", async (event) => {
      event.preventDefault();
      for (const item of event.clipboardData?.items ?? []) {
        if (item.kind !== "file") continue;
        const file = item.getAsFile();
        if (!file || file.size > 50 * 1024 * 1024) continue;
        const data = await new Promise((resolve) => {
          const reader = new FileReader();
          reader.onload = () => resolve(reader.result);
          reader.readAsDataURL(file);
        });
        await receiver.invokeMethodAsync(
          "Pasted",
          file.name || "clipboard.bin",
          file.type || "application/octet-stream",
          data.slice(data.indexOf(",") + 1),
        );
      }
      element.textContent = "在此粘贴图片或文件";
    });
  },
};

// 菜单使用顶层浮层，避免被泳道及窗口滚动容器裁切。
document.addEventListener("beforetoggle", (event) => {
  const menu = event.target;
  if (!menu.matches?.(".prompt-dropdown") || event.newState !== "open") return;
  const trigger = document.getElementById(menu.dataset.promptTrigger);
  if (!trigger) return;
  const anchor = trigger.getBoundingClientRect();
  // 展示前同步测量和定位，避免首帧出现在窗口左上角。
  menu.style.display = "block";
  const bounds = menu.getBoundingClientRect();
  menu.style.removeProperty("display");
  const left = Math.max(8, Math.min(anchor.right - bounds.width, window.innerWidth - bounds.width - 8));
  const below = anchor.bottom + 6;
  const top = below + bounds.height <= window.innerHeight - 8
    ? below
    : Math.max(8, anchor.top - bounds.height - 6);
  menu.style.left = `${left}px`;
  menu.style.top = `${top}px`;
}, true);

// 保留原生 Escape、点击外部关闭及焦点恢复，补充菜单方向键操作。
document.addEventListener("keydown", (event) => {
  const trigger = event.target.closest?.(".prompt-trigger");
  if (trigger && ["ArrowDown", "ArrowUp"].includes(event.key)) {
    event.preventDefault();
    const menu = document.getElementById(trigger.getAttribute("popovertarget"));
    menu?.showPopover();
    if (event.key === "ArrowUp") menu?.querySelector("button:last-child")?.focus({ preventScroll: true });
    return;
  }
  const menu = event.target.closest?.(".prompt-dropdown");
  if (!menu || !["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)) return;
  event.preventDefault();
  const items = [...menu.querySelectorAll("button")];
  const index = items.indexOf(document.activeElement);
  const next = event.key === "Home" ? 0 : event.key === "End" ? items.length - 1
    : (index + (event.key === "ArrowDown" ? 1 : -1) + items.length) % items.length;
  items[next]?.focus();
});

// 滚动看板或改变窗口大小后收起菜单，避免菜单脱离所属卡片。
const closePromptMenus = (event) => {
  if (event.type === "scroll" && event.target.closest?.(".prompt-dropdown")) return;
  document.querySelectorAll(".prompt-dropdown:popover-open").forEach(menu => menu.hidePopover());
};
document.addEventListener("scroll", closePromptMenus, true);
window.addEventListener("resize", closePromptMenus);
