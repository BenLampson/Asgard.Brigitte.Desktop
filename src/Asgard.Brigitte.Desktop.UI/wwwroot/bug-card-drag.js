// 使用指针事件兼容 Hybrid WebView；拖动期间不执行任何业务请求。
const cleanups = new WeakMap();
export function attach(board, receiver) {
  detach(board);
  const controller = new AbortController(), options = { signal: controller.signal };
  let card = null, target = null, preview = null, pointer = null, dragging = false, pending = false;
  let startX = 0, startY = 0, x = 0, y = 0, offsetX = 0, offsetY = 0, frame = 0, lastTime = 0, suppressUntil = 0;
  let from = "", targets = [];
  const enabled = () => board.isConnected && board.dataset.bugMoveEnabled === "true" && !pending;
  const lanes = () => [...board.querySelectorAll(":scope > [data-lane]")];
  const finish = () => {
    cancelAnimationFrame(frame); frame = lastTime = 0;
    if (dragging) suppressUntil = performance.now() + 500;
    card?.classList.remove("bug-card-dragging");
    lanes().forEach(lane => lane.classList.remove("bug-drop-allowed", "bug-drop-active"));
    preview?.remove(); preview = null; target = null;
    const captured = card, id = pointer;
    card = null; pointer = null; dragging = false;
    board.classList.remove("is-moving-bug");
    if (captured?.hasPointerCapture(id)) captured.releasePointerCapture(id);
  };
  const locate = () => {
    target?.classList.remove("bug-drop-active"); target = null;
    const bounds = board.getBoundingClientRect();
    if (x < Math.max(0, bounds.left) || x > Math.min(innerWidth, bounds.right)
      || y < Math.max(0, bounds.top) || y > Math.min(innerHeight, bounds.bottom)) return;
    const lane = document.elementFromPoint(x, y)?.closest("[data-lane]");
    if (lane?.parentElement === board && targets.includes(lane.dataset.lane)) {
      target = lane; target.classList.add("bug-drop-active");
    }
  };
  // 水平方向寻找屏幕外泳道，垂直方向滚动落点列；每帧重新命中实际 DOM。
  const scroll = time => {
    if (!dragging) return;
    if (!enabled() || !card.isConnected || !card.dataset.bugTargets || card.dataset.bugStatus !== from) { finish(); return; }
    const bounds = board.getBoundingClientRect();
    const left = Math.max(0, bounds.left), right = Math.min(innerWidth, bounds.right);
    const top = Math.max(0, bounds.top), bottom = Math.min(innerHeight, bounds.bottom);
    const delta = lastTime ? Math.min(32, time - lastTime) : 16; lastTime = time;
    const speed = (value, min, max) => value < min + 52 ? -Math.min(1, (min + 52 - value) / 52)
      : value > max - 52 ? Math.min(1, (value - max + 52) / 52) : 0;
    if (x >= left && x <= right && y >= top && y <= bottom) {
      board.scrollLeft += speed(x, left, right) * delta * .8;
      locate();
      const column = target?.querySelector(".lane-scroll");
      if (column) { const rect = column.getBoundingClientRect(); column.scrollTop += speed(y, rect.top, Math.min(bottom, rect.bottom)) * delta * .7; }
    }
    locate(); frame = requestAnimationFrame(scroll);
  };
  board.addEventListener("pointerdown", event => {
    suppressUntil = 0;
    if (!enabled() || !event.isPrimary || event.button !== 0 || pointer !== null) return;
    // 复制按钮、提示词菜单和可编辑内容保持原来的点击行为。
    if (event.target.closest?.("button,a,input,textarea,select,[contenteditable],.prompt-dropdown")) return;
    const next = event.target.closest?.(".bug-card[data-bug-id]");
    if (!next || !board.contains(next) || !next.dataset.bugTargets) return;
    card = next; pointer = event.pointerId;
    from = card.dataset.bugStatus; targets = card.dataset.bugTargets.split(",");
    startX = x = event.clientX; startY = y = event.clientY;
    const bounds = card.getBoundingClientRect(); offsetX = x - bounds.left; offsetY = y - bounds.top;
    card.setPointerCapture(pointer);
  }, options);
  board.addEventListener("pointermove", event => {
    if (event.pointerId !== pointer) return;
    if (!enabled() || !card.isConnected) { finish(); return; }
    x = event.clientX; y = event.clientY;
    if (!dragging) {
      if (Math.hypot(x - startX, y - startY) < 6) return;
      dragging = true; card.classList.add("bug-card-dragging"); board.classList.add("is-moving-bug");
      document.getSelection()?.removeAllRanges();
      lanes().filter(lane => targets.includes(lane.dataset.lane)).forEach(lane => lane.classList.add("bug-drop-allowed"));
      preview = card.cloneNode(true); preview.className = "bug-card bug-card-drag-preview";
      for (const element of [preview, ...preview.querySelectorAll("[id],[data-bug-id]")]) {
        element.removeAttribute("id"); element.removeAttribute("data-bug-id");
      }
      preview.inert = true; preview.setAttribute("aria-hidden", "true");
      preview.style.width = `${card.getBoundingClientRect().width}px`; board.append(preview);
      frame = requestAnimationFrame(scroll);
    }
    event.preventDefault();
    preview.style.left = `${x - offsetX}px`; preview.style.top = `${y - offsetY}px`; locate();
  }, options);
  board.addEventListener("pointerup", event => {
    if (event.pointerId !== pointer) return;
    x = event.clientX; y = event.clientY;
    if (dragging) { event.preventDefault(); locate(); }
    const id = card.dataset.bugId, source = from, to = dragging && enabled() && card.isConnected ? target?.dataset.lane : null;
    finish();
    if (to) {
      pending = true;
      receiver.invokeMethodAsync("MoveBug", id, source, to)
        .catch(error => console.error("BUG move failed", error)).finally(() => { pending = false; });
    }
  }, options);
  const cancelPointer = event => { if (event.pointerId === pointer) finish(); };
  board.addEventListener("pointercancel", cancelPointer, options);
  board.addEventListener("lostpointercapture", cancelPointer, options);
  board.addEventListener("dragstart", event => { if (card) event.preventDefault(); }, options);
  // 放下和 Escape 取消后的合成 click 不应打开 BUG 详情。
  board.addEventListener("click", event => {
    if (performance.now() < suppressUntil) { event.preventDefault(); event.stopImmediatePropagation(); suppressUntil = 0; }
  }, { ...options, capture: true });
  document.addEventListener("keydown", event => {
    if (event.key === "Escape" && card) { event.preventDefault(); finish(); }
  }, options);
  window.addEventListener("blur", finish, options);
  board.dataset.bugDragReady = "true";
  cleanups.set(board, () => { finish(); controller.abort(); delete board.dataset.bugDragReady; cleanups.delete(board); });
}
export function detach(board) { cleanups.get(board)?.(); }

// 原生 dialog 保留键盘焦点、Escape 和系统可访问性语义。
export function openReasonDialog(dialog, receiver) {
  if (!dialog.isConnected) return;
  dialog.addEventListener("cancel", event => { event.preventDefault(); receiver.invokeMethodAsync("Dismiss"); });
  dialog.showModal();
}
