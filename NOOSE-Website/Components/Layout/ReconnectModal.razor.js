// event handlers
const reconnectModal = document.getElementById("components-reconnect-modal");
reconnectModal.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);

const retryButton = document.getElementById("components-reconnect-button");
retryButton.addEventListener("click", retry);

const resumeButton = document.getElementById("components-resume-button");
resumeButton.addEventListener("click", resume);

const reloadButton = document.getElementById("components-reload-button");
reloadButton.addEventListener("click", () => location.reload());

const dismissButton = document.getElementById("components-dismiss-button");
dismissButton.addEventListener("click", () => reconnectModal.close());

const stateClasses = [
    "components-reconnect-show",
    "components-reconnect-retrying",
    "components-reconnect-failed",
    "components-reconnect-paused",
    "components-reconnect-resume-failed"
];

// never reload silently — unsaved Protokolle would be lost
function showRejected() {
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    reconnectModal.classList.remove(...stateClasses);
    reconnectModal.classList.add("components-reconnect-rejected");
    // reopen non-modally so the page behind stays selectable
    reconnectModal.close();
    reconnectModal.show();
}

function handleReconnectStateChanged(event) {
    if (event.detail.state === "show") {
        reconnectModal.showModal();
    } else if (event.detail.state === "hide") {
        reconnectModal.close();
    } else if (event.detail.state === "failed") {
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    } else if (event.detail.state === "rejected") {
        showRejected();
    }
}

async function retry() {
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);

    try {
        // true=ok, false=rejected, throws=offline
        const successful = await Blazor.reconnect();
        if (!successful) {
            // try resume
            const resumeSuccessful = await Blazor.resumeCircuit();
            if (!resumeSuccessful) {
                showRejected();
            } else {
                reconnectModal.close();
            }
        }
    } catch (err) {
        // offline
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    }
}

async function resume() {
    try {
        const successful = await Blazor.resumeCircuit();
        if (!successful) {
            showRejected();
        }
    } catch {
        reconnectModal.classList.replace("components-reconnect-paused", "components-reconnect-resume-failed");
    }
}

async function retryWhenDocumentBecomesVisible() {
    if (document.visibilityState === "visible") {
        await retry();
    }
}
