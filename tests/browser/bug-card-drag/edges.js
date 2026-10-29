// Run after check.js in the same preview session.
async (page) => {
  page.setDefaultTimeout(10000);
  const assert = (value, message) => { if (!value) throw new Error(message); };
  const state = async () => (await page.request.get('http://127.0.0.1:8766/test/state')).json();
  const initial = await state(), title = '正在处理的问题', id = initial.bugs.find(b => b.title === title).id;
  const card = page.locator(`.bug-card[data-bug-id="${id}"]`), board = page.locator('#board-scroll');
  const waitCount = async (key, count) => page.waitForFunction(async ({ key, count }) => (await (await fetch('/test/state')).json())[key] === count, { key, count });
  await page.setViewportSize({ width: 1280, height: 800 });
  const opened = initial.opened;
  // Viewing remains available on foreign locks and in a read-only workspace.
  await page.locator('.bug-card').filter({ hasText: '其他人正在处理的问题' }).getByRole('button', { name: '查看详情', exact: true }).click();
  await waitCount('opened', opened + 1);
  await page.getByRole('button', { name: '切换只读', exact: true }).click();
  await page.locator('#board-scroll[data-bug-move-enabled="false"]').waitFor();
  await card.getByRole('button', { name: '查看详情', exact: true }).click(); await waitCount('opened', opened + 2);
  await page.getByRole('button', { name: '切换只读', exact: true }).click();
  await page.locator('#board-scroll[data-bug-move-enabled="true"]').waitFor();
  // The prompt menu remains independent of card dragging.
  await card.getByRole('button', { name: '提示词', exact: true }).click();
  await page.locator('.prompt-dropdown:popover-open').waitFor();
  await page.locator('.prompt-dropdown:popover-open button').first().click();
  await waitCount('copies', initial.copies + 1);
  assert((await state()).opened === opened + 2, 'Prompt menu opened details');
  const start = async () => {
    await card.scrollIntoViewIfNeeded();
    const rect = await card.locator('.bug-title').boundingBox();
    await page.mouse.move(rect.x + 50, rect.y + rect.height / 2); await page.mouse.down();
    await page.mouse.move(rect.x + 70, rect.y + 20, { steps: 3 });
    await page.locator('.bug-card-drag-preview').waitFor();
  };
  await start();
  const bounds = await board.boundingBox();
  await page.mouse.move(Math.min(1270, bounds.x + bounds.width - 10), bounds.y + 180, { steps: 12 });
  await page.waitForFunction(() => {
    const board = document.querySelector('#board-scroll'), lane = board.querySelector('[data-lane="need_info"]');
    const b = board.getBoundingClientRect(), l = lane.getBoundingClientRect();
    return l.left > b.left + 60 && l.right < Math.min(innerWidth, b.right) - 60;
  });
  const target = await page.locator('[data-lane="need_info"]').boundingBox();
  await page.mouse.move(target.x + target.width / 2, target.y + 180, { steps: 6 });
  assert(await board.evaluate(el => el.scrollLeft > 1000), 'Right edge did not scroll');
  assert((await state()).bugs.find(b => b.id === id).lockOwnerId === null, 'Edge scrolling claimed early');
  await page.screenshot({ path: 'output/playwright/bug-card-drag/edge-light.png' });
  await page.mouse.up();
  await page.waitForFunction(id => document.querySelector(`.bug-card[data-bug-id="${id}"]`)?.dataset.bugStatus === 'need_info', id);
  assert((await state()).bugs.find(b => b.id === id).lockOwnerId === null, 'Long-distance move leaked lock');
  // Left-edge scrolling and window blur cancel without changing state.
  await start();
  const previous = await board.evaluate(el => el.scrollLeft);
  await page.mouse.move(bounds.x + 10, bounds.y + 180, { steps: 12 });
  await page.waitForFunction(previous => document.querySelector('#board-scroll').scrollLeft < previous - 300, previous);
  await page.evaluate(() => window.dispatchEvent(new Event('blur'))); await page.mouse.up();
  assert(await page.locator('.bug-card-drag-preview,.bug-drop-active').count() === 0, 'Blur left a drag preview');
  assert((await state()).bugs.find(b => b.id === id).status === 'need_info', 'Blur committed a drop');
  // Removing the source through filtering cleans up capture and animation frames.
  await start(); await page.getByRole('textbox', { name: '搜索 BUG', exact: true }).fill('无匹配的筛选');
  await card.waitFor({ state: 'detached' });
  await page.waitForFunction(() => !document.querySelector('.bug-card-drag-preview,.is-moving-bug'));
  await page.mouse.up(); await page.getByRole('textbox', { name: '搜索 BUG', exact: true }).fill('');
  await card.waitFor();
  // List/board changes dispose and reattach the handlers once.
  await page.getByRole('button', { name: '☷ 列表', exact: true }).click();
  await board.waitFor({ state: 'detached' });
  await page.getByRole('button', { name: '▥ 看板', exact: true }).click(); await board.waitFor();
  await start(); await page.keyboard.press('Escape'); await page.mouse.up();
  assert((await state()).bugs.find(b => b.id === id).status === 'need_info', 'Reattached handlers committed an Escape');
  // English labels fit the same dedicated button and reason dialog.
  await page.getByRole('button', { name: '切换语言', exact: true }).click();
  await card.getByRole('button', { name: 'View details', exact: true }).waitFor();
  await card.scrollIntoViewIfNeeded();
  await page.screenshot({ path: 'output/playwright/bug-card-drag/board-english.png' });
  assert((await state()).opened === opened + 2, 'Dragging opened details');
  return { passed: true, edgeScrolling: true, detailsWhileReadonly: true, promptMenu: true, cleanup: true };
}
