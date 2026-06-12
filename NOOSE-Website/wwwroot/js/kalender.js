// NOOSE Kalender-Interop (Phase 8, Block C). FullCalendar wird selbst gehostet unter /lib/fullcalendar und –
// wie vis-network im Beziehungsgraph – NUR auf der Kalender-Seite GEGUARDET nachgeladen, damit andere Seiten
// unbelastet bleiben. Schlaegt das Laden fehl, wirft render() einen Fehler, den die Blazor-Seite faengt
// (Fehlerhinweis statt Circuit-Abriss). Bei Aenderungen an dieser Datei die ?v=-Versionsnummer im import()
// der Razor-Seite hochzaehlen (dynamische Imports laufen nicht durch das Asset-Fingerprinting).

let fcLadenPromise = null;

// Farbe je Kalender-Quelle (Reihenfolge = enum KalenderQuelle; Blazor serialisiert das Enum als Zahl).
// Muss zu KalenderAnzeige.Farbe (C#) passen.
const QUELLE_FARBE = {
    0: '#3FB950', // Termin
    1: '#F0883E', // Operation
    2: '#58A6FF', // Observation
    3: '#8B98A8', // Aufgabe (faellig)
    4: '#D29922', // Wiedervorlage
    5: '#7C8CF8', // Fraktions-Aktivitaet
};

const instanzen = new Map(); // containerId -> { cal, dotnetRef }

function ladeFullCalendar() {
    if (window.FullCalendar && window.FullCalendar.Calendar) {
        return Promise.resolve();
    }
    if (fcLadenPromise) {
        return fcLadenPromise;
    }
    fcLadenPromise = new Promise((resolve, reject) => {
        const core = document.createElement('script');
        core.src = 'lib/fullcalendar/index.global.min.js';
        core.onload = () => {
            // Deutsche Locale optional nachladen (nicht fatal – die UI-Texte setzen wir zusaetzlich inline).
            const loc = document.createElement('script');
            loc.src = 'lib/fullcalendar/locales/de.global.min.js';
            loc.onload = () => resolve();
            loc.onerror = () => resolve();
            document.head.appendChild(loc);
        };
        core.onerror = () => reject(new Error('FullCalendar konnte nicht geladen werden.'));
        document.head.appendChild(core);
    });
    return fcLadenPromise;
}

// Einmalig FullCalendar an das dunkle NOOSE-/MudBlazor-Theme angleichen (FC-Default passt sonst nicht).
function stilSicherstellen() {
    if (document.querySelector('style[data-noose-kal]')) {
        return;
    }
    const st = document.createElement('style');
    st.setAttribute('data-noose-kal', '');
    st.textContent = `
.fc {
    --fc-border-color: var(--mud-palette-lines-default);
    --fc-page-bg-color: transparent;
    --fc-neutral-bg-color: var(--mud-palette-background-grey);
    --fc-today-bg-color: rgba(34, 211, 238, 0.07);
    --fc-now-indicator-color: var(--mud-palette-error);
    --fc-highlight-color: rgba(34, 211, 238, 0.12);
    --fc-list-event-hover-bg-color: var(--mud-palette-action-default-hover);
    font-family: inherit;
    color: var(--mud-palette-text-primary);
}
.fc .fc-toolbar-title { color: var(--mud-palette-text-primary); font-size: 1.15rem; font-weight: 600; }
.fc .fc-col-header-cell-cushion,
.fc .fc-list-day-text,
.fc .fc-list-day-side-text { color: var(--mud-palette-text-primary); text-decoration: none; padding: 6px 4px; }
.fc .fc-timegrid-slot-label-cushion,
.fc .fc-timegrid-axis-cushion,
.fc .fc-list-event-time { color: var(--mud-palette-text-secondary); font-size: 0.74rem; }
.fc .fc-timegrid-axis-cushion { white-space: normal; line-height: 1.05; }
.fc-theme-standard .fc-scrollgrid,
.fc-theme-standard td, .fc-theme-standard th { border-color: var(--mud-palette-lines-default); }
.fc .fc-day-today .fc-col-header-cell-cushion { color: var(--mud-palette-primary); font-weight: 700; }
.fc .fc-timegrid-slot-minor { border-top-style: dotted; }
/* Buttons an MudBlazor angleichen */
.fc .fc-button-primary {
    background-color: var(--mud-palette-surface);
    border-color: var(--mud-palette-lines-default);
    color: var(--mud-palette-text-primary);
    text-transform: none;
    box-shadow: none;
}
.fc .fc-button-primary:hover {
    background-color: var(--mud-palette-action-default-hover);
    border-color: var(--mud-palette-lines-default);
    color: var(--mud-palette-text-primary);
}
.fc .fc-button-primary:disabled { opacity: 0.4; }
.fc .fc-button:focus, .fc .fc-button-primary:focus { box-shadow: none; outline: none; }
.fc .fc-button-primary:not(:disabled).fc-button-active,
.fc .fc-button-primary:not(:disabled):active {
    background-color: var(--mud-palette-primary);
    border-color: var(--mud-palette-primary);
    color: var(--mud-palette-primary-text);
}
/* Ereignisse: dezent abgerundet + etwas Luft */
.fc .fc-event { border-radius: 6px; border: none; font-size: 0.76rem; }
.fc .fc-timegrid-event { box-shadow: none; }
.fc .fc-timegrid-event .fc-event-main { padding: 1px 5px; }
.fc .fc-daygrid-event { padding: 1px 5px; }
.fc .fc-h-event .fc-event-title, .fc .fc-event-title { font-weight: 600; }
.fc .fc-list-event:hover td { background: var(--mud-palette-action-default-hover); }
.noose-kal-hinfaellig { opacity: .6; text-decoration: line-through; }
/* Scrollbalken dezenter */
.fc .fc-scroller::-webkit-scrollbar { width: 10px; height: 10px; }
.fc .fc-scroller::-webkit-scrollbar-thumb { background: var(--mud-palette-lines-default); border-radius: 6px; }
.fc .fc-scroller::-webkit-scrollbar-track { background: transparent; }
`;
    document.head.appendChild(st);
}

// Lokale ISO-Datumszeichenkette (YYYY-MM-DD) n Tage spaeter – fuer das EXKLUSIVE Ende ganztaegiger Eintraege.
function addTage(iso, n) {
    const d = new Date(iso);
    if (isNaN(d.getTime())) {
        return iso;
    }
    d.setDate(d.getDate() + n);
    const p = (x) => String(x).padStart(2, '0');
    return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate());
}

function mapEreignis(e) {
    const farbe = QUELLE_FARBE[e.quelle] != null ? QUELLE_FARBE[e.quelle] : '#8B98A8';
    const ev = {
        id: e.id,
        title: e.titel,
        // Ganztaegig: Start auf das reine Datum normalisieren, damit eine evtl. mitgegebene Uhrzeit nie stört.
        start: e.ganzTaegig ? addTage(e.startLokal, 0) : e.startLokal,
        allDay: !!e.ganzTaegig,
        backgroundColor: farbe,
        borderColor: farbe,
        extendedProps: { href: e.href || null },
    };
    if (e.endeLokal) {
        // Ganztaegig: FullCalendar interpretiert das Ende EXKLUSIV → einen Tag dazurechnen, damit der Endtag mitzaehlt.
        ev.end = e.ganzTaegig ? addTage(e.endeLokal, 1) : e.endeLokal;
    }
    if (e.hinfaellig) {
        ev.classNames = ['noose-kal-hinfaellig'];
    }
    return ev;
}

export async function render(containerId, eintraege, dotnetRef, locale) {
    await ladeFullCalendar();
    stilSicherstellen();
    const el = document.getElementById(containerId);
    if (!el) {
        return;
    }
    zerstoere(containerId);

    const cal = new window.FullCalendar.Calendar(el, {
        // Standard = Wochenansicht mit Stunden-Rastern (in sich scrollbar) + roter „Jetzt"-Linie (wie Outlook).
        initialView: 'timeGridWeek',
        locale: locale || 'de',
        firstDay: 1,
        timeZone: 'local',
        // Feste Höhe → der Stunden-Raster scrollt in sich (statt die ganze Seite zu strecken).
        height: '78vh',
        nowIndicator: true,
        scrollTime: '07:00:00',
        slotLabelFormat: { hour: '2-digit', minute: '2-digit', hour12: false },
        eventTimeFormat: { hour: '2-digit', minute: '2-digit', hour12: false },
        headerToolbar: { left: 'prev,next today', center: 'title', right: 'timeGridDay,timeGridWeek,dayGridMonth,listWeek' },
        // Deutsche UI-Texte (greifen auch ohne separate Locale-Datei).
        buttonText: { today: 'Heute', month: 'Monat', week: 'Woche', day: 'Tag', list: 'Liste' },
        allDayText: 'Ganztägig',
        noEventsText: 'Keine Einträge in diesem Zeitraum',
        weekText: 'KW',
        moreLinkText: (n) => '+ ' + n + ' weitere',
        dayMaxEvents: true,
        navLinks: false,
        events: (eintraege || []).map(mapEreignis),
        // Klick auf einen Eintrag → in .NET zur Akte navigieren (NICHT FullCalendars natives url-Handling).
        eventClick: (info) => {
            info.jsEvent.preventDefault();
            const href = info.event.extendedProps && info.event.extendedProps.href;
            if (href) {
                try { dotnetRef.invokeMethodAsync('OnEreignisKlick', href); } catch (e) { /* Circuit weg */ }
            }
        },
        // Sichtbarer Zeitraum geaendert (Initial-Render + Monatswechsel) → Fenster in .NET nachladen.
        datesSet: (arg) => {
            try { dotnetRef.invokeMethodAsync('OnDatumsbereich', arg.startStr, arg.endStr); } catch (e) { /* Circuit weg */ }
        },
    });
    cal.render();
    instanzen.set(containerId, { cal, dotnetRef });
}

// Ersetzt die Eintragsmenge (Fensterwechsel) ohne Neuaufbau des Kalenders.
export function setzeEreignisse(containerId, eintraege) {
    const inst = instanzen.get(containerId);
    if (!inst) {
        return;
    }
    inst.cal.removeAllEvents();
    (eintraege || []).forEach((e) => inst.cal.addEvent(mapEreignis(e)));
}

export function zerstoere(containerId) {
    const inst = instanzen.get(containerId);
    if (inst) {
        try { inst.cal.destroy(); } catch (e) { /* ignore */ }
        instanzen.delete(containerId);
    }
}
