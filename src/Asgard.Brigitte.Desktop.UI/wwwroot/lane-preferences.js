const active = new WeakMap();

// 垂直列表使用指针捕获；拖动靠近滚动区域边缘时持续滚动。
export function attach(list, receiver) {
  detach(list);
  const controller = new AbortController(), options = { signal: controller.signal };
  let source = null, target = null, handle = null, pointerId = null, projectId = "", after = false;
  let x = 0, y = 0, startX = 0, startY = 0, grabY = 0, dragging = false, ghost = null, frame = 0, lastTime = 0;
  const clearTarget = () => { target?.classList.remove("drop-before", "drop-after"); target = null; };
  const finish = () => {
    cancelAnimationFrame(frame); frame = 0; lastTime = 0;
    source?.classList.remove("dragging"); source = null; clearTarget(); ghost?.remove(); ghost = null;
    const previousHandle = handle, previousPointer = pointerId;
    handle = null; pointerId = null; dragging = false;
    if (previousHandle?.hasPointerCapture(previousPointer)) previousHandle.releasePointerCapture(previousPointer);
    list.classList.remove("is-reordering");
  };
  const locate = () => {
    clearTarget();
    const bounds = list.getBoundingClientRect();
    if (x < bounds.left || x > bounds.right || y < Math.max(0, bounds.top) || y > Math.min(innerHeight, bounds.bottom)) return;
    const row = document.elementFromPoint(x, y)?.closest(".lane-pref-row");
    if (!row || row === source || row.parentElement !== list) return;
    target = row; const rect = row.getBoundingClientRect(); after = y >= rect.top + rect.height / 2;
    row.classList.add(after ? "drop-after" : "drop-before");
  };
  const scroll = time => {
    if (!dragging) return;
    const bounds = list.getBoundingClientRect(), top = Math.max(0, bounds.top), bottom = Math.min(innerHeight, bounds.bottom);
    const inside = x >= bounds.left && x <= bounds.right && y >= top && y <= bottom;
    const speed = !inside ? 0 : y < top + 60 ? -Math.min(1, (top + 60 - y) / 60) : y > bottom - 60 ? Math.min(1, (y - bottom + 60) / 60) : 0;
    const elapsed = lastTime ? Math.min(32, time - lastTime) : 16; lastTime = time;
    if (speed) { list.scrollTop += speed * elapsed * 0.8; locate(); }
    frame = requestAnimationFrame(scroll);
  };
  list.addEventListener("pointerdown", event => {
    const next = event.target.closest?.(".lane-pref-grip");
    if (!next || !event.isPrimary || event.button !== 0 || pointerId !== null) return;
    finish(); handle = next; source = next.closest(".lane-pref-row"); pointerId = event.pointerId; projectId = list.dataset.project || "";
    startX = x = event.clientX; startY = y = event.clientY; grabY = y - source.getBoundingClientRect().top;
    handle.setPointerCapture(pointerId);
  }, options);
  list.addEventListener("pointermove", event => {
    if (event.pointerId !== pointerId) return;
    x = event.clientX; y = event.clientY;
    if (!dragging) {
      if (Math.hypot(x - startX, y - startY) < 5) return;
      dragging = true; source.classList.add("dragging"); list.classList.add("is-reordering");
      const bounds = source.getBoundingClientRect(); ghost = source.cloneNode(true); ghost.className = "lane-pref-row lane-pref-ghost";
      ghost.removeAttribute("data-status"); ghost.inert = true; ghost.setAttribute("aria-hidden", "true");
      ghost.style.width = `${bounds.width}px`; ghost.style.left = `${bounds.left}px`; list.append(ghost);
      frame = requestAnimationFrame(scroll);
    }
    event.preventDefault(); ghost.style.top = `${y - grabY}px`; locate();
  }, options);
  list.addEventListener("pointerup", event => {
    if (event.pointerId !== pointerId) return;
    x = event.clientX; y = event.clientY; if (dragging) { event.preventDefault(); locate(); }
    const from = source?.dataset.status, to = target?.dataset.status, insertAfter = after, project = projectId;
    finish(); if (from && to) receiver.invokeMethodAsync("MoveLane", project, from, to, insertAfter);
  }, options);
  const cancel = event => { if (event.pointerId === pointerId) finish(); };
  list.addEventListener("pointercancel", cancel, options); list.addEventListener("lostpointercapture", cancel, options);
  list.addEventListener("dragstart", event => { if (event.target.closest?.(".lane-pref-grip")) event.preventDefault(); }, options);
  list.addEventListener("keydown", event => {
    const grip = event.target.closest?.(".lane-pref-grip");
    if (!grip || !event.altKey || !["ArrowUp", "ArrowDown"].includes(event.key)) return;
    event.preventDefault(); const rows = [...list.querySelectorAll(":scope > [data-status]")], from = grip.closest(".lane-pref-row");
    const direction = event.key === "ArrowUp" ? -1 : 1, to = rows[rows.indexOf(from) + direction];
    if (to) receiver.invokeMethodAsync("MoveLane", list.dataset.project || "", from.dataset.status, to.dataset.status, direction > 0);
  }, options);
  window.addEventListener("blur", finish, options);
  document.addEventListener("keydown", event => { if (event.key === "Escape" && source) { event.preventDefault(); finish(); } }, options);
  list.dataset.laneDragReady = "true";
  active.set(list, () => { finish(); controller.abort(); delete list.dataset.laneDragReady; active.delete(list); });
}

export function detach(list) { active.get(list)?.(); }
