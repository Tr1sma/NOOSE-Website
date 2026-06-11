// NOOSE WYSIWYG-Interop für den RichTextEditor (Quill 1.3.7, selbst gehostet unter /lib/quill).
// Quill + dessen CSS werden bei Bedarf nachgeladen (nur auf Editor-Seiten), damit andere Seiten
// nicht belastet werden. Die UMD-Variante setzt window.Quill.

let quillLadenPromise = null;

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

export async function initRichText(element, dotnetRef, initialHtml) {
    await ladeQuill();
    if (!element) {
        return;
    }
    const editor = new window.Quill(element, {
        theme: 'snow',
        placeholder: 'Dokument verfassen…',
        modules: {
            toolbar: [
                [{ header: [1, 2, 3, false] }],
                ['bold', 'italic', 'underline', 'strike'],
                [{ list: 'ordered' }, { list: 'bullet' }],
                ['blockquote', 'code-block'],
                [{ color: [] }, { background: [] }],
                ['link', 'clean'],
            ],
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
        // Entprellt an .NET zurückmelden (statt einer Interop-Runde je Tastenanschlag).
        timer = setTimeout(() => {
            dotnetRef.invokeMethodAsync('OnHtmlChanged', leseHtml(editor));
        }, 300);
    });

    element.__nooseQuill = editor;
}

// Liefert leeren String, wenn der Editor faktisch leer ist (Quill speichert sonst "<p><br></p>").
function leseHtml(editor) {
    return editor.getText().trim().length === 0 ? '' : editor.root.innerHTML;
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
