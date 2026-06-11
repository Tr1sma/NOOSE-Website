// NOOSE WYSIWYG-Interop für den RichTextEditor (Quill 1.3.7, selbst gehostet unter /lib/quill).
// Quill + dessen CSS werden bei Bedarf nachgeladen (nur auf Editor-Seiten), damit andere Seiten
// nicht belastet werden. Die UMD-Variante setzt window.Quill.
//
// Tabellen: Quill 1.3.7 hat KEINE eigene Tabellen-Funktion. Wir laden dafür das selbst gehostete
// Modul /lib/quill/table-module.js (vendored quill1.3.7-table-module). Es wird OPTIONAL und
// GEGUARDET geladen: schlägt es fehl, läuft der Editor ohne Tabellen-Button weiter (kein Abriss).

let quillLadenPromise = null;
let tabellenModulPromise = null; // -> Promise<TableHandler|null>

function ladeQuill() {
    if (window.Quill) {
        return Promise.resolve();
    }
    if (quillLadenPromise) {
        return quillLadenPromise;
    }
    quillLadenPromise = new Promise((resolve, reject) => {
        if (!document.querySelector('link[data-quill-css]')) {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = 'lib/quill/quill.snow.css';
            link.setAttribute('data-quill-css', '');
            document.head.appendChild(link);
        }
        const script = document.createElement('script');
        script.src = 'lib/quill/quill.min.js';
        script.onload = () => resolve();
        script.onerror = () => reject(new Error('Quill konnte nicht geladen werden.'));
        document.head.appendChild(script);
    });
    return quillLadenPromise;
}

// Lädt + registriert das Tabellen-Modul EINMALIG. Liefert die TableHandler-Klasse oder null
// (wenn das Modul nicht geladen/registriert werden konnte -> Editor läuft ohne Tabellen).
function ladeTabellenModul() {
    if (tabellenModulPromise) {
        return tabellenModulPromise;
    }
    tabellenModulPromise = (async () => {
        try {
            await ladeQuill(); // window.Quill muss stehen, bevor das Modul (-> ./quill-global.mjs) importiert wird
            if (!document.querySelector('link[data-quill-table-css]')) {
                const link = document.createElement('link');
                link.rel = 'stylesheet';
                link.href = 'lib/quill/table-module.css';
                link.setAttribute('data-quill-table-css', '');
                document.head.appendChild(link);
            }
            const mod = await import('../lib/quill/table-module.js');
            const TableHandler = mod.default;
            window.Quill.register({ ['modules/' + TableHandler.moduleName]: TableHandler }, true);
            if (typeof mod.rewirteFormats === 'function') {
                // Korrigiert native Formate (Listen) für die Anzeige innerhalb von Zellen.
                mod.rewirteFormats();
            }
            return TableHandler;
        } catch (e) {
            // Tabellen sind optional: bei Fehler ohne Tabellen weiter statt Editor-Abriss.
            console.error('NOOSE: Tabellen-Modul konnte nicht geladen werden – Editor läuft ohne Tabellen.', e);
            return null;
        }
    })();
    return tabellenModulPromise;
}

export async function initRichText(element, dotnetRef, initialHtml) {
    await ladeQuill();
    if (!element) {
        return;
    }
    const tableHandler = await ladeTabellenModul();

    const toolbarGruppen = [
        [{ header: [1, 2, 3, false] }],
        ['bold', 'italic', 'underline', 'strike'],
        [{ list: 'ordered' }, { list: 'bullet' }],
        ['blockquote', 'code-block'],
        [{ color: [] }, { background: [] }],
        ['link', 'clean'],
    ];
    const module = {};
    if (tableHandler) {
        // Eigene Toolbar-Gruppe mit dem Tabellen-Werkzeug. WICHTIG: als SELECT konfigurieren
        // ({ table: [] }) – NICHT als einfacher Button ([toolName]). Nur für ein <select> baut das
        // Snow-Theme einen echten Picker mit Label + ausklappbarem .ql-picker-options; genau dort
        // hinein hängt das Modul sein Auswahl-Raster (buildCustomSelect appended nur bei tagName
        // 'select' in .ql-picker-options, sonst direkt in den Button). Als Button läge das 8x8-Raster
        // sonst DAUERHAFT sichtbar in der Toolbar – und ohne Tabellen-Icon.
        toolbarGruppen.push([{ [tableHandler.toolName]: [] }]);
        module[tableHandler.moduleName] = {
            fullWidth: false,
            customButton: 'Eigene Größe',
        };
    }
    module.toolbar = toolbarGruppen;

    const editor = new window.Quill(element, {
        theme: 'snow',
        placeholder: 'Dokument verfassen…',
        modules: module,
    });

    if (initialHtml) {
        editor.clipboard.dangerouslyPasteHTML(initialHtml);
    }

    let timer = null;
    editor.on('text-change', () => {
        if (timer) {
            clearTimeout(timer);
        }
        // Entprellt an .NET zurückmelden (statt einer Interop-Runde je Tastenanschlag).
        timer = setTimeout(() => {
            dotnetRef.invokeMethodAsync('OnHtmlChanged', leseHtml(editor));
        }, 300);
    });

    element.__nooseQuill = editor;
}

// Liefert leeren String, wenn der Editor faktisch leer ist (Quill speichert sonst "<p><br></p>").
// Ausnahme: enthält der Editor eine Tabelle, gilt er NICHT als leer (leere Zellen liefern keinen Text).
function leseHtml(editor) {
    const ohneText = editor.getText().trim().length === 0;
    const ohneTabelle = editor.root.querySelector('table') === null;
    return ohneText && ohneTabelle ? '' : editor.root.innerHTML;
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
    return editor ? leseHtml(editor) : '';
}

export function destroyRichText(element) {
    if (element) {
        element.__nooseQuill = null;
    }
}
