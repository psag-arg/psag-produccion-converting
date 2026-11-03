// Dictado por voz con la Web Speech API del navegador (SpeechRecognition / webkitSpeechRecognition).
// Chrome pide permiso de micrófono la primera vez. Solo funciona en HTTPS o en localhost.
// OJO: en Chrome (PC y Android) el audio lo procesa el servicio de reconocimiento de Google,
// salvo que el navegador tenga instalado el reconocimiento "en el dispositivo" (processLocally).
// En Edge lo procesa el servicio de Microsoft.

const activos = new Map();
let siguienteId = 1;

function clase() {
    return window.SpeechRecognition || window.webkitSpeechRecognition || null;
}

// "sin-api": el navegador no tiene dictado (Firefox, algunos WebView).
// "sin-https": lo tiene, pero la página no es segura (http://IP): el micrófono queda bloqueado.
// "ok": se puede usar.
export function estado() {
    if (!clase()) return "sin-api";
    if (!window.isSecureContext) return "sin-https";
    return "ok";
}

async function intentarLocal(reconocedor, idioma, soloLocal) {
    // Chrome 139+ puede reconocer en el dispositivo (el audio no sale del equipo).
    const C = clase();
    if (!("processLocally" in reconocedor) || typeof C.available !== "function") {
        return soloLocal ? "sin-local" : "nube";
    }
    try {
        const disp = await C.available({ langs: [idioma], processLocally: true });
        if (disp === "available") {
            reconocedor.processLocally = true;
            return "local";
        }
    } catch { /* se sigue con el servicio del navegador */ }
    return soloLocal ? "sin-local" : "nube";
}

// Empieza a escuchar. dotnet recibe: AlEscuchar(), AlParcial(texto), AlFinal(texto), AlError(codigo), AlTerminar().
// Se corta solo cuando la persona deja de hablar (continuous = false); si se vuelve a tocar, sigue escribiendo.
export async function iniciar(dotnet, idioma, soloLocal) {
    const C = clase();
    if (!C) { await dotnet.invokeMethodAsync("AlError", "sin-api"); return 0; }
    const r = new C();
    r.lang = idioma || "es-AR";
    r.continuous = false;
    r.interimResults = true;
    r.maxAlternatives = 1;

    const modo = await intentarLocal(r, r.lang, !!soloLocal);
    if (modo === "sin-local") { await dotnet.invokeMethodAsync("AlError", "sin-local"); return 0; }

    const id = siguienteId++;
    activos.set(id, r);
    r.onstart = () => dotnet.invokeMethodAsync("AlEscuchar");
    r.onresult = (ev) => {
        let parcial = "";
        for (let i = ev.resultIndex; i < ev.results.length; i++) {
            const res = ev.results[i];
            const texto = res[0] ? res[0].transcript : "";
            if (res.isFinal) dotnet.invokeMethodAsync("AlFinal", texto);
            else parcial += texto;
        }
        dotnet.invokeMethodAsync("AlParcial", parcial);
    };
    r.onerror = (ev) => dotnet.invokeMethodAsync("AlError", ev.error || "desconocido");
    r.onend = () => { activos.delete(id); dotnet.invokeMethodAsync("AlTerminar"); };
    try {
        r.start();
    } catch (e) {
        activos.delete(id);
        await dotnet.invokeMethodAsync("AlError", "no-arranco");
        return 0;
    }
    return id;
}

export function detener(id) {
    const r = activos.get(id);
    if (r) { try { r.stop(); } catch { /* ya terminó */ } }
}
