// Ponte JavaScript verso il C# (vedi Bridge.cs). Ogni chiamata invia un messaggio con un
// identificativo univoco e resta in attesa della risposta corrispondente: così più richieste
// possono essere in corso contemporaneamente senza confondersi tra loro.
window.sciuscia = (function () {
  const pending = new Map();

  function generaId() {
    if (window.crypto && window.crypto.randomUUID) {
      return window.crypto.randomUUID();
    }
    return "id-" + Date.now() + "-" + Math.random().toString(16).slice(2);
  }

  function call(command, payload) {
    return new Promise((resolve, reject) => {
      if (!window.chrome || !window.chrome.webview) {
        reject(new Error("Il collegamento con il programma non è disponibile."));
        return;
      }

      const requestId = generaId();
      pending.set(requestId, { resolve, reject });
      window.chrome.webview.postMessage(JSON.stringify({
        requestId,
        command,
        payload: payload || {},
      }));
    });
  }

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", (event) => {
      let message;
      try {
        message = typeof event.data === "string" ? JSON.parse(event.data) : event.data;
      } catch {
        return;
      }

      const waiting = pending.get(message.requestId);
      if (!waiting) {
        return;
      }
      pending.delete(message.requestId);

      if (message.error) {
        waiting.reject(new Error(message.error));
      } else {
        waiting.resolve(message.result);
      }
    });
  }

  return { call };
})();
