(() => {
    "use strict";
    const byId = id => document.getElementById(id);
    const rows = Array.from(document.querySelectorAll(".bug-row"), node => ({
        node,
        values: { ...node.dataset, service: JSON.parse(node.dataset.service) }
    }));
    const filters = new Map();
    const controls = byId("pagination");
    const pageSize = byId("page-size");
    let page = 1;

    function render() {
        const size = Number(pageSize.value);
        const first = (page - 1) * size;
        const active = [...filters];
        let count = 0;
        for (const { node, values } of rows) {
            const matches = active.every(([dimension, { key }]) => dimension === "service"
                ? values.service.includes(key) : values[dimension] === key);
            node.classList.toggle("filter-hidden", !matches);
            node.classList.toggle("page-hidden", matches && (count < first || count >= first + size));
            if (matches) count++;
        }

        byId("no-matches").hidden = count !== 0;
        byId("result-count").textContent = count
            ? controls.dataset.count.replace(/\{([012])\}/g, (_, index) => [first + 1, Math.min(first + size, count), count][index])
            : controls.dataset.empty;
        const pages = Math.max(1, Math.ceil(count / size));
        byId("page-number").textContent = page + " / " + pages;
        byId("prev").disabled = page === 1;
        byId("next").disabled = page >= pages;
        syncSelection();
    }

    function syncSelection() {
        for (const target of document.querySelectorAll("[data-filter]")) {
            const selected = filters.get(target.dataset.filter)?.key === target.dataset.key;
            target.classList.toggle("selected", selected);
            if (target.hasAttribute("aria-pressed")) target.setAttribute("aria-pressed", String(selected));
        }
        for (const donut of document.querySelectorAll(".donut")) {
            donut.classList.toggle("has-selection", !!donut.querySelector(".slice.selected"));
        }
        const chips = byId("filters");
        chips.replaceChildren();
        for (const [dimension, { label }] of filters) {
            const chip = document.createElement("button");
            chip.type = "button";
            chip.textContent = label + " ×";
            chip.addEventListener("click", () => {
                filters.delete(dimension);
                resetPage();
            });
            chips.append(chip);
        }
        byId("filter-bar").hidden = filters.size === 0;
    }

    function resetPage() {
        page = 1;
        render();
    }

    function toggleFilter(target) {
        const { filter, key, label } = target.dataset;
        if (filters.get(filter)?.key === key) filters.delete(filter);
        else filters.set(filter, { key, label });
        resetPage();
    }

    // 日期是导出端已转换的本地日历值；用 UTC 做纯日期运算，避免阅读端时区和夏令时改变分组。
    function setupTrend() {
        const trend = byId("trend");
        if (!trend) return;
        const counts = new Map();
        for (const { values: { date } } of rows) {
            if (date) counts.set(date, (counts.get(date) ?? 0) + 1);
        }
        const dates = [...counts.keys()].sort();
        const day = 86_400_000;
        const latest = Date.parse(dates.at(-1));
        const earliest = Date.parse(dates[0]);
        const maxWindow = Math.floor((latest - earliest) / (30 * day));
        const older = byId("older");
        const newer = byId("newer");
        const plot = byId("trend-plot");
        let windowIndex = 0;
        const svg = (parent, type, attrs, label) => {
            const node = document.createElementNS("http://www.w3.org/2000/svg", type);
            for (const [name, value] of Object.entries(attrs)) node.setAttribute(name, value);
            if (label !== undefined) node.textContent = label;
            parent.append(node);
            return node;
        };

        function draw() {
            const end = latest - windowIndex * 30 * day;
            const start = Math.max(earliest, end - 29 * day);
            const data = Array.from({ length: (end - start) / day + 1 }, (_, i) => {
                const key = new Date(start + i * day).toISOString().slice(0, 10);
                return { key, count: counts.get(key) ?? 0 };
            });
            byId("trend-range").textContent = data.length === 1 ? data[0].key : data[0].key + " – " + data.at(-1).key;
            older.disabled = windowIndex >= maxWindow;
            newer.disabled = windowIndex === 0;

            const peak = Math.max(...data.map(d => d.count));
            const step = Math.max(1, Math.ceil(peak / 4));
            const ticks = Math.max(1, Math.ceil(peak / step));
            const top = 16, bottom = 155, left = 62, right = 864;
            const width = (right - left) / data.length;
            const y = value => bottom - (bottom - top) * value / (step * ticks);
            plot.replaceChildren();
            for (let tick = 0; tick <= ticks; tick++) {
                const value = tick * step;
                svg(plot, "line", { x1: left - 10, x2: right + 10, y1: y(value), y2: y(value), class: "grid-line" });
                svg(plot, "text", { x: left - 16, y: y(value), class: "axis-label", "text-anchor": "end", "dominant-baseline": "middle" }, value);
            }
            const points = data.map(({ count }, i) => (left + width * (i + .5)) + "," + y(count));
            svg(plot, "polyline", { points: points.join(" "), class: "trend-line" });
            const labelStep = Math.ceil(data.length / 6);
            data.forEach(({ key, count }, i) => {
                const x = left + width * (i + .5);
                const label = key + " · " + trend.dataset.pointLabel + ": " + count;
                const point = svg(plot, "g", {
                    class: "day", role: "button", tabindex: "0", "aria-pressed": "false", "aria-label": label,
                    "data-filter": "date", "data-key": key, "data-label": key
                });
                svg(point, "title", {}, label);
                svg(point, "rect", { x: left + width * i, y: top - 8, width, height: bottom - top + 16, class: "day-target" });
                svg(point, "circle", { cx: x, cy: y(count), r: 4, class: "day-dot" });
                if (i === 0 || i === data.length - 1 || (i % labelStep === 0 && i + 1 < data.length - 1)) {
                    svg(plot, "text", { x, y: 182, class: "axis-label", "text-anchor": "middle" }, key.slice(5));
                }
            });
            syncSelection();
        }

        older.addEventListener("click", () => { windowIndex++; draw(); });
        newer.addEventListener("click", () => { windowIndex--; draw(); });
        draw();
        trend.hidden = false;
    }

    document.addEventListener("click", event => {
        const target = event.target.closest("[data-filter]");
        if (target) toggleFilter(target);
    });
    document.addEventListener("keydown", event => {
        const point = event.target.closest(".day");
        if (point && (event.key === "Enter" || event.key === " ")) {
            event.preventDefault();
            if (!event.repeat) toggleFilter(point);
        }
    });
    byId("prev").addEventListener("click", () => { page--; render(); });
    byId("next").addEventListener("click", () => { page++; render(); });
    pageSize.addEventListener("change", resetPage);
    byId("clear").addEventListener("click", () => { filters.clear(); resetPage(); });
    setupTrend();
    render();
    document.querySelectorAll(".legend-item").forEach(button => { button.disabled = false; });
    document.body.classList.add("interactive");
    if (rows.length) {
        controls.hidden = false;
        byId("chart-hint").hidden = false;
    }
})();
