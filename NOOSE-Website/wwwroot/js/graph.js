// vis-network interop

let visLadenPromise = null;

// node colors
const TYP_FARBE = {
    Person: '#22D3EE',
    Faction: '#7C8CF8',
    PersonGroup: '#D29922',
    Party: '#3FB950',
    Operation: '#F0883E',
    Case: '#A371F7',
    Taskforce: '#2DD4BF',
    Job: '#8B98A8',
    Agent: '#E6EDF3',
    Law: '#C9A227',
    PersonDoc: '#6E7681',
    Observation: '#58A6FF',
};

// border by classification
const EINSTUFUNG_RAND = { 1: '#9BA8B8', 2: '#D29922', 3: '#F85149' };

// edge colors
const ART_FARBE = { 0: 'rgba(139,152,168,0.55)', 1: '#F85149', 2: '#3FB950' };

const instanzen = new Map(); // id → instance

function ladeVisNetwork() {
    if (window.vis && window.vis.Network) {
        return Promise.resolve();
    }
    if (visLadenPromise) {
        return visLadenPromise;
    }
    visLadenPromise = new Promise((resolve, reject) => {
        if (!document.querySelector('link[data-vis-css]')) {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = 'lib/vis-network/vis-network.min.css';
            link.setAttribute('data-vis-css', '');
            document.head.appendChild(link);
        }
        const script = document.createElement('script');
        script.src = 'lib/vis-network/vis-network.min.js';
        script.onload = () => resolve();
        script.onerror = () => reject(new Error('vis-network konnte nicht geladen werden.'));
        document.head.appendChild(script);
    });
    return visLadenPromise;
}

function knotenfarbe(k) {
    const basis = TYP_FARBE[k.type] || '#8B98A8';
    const rand = EINSTUFUNG_RAND[k.classificationLevel] || basis;
    return {
        background: '#161B22',
        border: rand,
        highlight: { background: '#1F2630', border: '#22D3EE' },
        hover: { background: '#1F2630', border: rand },
    };
}

function baueTooltip(k) {
    const div = document.createElement('div');
    div.className = 'noose-graph-tip';
    const typ = document.createElement('div');
    typ.style.cssText = 'font-size:11px;letter-spacing:.06em;text-transform:uppercase;color:#9BA8B8;';
    typ.textContent = k.type + (k.isClassified ? ' · Verschlusssache' : '');
    const name = document.createElement('div');
    name.style.cssText = 'font-weight:600;color:#E6EDF3;margin-top:2px;';
    name.textContent = k.designation;
    div.appendChild(typ);
    div.appendChild(name);
    if (k.subtitle) {
        const unter = document.createElement('div');
        unter.style.cssText = 'font-size:12px;color:#9BA8B8;';
        unter.textContent = k.subtitle;
        div.appendChild(unter);
    }
    return div;
}

// community palette (workbench cluster colouring)
const CLUSTER_FARBE = ['#22D3EE', '#F0883E', '#3FB950', '#A371F7', '#D29922', '#58A6FF', '#F85149', '#2DD4BF', '#7C8CF8', '#C9A227'];

function mapKnoten(k) {
    const schluessel = !!k.isKeyFigure;
    const node = {
        id: k.id,
        label: (schluessel ? '★ ' : '') + k.designation,
        title: baueTooltip(k),
        color: knotenfarbe(k),
        value: 1 + (k.degree || 0),
        borderWidth: schluessel ? 4 : (k.classificationLevel >= 3 ? 3 : 2),
        borderWidthSelected: 5,
        shadow: k.classificationLevel >= 3
            ? { enabled: true, color: 'rgba(248,81,73,0.55)', size: 18, x: 0, y: 0 }
            : schluessel
                ? { enabled: true, color: 'rgba(34,211,238,0.55)', size: 16, x: 0, y: 0 }
                : { enabled: true, color: 'rgba(0,0,0,0.5)', size: 8, x: 0, y: 2 },
    };
    if (k.photoUrl) {
        node.shape = 'circularImage';
        node.image = k.photoUrl;
        node.brokenImage = undefined;
    } else {
        node.shape = 'dot';
    }
    return node;
}

function mapKante(e) {
    const farbe = ART_FARBE[e.kind] != null ? ART_FARBE[e.kind] : ART_FARBE[0];
    return {
        from: e.source,
        to: e.target,
        label: e.label || undefined,
        dashes: !!e.automatic,
        width: e.kind === 1 || e.kind === 2 ? 2 : 1,
        color: { color: farbe, highlight: '#22D3EE', hover: '#22D3EE', opacity: 1 },
        font: { color: '#9BA8B8', size: 11, strokeWidth: 0, background: 'rgba(14,17,22,0.85)' },
    };
}

function optionen(knotenAnzahl, kantenAnzahl) {
    // skip layout for large graphs
    const gross = knotenAnzahl > 120 || kantenAnzahl > 200;
    return {
        autoResize: true,
        layout: { improvedLayout: !gross },
        nodes: {
            shape: 'dot',
            scaling: { min: 10, max: 40, label: { enabled: true, min: 12, max: 22 } },
            font: { color: '#E6EDF3', size: 14, face: 'Inter, Segoe UI, sans-serif' },
        },
        edges: {
            selectionWidth: 2,
            hoverWidth: 1,
            smooth: gross ? false : { enabled: true, type: 'continuous' },
        },
        physics: {
            solver: 'forceAtlas2Based',
            forceAtlas2Based: { gravitationalConstant: -75, centralGravity: 0.006, springLength: 150, springConstant: 0.12, damping: 0.5, avoidOverlap: 0.4 },
            stabilization: { enabled: true, iterations: gross ? 120 : 200, updateInterval: 25, fit: true },
        },
        interaction: { hover: true, tooltipDelay: 120, navigationButtons: true, keyboard: false, multiselect: false },
    };
}

export async function render(containerId, datenJson, dotnetRef) {
    await ladeVisNetwork();
    const container = document.getElementById(containerId);
    if (!container) {
        return;
    }
    zerstoere(containerId);

    const daten = typeof datenJson === 'string' ? JSON.parse(datenJson) : datenJson;
    const knotenListe = (daten.node || []).map(mapKnoten);
    const kantenListe = (daten.edges || []).map(mapKante);
    const nodes = new window.vis.DataSet(knotenListe);
    const edges = new window.vis.DataSet(kantenListe);
    const opts = optionen(knotenListe.length, kantenListe.length);
    const network = new window.vis.Network(container, { nodes, edges }, opts);

    // physics stays on

    // click handlers
    network.on('doubleClick', (params) => {
        let id = (params.nodes && params.nodes.length > 0) ? params.nodes[0] : null;
        if (!id && params.pointer && params.pointer.DOM) {
            id = network.getNodeAt(params.pointer.DOM);
        }
        if (!id) {
            const sel = network.getSelectedNodes();
            if (sel && sel.length > 0) { id = sel[0]; }
        }
        if (id) {
            try { dotnetRef.invokeMethodAsync('OnNodeClick', id); } catch (e) { /* ignore */ }
        }
    });
    network.on('selectNode', (params) => {
        if (params.nodes && params.nodes.length > 0) {
            const id = params.nodes[0];
            network.focus(id, { scale: 1.15, animation: { duration: 400, easingFunction: 'easeInOutQuad' } });
            try { dotnetRef.invokeMethodAsync('OnNodeSelect', id); } catch (e) { /* ignore */ }
        }
    });

    // save defaults
    instanzen.set(containerId, { network, nodes, edges, dotnetRef, physikStandard: opts.physics, frei: false, rohKnoten: daten.node || [] });
}

// free mode physics
const PHYSIK_FREI = {
    enabled: true,
    solver: 'forceAtlas2Based',
    forceAtlas2Based: { gravitationalConstant: 0, centralGravity: 0, springLength: 150, springConstant: 0.03, damping: 0.9, avoidOverlap: 0 },
    stabilization: false,
    minVelocity: 0.5,
};

export function setzeFreierModus(containerId, frei) {
    const inst = instanzen.get(containerId);
    if (!inst) {
        return;
    }
    inst.frei = !!frei;
    inst.network.setOptions({ physics: frei ? PHYSIK_FREI : inst.physikStandard });
}

// highlight path
export function markierePfad(containerId, knotenIds, kantenSchluessel) {
    const inst = instanzen.get(containerId);
    if (!inst) {
        return;
    }
    const aktiv = new Set(knotenIds || []);
    const kantenAktiv = new Set(kantenSchluessel || []);
    inst.nodes.forEach((n) => {
        const an = aktiv.size === 0 || aktiv.has(n.id);
        inst.nodes.update({ id: n.id, opacity: an ? 1 : 0.18 });
    });
    inst.edges.forEach((e) => {
        const an = kantenAktiv.size === 0 || kantenAktiv.has(e.from + '|' + e.to) || kantenAktiv.has(e.to + '|' + e.from);
        inst.edges.update({ id: e.id, hidden: !an && kantenAktiv.size > 0 });
    });
    if (aktiv.size > 0) {
        try { inst.network.fit({ nodes: [...aktiv], animation: { duration: 500, easingFunction: 'easeInOutQuad' } }); } catch (e) { /* ignore */ }
    }
}

// reset highlights
export function zuruecksetzen(containerId) {
    const inst = instanzen.get(containerId);
    if (!inst) {
        return;
    }
    inst.nodes.forEach((n) => inst.nodes.update({ id: n.id, opacity: 1 }));
    inst.edges.forEach((e) => inst.edges.update({ id: e.id, hidden: false }));
}

export function passeAn(containerId) {
    const inst = instanzen.get(containerId);
    if (inst) {
        try { inst.network.fit({ animation: { duration: 500, easingFunction: 'easeInOutQuad' } }); } catch (e) { /* ignore */ }
    }
}

export function fokussiere(containerId, nodeId) {
    const inst = instanzen.get(containerId);
    if (inst && nodeId) {
        try { inst.network.focus(nodeId, { scale: 1.3, animation: { duration: 500, easingFunction: 'easeInOutQuad' } }); inst.network.selectNodes([nodeId]); } catch (e) { /* ignore */ }
    }
}

export function vollbild(containerId) {
    const container = document.getElementById(containerId);
    if (!container) {
        return;
    }
    try {
        if (document.fullscreenElement) {
            document.exitFullscreen();
        } else {
            container.requestFullscreen();
        }
    } catch (e) { /* ignore */ }
}

export function alsBildExportieren(containerId) {
    const container = document.getElementById(containerId);
    const canvas = container ? container.querySelector('canvas') : null;
    if (!canvas) {
        return;
    }
    try {
        const a = document.createElement('a');
        a.href = canvas.toDataURL('image/png');
        a.download = 'noose-beziehungsgraph.png';
        a.click();
    } catch (e) { /* ignore */ }
}

// colour nodes by detected community, or restore base colours
export function faerbeNachCommunity(containerId, an) {
    const inst = instanzen.get(containerId);
    if (!inst) {
        return;
    }
    const roh = new Map((inst.rohKnoten || []).map((k) => [k.id, k]));
    inst.nodes.forEach((n) => {
        const k = roh.get(n.id);
        if (!k) {
            return;
        }
        if (an) {
            const farbe = CLUSTER_FARBE[(k.communityId || 0) % CLUSTER_FARBE.length];
            inst.nodes.update({ id: n.id, color: { background: '#161B22', border: farbe, highlight: { background: '#1F2630', border: '#22D3EE' }, hover: { background: '#1F2630', border: farbe } } });
        } else {
            inst.nodes.update({ id: n.id, color: knotenfarbe(k) });
        }
    });
}

// hide nodes created after the cutoff (network-growth scrubber); null shows all
export function setzeZeitfenster(containerId, grenzeIso) {
    const inst = instanzen.get(containerId);
    if (!inst) {
        return;
    }
    const grenze = grenzeIso ? new Date(grenzeIso).getTime() : null;
    const roh = new Map((inst.rohKnoten || []).map((k) => [k.id, k]));
    inst.nodes.forEach((n) => {
        const k = roh.get(n.id);
        const erstellt = k && k.createdAt ? new Date(k.createdAt).getTime() : null;
        const versteckt = grenze != null && erstellt != null && erstellt > grenze;
        inst.nodes.update({ id: n.id, hidden: versteckt });
    });
}

// edge-drawing mode: user drags between two nodes → OnEdgeDrawn(from,to); we never add it visually
export function starteVerknuepfungsModus(containerId) {
    const inst = instanzen.get(containerId);
    if (!inst) {
        return;
    }
    inst.network.setOptions({
        manipulation: {
            enabled: false,
            addEdge: (data, callback) => {
                if (data.from !== data.to) {
                    try { inst.dotnetRef.invokeMethodAsync('OnEdgeDrawn', data.from, data.to); } catch (e) { /* ignore */ }
                }
                callback(null); // C# persists + reloads
            },
        },
    });
    try { inst.network.addEdgeMode(); } catch (e) { /* ignore */ }
}

export function beendeVerknuepfungsModus(containerId) {
    const inst = instanzen.get(containerId);
    if (inst) {
        try { inst.network.disableEditMode(); } catch (e) { /* ignore */ }
    }
}

export function holePositionen(containerId) {
    const inst = instanzen.get(containerId);
    if (!inst) {
        return '{}';
    }
    try {
        inst.network.storePositions();
        return JSON.stringify(inst.network.getPositions());
    } catch (e) {
        return '{}';
    }
}

export function setzePositionen(containerId, json) {
    const inst = instanzen.get(containerId);
    if (!inst) {
        return;
    }
    let pos;
    try { pos = typeof json === 'string' ? JSON.parse(json) : json; } catch (e) { return; }
    // freeze physics so the loaded layout sticks
    inst.frei = true;
    inst.network.setOptions({ physics: false });
    Object.keys(pos || {}).forEach((id) => {
        const p = pos[id];
        if (p && typeof p.x === 'number' && typeof p.y === 'number') {
            try { inst.network.moveNode(id, p.x, p.y); } catch (e) { /* ignore */ }
        }
    });
    try { inst.network.fit({ animation: { duration: 400 } }); } catch (e) { /* ignore */ }
}

export function zerstoere(containerId) {
    const inst = instanzen.get(containerId);
    if (inst) {
        try { inst.network.destroy(); } catch (e) { /* ignore */ }
        instanzen.delete(containerId);
    }
}
