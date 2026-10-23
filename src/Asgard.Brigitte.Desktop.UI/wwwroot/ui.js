// 首屏先随浏览器的系统语言，原生宿主恢复偏好后以工作区的 lang 为准。
const bootLanguage = (navigator.languages?.[0] || navigator.language || "en").toLowerCase().startsWith("zh") ? "zh-CN" : "en";
document.documentElement.lang = bootLanguage;
const bootMessage = document.querySelector("[data-boot-message]");
const bootDetail = document.querySelector("[data-boot-detail]");
if (bootMessage) bootMessage.textContent = bootLanguage === "zh-CN" ? "正在点亮圣火…" : "Lighting the flame…";
if (bootDetail) bootDetail.textContent = bootLanguage === "zh-CN" ? "正在启动 Asgard Brigitte" : "Starting Asgard Brigitte";
const appRoot = document.getElementById("app");
if (appRoot) new MutationObserver(() => {
  const language = appRoot.querySelector(".client[lang]")?.lang;
  if (language && document.documentElement.lang !== language) document.documentElement.lang = language;
}).observe(appRoot, { subtree: true, childList: true, attributes: true, attributeFilter: ["lang"] });

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
      element.textContent = element.dataset.pastePlaceholder || "Paste an image or file here";
    });
  },
};

// Hybrid 宿主可能拦截 HTML 原生拖放；标题改用指针捕获，卡片和附件不参与排序。
const laneOrderCleanup = new WeakMap();
window.brigitte.attachLaneOrder = (board, receiver) => {
  laneOrderCleanup.get(board)?.();
  const controller = new AbortController();
  const options = { signal: controller.signal };
  let source = null, target = null, after = false, pointerX = 0, pointerY = 0, frame = 0, lastTime = 0;
  let handle = null, pointerId = null, startX = 0, startY = 0, grabX = 0, grabY = 0, dragging = false, preview = null;
  const clearTarget = () => {
    target?.classList.remove("lane-drop-before", "lane-drop-after");
    target = null;
  };
  const finish = () => {
    cancelAnimationFrame(frame);
    frame = 0;
    lastTime = 0;
    source?.classList.remove("lane-dragging");
    source = null;
    const capturedHandle = handle, capturedPointer = pointerId;
    handle = null;
    pointerId = null;
    dragging = false;
    preview?.remove();
    preview = null;
    if (capturedHandle?.hasPointerCapture(capturedPointer)) capturedHandle.releasePointerCapture(capturedPointer);
    board.classList.remove("is-reordering");
    clearTarget();
  };
  // 根据当前指针重新测量，滚动时落点仍与实际列对齐。
  const locate = () => {
    clearTarget();
    const bounds = board.getBoundingClientRect();
    if (pointerX < Math.max(0, bounds.left) || pointerX > Math.min(innerWidth, bounds.right)
      || pointerY < Math.max(0, bounds.top) || pointerY > Math.min(innerHeight, bounds.bottom)) return;
    const lane = document.elementFromPoint(pointerX, pointerY)?.closest("[data-lane]");
    if (!lane || lane === source || lane.parentElement !== board) return;
    target = lane;
    const laneBounds = lane.getBoundingClientRect();
    after = pointerX >= laneBounds.left + laneBounds.width / 2;
    lane.classList.add(after ? "lane-drop-after" : "lane-drop-before");
  };
  // 接近可视看板边缘时连续滚动，支持跨越屏幕外的泳道。
  const scroll = (time) => {
    if (!dragging) return;
    const bounds = board.getBoundingClientRect();
    const left = Math.max(0, bounds.left), right = Math.min(innerWidth, bounds.right);
    const inside = pointerY >= bounds.top && pointerY <= Math.min(innerHeight, bounds.bottom) && pointerX >= left && pointerX <= right;
    const speed = !inside ? 0 : pointerX < left + 64 ? -Math.min(1, (left + 64 - pointerX) / 64)
      : pointerX > right - 64 ? Math.min(1, (pointerX - right + 64) / 64) : 0;
    const elapsed = lastTime ? Math.min(32, time - lastTime) : 16;
    lastTime = time;
    if (speed) { board.scrollLeft += speed * elapsed * 0.8; locate(); }
    frame = requestAnimationFrame(scroll);
  };
  board.addEventListener("pointerdown", (event) => {
    const nextHandle = event.target.closest?.(".lane-drag-handle");
    if (!nextHandle || !event.isPrimary || event.button !== 0 || pointerId !== null) return;
    finish();
    handle = nextHandle;
    source = handle.closest("[data-lane]");
    pointerId = event.pointerId;
    startX = pointerX = event.clientX;
    startY = pointerY = event.clientY;
    const bounds = source.getBoundingClientRect();
    grabX = pointerX - bounds.left;
    grabY = pointerY - bounds.top;
    handle.setPointerCapture(pointerId);
  }, options);
  board.addEventListener("pointermove", (event) => {
    if (event.pointerId !== pointerId) return;
    pointerX = event.clientX;
    pointerY = event.clientY;
    if (!dragging) {
      if (Math.hypot(pointerX - startX, pointerY - startY) < 5) return;
      dragging = true;
      source.classList.add("lane-dragging");
      board.classList.add("is-reordering");
      const bounds = source.getBoundingClientRect();
      // 整列影像保留标题和卡片；不复制可交互身份，避免遮挡落点或重复 DOM ID。
      preview = source.cloneNode(true);
      preview.className = "lane lane-drag-preview";
      preview.removeAttribute("data-lane");
      preview.querySelectorAll("[id]").forEach(element => element.removeAttribute("id"));
      preview.inert = true;
      preview.style.width = `${bounds.width}px`;
      preview.style.height = `${bounds.height}px`;
      preview.setAttribute("aria-hidden", "true");
      board.append(preview);
      preview.querySelector(".lane-scroll").scrollTop = source.querySelector(".lane-scroll").scrollTop;
      frame = requestAnimationFrame(scroll);
    }
    event.preventDefault();
    preview.style.left = `${pointerX - grabX}px`;
    preview.style.top = `${pointerY - grabY}px`;
    locate();
  }, options);
  board.addEventListener("pointerup", (event) => {
    if (event.pointerId !== pointerId) return;
    pointerX = event.clientX;
    pointerY = event.clientY;
    if (dragging) { event.preventDefault(); locate(); }
    const from = source.dataset.lane, to = target?.dataset.lane, insertAfter = after;
    finish();
    if (to) receiver.invokeMethodAsync("MoveLane", from, to, insertAfter);
  }, options);
  const cancelPointer = (event) => { if (event.pointerId === pointerId) finish(); };
  board.addEventListener("pointercancel", cancelPointer, options);
  board.addEventListener("lostpointercapture", cancelPointer, options);
  board.addEventListener("dragstart", (event) => {
    if (event.target.closest?.(".lane-drag-handle")) event.preventDefault();
  }, options);
  window.addEventListener("blur", finish, options);
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && source) { event.preventDefault(); finish(); }
  }, options);
  // 标题按钮保留键盘入口，省去独立排序面板及重复的上下箭头。
  board.addEventListener("keydown", (event) => {
    const handle = event.target.closest?.(".lane-drag-handle");
    if (!handle || !event.altKey || !["ArrowLeft", "ArrowRight"].includes(event.key)) return;
    event.preventDefault();
    const lanes = [...board.querySelectorAll(":scope > [data-lane]")];
    const from = handle.closest("[data-lane]");
    const direction = event.key === "ArrowLeft" ? -1 : 1;
    const to = lanes[lanes.indexOf(from) + direction];
    if (to) receiver.invokeMethodAsync("MoveLane", from.dataset.lane, to.dataset.lane, direction > 0);
  }, options);
  laneOrderCleanup.set(board, () => { finish(); controller.abort(); laneOrderCleanup.delete(board); });
};
window.brigitte.detachLaneOrder = (board) => laneOrderCleanup.get(board)?.();

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
