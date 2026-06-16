// JS-Interop der NOOSE-Website. Aktuell: globaler Strg+K-/Cmd+K-Hotkey für die Command-Palette.
let commandPaletteHandler = null;

export function registerCommandPalette(dotnetRef) {
    // Doppelte Registrierung vermeiden (z. B. bei Reconnect).
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
