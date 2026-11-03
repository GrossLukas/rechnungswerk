// Kleine Helfer ohne Inline-Skripte, damit die strenge Content-Security-Policy greift.
document.addEventListener('click', (event) => {
    const trigger = event.target instanceof Element ? event.target.closest('[data-print]') : null;
    if (trigger) {
        event.preventDefault();
        window.print();
    }
});
