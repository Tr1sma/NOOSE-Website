// vis-timeline Graph2d interop: activity density band for the agency-wide chronicle

let loadPromise = null;

function loadVisTimeline() {
    if (window.vis && window.vis.Graph2d) {
        return Promise.resolve();
    }
    if (loadPromise) {
        return loadPromise;
    }
    loadPromise = new Promise((resolve, reject) => {
        if (!document.querySelector('link[data-vistl-css]')) {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = 'lib/vis-timeline/vis-timeline-graph2d.min.css';
            link.setAttribute('data-vistl-css', '');
            document.head.appendChild(link);
        }
        const script = document.createElement('script');
        script.src = 'lib/vis-timeline/vis-timeline-graph2d.min.js';
        script.onload = () => resolve();
        script.onerror = () => reject(new Error('vis-timeline konnte nicht geladen werden.'));
        document.head.appendChild(script);
    });
    return loadPromise;
}

const instances = new Map(); // containerId → instance

function buildItems(data) {
    // no per-bar labels: the band shows shape, the feed's day headers carry the exact counts
    return new window.vis.DataSet((data.buckets || []).map((b, i) => ({
        id: i,
        x: b.start,
        y: b.count,
    })));
}

function buildOptions(data) {
    return {
        style: 'bar',
        barChart: { width: data.barWidth || 18, align: 'center', sideBySide: false },
        drawPoints: false,
        shaded: false,
        height: '110px',
        start: data.windowStart,
        end: data.windowEnd,
        min: data.windowStart,
        max: data.windowEnd,
        zoomMin: 1000 * 60 * 60 * 6,
        moveable: true,
        zoomable: true,
        showCurrentTime: false,
        dataAxis: { visible: false },
        legend: false,
    };
}

export async function render(containerId, dataJson, dotnetRef) {
    await loadVisTimeline();
    const container = document.getElementById(containerId);
    if (!container) {
        return;
    }
    destroy(containerId);

    const data = typeof dataJson === 'string' ? JSON.parse(dataJson) : dataJson;
    const items = buildItems(data);
    const graph = new window.vis.Graph2d(container, items, buildOptions(data));

    let debounce = null;
    graph.on('rangechanged', (props) => {
        if (debounce) {
            clearTimeout(debounce);
        }
        debounce = setTimeout(() => {
            try {
                dotnetRef.invokeMethodAsync('OnRangeChangedJs',
                    new Date(props.start).toISOString(), new Date(props.end).toISOString());
            } catch (e) { /* ignore */ }
        }, 350);
    });

    instances.set(containerId, { graph, items, dotnetRef });
}

export function setItems(containerId, dataJson) {
    const inst = instances.get(containerId);
    if (!inst) {
        return;
    }
    const data = typeof dataJson === 'string' ? JSON.parse(dataJson) : dataJson;
    inst.items.clear();
    inst.items.add(buildItems(data).get());
    // a new preset changes the outer bounds, so push them before moving the window
    try {
        inst.graph.setOptions(buildOptions(data));
    } catch (e) { /* ignore */ }
}

export function destroy(containerId) {
    const inst = instances.get(containerId);
    if (inst) {
        try { inst.graph.destroy(); } catch (e) { /* ignore */ }
        instances.delete(containerId);
    }
}
