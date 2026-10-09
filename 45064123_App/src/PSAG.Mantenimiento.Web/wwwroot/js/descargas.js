// Descarga un archivo generado por el servidor. El contenido llega codificado en base64.
export function descargarArchivo(nombre, tipoMime, base64) {
    const binario = atob(base64);
    const bytes = new Uint8Array(binario.length);
    for (let i = 0; i < binario.length; i++) {
        bytes[i] = binario.charCodeAt(i);
    }

    const url = URL.createObjectURL(new Blob([bytes], { type: tipoMime }));
    const enlace = document.createElement('a');
    enlace.href = url;
    enlace.download = nombre;
    document.body.appendChild(enlace);
    enlace.click();
    enlace.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}
