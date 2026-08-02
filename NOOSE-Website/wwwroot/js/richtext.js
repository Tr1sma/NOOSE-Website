// quill interop

let quillLadenPromise = null;
let tabellenModulPromise = null; // table handler
let groessenRegistriert = false;
const SCHRIFTGROESSEN = ['0.75em', '1.5em', '2.5em']; // inline font-size values
const SCROLL_TOLERANZ = 2; // ignore sub-pixel drift

// register style-based size so font-size survives sanitization
function registriereGroessen() {
    if (groessenRegistriert || !window.Quill) {
        return;
    }
    const SizeStyle = window.Quill.import('attributors/style/size');
    SizeStyle.whitelist = SCHRIFTGROESSEN;
    window.Quill.register(SizeStyle, true);
    groessenRegistriert = true;
}

// quill focuses hidden nodes; suppress the induced scroll
function entschaerfeFokus(node) {
    if (!node || node.__nooseFokusOhneScroll || typeof node.focus !== 'function') {
        return;
    }
    const originalFokus = node.focus;
    node.focus = function (optionen) {
        return originalFokus.call(this, Object.assign({}, optionen, { preventScroll: true }));
    };
    node.__nooseFokusOhneScroll = true;
}

// window plus every scrollable ancestor (dialog body)
function sammleScrollStand(node) {
    const stand = [{ ziel: window, x: window.scrollX, y: window.scrollY }];
    let eltern = node ? node.parentElement : null;
    while (eltern && eltern !== document.body && eltern !== document.documentElement) {
        if (eltern.scrollHeight > eltern.clientHeight || eltern.scrollWidth > eltern.clientWidth) {
            stand.push({ ziel: eltern, x: eltern.scrollLeft, y: eltern.scrollTop });
        }
        eltern = eltern.parentElement;
    }
    return stand;
}

// only correct real drift
function stelleScrollStandHer(stand) {
    for (const eintrag of stand) {
        if (eintrag.ziel === window) {
            if (Math.abs(window.scrollY - eintrag.y) > SCROLL_TOLERANZ
                || Math.abs(window.scrollX - eintrag.x) > SCROLL_TOLERANZ) {
                window.scrollTo(eintrag.x, eintrag.y);
            }
            continue;
        }
        if (Math.abs(eintrag.ziel.scrollTop - eintrag.y) > SCROLL_TOLERANZ) {
            eintrag.ziel.scrollTop = eintrag.y;
        }
        if (Math.abs(eintrag.ziel.scrollLeft - eintrag.x) > SCROLL_TOLERANZ) {
            eintrag.ziel.scrollLeft = eintrag.x;
        }
    }
}

// second layer for engines without preventScroll
function haengeScrollWaechterAn(element) {
    if (!element || element.__nooseScrollWaechter) {
        return;
    }
    // capture runs before quill's own handler
    element.addEventListener('paste', () => {
        const stand = sammleScrollStand(element);
        requestAnimationFrame(() => stelleScrollStandHer(stand));
        // quill finishes inside setTimeout(1)
        setTimeout(() => stelleScrollStandHer(stand), 40);
    }, true);
    element.__nooseScrollWaechter = true;
}

// quill 1.x drops pasted image files (screenshots)
function haengeBildEinfuegungAn(element) {
    element.addEventListener('paste', (ereignis) => {
        const daten = ereignis.clipboardData;
        if (!daten || daten.getData('text/html')) {
            return; // html pastes go through the img matcher
        }
        const bild = Array.from(daten.files || []).find(d => d.type.startsWith('image/'));
        const editor = bild && element.__nooseQuill;
        if (!editor) {
            return;
        }
        ereignis.preventDefault();
        ereignis.stopPropagation();
        const leser = new FileReader();
        leser.onload = () => {
            const bereich = editor.getSelection(true);
            editor.insertEmbed(bereich.index, 'image', leser.result, 'user');
            editor.setSelection(bereich.index + 1, 0, 'silent');
        };
        leser.readAsDataURL(bild);
    }, true); // capture runs before quill's own handler
}

// pasted images with external or blob src: inline as data uri — links rot, blob: dies with the tab
function registriereBildMatcher(editor) {
    editor.clipboard.addMatcher('img', (node, delta) => {
        const quelle = (node.getAttribute('src') || '').trim();
        if (!quelle || quelle.startsWith('data:')) {
            return delta;
        }
        // matchers stay sync; replace async, original src is the fallback
        setTimeout(() => ersetzeDurchDataUrl(editor, quelle), 1);
        return delta;
    });
}

// fetch + swap every matching img blot
async function ersetzeDurchDataUrl(editor, quelle) {
    let dataUrl;
    try {
        const antwort = await fetch(quelle);
        if (!antwort.ok) {
            return;
        }
        const blob = await antwort.blob();
        if (!blob.type.startsWith('image/')) {
            return;
        }
        dataUrl = await new Promise((resolve, reject) => {
            const leser = new FileReader();
            leser.onload = () => resolve(leser.result);
            leser.onerror = reject;
            leser.readAsDataURL(blob);
        });
    } catch (e) {
        return; // cors etc: keep original src
    }
    const Delta = window.Quill.import('delta');
    for (const img of Array.from(editor.root.querySelectorAll('img'))) {
        if ((img.getAttribute('src') || '') !== quelle || !img.isConnected) {
            continue;
        }
        const blot = window.Quill.find(img);
        if (!blot) {
            continue;
        }
        try {
            const index = editor.getIndex(blot);
            editor.updateContents(new Delta().retain(index).insert({ image: dataUrl }).delete(1), 'user');
        } catch (e) {
            /* blot already gone */
        }
    }
}

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

// load table module
function ladeTabellenModul() {
    if (tabellenModulPromise) {
        return tabellenModulPromise;
    }
    tabellenModulPromise = (async () => {
        try {
            await ladeQuill(); // quill first
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
                // fix list formats
                mod.rewirteFormats();
            }
            return TableHandler;
        } catch (e) {
            // fallback: no tables
            console.error('table module failed', e);
            return null;
        }
    })();
    return tabellenModulPromise;
}

export async function initRichText(element, dotnetRef, initialHtml, minHeight) {
    await ladeQuill();
    if (!element) {
        return;
    }
    registriereGroessen();
    const tableHandler = await ladeTabellenModul();

    const toolbarGruppen = [
        [{ header: [1, 2, 3, false] }],
        [{ size: ['0.75em', false, '1.5em', '2.5em'] }],
        ['bold', 'italic', 'underline', 'strike'],
        [{ list: 'ordered' }, { list: 'bullet' }],
        ['blockquote', 'code-block'],
        [{ color: [] }, { background: [] }],
        ['link', 'image', 'clean'],
    ];
    const module = {};
    if (tableHandler) {
        // table toolbar
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

    // must run before any content injection
    entschaerfeFokus(editor.root);
    entschaerfeFokus(editor.clipboard && editor.clipboard.container);
    haengeScrollWaechterAn(element);
    haengeBildEinfuegungAn(element);
    registriereBildMatcher(editor);

    if (minHeight) {
        editor.root.style.minHeight = minHeight;
    }

    if (initialHtml) {
        editor.clipboard.dangerouslyPasteHTML(initialHtml);
    }

    let timer = null;
    editor.on('text-change', () => {
        if (timer) {
            clearTimeout(timer);
        }
        // debounce
        timer = setTimeout(() => {
            dotnetRef.invokeMethodAsync('OnHtmlChanged', leseHtml(editor));
        }, 300);
    });

    element.__nooseQuill = editor;
}

// empty check
function leseHtml(editor) {
    const ohneText = editor.getText().trim().length === 0;
    const ohneTabelle = editor.root.querySelector('table') === null;
    const ohneBild = editor.root.querySelector('img') === null;
    return ohneText && ohneTabelle && ohneBild ? '' : editor.root.innerHTML;
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
