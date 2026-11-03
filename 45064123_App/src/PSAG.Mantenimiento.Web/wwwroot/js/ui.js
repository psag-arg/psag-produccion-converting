// Ayudas de pantalla para celular y tablet.

// Lleva la vista a un elemento (por ejemplo el bloque de análisis) sin depender del scroll del usuario.
export function irA(id) {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: "smooth", block: "start" });
}

// Vuelve al principio de la página (botón "Siguiente grupo" del control de proceso).
export function arriba() {
    window.scrollTo({ top: 0, behavior: "smooth" });
}
