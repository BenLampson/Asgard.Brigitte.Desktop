// Run with playwright-cli run-code --filename after opening the loopback preview.
async (page) => {
  page.setDefaultTimeout(10000);
  const step = name => page.evaluate(name => console.info('bug-card-test: ' + name), name);
  const assert = (value, message) => { if (!value) throw new Error(message); };
  const state = async () => (await page.request.get('http://127.0.0.1:8766/test/state')).json();
  const data = await state();
  const opened = data.opened, copies = data.copies, confirmations = data.confirmations;
  const waitCount = async (key, count) => page.waitForFunction(async ({ key, count }) => (await (await fetch('/test/state')).json())[key] === count, { key, count });
  const byTitle = title => data.bugs.find(bug => bug.title === title).id;
  const card = title => page.locator(`.bug-card[data-bug-id="${byTitle(title)}"]`);
  const bug = async title => (await state()).bugs.find(b => b.title === title);
  const waitStatus = async (title, status) => {
    await page.waitForFunction(({ id, status }) => document.querySelector(`.bug-card[data-bug-id="${id}"]`)?.dataset.bugStatus === status, { id: byTitle(title), status });
    assert((await bug(title)).status === status, `${title}: status not persisted`);
  };
  const idle = async () => page.waitForFunction(() => !document.querySelector('.bug-card-drag-preview,.bug-card-dragging,.bug-drop-active,.is-moving-bug'));
  const position = async (title, status) => {
    const source = card(title), lane = page.locator(`[data-lane="${status}"]`);
    await source.scrollIntoViewIfNeeded();
    await page.locator('#board-scroll').evaluate((board, target) => {
      const source = board.querySelector(`[data-bug-id="${target.id}"]`).closest('[data-lane]');
      const lane = board.querySelector(`[data-lane="${target.status}"]`);
      const left = Math.min(source.getBoundingClientRect().left, lane.getBoundingClientRect().left);
      const right = Math.max(source.getBoundingClientRect().right, lane.getBoundingClientRect().right);
      if (right - left <= board.clientWidth - 24) board.scrollLeft += left - board.getBoundingClientRect().left - 12;
    }, { id: byTitle(title), status });
    const start = await source.locator('.bug-title').boundingBox(), end = await lane.boundingBox();
    return { start: { x: start.x + Math.min(70, start.width / 2), y: start.y + start.height / 2 }, end: { x: end.x + end.width / 2, y: end.y + 160 } };
  };
  const begin = async (title, status) => {
    const { start, end } = await position(title, status);
    await page.mouse.move(start.x, start.y); await page.mouse.down();
    // Distant lanes require the same edge scrolling as a real desktop drag.
    const bounds = await page.locator('#board-scroll').boundingBox();
    const left = Math.max(0, bounds.x), right = Math.min(page.viewportSize().width, bounds.x + bounds.width);
    if (end.x < left + 52 || end.x > right - 52) {
      await page.mouse.move(end.x < left + 52 ? left + 10 : right - 10, end.y, { steps: 12 });
      await page.waitForFunction(status => {
        const board = document.querySelector('#board-scroll'), lane = board.querySelector(`[data-lane="${status}"]`);
        const b = board.getBoundingClientRect(), l = lane.getBoundingClientRect();
        return l.left > b.left + 60 && l.right < Math.min(innerWidth, b.right) - 60;
      }, status);
      const target = await page.locator(`[data-lane="${status}"]`).boundingBox();
      end.x = target.x + target.width / 2;
    }
    await page.mouse.move(end.x, end.y, { steps: 12 });
    return end;
  };
  const drop = async (title, status) => { await begin(title, status); await page.mouse.up(); await idle(); };
  await page.setViewportSize({ width: 1680, height: 920 });
  await page.locator('#board-scroll[data-bug-move-enabled="true"][data-bug-drag-ready="true"]').waitFor();
  assert((await card('可直接拖动的问题').locator('.owner-group').innerText()) === '负责人：无', 'Missing Chinese unassigned label');
  await page.getByRole('button', { name: '切换语言' }).click();
  await page.waitForFunction(() => document.querySelector('.client')?.getAttribute('lang') === 'en');
  assert((await card('可直接拖动的问题').locator('.owner-group').innerText()) === 'Assignee: None', 'Missing English unassigned label');
  await page.getByRole('button', { name: '切换语言' }).click();
  await page.waitForFunction(() => document.querySelector('.client')?.getAttribute('lang') === 'zh-CN');
  const initialOrder = await page.locator('#board-scroll > [data-lane]').evaluateAll(lanes => lanes.map(lane => lane.dataset.lane).join(','));
  // Card clicks and tiny movements do not navigate; details have a dedicated button.
  await step('details');
  const title = '可直接拖动的问题';
  await card(title).locator('.bug-title').click();
  assert((await state()).opened === opened, 'Card click opened details');
  let p = await position(title, 'processing');
  await page.mouse.move(p.start.x, p.start.y); await page.mouse.down(); await page.mouse.move(p.start.x + 2, p.start.y + 1); await page.mouse.up();
  assert((await state()).opened === opened, 'Small movement opened details');
  const details = card(title).getByRole('button', { name: '查看详情', exact: true });
  await details.click(); await waitCount('opened', opened + 1);
  await details.focus(); await page.keyboard.press('Enter'); await waitCount('opened', opened + 2);
  await card(title).getByRole('button', { name: '复制 BUG ID', exact: true }).click();
  await waitCount('copies', copies + 1);
  assert((await state()).opened === opened + 2, 'Copy button opened details');
  // Real mouse input produces an inert shadow and valid target highlight.
  await step('drag');
  await begin(title, 'processing');
  assert(await page.locator('.bug-card-drag-preview').evaluate(el => el.inert), 'No inert drag preview');
  assert(await page.locator('[data-lane="processing"]').evaluate(el => el.classList.contains('bug-drop-active')), 'No target highlight');
  assert((await bug(title)).lockOwnerId === null, 'Dragging acquired a lock before drop');
  await page.screenshot({ path: 'output/playwright/bug-card-drag/drag-dark.png' });
  await page.mouse.up(); await idle(); await waitStatus(title, 'processing');
  assert((await bug(title)).lockOwnerId === null, 'Automatic lock leaked');
  assert((await state()).opened === opened + 2, 'Drop opened details');
  // Escape, same-lane and outside drops never change the status or open details.
  await step('cancel');
  await begin(title, 'waiting'); await page.keyboard.press('Escape'); await page.mouse.up(); await idle();
  await waitStatus(title, 'processing');
  await drop(title, 'processing'); await waitStatus(title, 'processing');
  await begin(title, 'waiting'); await page.mouse.move(20, 20, { steps: 8 }); await page.mouse.up(); await idle();
  await waitStatus(title, 'processing'); assert((await state()).opened === opened + 2, 'Cancelled drag opened details');
  // An invalid transition cannot be highlighted or submitted.
  await step('invalid');
  await drop(title, 'human_verified'); await waitStatus(title, 'processing');
  assert(await page.locator('.bug-move-dialog').count() === 0, 'Invalid transition opened a form');
  // Direct closure needs a reason, but does not require a human verification transition.
  await step('direct-close');
  await drop(title, 'closed'); await page.locator('.bug-move-dialog[open]').waitFor();
  assert((await bug(title)).status === 'processing' && (await bug(title)).lockOwnerId === null, 'Closure form wrote before save');
  assert(await page.locator('.bug-move-dialog button[type="submit"]').isDisabled(), 'Empty closure reason accepted');
  await page.keyboard.press('Escape'); await page.locator('.bug-move-dialog').waitFor({ state: 'detached' });
  await waitStatus(title, 'processing');
  await drop(title, 'closed'); await page.locator('.bug-move-dialog[open]').waitFor();
  await page.getByRole('textbox', { name: '状态变更原因', exact: true }).fill('人工决定从处理中直接关闭');
  await page.screenshot({ path: 'output/playwright/bug-card-drag/direct-close-dark.png' });
  await page.locator('.bug-move-dialog button[type="submit"]').click();
  await waitStatus(title, 'closed');
  assert((await bug(title)).lockOwnerId === null, 'Direct closure leaked its automatic lock');
  assert((await state()).confirmations === confirmations, 'Direct closure incorrectly required human verification');
  await drop(title, 'processing'); await waitStatus(title, 'processing');
  // Required reasons are collected before acquiring any lock; Escape cancels.
  await step('reason');
  await drop(title, 'pending_deployment');
  await page.locator('.bug-move-dialog[open]').waitFor();
  assert((await bug(title)).lockOwnerId === null && (await bug(title)).status === 'processing', 'Reason form wrote before save');
  assert(await page.locator('.bug-move-dialog button[type="submit"]').isDisabled(), 'Empty reason accepted');
  await page.keyboard.press('Escape'); await page.locator('.bug-move-dialog').waitFor({ state: 'detached' });
  await waitStatus(title, 'processing');
  await drop(title, 'pending_deployment'); await page.locator('.bug-move-dialog[open]').waitFor();
  await page.getByRole('textbox', { name: '状态变更原因', exact: true }).fill('虚构测试环境 v2：修复完成，等待部署');
  await page.screenshot({ path: 'output/playwright/bug-card-drag/reason-dark.png' });
  await page.locator('.bug-move-dialog button[type="submit"]').click();
  await waitStatus(title, 'pending_deployment'); assert((await bug(title)).lockOwnerId === null, 'Reason move leaked lock');
  // Foreign locks and read-only workspaces do not allow dragging.
  await step('readonly');
  const locked = '其他人正在处理的问题';
  assert(await card(locked).getAttribute('data-bug-targets') === '', 'Foreign lock is draggable');
  await page.getByRole('button', { name: '切换只读', exact: true }).click();
  await page.locator('#board-scroll[data-bug-move-enabled="false"]').waitFor();
  assert(await card(title).getAttribute('data-bug-targets') === '', 'Read-only card is draggable');
  await page.getByRole('button', { name: '切换只读', exact: true }).click();
  await page.locator('#board-scroll[data-bug-move-enabled="true"]').waitFor();
  // An existing manual claim stays held after moving.
  await step('manual-lock');
  await drop('我已手工领取的问题', 'processing'); await waitStatus('我已手工领取的问题', 'processing');
  assert((await bug('我已手工领取的问题')).lockOwnerId === 'preview-worker', 'Manual lock was released');
  // Human acceptance still requires the human confirmation, including cancellation.
  await step('verification');
  const verification = '等待人工回归的问题';
  await drop(verification, 'human_verified'); await page.locator('.bug-move-dialog[open]').waitFor();
  await page.getByRole('textbox', { name: '状态变更原因', exact: true }).fill('虚构测试环境 v2：人工检查通过');
  await page.locator('.bug-move-dialog button[type="submit"]').click();
  await page.locator('.bug-move-dialog').waitFor({ state: 'detached' }); await waitStatus(verification, 'pending_regression');
  assert((await state()).confirmations === confirmations + 1 && (await bug(verification)).lockOwnerId === null, 'Cancelled human acceptance acquired lock');
  await page.getByRole('button', { name: '人工确认：取消', exact: true }).click();
  await drop(verification, 'human_verified'); await page.locator('.bug-move-dialog[open]').waitFor();
  await page.getByRole('textbox', { name: '状态变更原因', exact: true }).fill('虚构测试环境 v2：人工检查通过');
  await page.locator('.bug-move-dialog button[type="submit"]').click();
  await waitStatus(verification, 'human_verified'); assert((await state()).confirmations === confirmations + 2, 'Human confirmation skipped');
  // Card dragging never changes the lane order.
  assert(await page.locator('#board-scroll > [data-lane]').evaluateAll(lanes => lanes.map(lane => lane.dataset.lane).join(',')) === initialOrder, 'Card move reordered lanes');
  await page.getByRole('button', { name: '切换日夜模式', exact: true }).click();
  await page.locator('#board-scroll').evaluate(board => board.scrollLeft = 0);
  await page.screenshot({ path: 'output/playwright/bug-card-drag/board-light.png' });
  return { passed: true, detailOpens: (await state()).opened - opened, humanConfirmations: (await state()).confirmations - confirmations };
}
