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
