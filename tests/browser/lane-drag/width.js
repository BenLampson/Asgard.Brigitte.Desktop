// Run with playwright-cli against the isolated lane-drag preview host.
async (page) => {
  const assert = (value, message) => { if (!value) throw new Error(message); };
  await page.waitForFunction(() => document.querySelector('.statusbar')?.dataset.ready === 'ready');
  const measure = () => page.evaluate(() => {
    const board = document.querySelector('#board-scroll');
    const bounds = board.getBoundingClientRect();
    const lanes = [...board.querySelectorAll(':scope > [data-lane]')].map(lane => lane.getBoundingClientRect());
    return {
      widths: lanes.map(lane => lane.width),
      visible: lanes.filter(lane => lane.left >= bounds.left - .5 && lane.right <= bounds.right + .5).length,
      overflowing: board.scrollWidth > board.clientWidth,
    };
  });
  let narrow;
  for (const width of [800, 1280, 1920, 2560]) {
    await page.setViewportSize({ width, height: 800 });
    await page.locator('#board-scroll').evaluate(board => { board.scrollLeft = 0; });
    const result = await measure();
    assert(result.widths.length > 4 && result.widths.every(value => Math.abs(value - 240) < .5),
      `Lanes changed width at ${width}px: ${JSON.stringify(result)}`);
    assert(result.overflowing, `Board should scroll horizontally at ${width}px`);
    if (width === 800) narrow = result.visible;
    if (width === 1920) assert(result.visible > narrow, `Wider window did not reveal more lanes: ${JSON.stringify(result)}`);
  }
  // A status filter that leaves only one lane must not stretch it either.
  await page.locator('.status-toggle').click();
  await page.locator('#status-filter-waiting').check();
  await page.waitForFunction(() => document.querySelectorAll('#board-scroll > [data-lane]').length === 1);
  const filtered = await measure();
  assert(Math.abs(filtered.widths[0] - 240) < .5, `Filtered lane changed width: ${JSON.stringify(filtered)}`);
  await page.locator('#status-filter-waiting').uncheck();
  await page.locator('.status-toggle').click();
  await page.screenshot({ path: 'output/playwright/lane-drag-tests/fixed-width.png' });
  return `PASS: 240px lanes at four window widths; visible lanes increased from ${narrow}; single filtered lane stays 240px`;
}
