// Ctrl+V of an image into a plain text field: the bitmap goes to the server, the text keeps a token.
// A textarea cannot hold a picture, and blazor's ClipboardEventArgs carries no data, so the read happens here.

const MAX_BYTES = 8 * 1024 * 1024;

function findeFeld(container) {
    return container ? container.querySelector('textarea, input[type=text]') : null;
}

// data uri minus the "data:<type>;base64," head: the server wants the bytes, not the envelope
function teileDataUrl(dataUrl) {
    const komma = dataUrl.indexOf(',');
    const kopf = dataUrl.slice(0, komma);
    return {
        contentType: kopf.slice(5, kopf.indexOf(';')),
        base64: dataUrl.slice(komma + 1),
    };
}

export function attach(container, ref) {
    const feld = findeFeld(container);
    if (!feld || feld.__nooseTextbild) {
        return;
    }
    const handler = (ereignis) => {
        const daten = ereignis.clipboardData;
        if (!daten) {
            return;
        }
        const bild = Array.from(daten.files || []).find(d => d.type.startsWith('image/'));
        // text wins whenever the clipboard carries any: pasting a copied cell range must stay a paste of text
        if (!bild || daten.getData('text/plain')) {
            return;
        }
        if (bild.size > MAX_BYTES) {
            ref.invokeMethodAsync('OnImageTooLarge').catch(() => { });
            return;
        }
        ereignis.preventDefault();
        ereignis.stopPropagation();
        const caret = typeof feld.selectionStart === 'number' ? feld.selectionStart : -1;
        const leser = new FileReader();
        leser.onload = () => {
            const teile = teileDataUrl(String(leser.result));
            ref.invokeMethodAsync('OnImagePastedAsync', teile.base64, teile.contentType, caret)
                .catch(() => { });
        };
        leser.readAsDataURL(bild);
    };
    feld.addEventListener('paste', handler, true); // capture: before the field inserts anything itself
    feld.__nooseTextbild = handler;
}

export function detach(container) {
    const feld = findeFeld(container);
    if (feld && feld.__nooseTextbild) {
        feld.removeEventListener('paste', feld.__nooseTextbild, true);
        feld.__nooseTextbild = null;
    }
}
