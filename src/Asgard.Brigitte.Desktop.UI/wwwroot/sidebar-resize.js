// 只保存设备上的界面宽度，不保存账号或项目数据。
const storageKey = "brigitte.sidebar.width";
const minimum = 196;
const maximum = 520;

export function attach(handle) {
  const workspace = handle?.closest(".workspace");
  const sidebar = workspace?.querySelector(".sidebar");
  if (!workspace || !sidebar) return { dispose() {} };

  let preferred = minimum;
  let drag = null;
  try {
    const saved = localStorage.getItem(storageKey);
    const value = Number(saved);
    if (saved !== null && Number.isFinite(value)) preferred = Math.max(minimum, Math.min(maximum, value));
  } catch { /* 存储不可用时仍可拖动，本次窗口使用默认宽度。 */ }

  // 保留看板筛选栏所需空间，窗口放大后恢复用户之前选择的宽度。
  const upperBound = () => Math.max(minimum, Math.min(maximum, workspace.clientWidth - 760));
  const clamp = (value) => Math.max(minimum, Math.min(upperBound(), Math.round(value)));
  const render = () => {
    const width = clamp(preferred);
    workspace.style.setProperty("--sidebar-width", `${width}px`);
    handle.setAttribute("aria-valuemax", String(upperBound()));
    handle.setAttribute("aria-valuenow", String(width));
    handle.setAttribute("aria-valuetext", `${width} 像素`);
  };
  const persist = () => {
    try { localStorage.setItem(storageKey, String(preferred)); }
    catch { /* 宽度保存失败不阻断项目操作。 */ }
  };

  // 捕获指针后即使移出分隔线仍可连续拖动；不触发业务请求。
  const onPointerDown = (event) => {
    if (!event.isPrimary || event.button !== 0 || drag) return;
    event.preventDefault();
    handle.focus({ preventScroll: true });
    handle.setPointerCapture(event.pointerId);
    drag = { id: event.pointerId, x: event.clientX, width: sidebar.getBoundingClientRect().width, original: preferred };
    workspace.classList.add("sidebar-resizing");
  };
  const onPointerMove = (event) => {
    if (!drag || event.pointerId !== drag.id) return;
    preferred = clamp(drag.width + event.clientX - drag.x);
    render();
  };
  const onPointerEnd = (event) => {
    if (!drag || event.pointerId !== drag.id) return;
    const id = drag.id;
    if (event.type !== "pointerup") preferred = drag.original;
    drag = null;
    if (handle.hasPointerCapture(id)) handle.releasePointerCapture(id);
    workspace.classList.remove("sidebar-resizing");
    render();
    if (event.type === "pointerup") persist();
  };
  const cancelDrag = () => {
    if (drag) onPointerEnd({ pointerId: drag.id, type: "pointercancel" });
  };
  const reset = () => { preferred = minimum; render(); persist(); };

  // 键盘用户可直接聚焦分隔线，左右微调、Home/End 到达宽度边界。
  const onKeyDown = (event) => {
    if (event.key === "Escape" && drag) { event.preventDefault(); cancelDrag(); return; }
    if (drag || !["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
    event.preventDefault();
    const step = event.shiftKey ? 40 : 10;
    preferred = event.key === "Home" ? minimum : event.key === "End" ? upperBound()
      : clamp(clamp(preferred) + (event.key === "ArrowLeft" ? -step : step));
    render();
    persist();
  };

  handle.addEventListener("pointerdown", onPointerDown);
  handle.addEventListener("pointermove", onPointerMove);
  handle.addEventListener("pointerup", onPointerEnd);
  handle.addEventListener("pointercancel", onPointerEnd);
  handle.addEventListener("lostpointercapture", onPointerEnd);
  handle.addEventListener("dblclick", reset);
  handle.addEventListener("keydown", onKeyDown);
  window.addEventListener("blur", cancelDrag);
  const observer = new ResizeObserver(render);
  observer.observe(workspace);
  render();

  // 登出或切换到启动画面时移除所有监听，避免反复登录后重复绑定。
  return {
    dispose() {
      cancelDrag();
      observer.disconnect();
      handle.removeEventListener("pointerdown", onPointerDown);
      handle.removeEventListener("pointermove", onPointerMove);
      handle.removeEventListener("pointerup", onPointerEnd);
      handle.removeEventListener("pointercancel", onPointerEnd);
      handle.removeEventListener("lostpointercapture", onPointerEnd);
      handle.removeEventListener("dblclick", reset);
      handle.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("blur", cancelDrag);
    },
  };
}
