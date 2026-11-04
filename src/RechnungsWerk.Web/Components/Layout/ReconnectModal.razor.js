// Set up event handlers
const reconnectModal = document.getElementById("components-reconnect-modal");
reconnectModal.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);

const retryButton = document.getElementById("components-reconnect-button");
retryButton.addEventListener("click", retry);

const resumeButton = document.getElementById("components-resume-button");
resumeButton.addEventListener("click", resume);

// Solange keine Verbindung besteht, bleibt der Hinweis offen: Esc schloss ihn sonst dauerhaft, und Eingaben liefen
// ohne Hinweis ins Leere. Chrome schließt beim zweiten Esc trotz preventDefault – dann öffnet er sich gleich wieder.
let shown = false;
reconnectModal.addEventListener("cancel", (event) => event.preventDefault());
reconnectModal.addEventListener("close", () => {
    if (shown) {
        reconnectModal.showModal();
    }
});

function showModal() {
    shown = true;
    if (!reconnectModal.open) {
        reconnectModal.showModal();
    }
}

function hideModal() {
    shown = false;
    reconnectModal.close();
}

function handleReconnectStateChanged(event) {
    const state = event.detail.state;
    if (state === "hide") {
        hideModal();
    } else if (state === "rejected") {
        location.reload();
    } else {
        // show, retrying, failed, paused: Der Hinweis muss sichtbar sein, auch wenn er zwischendurch zu war.
        showModal();
        if (state === "failed") {
            document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
        } else if (state === "paused") {
            resumeButton.textContent = "Fortsetzen";
        }
    }
}

async function retry() {
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);

    try {
        // Reconnect will asynchronously return:
        // - true to mean success
        // - false to mean we reached the server, but it rejected the connection (e.g., unknown circuit ID)
        // - exception to mean we didn't reach the server (this can be sync or async)
        const successful = await Blazor.reconnect();
        if (!successful) {
            // We have been able to reach the server, but the circuit is no longer available.
            // We'll reload the page so the user can continue using the app as quickly as possible.
            const resumeSuccessful = await Blazor.resumeCircuit();
            if (!resumeSuccessful) {
                location.reload();
            } else {
                hideModal();
            }
        }
    } catch (err) {
        // We got an exception, server is currently unavailable
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    }
}

async function resume() {
    try {
        const successful = await Blazor.resumeCircuit();
        if (!successful) {
            location.reload();
        }
    } catch {
        reconnectModal.classList.replace("components-reconnect-paused", "components-reconnect-resume-failed");
        // Die Meldung sagt „lässt sich nicht fortsetzen“ – der Knopf versucht es noch einmal.
        resumeButton.textContent = "Erneut versuchen";
    }
}

async function retryWhenDocumentBecomesVisible() {
    if (document.visibilityState === "visible") {
        await retry();
    }
}
