// command palette interop
let commandPaletteHandler = null;

export function registerCommandPalette(dotnetRef) {
    // dedup
    unregisterCommandPalette();
    commandPaletteHandler = (e) => {
        if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
            e.preventDefault();
            dotnetRef.invokeMethodAsync('Open');
        }
    };
    window.addEventListener('keydown', commandPaletteHandler);
}

export function unregisterCommandPalette() {
    if (commandPaletteHandler) {
        window.removeEventListener('keydown', commandPaletteHandler);
        commandPaletteHandler = null;
    }
}

// scrolls an element into view (smooth, below the fixed app bar via scroll-margin)
export function scrollToElement(element) {
    element?.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

// keyboard shortcuts; the browser only recognises the sequence, C# decides what it means
let shortcutHandler = null;
let chordTimer = null;

// never steal a keystroke the agent is typing into something
function isTyping(target) {
    if (!target) { return false; }
    const tag = target.tagName;
    return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || target.isContentEditable === true;
}

export function registerShortcuts(dotnetRef) {
    unregisterShortcuts();
    shortcutHandler = (e) => {
        if (e.ctrlKey || e.metaKey || e.altKey || isTyping(e.target) || e.isComposing) { return; }

        // second letter of a chord, while the window is open
        if (chordTimer !== null) {
            clearTimeout(chordTimer);
            chordTimer = null;
            if (e.key.length === 1) {
                e.preventDefault();
                dotnetRef.invokeMethodAsync('Chord', e.key);
            }
            return;
        }

        if (e.key === 'g' || e.key === 'G') {
            e.preventDefault();
            // 1.2 s is long enough to think and short enough not to swallow the next real keystroke
            chordTimer = setTimeout(() => { chordTimer = null; }, 1200);
            return;
        }
        if (e.key === '?' || e.key === 'n' || e.key === 'N' || e.key === 'e' || e.key === 'E') {
            e.preventDefault();
            dotnetRef.invokeMethodAsync('Press', e.key === '?' ? '?' : e.key.toLowerCase());
        }
    };
    window.addEventListener('keydown', shortcutHandler);
}

export function unregisterShortcuts() {
    if (chordTimer !== null) {
        clearTimeout(chordTimer);
        chordTimer = null;
    }
    if (shortcutHandler) {
        window.removeEventListener('keydown', shortcutHandler);
        shortcutHandler = null;
    }
}
