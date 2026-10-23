// Run with playwright-cli run-code --filename after opening the loopback test host.
async (page) => {
  const assert = (value, message) => { if (!value) throw new Error(message); };
  const saved = () => page.locator('#saved-order').textContent();
  const idle = async () => {
    await page.waitForFunction(() => !document.querySelector('.lane-drag-preview,.lane-dragging,.lane-drop-before,.lane-drop-after,.is-reordering'));
  };
  const point = async (status, fraction = .3) => {
    await page.locator(`[data-lane="${status}"] .lane-drag-handle`).waitFor({ state: 'visible' });
    const box = await page.locator(`[data-lane="${status}"] .lane-drag-handle`).boundingBox();
    return { x: box.x + box.width * fraction, y: box.y + box.height / 2 };
  };
  const start = async (status) => {
    const p = await point(status);
    await page.mouse.move(p.x, p.y);
    await page.mouse.down();
    return p;
  };
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.waitForFunction(() => document.querySelector('.statusbar')?.dataset.ready === 'ready');
  await page.locator('[data-lane="reproducing"] .lane-drag-handle').waitFor({ state: 'visible' });
  await page.getByRole('button', { name: '恢复泳道顺序', exact: true }).click();
  await page.waitForFunction(() => document.querySelector('#saved-order').textContent.startsWith('waiting,confirmed,reproducing'));
  const original = await saved();
  // Ordinary clicks and tiny movement cannot save an accidental reorder.
  let p = await start('reproducing');
  await page.mouse.move(p.x + 2, p.y);
  await page.mouse.up();
  await idle();
  assert(await saved() === original, 'Click changed the order');
  // Actual mouse input, pointer capture, a complete column shadow and target marker.
  await start('reproducing');
  p = await point('confirmed', .15);
  await page.mouse.move(p.x, p.y, { steps: 12 });
  await page.waitForSelector('.lane-drag-preview .bug-card');
  assert(await page.locator('[data-lane="confirmed"]').evaluate(el => el.classList.contains('lane-drop-before')), 'No insertion marker');
  assert(await page.locator('.lane-drag-preview').evaluate(el => el.inert && getComputedStyle(el).boxShadow !== 'none'), 'No inert column shadow');
  await page.screenshot({ path: 'output/playwright/lane-drag-tests/drag-shadow.png' });
  await page.mouse.up();
  await page.waitForFunction(() => document.querySelector('#saved-order').textContent.startsWith('waiting,reproducing,confirmed'));
  await idle();
  const moved = await saved();
  await page.reload();
  await page.waitForFunction(() => document.querySelector('.statusbar')?.dataset.ready === 'ready');
  await page.waitForFunction(() => document.querySelector('#saved-order').textContent.startsWith('waiting,reproducing,confirmed'));
  assert(await saved() === moved, 'Reload lost saved order');
  // Escape and a release outside the board must not save.
  await start('reproducing');
  p = await point('confirmed', .85);
  await page.mouse.move(p.x, p.y, { steps: 8 });
  await page.keyboard.press('Escape');
  await page.mouse.up();
  await idle();
  assert(await saved() === moved, 'Escape changed order');
  await start('reproducing');
  await page.mouse.move(20, 20, { steps: 8 });
  await page.mouse.up();
  await idle();
  assert(await saved() === moved, 'Outside release changed order');
  // Dragging card contents must never reorder lanes.
  const card = await page.locator('[data-lane="reproducing"] .bug-card').boundingBox();
  await page.mouse.move(card.x + 40, card.y + 40);
  await page.mouse.down();
  await page.mouse.move(card.x - 150, card.y + 40, { steps: 8 });
  await page.mouse.up();
  await idle();
  assert(await saved() === moved, 'Card drag reordered lanes');
  // The pointer can cross off-screen lanes without a native HTML drag session.
  await start('reproducing');
  const board = await page.locator('#board-scroll').boundingBox();
  await page.mouse.move(board.x + board.width - 8, board.y + 30, { steps: 10 });
  await page.waitForFunction(() => document.querySelector('#board-scroll').scrollLeft > 250);
  await page.keyboard.press('Escape');
  await page.mouse.up();
  await idle();
  assert(await saved() === moved, 'Edge-scroll cancellation changed order');
  await page.getByRole('button', { name: '仅显示交付状态', exact: true }).click();
  await page.locator('[data-lane="pending_deployment"]').scrollIntoViewIfNeeded();
  await start('pending_deployment');
  p = await point('pending_regression', .85);
  await page.mouse.move(p.x, p.y, { steps: 10 });
  await page.mouse.up();
  await page.waitForFunction(() => document.querySelector('#saved-order').textContent.includes('pending_regression,pending_deployment'));
  const filtered = (await saved()).split(',');
  assert(filtered.length === original.split(',').length && new Set(filtered).size === filtered.length, 'Filtered drag lost hidden lanes');
  await page.locator('[data-lane="pending_deployment"] .lane-drag-handle').press('Alt+ArrowLeft');
  await page.waitForFunction(() => document.querySelector('#saved-order').textContent.includes('pending_deployment,pending_regression'));
  await page.getByRole('button', { name: '恢复泳道顺序', exact: true }).click();
  await page.waitForFunction(() => document.querySelector('#saved-order').textContent.startsWith('waiting,confirmed,reproducing'));
  assert(await saved() === original, 'Reset did not restore the full order');
  return 'PASS: real pointer dragging, full-column shadow, persistence, Escape, outside release, cards, edge scroll, filtered order, keyboard and reset';
}
