// NOOSE WYSIWYG-Interop für den RichTextEditor (Quill 2.x + quill-table-better, selbst gehostet unter /lib/quill).
// Quill 2, das Tabellen-Modul und deren CSS werden bei Bedarf nachgeladen (nur auf Editor-Seiten), damit andere
// Seiten nicht belastet werden. WICHTIG: quill.js MUSS vor quill-table-better.js geladen sein – das Modul liest
// window.Quill bereits beim Laden (UMD: QuillTableBetter = factory(window.Quill)).

let ladePromise = null;

function ladeSkript(src) {
    return new Promise((resolve, reject) => {
        const s = document.createElement('script');
        s.src = src;
        s.onload = () => resolve();
        s.onerror = () => reject(new Error('Skript konnte nicht geladen werden: ' + src));
        document.head.appendChild(s);
    });
}

function ladeCss(href, marker) {
    if (document.querySelector('link[' + marker + ']')) {
        return;
    }
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = href;
    link.setAttribute(marker, '');
    document.head.appendChild(link);
}

function ladeQuill() {
    if (window.Quill && window.QuillTableBetter) {
        return Promise.resolve();
    }
    if (ladePromise) {
        return ladePromise;
    }
    ladePromise = (async () => {
        ladeCss('lib/quill/quill.snow.css', 'data-quill-css');
        ladeCss('lib/quill/quill-table-better.css', 'data-quill-table-css');
        if (!window.Quill) {
            await ladeSkript('lib/quill/quill.js');
        }
        if (!window.QuillTableBetter) {
            await ladeSkript('lib/quill/quill-table-better.js'); // liest window.Quill beim Laden
        }
        // Tabellen-Modul registrieren (true = vorhandene Registrierung überschreiben).
        window.Quill.register({ 'modules/table-better': window.QuillTableBetter }, true);
    })();
    return ladePromise;
}

export async function initRichText(element, dotnetRef, initialHtml) {
    await ladeQuill();
    if (!element) {
        return;
    }
    const editor = new window.Quill(element, {
        theme: 'snow',
        placeholder: 'Dokument verfassen…',
        modules: {
            table: false, // eingebautes (rudimentäres) Tabellen-Modul aus – quill-table-better übernimmt.
            toolbar: [
                [{ header: [1, 2, 3, false] }],
                ['bold', 'italic', 'underline', 'strike'],
                [{ list: 'ordered' }, { list: 'bullet' }],
                ['blockquote', 'code-block'],
                [{ color: [] }, { background: [] }],
                ['link', 'clean'],
                ['table-better'],
            ],
            'table-better': {
                language: 'de_DE',
                menus: ['column', 'row', 'merge', 'table', 'cell', 'wrap', 'copy', 'delete'],
                toolbarTable: true,
            },
            keyboard: {
                bindings: window.QuillTableBetter.keyboardBindings, // korrekte Tabellen-Navigation
            },
        },
    });

    if (initialHtml) {
        editor.clipboard.dangerouslyPasteHTML(initialHtml);
    }

    let timer = null;
    editor.on('text-change', () => {
        if (timer) {
            clearTimeout(timer);
        }
        // Entprellt an .NET zurückmelden (statt einer Interop-Runde je Tastenanschlag). Leichtgewichtig –
        // ohne die Tabellen-Hilfs-UI zu entfernen (das passiert erst beim endgültigen Lesen via getHtml).
        timer = setTimeout(() => {
            dotnetRef.invokeMethodAsync('OnHtmlChanged', leseHtml(editor));
        }, 300);
    });

    element.__nooseQuill = editor;
}

// True, wenn der Editor faktisch leer ist. Achtung: eine Tabelle mit leeren Zellen hat leeren getText() –
// daher zusätzlich auf eine vorhandene Tabelle prüfen, sonst ginge sie als „leer" verloren.
function istLeer(editor) {
    return editor.getText().trim().length === 0 && !editor.root.querySelector('table');
}

// Laufende (entprellte) Änderungsmeldung: semantisches HTML, ohne die Tabellen-UI zu stören.
function leseHtml(editor) {
    return istLeer(editor) ? '' : editor.getSemanticHTML();
}

// Endgültiges Lesen fürs Speichern: zuerst die Tabellen-Hilfs-/Toolbar-Elemente entfernen, dann sauberes
// semantisches HTML (echte <ul>/<table>-Struktur statt Quills interner data-list-/Temp-Elemente).
function leseHtmlFinal(editor) {
    try {
        const tabelle = editor.getModule('table-better');
        if (tabelle) {
            tabelle.deleteTableTemporary();
        }
    } catch (e) {
        // Kein Tabellen-Modul / keine aktive Tabelle – ignorieren.
    }
    return istLeer(editor) ? '' : editor.getSemanticHTML();
}

export function setHtml(element, html) {
    const editor = element && element.__nooseQuill;
    if (!editor) {
        return;
    }
    editor.setText('');
    if (html) {
        editor.clipboard.dangerouslyPasteHTML(html);
    }
}

export function getHtml(element) {
    const editor = element && element.__nooseQuill;
    return editor ? leseHtmlFinal(editor) : '';
}

export function destroyRichText(element) {
    if (element) {
        element.__nooseQuill = null;
    }
}
