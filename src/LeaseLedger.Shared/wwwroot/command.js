window.leaseLedgerBack = () => history.back();

window.leaseLedgerScrollEnd = (element) => {
    if (element)
        element.scrollTop = element.scrollHeight;
};

window.leaseLedgerInputValue = (element) => element?.value ?? "";

window.leaseLedgerSetInput = (element, value) => {
    if (element)
        element.value = value ?? "";
};

window.leaseLedgerListen = () => new Promise((resolve) => {
    const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!Recognition) {
        resolve("");
        return;
    }

    const recognition = new Recognition();
    recognition.lang = "en-CA";
    recognition.onresult = (event) => resolve(event.results[0][0].transcript || "");
    recognition.onerror = () => resolve("");
    recognition.start();
});

const leasePdfJsRoot = "/_content/LeaseLedger.Shared/pdfjs/";

function leaseLedgerBytes(content) {
    if (typeof content === "string")
        return Uint8Array.from(atob(content), character => character.charCodeAt(0));
    return content instanceof Uint8Array ? content : new Uint8Array(content);
}

function ensurePdfJs() {
    if (window.pdfjsLib)
        return Promise.resolve(window.pdfjsLib);

    window._leasePdfJs ??= new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = leasePdfJsRoot + "pdf.min.js";
        script.onload = () => {
            window.pdfjsLib.GlobalWorkerOptions.workerSrc = leasePdfJsRoot + "pdf.worker.min.js";
            resolve(window.pdfjsLib);
        };
        script.onerror = () => reject(new Error("PDF viewer failed to load."));
        document.head.appendChild(script);
    });
    return window._leasePdfJs;
}

window.leaseLedgerPreview = async (host, content) => {
    const bytes = leaseLedgerBytes(content);
    const showMessage = (text) => {
        host.replaceChildren();
        const message = document.createElement("p");
        message.className = "muted";
        message.textContent = text;
        host.appendChild(message);
    };

    showMessage("Loading the PDF…");
    try {
        const pdfjs = await ensurePdfJs();
        const pdf = await pdfjs.getDocument({ data: bytes }).promise;
        if (!host.isConnected)
            return;

        host.replaceChildren();
        const width = Math.max(host.clientWidth - 16, 280);
        for (let number = 1; number <= pdf.numPages; number++) {
            const page = await pdf.getPage(number);
            const base = page.getViewport({ scale: 1 });
            const viewport = page.getViewport({ scale: width / base.width });
            const canvas = document.createElement("canvas");
            const ratio = window.devicePixelRatio || 1;
            canvas.width = Math.floor(viewport.width * ratio);
            canvas.height = Math.floor(viewport.height * ratio);
            const context = canvas.getContext("2d");
            context.setTransform(ratio, 0, 0, ratio, 0, 0);
            host.appendChild(canvas);
            await page.render({ canvasContext: context, viewport }).promise;
        }
    } catch {
        if (host.isConnected)
            showMessage("The PDF could not be shown.");
    }
};

window.leaseLedgerDownload = (name, content) => {
    const bytes = leaseLedgerBytes(content);
    const url = URL.createObjectURL(new Blob([bytes], { type: "application/pdf" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = name || "form.pdf";
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
};
