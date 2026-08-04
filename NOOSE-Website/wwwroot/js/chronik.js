// vis-timeline interop (agency-wide chronicle)

let ladenPromise = null;

function ladeVisTimeline() {
    if (window.vis && window.vis.Timeline) {
        return Promise.resolve();
    }
    if (ladenPromise) {
        return ladenPromise;
    }
    ladenPromise = new Promise((resolve, reject) => {
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
    return ladenPromise;
}

const instanzen = new Map(); // containerId → instance

function baueDatensaetze(daten) {
    const groups = new window.vis.DataSet((daten.groups || []).map(g => ({ id: g.id, content: g.content })));
    const items = new window.vis.DataSet((daten.items || []).map(i => ({
        id: i.id,
        group: i.group,
        content: i.content,
        start: i.start,
        title: i.title,
        style: 'border-color:' + i.color + ';box-shadow:-3px 0 0 ' + i.color + ';',
        _href: i.href,
    })));
    return { groups, items };
}

export async function render(containerId, datenJson, dotnetRef) {
    await ladeVisTimeline();
    const container = document.getElementById(containerId);
    if (!container) {
        return;
    }
    zerstoere(containerId);

    const daten = typeof datenJson === 'string' ? JSON.parse(datenJson) : datenJson;
    const { groups, items } = baueDatensaetze(daten);
    const options = {
        stack: true,
        orientation: 'top',
        zoomable: true,
        moveable: true,
        horizontalScroll: true,
        verticalScroll: true,
        maxHeight: '620px',
        tooltip: { followMouse: true, overflowMethod: 'cap' },
    };
    const timeline = new window.vis.Timeline(container, items, groups, options);

    timeline.on('doubleClick', (props) => {
        if (props.item != null) {
            const it = items.get(props.item);
            if (it && it._href) {
                try { dotnetRef.invokeMethodAsync('OnItemClick', it._href); } catch (e) { /* ignore */ }
            }
        }
    });

    let debounce = null;
    timeline.on('rangechanged', (props) => {
        if (debounce) {
            clearTimeout(debounce);
        }
        debounce = setTimeout(() => {
            try {
                dotnetRef.invokeMethodAsync('OnRangeChangedJs',
                    new Date(props.start).toISOString(), new Date(props.end).toISOString());
            } catch (e) { /* ignore */ }
        }, 300);
    });

    instanzen.set(containerId, { timeline, items, groups, dotnetRef });
}

export function setzeItems(containerId, datenJson) {
    const inst = instanzen.get(containerId);
    if (!inst) {
        return;
    }
    const daten = typeof datenJson === 'string' ? JSON.parse(datenJson) : datenJson;
    const { groups, items } = baueDatensaetze(daten);
    inst.groups.clear();
    inst.groups.add(groups.get());
    inst.items.clear();
    inst.items.add(items.get());
}

export function zerstoere(containerId) {
    const inst = instanzen.get(containerId);
    if (inst) {
        try { inst.timeline.destroy(); } catch (e) { /* ignore */ }
        instanzen.delete(containerId);
    }
}
