window.workHours = {
    // Chiede al browser di non cancellare mai i dati salvati (se supportato).
    requestPersistentStorage: async () => {
        if (navigator.storage && navigator.storage.persist) {
            try { return await navigator.storage.persist(); } catch { return false; }
        }
        return false;
    },

    // Esporta il backup: su iPhone apre il menu Condividi (Salva su File, invia, ecc.),
    // altrove scarica il file.
    saveFile: async (fileName, content) => {
        const file = new File([content], fileName, { type: "application/json" });
        if (navigator.canShare && navigator.canShare({ files: [file] })) {
            try {
                await navigator.share({ files: [file], title: fileName });
                return true;
            } catch (e) {
                if (e.name === "AbortError") return false; // annullato dall'utente
            }
        }
        const url = URL.createObjectURL(file);
        const a = document.createElement("a");
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 10000); // Safari ha bisogno di tempo per leggerlo
        return true;
    }
};
