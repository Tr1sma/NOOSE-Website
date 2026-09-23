// Browser-local drafts for every long text: the rich text editor and the plain text fields share one store, one
// sweep and one wipe on logout (window.nooseEntwuerfeLoeschen in App.razor). Everything here runs in the browser
// alone, so a field keeps saving while the circuit is gone - the one moment the draft exists for.
// IndexedDB, not localStorage: base64 images would blow the 5 MB string quota on the first screenshot.

// the name predates the plain fields; the logout wipe and every stored draft use it
const ENTWURF_DB = 'noose-rte';
const ENTWURF_STORE = 'entwuerfe';
// Seven, not thirty: the content is the full text in the clear, classified documents and personnel notes
// included, and it sits in a browser profile that outlives the session.
export const ENTWURF_ALTER_TAGE = 7;
export const ENTWURF_VERZOEGERUNG = 800;
const ENTWURF_OEFFNEN_MS = 3000;
const TAG_MS = 86400000;

let dbPromise = null;
let aufgeraeumt = false;

// Set by the logout form on its own page only: a flush on the way out would otherwise write a draft into the
// database the logout is deleting. Another open tab stays signed in and keeps drafting.
function gesperrt() {
    return window.nooseEntwuerfeGesperrt === true;
}

export function ladeEntwurfsDb() {
    if (gesperrt()) {
        return Promise.resolve(null);
    }
    if (dbPromise) {
        return dbPromise;
    }
    dbPromise = new Promise((resolve) => {
        if (!window.indexedDB) {
            resolve(null);
            return;
        }
        // An open queued behind a deletion that another tab still blocks never settles, and a save awaiting
        // "mark saved" would sit on its spinner until the interop timeout. Without the store the page just keeps
        // no drafts; the next call tries again.
        let aufgegeben = false;
        const frist = setTimeout(() => {
            aufgegeben = true;
            dbPromise = null;
            resolve(null);
        }, ENTWURF_OEFFNEN_MS);
        try {
            const anfrage = window.indexedDB.open(ENTWURF_DB, 1);
            anfrage.onupgradeneeded = () => {
                const db = anfrage.result;
                if (!db.objectStoreNames.contains(ENTWURF_STORE)) {
                    db.createObjectStore(ENTWURF_STORE, { keyPath: 'schluessel' });
                }
            };
            anfrage.onsuccess = () => {
                clearTimeout(frist);
                const db = anfrage.result;
                if (aufgegeben) {
                    // too late: an unreferenced open connection would block the next logout wipe
                    try {
                        db.close();
                    } catch (e) {
                        /* ignore */
                    }
                    return;
                }
                // Only a deletion asks for another version. An open connection would block the logout wipe until
                // this tab closes; letting go lets it through, and the next write here opens a fresh store.
                db.onversionchange = () => {
                    dbPromise = null;
                    // what was still waiting was typed before the logout; it must not land in the fresh store
                    verwerfeWartende();
                    try {
                        db.close();
                    } catch (e) {
                        /* ignore */
                    }
                };
                resolve(db);
            };
            anfrage.onerror = () => {
                clearTimeout(frist);
                resolve(null);
            };
        } catch (e) {
            // private mode and hardened browsers may throw right here
            clearTimeout(frist);
            resolve(null);
        }
    });
    return dbPromise;
}

export function entwurfSchreiben(db, eintrag) {
    if (gesperrt()) {
        return Promise.resolve(false);
    }
    return new Promise((resolve) => {
        try {
            const tx = db.transaction(ENTWURF_STORE, 'readwrite');
            tx.objectStore(ENTWURF_STORE).put(eintrag);
            tx.oncomplete = () => resolve(true);
            tx.onerror = () => resolve(false);
        } catch (e) {
            resolve(false);
        }
    });
}

export function entwurfLesen(db, schluessel) {
    return new Promise((resolve) => {
        try {
            const anfrage = db.transaction(ENTWURF_STORE, 'readonly').objectStore(ENTWURF_STORE).get(schluessel);
            anfrage.onsuccess = () => resolve(anfrage.result || null);
            anfrage.onerror = () => resolve(null);
        } catch (e) {
            resolve(null);
        }
    });
}

export function entwurfLoeschen(db, schluessel) {
    return new Promise((resolve) => {
        try {
            const tx = db.transaction(ENTWURF_STORE, 'readwrite');
            tx.objectStore(ENTWURF_STORE).delete(schluessel);
            tx.oncomplete = () => resolve(true);
            tx.onerror = () => resolve(false);
        } catch (e) {
            resolve(false);
        }
    });
}

function entwurfLoeschenMitPraefix(db, praefix) {
    return new Promise((resolve) => {
        try {
            const tx = db.transaction(ENTWURF_STORE, 'readwrite');
            // every key that starts with the prefix; the prefix ends on ':' so "dok:1" leaves "dok:12" alone
            tx.objectStore(ENTWURF_STORE).delete(IDBKeyRange.bound(praefix, praefix + String.fromCharCode(0xffff)));
            tx.oncomplete = () => resolve(true);
            tx.onerror = () => resolve(false);
        } catch (e) {
            resolve(false);
        }
    });
}

export function istAbgelaufen(eintrag) {
    return !eintrag || typeof eintrag.zeit !== 'number' || Date.now() - eintrag.zeit > ENTWURF_ALTER_TAGE * TAG_MS;
}

// stale drafts of any field are dropped once per page, so nothing prehistoric resurfaces
export function entwurfAufraeumen(db) {
    if (aufgeraeumt) {
        return;
    }
    aufgeraeumt = true;
    try {
        const tx = db.transaction(ENTWURF_STORE, 'readwrite');
        const lauf = tx.objectStore(ENTWURF_STORE).openCursor();
        lauf.onsuccess = () => {
            const cursor = lauf.result;
            if (!cursor) {
                return;
            }
            if (istAbgelaufen(cursor.value)) {
                cursor.delete();
            }
            cursor.continue();
        };
    } catch (e) {
        /* best effort */
    }
}

// ---- writes still waiting out their debounce ----
// Closing the tab or reloading never ran the pending write, and those last 800 ms were lost - often the final
// sentence. Every field registers here while it has one, and leaving the page flushes them all at once.
const wartend = new Set();

export function vormerken(zustand) {
    wartend.add(zustand);
}

export function abmelden(zustand) {
    wartend.delete(zustand);
}

function verwerfeWartende() {
    for (const zustand of Array.from(wartend)) {
        if (zustand.schreiben) {
            clearTimeout(zustand.schreiben);
            zustand.schreiben = 0;
        }
        wartend.delete(zustand);
    }
}

function allesSichern() {
    for (const zustand of Array.from(wartend)) {
        try {
            if (zustand.sofort) {
                zustand.sofort();
            }
        } catch (e) {
            /* best effort */
        }
    }
}

window.addEventListener('pagehide', allesSichern);
document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') {
        allesSichern();
    }
});

// ---- plain text fields (TextDraft.razor) ----
// A field is addressed by the number attach hands back, not by its element: a detached element can no longer be
// found by blazor, and the teardown is exactly when the last keystrokes still have to be written.
const felder = new Map();
let naechsteId = 1;
// Keys whose field went away with unsaved text on this page - a tab switch, a changed source type. A field that
// comes back under the same key and shows that text is carrying it over; anything else equal to the stored draft
// is the saved state, and the draft goes.
const unsaved = new Map();

function findeFeld(container) {
    if (!container) {
        return null;
    }
    return container.querySelector('textarea') || container.querySelector('input[type=text], input:not([type])');
}

// Trailing blanks and line-ending style are not worth an offer: a field that trims on save would otherwise
// offer its own saved text back every time.
function normal(text) {
    return String(text == null ? '' : text).replace(/\r\n/g, '\n').trim();
}

function gleich(a, b) {
    return normal(a) === normal(b);
}

function melde(zustand, zeit) {
    if (zustand.dotnetRef) {
        zustand.dotnetRef.invokeMethodAsync('OnDraftFound', zeit).catch(() => { });
    }
}

/// key = agent + field, supplied by the component; returns the handle for every later call, 0 when nothing attached
export function feldAnhaengen(container, dotnetRef, schluessel) {
    const feld = findeFeld(container);
    if (!feld || !schluessel) {
        return 0;
    }
    const id = naechsteId++;
    // basis: what the server holds (null once that is unknown); text: what the field last showed. The write
    // uses text, not the element: by the time a pending write runs the element may already show another record.
    const zustand = {
        id, feld, schluessel, dotnetRef,
        basis: feld.value, text: feld.value,
        schreiben: 0, tot: false, angeboten: null,
    };

    zustand.jetztSchreiben = async () => {
        const ref = zustand.dotnetRef;
        // after the check: a write that ran first would overwrite the stored draft before anyone saw it
        await zustand.geprueft;
        if (zustand.angeboten !== null) {
            // the offer pauses writing; the answer to it writes again
            return;
        }
        const db = await ladeEntwurfsDb();
        if (!db) {
            return;
        }
        const key = zustand.schluessel;
        const text = zustand.text;
        if (normal(text).length === 0 || (zustand.basis !== null && gleich(text, zustand.basis))) {
            // back to the saved state (or empty): nothing worth recovering
            await entwurfLoeschen(db, key);
            return;
        }
        if (await entwurfSchreiben(db, { schluessel: key, text, zeit: Date.now() }) && !zustand.gemeldet && ref) {
            // the owner learns which keys this field really wrote under, not which it meant to
            zustand.gemeldet = true;
            ref.invokeMethodAsync('OnDraftWritten', zustand.id).catch(() => { });
        }
    };

    zustand.sofort = () => {
        abmelden(zustand);
        if (!zustand.schreiben) {
            return;
        }
        clearTimeout(zustand.schreiben);
        zustand.schreiben = 0;
        zustand.jetztSchreiben().catch(() => { });
    };

    zustand.planen = () => {
        if (zustand.schreiben) {
            clearTimeout(zustand.schreiben);
        }
        vormerken(zustand);
        zustand.schreiben = setTimeout(() => {
            zustand.schreiben = 0;
            abmelden(zustand);
            zustand.jetztSchreiben().catch(() => { });
        }, ENTWURF_VERZOEGERUNG);
    };

    zustand.getipptJetzt = () => {
        // typing past an offer is the answer to it: the stored text is about to be overwritten, so the
        // buttons would restore what was just typed
        if (zustand.angeboten !== null) {
            zustand.angeboten = null;
            melde(zustand, 0);
        }
    };

    zustand.eingabe = () => {
        if (zustand.tot) {
            return;
        }
        zustand.text = feld.value;
        zustand.getipptJetzt();
        zustand.planen();
    };

    feld.addEventListener('input', zustand.eingabe);
    felder.set(id, zustand);

    zustand.geprueft = ladeEntwurfsDb().then(async (db) => {
        if (!db || zustand.tot) {
            return;
        }
        entwurfAufraeumen(db);
        const key = zustand.schluessel;
        const eintrag = await entwurfLesen(db, key);
        if (!eintrag || typeof eintrag.text !== 'string' || zustand.tot) {
            return;
        }
        if (istAbgelaufen(eintrag)) {
            entwurfLoeschen(db, key);
            return;
        }
        if (gleich(eintrag.text, zustand.text)) {
            const mitgebracht = unsaved.has(key) && gleich(unsaved.get(key), zustand.text);
            if (!mitgebracht) {
                // the field shows what the record holds: nothing left to recover
                entwurfLoeschen(db, key);
                return;
            }
            // carried over from a field of the same key a moment ago: still unsaved, and no saved state known
            zustand.basis = null;
            return;
        }
        if (zustand.basis !== null && gleich(eintrag.text, zustand.basis)) {
            // the saved state itself, left over by a drop that never arrived; whatever was typed since is newer
            entwurfLoeschen(db, key);
            return;
        }
        // Offered even when the first keystrokes came before this read: holding it back let the pending write
        // overwrite a draft nobody had seen. That write waits for the answer instead.
        if (zustand.schreiben) {
            clearTimeout(zustand.schreiben);
            zustand.schreiben = 0;
        }
        abmelden(zustand);
        zustand.angeboten = eintrag.text;
        melde(zustand, eintrag.zeit);
    }).catch(() => { });

    return id;
}

/// the offered draft's text, or null when there is none (any more)
export async function feldEntwurf(id) {
    const zustand = felder.get(id);
    if (!zustand || zustand.angeboten === null) {
        return null;
    }
    const text = zustand.angeboten;
    zustand.angeboten = null;
    // the field shows it from the next render on; the stored draft stays until a save
    zustand.text = text;
    return text;
}

/// "Verwerfen": the offer and the stored draft go, the field keeps what it shows
export async function feldVerwerfen(id) {
    const zustand = felder.get(id);
    if (!zustand) {
        return;
    }
    zustand.angeboten = null;
    unsaved.delete(zustand.schluessel);
    const db = await ladeEntwurfsDb();
    if (db) {
        await entwurfLoeschen(db, zustand.schluessel);
    }
    // what was typed before the offer came is unsaved too, and the offer held its write back
    if (!zustand.tot && normal(zustand.text).length > 0
        && (zustand.basis === null || !gleich(zustand.text, zustand.basis))) {
        zustand.planen();
    }
}

/// a change made from .NET (snippet, mention, pasted image) fires no input event; it is saved the same way
export function feldGeaendert(id, text) {
    const zustand = felder.get(id);
    if (!zustand || zustand.tot) {
        return;
    }
    zustand.text = text == null ? '' : String(text);
    zustand.getipptJetzt();
    zustand.planen();
}

/// The owner set the field from outside - loaded the record, applied a template, emptied it after sending. That
/// value is the new baseline. A field rendered before its record arrived would otherwise take the empty value it
/// started with for the saved state.
export async function feldBasis(id, text) {
    const zustand = felder.get(id);
    if (!zustand || zustand.tot) {
        return;
    }
    const wert = text == null ? '' : String(text);
    zustand.basis = wert;
    zustand.text = wert;
    unsaved.delete(zustand.schluessel);
    if (zustand.angeboten === null || !gleich(zustand.angeboten, wert)) {
        return;
    }
    // the offered draft is what the record holds after all: nothing to offer, nothing to keep
    zustand.angeboten = null;
    melde(zustand, 0);
    const db = await ladeEntwurfsDb();
    if (db) {
        await entwurfLoeschen(db, zustand.schluessel);
    }
}

function alsGespeichert(zustand) {
    if (zustand.schreiben) {
        clearTimeout(zustand.schreiben);
        zustand.schreiben = 0;
    }
    abmelden(zustand);
    zustand.angeboten = null;
    zustand.text = zustand.feld.value;
    zustand.basis = zustand.feld.value;
    unsaved.delete(zustand.schluessel);
}

/// called after a successful save: the draft goes, and what the field shows now is the saved state
export async function feldGespeichert(id) {
    const zustand = felder.get(id);
    if (!zustand) {
        return;
    }
    alsGespeichert(zustand);
    const db = await ladeEntwurfsDb();
    if (db) {
        await entwurfLoeschen(db, zustand.schluessel);
    }
}

export function feldAbhaengen(id) {
    const zustand = felder.get(id);
    if (!zustand) {
        return;
    }
    felder.delete(id);
    // remembered for a field that comes back under this key in a moment and shows the same text
    if (zustand.basis === null || !gleich(zustand.text, zustand.basis)) {
        unsaved.set(zustand.schluessel, zustand.text);
    } else {
        unsaved.delete(zustand.schluessel);
    }
    // flush before letting go: the pending write holds the newest keystrokes
    zustand.sofort();
    zustand.tot = true;
    zustand.dotnetRef = null;
    zustand.feld.removeEventListener('input', zustand.eingabe);
}

/// Drops drafts by key or by scope prefix, for a caller that stores the row after its fields are gone. A field
/// still attached under that key counts as saved at once, or its pending write would bring the draft back.
export async function verwerfen(schluessel, praefix) {
    for (const zustand of felder.values()) {
        if (zustand.schluessel === schluessel || (praefix && zustand.schluessel.startsWith(praefix))) {
            alsGespeichert(zustand);
        }
    }
    for (const key of Array.from(unsaved.keys())) {
        if (key === schluessel || (praefix && key.startsWith(praefix))) {
            unsaved.delete(key);
        }
    }
    const db = await ladeEntwurfsDb();
    if (!db) {
        return;
    }
    if (schluessel) {
        await entwurfLoeschen(db, schluessel);
    }
    if (praefix) {
        await entwurfLoeschenMitPraefix(db, praefix);
    }
}
