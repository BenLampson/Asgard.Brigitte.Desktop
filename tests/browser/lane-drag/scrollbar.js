// Run with playwright-cli against the isolated lane-drag preview host.
async (page) => {
  const assert = (value, message) => { if (!value) throw new Error(message); };
  await page.waitForFunction(() => document.querySelector('.statusbar')?.dataset.ready === 'ready');

  for (const scenario of [
    { width: 1920, height: 1000, theme: 'dark', banners: 0 },
    { width: 1920, height: 1000, theme: 'dark', banners: 1 },
    { width: 1280, height: 800, theme: 'light', banners: 2 },
    { width: 800, height: 600, theme: 'dark', banners: 1 },
  ]) {
    await page.setViewportSize({ width: scenario.width, height: scenario.height });
    await page.evaluate(({ theme, banners }) => {
      const client = document.querySelector('.client');
      client.classList.toggle('dark', theme === 'dark');
      client.classList.toggle('light', theme === 'light');
      client.querySelectorAll('.test-banner').forEach(element => element.remove());
      const workspace = client.querySelector('.workspace');
      if (banners > 0) {
        const update = document.createElement('aside');
        update.className = 'update-reminder test-banner';
        update.innerHTML = '<span>商店有可用更新 · 正在安装更新…</span><button>立即更新</button><button>稍后提醒</button>';
        client.insertBefore(update, workspace);
      }
      if (banners > 1) {
        const notice = document.createElement('div');
        notice.className = 'notice warning test-banner';
        notice.textContent = '本地测试警告';
        client.insertBefore(notice, workspace);
      }
      document.querySelector('#board-scroll').scrollLeft = 0;
    }, scenario);

    const geometry = await page.evaluate(() => {
      const board = document.querySelector('#board-scroll');
      const rect = board.getBoundingClientRect();
      const footer = document.querySelector('.statusbar').getBoundingClientRect();
      return { x: rect.x, y: rect.y, width: rect.width, bottom: rect.bottom,
        barHeight: board.offsetHeight - board.clientHeight, footerBottom: footer.bottom,
        scrollWidth: board.scrollWidth, clientWidth: board.clientWidth };
    });
    assert(geometry.barHeight >= 20, `Scrollbar too small: ${JSON.stringify({ scenario, geometry })}`);
    assert(geometry.scrollWidth > geometry.clientWidth, 'Test board must overflow horizontally');
    assert(geometry.bottom <= scenario.height && geometry.footerBottom <= scenario.height,
      `Board or status bar is clipped: ${JSON.stringify({ scenario, geometry })}`);

    // Drag the real Chromium scrollbar thumb using mouse input, not scrollLeft assignment.
    const y = geometry.bottom - geometry.barHeight / 2;
    await page.mouse.move(geometry.x + 40, y);
    await page.mouse.down();
    await page.mouse.move(geometry.x + Math.min(geometry.width - 30, 300), y, { steps: 12 });
    await page.mouse.up();
    const scrolled = await page.locator('#board-scroll').evaluate(element => element.scrollLeft);
    assert(scrolled > 100, `Mouse drag did not scroll the board: ${JSON.stringify({ scenario, scrolled })}`);
  }
  await page.screenshot({ path: 'output/playwright/lane-drag-tests/scrollbar.png' });
  return 'PASS: visible 20px scrollbar and mouse dragging with update/warning banners, light/dark themes and multiple window sizes';
}
