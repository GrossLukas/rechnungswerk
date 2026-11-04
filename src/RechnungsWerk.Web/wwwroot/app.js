// Kleine Helfer ohne Inline-Skripte, damit die strenge Content-Security-Policy greift.

// Knöpfe mit data-print öffnen den Druckdialog.
document.addEventListener('click', (event) => {
    const trigger = event.target instanceof Element ? event.target.closest('[data-print]') : null;
    if (trigger) {
        event.preventDefault();
        window.print();
    }
});

// „Zum Inhalt springen“ setzt den Fokus in den Inhalt derselben Seite. Der Anker allein würde über <base href="/">
// zur Startseite führen und Eingaben verwerfen.
document.addEventListener('click', (event) => {
    const skip = event.target instanceof Element ? event.target.closest('.skip-link') : null;
    if (skip) {
        event.preventDefault();
        document.getElementById('main')?.focus();
    }
});

// Strg/Cmd+Enter speichert das Formular, in dem der Fokus steht.
document.addEventListener('keydown', (event) => {
    if (event.key !== 'Enter' || !(event.ctrlKey || event.metaKey) || !(event.target instanceof Element)) {
        return;
    }

    const form = event.target.closest('form');
    if (form) {
        event.preventDefault();
        // Erst den gerade getippten Wert übernehmen, dann absenden.
        event.target.dispatchEvent(new Event('change', { bubbles: true }));
        form.requestSubmit();
    }
});

// Ablagefläche hervorheben, solange eine Datei darüber gezogen wird.
const dropzoneOf = (event) => (event.target instanceof Element ? event.target.closest('.dropzone') : null);
for (const type of ['dragenter', 'dragover']) {
    document.addEventListener(type, (event) => dropzoneOf(event)?.classList.add('is-dragover'));
}
for (const type of ['dragleave', 'drop']) {
    document.addEventListener(type, (event) => dropzoneOf(event)?.classList.remove('is-dragover'));
}

// Aufrufe aus den Live-Seiten (JS-Interop).
window.rechnungsWerk = {
    // Live-Seiten zeichnen erst in der Verbindung; ihr <PageTitle> erreicht die statische HeadOutlet nie.
    setTitle: (title) => {
        document.title = title;
    },
    // Fokus auf ein Element, das erst mit dem letzten Zeichnen erschienen ist.
    focus: (selector) => {
        document.querySelector(selector)?.focus();
    },
};
