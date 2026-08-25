// Copy deterrents for the applicant questionnaire.
// NOT a security control: the questions are in the DOM, so devtools, "save as" or a screenshot
// with OCR always get them. This only stops convenient copying (select + Ctrl+C, right-click,
// Ctrl+P), which is the case that actually happens.

const BLOCKED_KEYS = new Set(['c', 'x', 'a', 'p', 's']);
const SWALLOWED = ['selectstart', 'copy', 'cut', 'dragstart', 'contextmenu'];

// handles stay on the JS side so no object reference has to cross the interop boundary
const active = new Map();

// Typing must keep working, so only the shortcuts are swallowed - never keydown wholesale.
function onKeyDown(event) {
    if (!event.ctrlKey && !event.metaKey) {
        return;
    }
    if (BLOCKED_KEYS.has(event.key?.toLowerCase())) {
        event.preventDefault();
        event.stopPropagation();
    }
}

function swallow(event) {
    event.preventDefault();
}

/// Attach the deterrents to a container; repeated calls for the same id are a no-op.
export function schuetzen(containerId) {
    const container = document.getElementById(containerId);
    if (!container || active.has(containerId)) {
        return;
    }
    // capture phase so a MudBlazor handler cannot swallow the event first
    container.addEventListener('keydown', onKeyDown, true);
    SWALLOWED.forEach(name => container.addEventListener(name, swallow, true));
    active.set(containerId, container);
}

/// Detach again; safe to call for an unknown or already-released id.
export function freigeben(containerId) {
    const container = active.get(containerId);
    if (!container) {
        return;
    }
    container.removeEventListener('keydown', onKeyDown, true);
    SWALLOWED.forEach(name => container.removeEventListener(name, swallow, true));
    active.delete(containerId);
}
