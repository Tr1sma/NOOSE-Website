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
