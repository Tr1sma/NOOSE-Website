// NOOSE Beziehungsgraph-Interop (Phase 8, Block A). vis-network wird selbst gehostet unter
// /lib/vis-network und – wie Quill im RichTextEditor – NUR auf der Graph-Seite GEGUARDET nachgeladen,
// damit andere Seiten unbelastet bleiben. Schlaegt das Laden fehl, wirft render() einen Fehler, den die
// Blazor-Seite faengt (Fehlerhinweis statt Circuit-Abriss). Bei Aenderungen an dieser Datei die
// ?v=-Versionsnummer im import() der Razor-Seite hochzaehlen (dynamische Imports laufen nicht durch das
// Asset-Fingerprinting).

let visLadenPromise = null;

// Knotenfarbe je Aktentyp (auf das dunkle NOOSE-Theme abgestimmt).
const TYP_FARBE = {
    Person: '#22D3EE',
    Fraktion: '#7C8CF8',
    Personengruppe: '#D29922',
    Partei: '#3FB950',
    Operation: '#F0883E',
    Vorgang: '#A371F7',
    Taskforce: '#2DD4BF',
    Aufgabe: '#8B98A8',
    Agent: '#E6EDF3',
    Gesetz: '#C9A227',
    PersonDok: '#6E7681',
    Observation: '#58A6FF',
};

// Randfarbe je Einstufung (0 = keine -> Typfarbe; 3 = gesichert staatsgefaehrdend -> rot).
const EINSTUFUNG_RAND = { 1: '#9BA8B8', 2: '#D29922', 3: '#F85149' };

// Kantenfarbe je Verknuepfungs-Art (0 = Standard, 1 = Konflikt, 2 = Buendnis).
const ART_FARBE = { 0: 'rgba(139,152,168,0.55)', 1: '#F85149', 2: '#3FB950' };

const instanzen = new Map(); // containerId -> { network, nodes, edges, dotnetRef }

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
    const basis = TYP_FARBE[k.typ] || '#8B98A8';
    const rand = EINSTUFUNG_RAND[k.einstufungStufe] || basis;
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
    typ.textContent = k.typ + (k.istVerschluss ? ' · Verschlusssache' : '');
    const name = document.createElement('div');
    name.style.cssText = 'font-weight:600;color:#E6EDF3;margin-top:2px;';
    name.textContent = k.bezeichnung;
    div.appendChild(typ);
    div.appendChild(name);
    if (k.untertitel) {
        const unter = document.createElement('div');
        unter.style.cssText = 'font-size:12px;color:#9BA8B8;';
        unter.textContent = k.untertitel;
        div.appendChild(unter);
    }
    return div;
}

function mapKnoten(k) {
    const node = {
        id: k.id,
        label: k.bezeichnung,
        title: baueTooltip(k),
        color: knotenfarbe(k),
        value: 1 + (k.grad || 0),
        borderWidth: k.einstufungStufe >= 3 ? 3 : 2,
        borderWidthSelected: 4,
        shadow: k.einstufungStufe >= 3
            ? { enabled: true, color: 'rgba(248,81,73,0.55)', size: 18, x: 0, y: 0 }
            : { enabled: true, color: 'rgba(0,0,0,0.5)', size: 8, x: 0, y: 2 },
    };
    if (k.fotoUrl) {
        node.shape = 'circularImage';
        node.image = k.fotoUrl;
        node.brokenImage = undefined;
    } else {
        node.shape = 'dot';
    }
    return node;
}

function mapKante(e) {
    const farbe = ART_FARBE[e.art] != null ? ART_FARBE[e.art] : ART_FARBE[0];
    return {
        from: e.von,
        to: e.nach,
        label: e.label || undefined,
        dashes: !!e.automatisch,
        width: e.art === 1 || e.art === 2 ? 2 : 1,
        color: { color: farbe, highlight: '#22D3EE', hover: '#22D3EE', opacity: 1 },
        font: { color: '#9BA8B8', size: 11, strokeWidth: 0, background: 'rgba(14,17,22,0.85)' },
    };
}

function optionen(knotenAnzahl, kantenAnzahl) {
    // Große Graphen sind teuer: improvedLayout (Kamada-Kawai-Vorlayout) und dynamisches Kanten-
    // Smoothing verursachen sekundenlange Freezes. Ab einer Schwelle bewusst abschalten/vereinfachen.
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
            // Stärkere Abstoßung + geringere zentrale Anziehung → Fraktions-Sterne drücken sich weiter
            // auseinander; höhere springConstant hält die Mitglieder eng an ihrem Hub (kompakte Cluster);
            // avoidOverlap verhindert, dass sich Cluster überlagern.
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
    const knotenListe = (daten.knoten || []).map(mapKnoten);
    const kantenListe = (daten.kanten || []).map(mapKante);
    const nodes = new window.vis.DataSet(knotenListe);
    const edges = new window.vis.DataSet(kantenListe);
    const network = new window.vis.Network(container, { nodes, edges }, optionen(knotenListe.length, kantenListe.length));

    // Physik bleibt AN: der Graph schwingt ein und federt beim Ziehen mit („lebendig"). Er kommt von
    // selbst zur Ruhe, wenn die Bewegungsenergie gering ist (kein Dauer-CPU-Verbrauch im Ruhezustand).

    // Einfachklick: fokussieren/zentrieren. Doppelklick: zur Akte navigieren (in .NET).
    network.on('doubleClick', (params) => {
        // Knoten robust bestimmen – auch wenn der zweite Klick einen (durch die laufende Physik)
        // bewegten Knoten knapp verfehlt: erst die getroffenen Knoten, dann der Knoten unter dem
        // Zeiger, dann der bei Klick 1 ausgewählte.
        let id = (params.nodes && params.nodes.length > 0) ? params.nodes[0] : null;
        if (!id && params.pointer && params.pointer.DOM) {
            id = network.getNodeAt(params.pointer.DOM);
        }
        if (!id) {
            const sel = network.getSelectedNodes();
            if (sel && sel.length > 0) { id = sel[0]; }
        }
        if (id) {
            try { dotnetRef.invokeMethodAsync('OnKnotenKlick', id); } catch (e) { /* Circuit weg */ }
        }
    });
    network.on('selectNode', (params) => {
        if (params.nodes && params.nodes.length > 0) {
            network.focus(params.nodes[0], { scale: 1.15, animation: { duration: 400, easingFunction: 'easeInOutQuad' } });
        }
    });

    instanzen.set(containerId, { network, nodes, edges, dotnetRef });
}

// Hebt den gefundenen Pfad hervor und blendet den Rest aus (ausgegraut).
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

// Hebt jede Hervorhebung wieder auf.
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
    } catch (e) { /* Browser ohne Fullscreen-API */ }
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

export function zerstoere(containerId) {
    const inst = instanzen.get(containerId);
    if (inst) {
        try { inst.network.destroy(); } catch (e) { /* ignore */ }
        instanzen.delete(containerId);
    }
}
