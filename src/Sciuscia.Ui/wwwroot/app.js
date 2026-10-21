(function () {
  const corpoTabella = document.getElementById("corpo-tabella-commesse");
  const statoVuoto = document.getElementById("stato-vuoto");
  const bannerErrore = document.getElementById("error-banner");

  const modale = document.getElementById("modal-nuova-commessa");
  const inputRicercaCliente = document.getElementById("ricerca-cliente");
  const risultatiClienti = document.getElementById("risultati-clienti");
  const boxNuovoCliente = document.getElementById("box-nuovo-cliente");

  const vistaHome = document.getElementById("view-home");
  const vistaElenco = document.getElementById("view-elenco-commesse");
  const vistaDettaglio = document.getElementById("view-dettaglio-commessa");
  const listaArticoli = document.getElementById("lista-articoli");
  const dettaglioStatoVuoto = document.getElementById("dettaglio-stato-vuoto");

  const modaleArticolo = document.getElementById("modal-nuovo-articolo");
  const modaleLavorazione = document.getElementById("modal-lavorazione");
  const campoFascia = document.getElementById("campo-fascia");
  const campoArtigiano = document.getElementById("campo-artigiano");
  const checkboxEsterna = document.getElementById("lavorazione-esterna");
  const selectLavorazioneTipo = document.getElementById("lavorazione-tipo");
  const selectLavorazioneFascia = document.getElementById("lavorazione-fascia");

  const modaleFoto = document.getElementById("modal-foto");
  const videoFoto = document.getElementById("foto-video");
  const canvasFoto = document.getElementById("foto-canvas");
  const anteprimaWebcam = document.getElementById("foto-anteprima-webcam");
  const anteprimaFile = document.getElementById("foto-anteprima-file");
  const erroreWebcam = document.getElementById("foto-webcam-errore");
  const bottoneScattaFoto = document.getElementById("btn-scatta-foto");
  const bottoneRifaiFoto = document.getElementById("btn-rifai-foto");
  const bottoneSalvaFoto = document.getElementById("btn-salva-foto");
  const inputFileFoto = document.getElementById("foto-file-input");
  const bottoneSceglieFileFoto = document.getElementById("btn-scegli-file-foto");
  const nomeFileFotoScelta = document.getElementById("foto-file-nome");

  const corpoTabellaPagamenti = document.getElementById("corpo-tabella-pagamenti");
  const pagamentiStatoVuoto = document.getElementById("pagamenti-stato-vuoto");
  const corpoTabellaRitiri = document.getElementById("corpo-tabella-ritiri");
  const ritiriStatoVuoto = document.getElementById("ritiri-stato-vuoto");
  const corpoTabellaMessaggi = document.getElementById("corpo-tabella-messaggi");
  const messaggiStatoVuoto = document.getElementById("messaggi-stato-vuoto");
  const corpoTabellaStampe = document.getElementById("corpo-tabella-stampe");
  const stampeStatoVuoto = document.getElementById("stampe-stato-vuoto");

  const modalePagamento = document.getElementById("modal-pagamento");
  const modaleCorrezionePagamento = document.getElementById("modal-correzione-pagamento");
  const modaleRitiro = document.getElementById("modal-ritiro");
  const modaleMessaggio = document.getElementById("modal-messaggio");
  const areaStampaContenuto = document.getElementById("area-stampa-contenuto");

  const ETICHETTE_TIPO_PAGAMENTO = {
    CONTANTI: "Contanti", CARTA: "Carta", BONIFICO: "Bonifico", ALTRO: "Altro", CORREZIONE: "Correzione", RIMBORSO: "Rimborso",
  };
  const ETICHETTE_TIPO_MESSAGGIO = {
    WHATSAPP: "WhatsApp", SMS: "SMS", EMAIL: "Email", TELEFONO: "Telefonata", ALTRO: "Altro",
  };
  const ETICHETTE_SCOPO_MESSAGGIO = {
    AVVISO_PRONTO: "Avviso pronto", PREVENTIVO: "Preventivo", SOLLECITO: "Sollecito ritiro", ALTRO: "Altro",
  };
  const ETICHETTE_DOCUMENTO_STAMPA = {
    COPIA_CLIENTE: "Ricevuta cliente", COPIA_INTERNA: "Copia interna", FOTOGRAFIE: "Fotografie", ETICHETTA: "Etichetta",
  };
  const ETICHETTE_ESITO_STAMPA = { RIUSCITA: "Riuscita", ERRORE: "Errore", ANNULLATA: "Annullata" };

  const ETICHETTE_STATO = {
    DA_LAVORARE: "Da lavorare",
    PRESSO_ARTIGIANO: "Presso l'artigiano",
    PRONTO_DA_AVVISARE: "Pronto, da avvisare",
    AVVISATO: "Avvisato",
    CONSEGNATO: "Consegnato",
    ANNULLATO: "Annullato",
  };

  const vistaAnagrafiche = document.getElementById("view-anagrafiche");
  const vistaImpostazioni = document.getElementById("view-impostazioni");
  const vistaAgenda = document.getElementById("view-agenda");
  const vistaPdr = document.getElementById("view-pdr");
  const vistaPa = document.getElementById("view-pa");
  let ricercaClientiAnagraficaInCorso = null;
  let articoloDaEvidenziareId = null;
  let agendaCache = [];
  let anteprimaAgendaCorrente = null;
  let consultaCaricoContesto = null;
  let pdrCache = [];
  let paCache = [];
  let anteprimaArtigianiCorrente = null;

  let clienteSelezionatoId = null;
  let ricercaInCorso = null;
  let commessaCorrenteId = null;
  let articoloPerLavorazioneId = null;
  let articoloPerLavorazioneCodice = null;
  let listinoCache = null;
  let clienteAnagraficaInModifica = null;
  let categoriaAnagraficaInModifica = null;
  let artigianoAnagraficaInModifica = null;
  let marcaAnagraficaInModifica = null;
  let lavorazioneListinoInModifica = null;
  let lavorazioneListinoPerFasce = null;
  let lavorazioneInModifica = null;
  let articoloPerFotoId = null;
  let streamWebcamAttivo = null;
  let fotoCatturataDataUrl = null;
  let fotoDaFileDataUrl = null;
  let fotoDaFileNome = null;
  let dettaglioCorrente = null;
  let pagamentoInCorrezioneId = null;
  let stampaInCorso = null;

  function formattaEuro(centesimi) {
    return (centesimi / 100).toLocaleString("it-IT", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }) + " €";
  }

  function formattaData(isoText) {
    if (!isoText) {
      return "";
    }
    const data = new Date(isoText);
    if (Number.isNaN(data.getTime())) {
      return isoText;
    }
    return data.toLocaleDateString("it-IT");
  }

  function mostraErrore(messaggio) {
    bannerErrore.textContent = messaggio;
    bannerErrore.hidden = false;
  }

  function nascondiErrore() {
    bannerErrore.hidden = true;
    bannerErrore.textContent = "";
  }

  async function caricaCommesse() {
    nascondiErrore();
    try {
      const commesse = await window.sciuscia.call("commesse.elenco");
      renderCommesse(commesse || []);
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare le commesse: " + errore.message);
    }
  }

  function renderCommesse(commesse) {
    corpoTabella.innerHTML = "";

    if (commesse.length === 0) {
      statoVuoto.hidden = false;
      return;
    }
    statoVuoto.hidden = true;

    for (const commessa of commesse) {
      const riga = document.createElement("tr");
      riga.className = "riga-cliccabile";

      const saldoClasse = commessa.balanceCents > 0 ? "saldo-positivo" : "saldo-zero";

      riga.innerHTML = `
        <td>${escapeHtml(commessa.visibleCode)}</td>
        <td>${escapeHtml(commessa.customerName)}</td>
        <td>${commessa.itemCount}</td>
        <td><span class="badge aperta">Aperta</span></td>
        <td>${formattaEuro(commessa.totalCents)}</td>
        <td>${formattaEuro(commessa.paidCents)}</td>
        <td class="${saldoClasse}">${formattaEuro(commessa.balanceCents)}</td>
        <td><button type="button" class="btn-apri-commessa">Apri</button></td>
      `;
      riga.querySelector(".btn-apri-commessa").addEventListener("click", () => apriDettaglioCommessa(commessa.orderId));
      corpoTabella.appendChild(riga);
    }
  }

  function escapeHtml(testo) {
    const div = document.createElement("div");
    div.textContent = testo ?? "";
    return div.innerHTML;
  }

  // --- Nuova commessa ---

  function apriModale() {
    modale.hidden = false;
    inputRicercaCliente.value = "";
    risultatiClienti.hidden = true;
    risultatiClienti.innerHTML = "";
    boxNuovoCliente.hidden = true;
    document.getElementById("nuovo-cognome").value = "";
    document.getElementById("nuovo-nome").value = "";
    document.getElementById("nuovo-cellulare").value = "";
    clienteSelezionatoId = null;
    inputRicercaCliente.focus();
  }

  function chiudiModale() {
    modale.hidden = true;
  }

  function pianificaRicercaCliente() {
    if (ricercaInCorso) {
      clearTimeout(ricercaInCorso);
    }
    ricercaInCorso = setTimeout(eseguiRicercaCliente, 300);
  }

  async function eseguiRicercaCliente() {
    const query = inputRicercaCliente.value.trim();
    if (query.length < 2) {
      risultatiClienti.hidden = true;
      risultatiClienti.innerHTML = "";
      return;
    }

    try {
      const clienti = await window.sciuscia.call("clienti.cerca", { query });
      renderRisultatiClienti(clienti || []);
    } catch (errore) {
      mostraErrore("Ricerca cliente non riuscita: " + errore.message);
    }
  }

  function renderRisultatiClienti(clienti) {
    risultatiClienti.innerHTML = "";

    if (clienti.length === 0) {
      risultatiClienti.hidden = true;
      return;
    }

    risultatiClienti.hidden = false;
    for (const cliente of clienti) {
      const riga = document.createElement("div");
      riga.className = "result-row";
      const nomeCompleto = [cliente.surname, cliente.firstName].filter(Boolean).join(" ");
      riga.innerHTML = `
        <div>
          <div class="name">${escapeHtml(nomeCompleto)}</div>
          <div class="mobile">${escapeHtml(cliente.mobileOriginal || "")}</div>
        </div>
        <button type="button">Seleziona</button>
      `;
      riga.querySelector("button").addEventListener("click", () => selezionaClienteEApriCommessa(cliente.customerId));
      risultatiClienti.appendChild(riga);
    }
  }

  async function selezionaClienteEApriCommessa(clienteId) {
    nascondiErrore();
    try {
      await window.sciuscia.call("commesse.crea", { clienteId });
      chiudiModale();
      await caricaCommesse();
    } catch (errore) {
      mostraErrore("Creazione commessa non riuscita: " + errore.message);
    }
  }

  async function creaClienteRapidoEApriCommessa() {
    const cognome = document.getElementById("nuovo-cognome").value.trim();
    const nome = document.getElementById("nuovo-nome").value.trim();
    const cellulare = document.getElementById("nuovo-cellulare").value.trim();

    if (!cognome) {
      mostraErrore("Il cognome del cliente è obbligatorio.");
      return;
    }

    nascondiErrore();
    try {
      const cliente = await window.sciuscia.call("clienti.creaRapido", {
        cognome,
        nome: nome || null,
        cellulare: cellulare || null,
      });
      await selezionaClienteEApriCommessa(cliente.customerId);
    } catch (errore) {
      mostraErrore("Creazione cliente non riuscita: " + errore.message);
    }
  }

  document.getElementById("btn-nuova-commessa").addEventListener("click", apriModale);
  document.getElementById("btn-annulla-commessa").addEventListener("click", chiudiModale);
  document.getElementById("btn-mostra-nuovo-cliente").addEventListener("click", () => {
    boxNuovoCliente.hidden = !boxNuovoCliente.hidden;
  });
  document.getElementById("btn-crea-cliente").addEventListener("click", creaClienteRapidoEApriCommessa);
  inputRicercaCliente.addEventListener("input", pianificaRicercaCliente);

  // --- Dettaglio commessa ---

  function mostraVistaElenco() {
    commessaCorrenteId = null;
    vistaDettaglio.hidden = true;
    vistaElenco.hidden = false;
    fermaWebcam();
  }

  function mostraVistaDettaglio() {
    vistaElenco.hidden = true;
    vistaDettaglio.hidden = false;
  }

  async function apriDettaglioCommessa(commessaId) {
    nascondiErrore();
    commessaCorrenteId = commessaId;
    mostraVistaDettaglio();
    await ricaricaDettaglioCommessa();
  }

  async function ricaricaDettaglioCommessa() {
    if (!commessaCorrenteId) {
      return;
    }
    nascondiErrore();
    try {
      const dettaglio = await window.sciuscia.call("commesse.dettaglio", { commessaId: commessaCorrenteId });
      renderDettaglioCommessa(dettaglio);
    } catch (errore) {
      mostraErrore("Non è stato possibile aprire la commessa: " + errore.message);
    }
  }

  function renderDettaglioCommessa(dettaglio) {
    dettaglioCorrente = dettaglio;
    const ordine = dettaglio.order;
    document.getElementById("dettaglio-titolo").textContent = "Commessa " + ordine.visibleCode;
    document.getElementById("dettaglio-cliente").textContent = dettaglio.customerName;
    document.getElementById("dettaglio-data").textContent = formattaData(ordine.receivedAt);
    document.getElementById("dettaglio-totale").textContent = formattaEuro(dettaglio.financials.totalCents);
    document.getElementById("dettaglio-pagato").textContent = formattaEuro(dettaglio.financials.paidCents);
    document.getElementById("dettaglio-saldo").textContent = formattaEuro(dettaglio.financials.balanceCents);

    listaArticoli.innerHTML = "";

    if (dettaglio.items.length === 0) {
      dettaglioStatoVuoto.hidden = false;
    } else {
      dettaglioStatoVuoto.hidden = true;
      for (const dettaglioArticolo of dettaglio.items) {
        listaArticoli.appendChild(costruisciSchedaArticolo(dettaglioArticolo));
      }
    }

    renderPagamenti(dettaglio);
    renderRitiri(dettaglio);
    renderMessaggi(dettaglio);
    renderStampe(dettaglio);

    if (articoloDaEvidenziareId) {
      const schedaEvidenziata = listaArticoli.querySelector(`[data-item-id="${articoloDaEvidenziareId}"]`);
      if (schedaEvidenziata) {
        schedaEvidenziata.classList.add("scheda-articolo-evidenziata");
        schedaEvidenziata.scrollIntoView({ behavior: "smooth", block: "center" });
      }
      articoloDaEvidenziareId = null;
    }
  }

  // --- Operazioni: Pagamenti ---

  function renderPagamenti(dettaglio) {
    corpoTabellaPagamenti.innerHTML = "";
    if (dettaglio.payments.length === 0) {
      pagamentiStatoVuoto.hidden = false;
      return;
    }
    pagamentiStatoVuoto.hidden = true;

    for (const pagamento of dettaglio.payments) {
      const classeImporto = pagamento.amountCents < 0 ? "importo-negativo" : "importo-positivo";
      const riga = document.createElement("tr");
      riga.innerHTML = `
        <td>${formattaData(pagamento.occurredAt)}</td>
        <td>${escapeHtml(ETICHETTE_TIPO_PAGAMENTO[pagamento.paymentType] || pagamento.paymentType)}</td>
        <td class="${classeImporto}">${formattaEuro(pagamento.amountCents)}</td>
        <td>${escapeHtml(pagamento.reason || "")}</td>
        <td><button type="button" class="btn-correggi-pagamento">Correggi</button></td>
      `;
      riga.querySelector(".btn-correggi-pagamento").addEventListener("click", () => apriModaleCorrezionePagamento(pagamento));
      corpoTabellaPagamenti.appendChild(riga);
    }
  }

  function apriModalePagamento() {
    if (!dettaglioCorrente) {
      return;
    }
    document.getElementById("pagamento-commessa-codice").textContent = "Commessa " + dettaglioCorrente.order.visibleCode;
    document.getElementById("pagamento-saldo-residuo").textContent =
      "Saldo residuo da pagare: " + formattaEuro(dettaglioCorrente.financials.balanceCents);
    document.getElementById("pagamento-importo").value = "";
    document.getElementById("pagamento-tipo").value = "CONTANTI";
    modalePagamento.hidden = false;
  }

  function chiudiModalePagamento() {
    modalePagamento.hidden = true;
  }

  async function salvaPagamento() {
    const importo = parseFloat(document.getElementById("pagamento-importo").value.replace(",", "."));
    if (!importo || importo <= 0) {
      mostraErrore("Indicare un importo maggiore di zero.");
      return;
    }
    const tipo = document.getElementById("pagamento-tipo").value;
    nascondiErrore();
    try {
      await window.sciuscia.call("operazioni.registraPagamento", {
        commessaId: commessaCorrenteId,
        importoCents: Math.round(importo * 100),
        tipo,
      });
      chiudiModalePagamento();
      await ricaricaDettaglioCommessa();
    } catch (errore) {
      mostraErrore("Non è stato possibile registrare il pagamento: " + errore.message);
    }
  }

  function apriModaleCorrezionePagamento(pagamento) {
    pagamentoInCorrezioneId = pagamento.paymentEventId;
    document.getElementById("correzione-pagamento-descrizione").textContent =
      "Pagamento del " + formattaData(pagamento.occurredAt) + ": " + formattaEuro(pagamento.amountCents) +
      " (" + (ETICHETTE_TIPO_PAGAMENTO[pagamento.paymentType] || pagamento.paymentType) + ")";
    document.getElementById("correzione-pagamento-importo").value = "";
    document.getElementById("correzione-pagamento-motivo").value = "";
    modaleCorrezionePagamento.hidden = false;
  }

  function chiudiModaleCorrezionePagamento() {
    modaleCorrezionePagamento.hidden = true;
    pagamentoInCorrezioneId = null;
  }

  async function salvaCorrezionePagamento() {
    const importo = parseFloat(document.getElementById("correzione-pagamento-importo").value.replace(",", "."));
    if (!importo) {
      mostraErrore("Indicare l'importo della correzione (diverso da zero).");
      return;
    }
    const motivo = document.getElementById("correzione-pagamento-motivo").value.trim();
    if (!motivo) {
      mostraErrore("Indicare il motivo della correzione.");
      return;
    }
    nascondiErrore();
    try {
      await window.sciuscia.call("operazioni.correggiPagamento", {
        commessaId: commessaCorrenteId,
        correggePagamentoId: pagamentoInCorrezioneId,
        importoCents: Math.round(importo * 100),
        motivo,
      });
      chiudiModaleCorrezionePagamento();
      await ricaricaDettaglioCommessa();
    } catch (errore) {
      mostraErrore("Non è stato possibile registrare la correzione: " + errore.message);
    }
  }

  // --- Operazioni: Ritiri ---

  function renderRitiri(dettaglio) {
    corpoTabellaRitiri.innerHTML = "";
    if (dettaglio.pickups.length === 0) {
      ritiriStatoVuoto.hidden = false;
      return;
    }
    ritiriStatoVuoto.hidden = true;

    const descrizionePerId = {};
    for (const dettaglioArticolo of dettaglio.items) {
      descrizionePerId[dettaglioArticolo.item.itemId] =
        dettaglioArticolo.item.visibleCode + " (" + dettaglioArticolo.item.description + ")";
    }

    for (const ritiro of dettaglio.pickups) {
      const articoli = (ritiro.itemIds || []).map((id) => descrizionePerId[id] || id).join("; ");
      const riga = document.createElement("tr");
      riga.innerHTML = `
        <td>${formattaData(ritiro.occurredAt)}</td>
        <td>${escapeHtml(articoli)}</td>
        <td>${escapeHtml(ritiro.notes || "")}</td>
      `;
      corpoTabellaRitiri.appendChild(riga);
    }
  }

  function apriModaleRitiro() {
    if (!dettaglioCorrente) {
      return;
    }
    document.getElementById("ritiro-commessa-codice").textContent = "Commessa " + dettaglioCorrente.order.visibleCode;
    const lista = document.getElementById("ritiro-lista-articoli");
    lista.innerHTML = "";
    const articoliDaRitirare = dettaglioCorrente.items.filter(
      (d) => d.item.workflowStatus !== "CONSEGNATO" && d.item.workflowStatus !== "ANNULLATO"
    );
    document.getElementById("ritiro-nessun-articolo").hidden = articoliDaRitirare.length > 0;
    for (const dettaglioArticolo of articoliDaRitirare) {
      const etichetta = document.createElement("label");
      const checkbox = document.createElement("input");
      checkbox.type = "checkbox";
      checkbox.value = dettaglioArticolo.item.itemId;
      etichetta.appendChild(checkbox);
      etichetta.appendChild(document.createTextNode(
        " " + dettaglioArticolo.item.visibleCode + " — " + dettaglioArticolo.categoryName + " — " + dettaglioArticolo.item.description
      ));
      lista.appendChild(etichetta);
    }
    document.getElementById("ritiro-note").value = "";
    modaleRitiro.hidden = false;
  }

  function chiudiModaleRitiro() {
    modaleRitiro.hidden = true;
  }

  async function salvaRitiro() {
    const selezionati = Array.from(document.querySelectorAll("#ritiro-lista-articoli input[type=checkbox]:checked")).map((c) => c.value);
    if (selezionati.length === 0) {
      mostraErrore("Selezionare almeno un articolo da ritirare.");
      return;
    }
    const note = document.getElementById("ritiro-note").value.trim();
    nascondiErrore();
    try {
      await window.sciuscia.call("operazioni.registraRitiro", {
        commessaId: commessaCorrenteId,
        articoliId: selezionati,
        note: note || null,
      });
      chiudiModaleRitiro();
      await ricaricaDettaglioCommessa();
    } catch (errore) {
      mostraErrore("Non è stato possibile registrare il ritiro: " + errore.message);
    }
  }

  // --- Operazioni: Messaggi ---

  function renderMessaggi(dettaglio) {
    corpoTabellaMessaggi.innerHTML = "";
    if (dettaglio.messages.length === 0) {
      messaggiStatoVuoto.hidden = false;
      return;
    }
    messaggiStatoVuoto.hidden = true;

    for (const messaggio of dettaglio.messages) {
      const riga = document.createElement("tr");
      riga.innerHTML = `
        <td>${formattaData(messaggio.sentAt)}</td>
        <td>${escapeHtml(ETICHETTE_TIPO_MESSAGGIO[messaggio.messageType] || messaggio.messageType)}</td>
        <td>${escapeHtml(ETICHETTE_SCOPO_MESSAGGIO[messaggio.purpose] || messaggio.purpose)}</td>
        <td>${escapeHtml(messaggio.recipient || "—")}</td>
        <td>${escapeHtml(messaggio.finalText)}</td>
      `;
      corpoTabellaMessaggi.appendChild(riga);
    }
  }

  /// Lavorazioni di un articolo ancora senza prezzo, in attesa di risposta del cliente al
  /// preventivo (non rifiutate, non annullate): sono quelle che un messaggio di tipo PREVENTIVO
  /// deve elencare, invece del totale già definito della commessa.
  function lavorazioniDaPreventivare(dettaglioArticolo) {
    return dettaglioArticolo.works.filter(
      (dl) => dl.work.priceStatus === "DA_DEFINIRE" && dl.work.approvalStatus !== "RIFIUTATA" && dl.work.canceledAt == null
    );
  }

  function testoMessaggioPredefinito(scopo, dettaglio, articoliSelezionatiId) {
    const codice = dettaglio.order.visibleCode;
    const articoliSelezionati = (articoliSelezionatiId || [])
      .map((id) => dettaglio.items.find((da) => da.item.itemId === id))
      .filter((da) => da != null);

    switch (scopo) {
      case "AVVISO_PRONTO":
        if (articoliSelezionati.length === 1) {
          return "Gentile cliente, il suo articolo (" + articoliSelezionati[0].categoryName + ", commessa " + codice +
            ") è pronto per il ritiro. Grazie!";
        }
        return "Gentile cliente, il suo articolo (commessa " + codice + ") è pronto per il ritiro. Grazie!";
      case "PREVENTIVO": {
        const voci = [];
        for (const dettaglioArticolo of articoliSelezionati) {
          for (const dl of lavorazioniDaPreventivare(dettaglioArticolo)) {
            voci.push(dettaglioArticolo.categoryName + " — " + dl.work.appliedLabelText);
          }
        }
        if (voci.length > 0) {
          return "Gentile cliente, per la commessa " + codice + " dobbiamo ancora definire il prezzo per: " +
            voci.join("; ") + ". Ci faccia sapere come procedere, grazie.";
        }
        if (articoliSelezionati.length === 1) {
          const articoloSelezionato = articoliSelezionati[0];
          const dettagliArticolo = articoloSelezionato.brandName
            ? articoloSelezionato.categoryName + ", " + articoloSelezionato.brandName
            : articoloSelezionato.categoryName;

          // Commessa con un solo articolo: il totale della commessa è senza ambiguità il totale di
          // quell'articolo, basta citare categoria/marca per contestualizzare.
          if (dettaglio.items.length === 1) {
            return "Gentile cliente, per la commessa " + codice + " (" + dettagliArticolo + ") il preventivo è di " +
              formattaEuro(dettaglio.financials.totalCents) + ". Fateci sapere se possiamo procedere.";
          }

          // Commessa con più articoli: il totale della commessa include anche gli altri articoli,
          // quindi citiamo anche la lavorazione per chiarire a cosa si riferisce il preventivo.
          const nomiLavorazioni = articoloSelezionato.works
            .filter((dl) => dl.work.canceledAt == null && dl.work.approvalStatus !== "RIFIUTATA")
            .map((dl) => dl.work.appliedName);
          const perLavorazione = nomiLavorazioni.length > 0 ? " per " + nomiLavorazioni.join(" e ") : "";
          return "Gentile cliente, per la commessa " + codice + " il preventivo" + perLavorazione + " (" + dettagliArticolo +
            ") è di " + formattaEuro(dettaglio.financials.totalCents) + ". Fateci sapere se possiamo procedere.";
        }
        return "Gentile cliente, il preventivo per la commessa " + codice + " è di " +
          formattaEuro(dettaglio.financials.totalCents) + ". Fateci sapere se possiamo procedere.";
      }
      case "SOLLECITO":
        return "Gentile cliente, le ricordiamo che i suoi capi sono pronti per il ritiro.";
      default:
        return "";
    }
  }

  function articoliSelezionatiMessaggio() {
    return Array.from(document.querySelectorAll("#messaggio-lista-articoli input[type=checkbox]:checked")).map((c) => c.value);
  }

  function aggiornaTestoMessaggioDaSelezione() {
    if (!dettaglioCorrente) {
      return;
    }
    const scopo = document.getElementById("messaggio-scopo").value;
    document.getElementById("messaggio-testo").value =
      testoMessaggioPredefinito(scopo, dettaglioCorrente, articoliSelezionatiMessaggio());
  }

  function aggiornaBottoneApriMessaggioEsterno() {
    const tipo = document.getElementById("messaggio-tipo").value;
    const bottone = document.getElementById("btn-apri-messaggio-esterno");
    bottone.hidden = tipo !== "WHATSAPP" && tipo !== "EMAIL";
    bottone.textContent = tipo === "WHATSAPP" ? "Apri WhatsApp con il testo pronto" : "Apri la posta con il testo pronto";
  }

  function aggiornaDestinatarioMessaggio() {
    const tipo = document.getElementById("messaggio-tipo").value;
    const campo = document.getElementById("messaggio-destinatario");
    if (dettaglioCorrente) {
      if ((tipo === "WHATSAPP" || tipo === "SMS" || tipo === "TELEFONO") && dettaglioCorrente.customerMobile) {
        campo.value = dettaglioCorrente.customerMobile;
      } else if (tipo === "EMAIL" && dettaglioCorrente.customerEmail) {
        campo.value = dettaglioCorrente.customerEmail;
      }
    }
    aggiornaBottoneApriMessaggioEsterno();
  }

  function apriModaleMessaggio(scopoIniziale, articoliPreselezionatiId) {
    if (!dettaglioCorrente) {
      return;
    }
    document.getElementById("messaggio-commessa-codice").textContent = "Commessa " + dettaglioCorrente.order.visibleCode;
    document.getElementById("messaggio-tipo").value = "WHATSAPP";
    document.getElementById("messaggio-scopo").value = scopoIniziale || "AVVISO_PRONTO";
    document.getElementById("messaggio-destinatario").value = "";
    aggiornaDestinatarioMessaggio();

    document.getElementById("messaggio-seleziona-tutti").checked = false;
    const lista = document.getElementById("messaggio-lista-articoli");
    lista.innerHTML = "";
    for (const dettaglioArticolo of dettaglioCorrente.items) {
      const etichetta = document.createElement("label");
      const checkbox = document.createElement("input");
      checkbox.type = "checkbox";
      checkbox.value = dettaglioArticolo.item.itemId;
      if (articoliPreselezionatiId && articoliPreselezionatiId.includes(dettaglioArticolo.item.itemId)) {
        checkbox.checked = true;
      }
      etichetta.appendChild(checkbox);
      etichetta.appendChild(document.createTextNode(
        " " + dettaglioArticolo.item.visibleCode + " — " + dettaglioArticolo.categoryName + " — " + dettaglioArticolo.item.description
      ));
      lista.appendChild(etichetta);
    }

    aggiornaTestoMessaggioDaSelezione();
    modaleMessaggio.hidden = false;
  }

  function chiudiModaleMessaggio() {
    modaleMessaggio.hidden = true;
  }

  async function apriMessaggioEsterno() {
    const tipo = document.getElementById("messaggio-tipo").value;
    const destinatario = document.getElementById("messaggio-destinatario").value.trim();
    const testo = document.getElementById("messaggio-testo").value;
    nascondiErrore();
    try {
      let url;
      if (tipo === "WHATSAPP") {
        const numero = destinatario.replace(/[^0-9+]/g, "").replace(/^\+/, "");
        if (!numero) {
          mostraErrore("Indicare il numero del cliente per aprire WhatsApp.");
          return;
        }
        url = "https://wa.me/" + numero + "?text=" + encodeURIComponent(testo);
      } else if (tipo === "EMAIL") {
        if (!destinatario) {
          mostraErrore("Indicare l'email del cliente per aprire la posta.");
          return;
        }
        url = "mailto:" + encodeURIComponent(destinatario) + "?body=" + encodeURIComponent(testo);
      } else {
        return;
      }
      await window.sciuscia.call("sistema.apriLink", { url });
    } catch (errore) {
      mostraErrore("Non è stato possibile aprire il collegamento: " + errore.message);
    }
  }

  async function salvaMessaggio() {
    const tipo = document.getElementById("messaggio-tipo").value;
    const scopo = document.getElementById("messaggio-scopo").value;
    const testo = document.getElementById("messaggio-testo").value.trim();
    if (!testo) {
      mostraErrore("Scrivere il testo del messaggio.");
      return;
    }
    const destinatario = document.getElementById("messaggio-destinatario").value.trim();
    const selezionati = Array.from(document.querySelectorAll("#messaggio-lista-articoli input[type=checkbox]:checked")).map((c) => c.value);
    nascondiErrore();
    try {
      await window.sciuscia.call("operazioni.inviaMessaggio", {
        commessaId: commessaCorrenteId,
        tipo,
        scopo,
        testo,
        destinatario: destinatario || null,
        articoliId: selezionati.length > 0 ? selezionati : null,
      });
      chiudiModaleMessaggio();
      await ricaricaDettaglioCommessa();
    } catch (errore) {
      mostraErrore("Non è stato possibile registrare il messaggio: " + errore.message);
    }
  }

  // --- Operazioni: Stampe ---

  function renderStampe(dettaglio) {
    corpoTabellaStampe.innerHTML = "";
    if (dettaglio.prints.length === 0) {
      stampeStatoVuoto.hidden = false;
      return;
    }
    stampeStatoVuoto.hidden = true;

    const codiciPerId = {};
    for (const dettaglioArticolo of dettaglio.items) {
      codiciPerId[dettaglioArticolo.item.itemId] = dettaglioArticolo.item.visibleCode;
    }

    for (const stampa of dettaglio.prints) {
      const riga = document.createElement("tr");
      riga.innerHTML = `
        <td>${formattaData(stampa.printedAt)}</td>
        <td>${escapeHtml(ETICHETTE_DOCUMENTO_STAMPA[stampa.documentType] || stampa.documentType)}</td>
        <td>${escapeHtml(stampa.itemId ? (codiciPerId[stampa.itemId] || stampa.itemId) : "—")}</td>
        <td>${escapeHtml(ETICHETTE_ESITO_STAMPA[stampa.outcome] || stampa.outcome)}</td>
      `;
      corpoTabellaStampe.appendChild(riga);
    }
  }

  function costruisciContenutoRicevuta(dettaglio) {
    const righe = dettaglio.items.map((d) => {
      const scadenza = d.item.dueDate ? formattaData(d.item.dueDate) : "—";
      const urgente = d.item.isUrgent ? "Sì" : "—";
      const priorita = d.item.isPriority ? "Sì" : "—";
      return "<tr>" +
        "<td>" + escapeHtml(d.item.visibleCode) + "</td>" +
        "<td>" + escapeHtml(d.categoryName) + "</td>" +
        "<td>" + escapeHtml(d.brandName) + "</td>" +
        "<td>" + escapeHtml(d.item.description) + "</td>" +
        "<td>" + escapeHtml(scadenza) + "</td>" +
        "<td>" + escapeHtml(urgente) + "</td>" +
        "<td>" + escapeHtml(priorita) + "</td>" +
        "</tr>";
    }).join("");
    return `
      <p>Cliente: ${escapeHtml(dettaglio.customerName)}</p>
      <p>Commessa: ${escapeHtml(dettaglio.order.visibleCode)} del ${formattaData(dettaglio.order.receivedAt)}</p>
      <table>
        <thead><tr><th>Codice</th><th>Categoria</th><th>Marca</th><th>Descrizione</th><th>Consegna prevista</th><th>Urgente</th><th>Priorità</th></tr></thead>
        <tbody>${righe}</tbody>
      </table>
      <p>Totale: ${formattaEuro(dettaglio.financials.totalCents)} — Pagato: ${formattaEuro(dettaglio.financials.paidCents)} — Saldo: ${formattaEuro(dettaglio.financials.balanceCents)}</p>
    `;
  }

  function costruisciContenutoEtichetta(dettaglioArticolo) {
    // Le lavorazioni annullate non devono comparire nei documenti stampati (etichetta, ricevuta):
    // una volta annullata, una lavorazione va trattata come se non esistesse per chi legge il documento.
    const lavorazioni = dettaglioArticolo.works
      .filter((w) => w.work.canceledAt == null)
      .map((w) => escapeHtml(w.work.appliedLabelText))
      .join(", ");
    return `
      <p style="font-size:22px"><strong>${escapeHtml(dettaglioArticolo.item.visibleCode)}</strong></p>
      <p>${escapeHtml(dettaglioArticolo.item.description)}</p>
      <p>${lavorazioni || "—"}</p>
    `;
  }

  /// Prepara il contenuto da stampare nell'area dedicata (invisibile a schermo, visibile solo
  /// nel documento stampato) e apre la finestra di stampa del PC; la registrazione dell'esito
  /// avviene dopo, quando il browser avvisa che la stampa è conclusa (evento "afterprint").
  /// "voci" è un elenco di {tipoDocumento, articoloId}: più di una voce quando con un'unica
  /// stampa (ad esempio "Stampa entrambe insieme") si producono più pagine da registrare insieme.
  function stampaDocumenti(voci, titolo, contenutoHtml) {
    stampaInCorso = voci;
    areaStampaContenuto.innerHTML = "<h2>" + escapeHtml(titolo) + "</h2>" + contenutoHtml;
    window.print();
  }

  window.addEventListener("afterprint", async () => {
    if (!stampaInCorso || stampaInCorso.length === 0 || !commessaCorrenteId) {
      return;
    }
    const voci = stampaInCorso;
    stampaInCorso = null;
    nascondiErrore();
    try {
      for (const { tipoDocumento, articoloId } of voci) {
        await window.sciuscia.call("operazioni.registraStampa", {
          commessaId: commessaCorrenteId,
          articoloId,
          tipoDocumento,
          esito: "RIUSCITA",
        });
      }
      await ricaricaDettaglioCommessa();
    } catch (errore) {
      mostraErrore("Non è stato possibile registrare la stampa: " + errore.message);
    }
  });

  function costruisciSchedaArticolo(dettaglioArticolo) {
    const articolo = dettaglioArticolo.item;
    const scheda = document.createElement("div");
    scheda.className = "panel scheda-articolo";
    scheda.dataset.itemId = articolo.itemId;

    const etichettaStato = ETICHETTE_STATO[articolo.workflowStatus] || articolo.workflowStatus;
    const bollini = [];
    if (articolo.isUrgent) bollini.push('<span class="badge urgente">Urgente</span>');
    if (articolo.isPriority) bollini.push('<span class="badge priorita">Priorità</span>');

    const testoScadenza = articolo.dueDate ? formattaDataItaliana(articolo.dueDate) : "Nessuna data prevista";
    const haLavorazioniDaPreventivare = lavorazioniDaPreventivare(dettaglioArticolo).length > 0;

    scheda.innerHTML = `
      <div class="scheda-articolo-intestazione">
        <div>
          <strong>${escapeHtml(articolo.visibleCode)}</strong> — ${escapeHtml(dettaglioArticolo.categoryName)} / ${escapeHtml(dettaglioArticolo.brandName)}
          ${bollini.join(" ")}
          <div class="descrizione-articolo">${escapeHtml(articolo.description)}</div>
          <div class="scadenza-articolo">
            Consegna prevista: ${escapeHtml(testoScadenza)}
            <button type="button" class="btn-modifica-scadenza">Modifica</button>
          </div>
        </div>
        <div class="scheda-articolo-azioni">
          <select class="select-stato-articolo"></select>
          <button type="button" class="btn-aggiungi-lavorazione">+ Lavorazione</button>
          <button type="button" class="btn-aggiungi-foto">+ Foto</button>
          <button type="button" class="btn-aggiungi-etichetta">Stampa etichetta</button>
          ${haLavorazioniDaPreventivare ? '<button type="button" class="btn-invia-preventivo">Invia preventivo</button>' : ""}
        </div>
      </div>
      <div class="lista-lavorazioni"></div>
      <div class="lista-foto"></div>
    `;

    scheda.querySelector(".btn-modifica-scadenza").addEventListener("click", () =>
      apriConsultaCarico({ articoloEsistente: articolo, commessaId: commessaCorrenteId })
    );
    const bottonePreventivo = scheda.querySelector(".btn-invia-preventivo");
    if (bottonePreventivo) {
      bottonePreventivo.addEventListener("click", () => apriPreventivoPerArticolo(articolo.itemId));
    }

    const selectStato = scheda.querySelector(".select-stato-articolo");
    for (const codice of Object.keys(ETICHETTE_STATO)) {
      const opzione = document.createElement("option");
      opzione.value = codice;
      opzione.textContent = ETICHETTE_STATO[codice];
      if (codice === articolo.workflowStatus) {
        opzione.selected = true;
      }
      selectStato.appendChild(opzione);
    }
    selectStato.addEventListener("change", () => cambiaStatoArticolo(articolo.itemId, articolo.rowVersion, selectStato.value));

    scheda.querySelector(".btn-aggiungi-lavorazione").addEventListener("click", () => apriModaleLavorazione(articolo.itemId, articolo.visibleCode));
    scheda.querySelector(".btn-aggiungi-foto").addEventListener("click", () => apriModaleFoto(articolo.itemId, articolo.visibleCode));
    scheda.querySelector(".btn-aggiungi-etichetta").addEventListener("click", () =>
      stampaDocumenti(
        [{ tipoDocumento: "ETICHETTA", articoloId: articolo.itemId }],
        "Etichetta " + articolo.visibleCode,
        costruisciContenutoEtichetta(dettaglioArticolo)
      )
    );

    const listaLavorazioni = scheda.querySelector(".lista-lavorazioni");
    if (dettaglioArticolo.works.length === 0) {
      listaLavorazioni.innerHTML = '<div class="nessuna-lavorazione">Nessuna lavorazione applicata.</div>';
    } else {
      for (const dettaglioLavorazione of dettaglioArticolo.works) {
        listaLavorazioni.appendChild(costruisciRigaLavorazione(dettaglioLavorazione));
      }
    }

    const listaFoto = scheda.querySelector(".lista-foto");
    listaFoto.appendChild(costruisciGalleriaFoto(dettaglioArticolo.photos || []));

    return scheda;
  }

  function costruisciGalleriaFoto(foto) {
    const contenitore = document.createElement("div");

    if (foto.length === 0) {
      contenitore.innerHTML = '<div class="nessuna-foto">Nessuna fotografia.</div>';
      return contenitore;
    }

    const galleria = document.createElement("div");
    galleria.className = "galleria-foto";

    for (const f of foto) {
      const miniatura = document.createElement("div");
      miniatura.className = "miniatura-foto";
      miniatura.innerHTML = `
        <img src="${f.dataUrl}" alt="Fotografia articolo" />
        <button type="button" class="btn-elimina-foto" title="Elimina fotografia">×</button>
      `;
      miniatura.querySelector("img").addEventListener("click", () => mostraFotoGrande(f.dataUrl));
      miniatura.querySelector(".btn-elimina-foto").addEventListener("click", (evento) => {
        evento.stopPropagation();
        eliminaFoto(f.photoId, f.rowVersion);
      });
      galleria.appendChild(miniatura);
    }

    contenitore.appendChild(galleria);
    return contenitore;
  }

  /// Galleria di sola lettura (senza il pulsante Elimina), usata nell'anteprima dell'Agenda:
  /// quella finestra serve solo a dare un'occhiata rapida, non a gestire le fotografie.
  function costruisciGalleriaFotoSoloLettura(foto) {
    const contenitore = document.createElement("div");

    if (foto.length === 0) {
      contenitore.innerHTML = '<div class="nessuna-foto">Nessuna fotografia.</div>';
      return contenitore;
    }

    const galleria = document.createElement("div");
    galleria.className = "galleria-foto";

    for (const f of foto) {
      const miniatura = document.createElement("div");
      miniatura.className = "miniatura-foto";
      miniatura.innerHTML = `<img src="${f.dataUrl}" alt="Fotografia articolo" />`;
      miniatura.querySelector("img").addEventListener("click", () => mostraFotoGrande(f.dataUrl));
      galleria.appendChild(miniatura);
    }

    contenitore.appendChild(galleria);
    return contenitore;
  }

  function mostraFotoGrande(dataUrl) {
    const overlay = document.createElement("div");
    overlay.className = "anteprima-foto-grande-overlay";
    overlay.innerHTML = `<img src="${dataUrl}" alt="Fotografia ingrandita" />`;
    overlay.addEventListener("click", () => overlay.remove());
    document.body.appendChild(overlay);
  }

  async function eliminaFoto(fotoId, rowVersion) {
    if (!window.confirm("Eliminare questa fotografia? Il file resta comunque conservato nel cestino interno.")) {
      return;
    }
    nascondiErrore();
    try {
      await window.sciuscia.call("articoli.eliminaFoto", { fotoId, rowVersion });
      await ricaricaDettaglioCommessa();
    } catch (errore) {
      mostraErrore("Eliminazione fotografia non riuscita: " + errore.message);
    }
  }

  function costruisciRigaLavorazione(dettaglioLavorazione) {
    const lavorazione = dettaglioLavorazione.work;
    const riga = document.createElement("div");
    const annullata = lavorazione.canceledAt != null;
    const rifiutata = !annullata && lavorazione.approvalStatus === "RIFIUTATA";
    const daPreventivare = !annullata && !rifiutata && lavorazione.priceStatus === "DA_DEFINIRE";
    riga.className = "riga-lavorazione" + (annullata ? " lavorazione-annullata" : "") + (rifiutata ? " lavorazione-rifiutata" : "");

    const prezzoTesto = lavorazione.priceStatus === "DEFINITO" ? formattaEuro(lavorazione.priceCents) : "Prezzo da definire";
    const esecuzioneTesto = lavorazione.executionType === "ESTERNA"
      ? "Esterna" + (dettaglioLavorazione.artisanName ? " — " + escapeHtml(dettaglioLavorazione.artisanName) : "")
      : "Interna";

    riga.innerHTML = `
      <span class="nome-lavorazione">${escapeHtml(lavorazione.appliedName)}</span>
      <span class="esecuzione-lavorazione">${esecuzioneTesto}</span>
      <span class="prezzo-lavorazione">${prezzoTesto}</span>
      ${daPreventivare ? '<span class="badge-da-preventivare">Da preventivare</span>' : ""}
      ${annullata
        ? `<span class="badge-annullata" title="${escapeHtml(lavorazione.cancellationReason || "")}">Annullata</span>`
        : rifiutata
          ? '<span class="badge-rifiutata">Preventivo rifiutato</span>'
          : `<button type="button" class="btn-modifica-lavorazione">Modifica</button>
             ${daPreventivare ? '<button type="button" class="btn-rifiuta-lavorazione">Rifiuta</button>' : ""}`}
    `;

    if (!annullata && !rifiutata) {
      riga.querySelector(".btn-modifica-lavorazione").addEventListener("click", () =>
        apriModaleModificaLavorazione(dettaglioLavorazione)
      );
      const bottoneRifiuta = riga.querySelector(".btn-rifiuta-lavorazione");
      if (bottoneRifiuta) {
        bottoneRifiuta.addEventListener("click", () => rifiutaLavorazione(lavorazione.itemWorkId, lavorazione.rowVersion));
      }
    }

    return riga;
  }

  /// Segna una lavorazione ancora senza prezzo come rifiutata dal cliente dopo il preventivo:
  /// resta nello storico senza prezzo, esce dal totale della commessa e da quelle ancora in attesa.
  async function rifiutaLavorazione(lavorazioneId, rowVersion) {
    if (!window.confirm("Segnare questa lavorazione come rifiutata dal cliente? Resterà senza prezzo e non entrerà nel totale della commessa.")) {
      return;
    }
    nascondiErrore();
    try {
      await window.sciuscia.call("articoli.rifiutaLavorazione", { lavorazioneId, rowVersion });
      await ricaricaDettaglioCommessa();
    } catch (errore) {
      mostraErrore("Operazione non riuscita: " + errore.message);
    }
  }

  function apriModaleModificaLavorazione(dettaglioLavorazione) {
    nascondiErrore();
    lavorazioneInModifica = dettaglioLavorazione.work;
    document.getElementById("modifica-lavorazione-nome").textContent = lavorazioneInModifica.appliedName;
    document.getElementById("modifica-lavorazione-prezzo").value = lavorazioneInModifica.priceStatus === "DEFINITO"
      ? (lavorazioneInModifica.priceCents / 100).toFixed(2)
      : "";
    const campoCosto = document.getElementById("campo-modifica-lavorazione-costo");
    campoCosto.hidden = lavorazioneInModifica.executionType !== "ESTERNA";
    document.getElementById("modifica-lavorazione-costo").value = lavorazioneInModifica.costCents != null
      ? (lavorazioneInModifica.costCents / 100).toFixed(2)
      : "";
    document.getElementById("modifica-lavorazione-motivo").value = "";
    document.getElementById("modal-modifica-lavorazione").hidden = false;
  }

  function chiudiModaleModificaLavorazione() {
    document.getElementById("modal-modifica-lavorazione").hidden = true;
    lavorazioneInModifica = null;
  }

  async function salvaModificaLavorazione() {
    if (!lavorazioneInModifica) {
      return;
    }

    const prezzoTesto = document.getElementById("modifica-lavorazione-prezzo").value.trim();
    const costoTesto = document.getElementById("modifica-lavorazione-costo").value.trim();
    const costoVisibile = !document.getElementById("campo-modifica-lavorazione-costo").hidden;

    nascondiErrore();
    try {
      if (prezzoTesto !== "") {
        const prezzoCents = Math.round(parseFloat(prezzoTesto.replace(",", ".")) * 100);
        await window.sciuscia.call("articoli.impostaPrezzoLavorazione", {
          lavorazioneId: lavorazioneInModifica.itemWorkId,
          prezzoCents,
          rowVersion: lavorazioneInModifica.rowVersion,
        });
      }
      if (costoVisibile && costoTesto !== "") {
        const costoCents = Math.round(parseFloat(costoTesto.replace(",", ".")) * 100);
        await window.sciuscia.call("articoli.impostaCostoLavorazione", {
          lavorazioneId: lavorazioneInModifica.itemWorkId,
          costoCents,
          rowVersion: lavorazioneInModifica.rowVersion,
        });
      }
      chiudiModaleModificaLavorazione();
      await ricaricaDettaglioCommessa();
    } catch (errore) {
      mostraErrore("Non è stato possibile salvare le modifiche: " + errore.message);
    }
  }

  async function annullaLavorazioneApplicata() {
    if (!lavorazioneInModifica) {
      return;
    }

    const motivo = document.getElementById("modifica-lavorazione-motivo").value.trim();
    if (!motivo) {
      mostraErrore("Indicare il motivo dell'annullamento.");
      return;
    }

    if (!window.confirm("Annullare questa lavorazione? Resterà visibile nello storico ma non conterà più nel totale.")) {
      return;
    }

    nascondiErrore();
    try {
      await window.sciuscia.call("articoli.annullaLavorazione", {
        lavorazioneId: lavorazioneInModifica.itemWorkId,
        motivo,
        rowVersion: lavorazioneInModifica.rowVersion,
      });
      chiudiModaleModificaLavorazione();
      await ricaricaDettaglioCommessa();
    } catch (errore) {
      mostraErrore("Non è stato possibile annullare la lavorazione: " + errore.message);
    }
  }

  async function cambiaStatoArticolo(articoloId, rowVersion, nuovoStato) {
    nascondiErrore();
    try {
      await window.sciuscia.call("articoli.cambiaStato", { articoloId, rowVersion, nuovoStato });
      await ricaricaDettaglioCommessa();
      if (nuovoStato === "PRONTO_DA_AVVISARE"
          && window.confirm("Articolo segnato come pronto. Preparare subito il messaggio di avviso al cliente?")) {
        apriModaleMessaggioPerArticolo(articoloId);
      }
    } catch (errore) {
      mostraErrore("Cambio stato non riuscito: " + errore.message);
      await ricaricaDettaglioCommessa();
    }
  }

  /// Apre la finestra "Prepara messaggio" già pronta per avvisare un singolo articolo appena
  /// segnato pronto, con solo quell'articolo selezionato nell'elenco (l'operatore può comunque
  /// aggiungerne altri prima di salvare). Il testo cita già la categoria, così il cliente capisce
  /// che riguarda quel singolo articolo e non l'intera commessa.
  function apriModaleMessaggioPerArticolo(itemId) {
    apriModaleMessaggio("AVVISO_PRONTO", [itemId]);
  }

  /// Apre la finestra "Prepara messaggio" già pronta per inviare il preventivo delle lavorazioni
  /// ancora senza prezzo di un articolo, con solo quell'articolo selezionato.
  function apriPreventivoPerArticolo(itemId) {
    apriModaleMessaggio("PREVENTIVO", [itemId]);
  }

  // --- Nuovo articolo ---

  async function apriModaleArticolo() {
    nascondiErrore();
    document.getElementById("articolo-descrizione").value = "";
    document.getElementById("articolo-scadenza").value = "";
    document.getElementById("articolo-urgente").checked = false;
    document.getElementById("articolo-priorita").checked = false;
    document.getElementById("box-nuova-categoria").hidden = true;
    document.getElementById("box-nuova-marca").hidden = true;
    document.getElementById("nuova-categoria-nome").value = "";
    document.getElementById("nuova-marca-nome").value = "";

    try {
      await ricaricaCategorieEMarche();
      modaleArticolo.hidden = false;
    } catch (errore) {
      mostraErrore("Non è stato possibile preparare il modulo articolo: " + errore.message);
    }
  }

  async function ricaricaCategorieEMarche(categoriaDaSelezionare, marcaDaSelezionare) {
    const [categorie, marche] = await Promise.all([
      window.sciuscia.call("anagrafiche.categorie"),
      window.sciuscia.call("anagrafiche.marche"),
    ]);
    popolaSelect(document.getElementById("articolo-categoria"), categorie, (c) => c.categoryId, (c) => c.name);
    popolaSelect(document.getElementById("articolo-marca"), marche, (m) => m.brandId, (m) => m.canonicalName);
    if (categoriaDaSelezionare) {
      document.getElementById("articolo-categoria").value = categoriaDaSelezionare;
    }
    if (marcaDaSelezionare) {
      document.getElementById("articolo-marca").value = marcaDaSelezionare;
    }
  }

  async function creaCategoriaRapida() {
    const nome = document.getElementById("nuova-categoria-nome").value.trim();
    if (!nome) {
      mostraErrore("Il nome della categoria è obbligatorio.");
      return;
    }

    nascondiErrore();
    try {
      const categoria = await window.sciuscia.call("anagrafiche.creaCategoria", { nome });
      await ricaricaCategorieEMarche(categoria.categoryId, null);
      document.getElementById("box-nuova-categoria").hidden = true;
      document.getElementById("nuova-categoria-nome").value = "";
    } catch (errore) {
      mostraErrore("Creazione categoria non riuscita: " + errore.message);
    }
  }

  async function creaMarcaRapida() {
    const nome = document.getElementById("nuova-marca-nome").value.trim();
    if (!nome) {
      mostraErrore("Il nome della marca è obbligatorio.");
      return;
    }

    nascondiErrore();
    try {
      const marca = await window.sciuscia.call("anagrafiche.creaMarca", { nome });
      await ricaricaCategorieEMarche(null, marca.brandId);
      document.getElementById("box-nuova-marca").hidden = true;
      document.getElementById("nuova-marca-nome").value = "";
    } catch (errore) {
      mostraErrore("Creazione marca non riuscita: " + errore.message);
    }
  }

  function popolaSelect(selectEl, elementi, ottieniValore, ottieniTesto) {
    selectEl.innerHTML = "";
    for (const elemento of elementi) {
      const opzione = document.createElement("option");
      opzione.value = ottieniValore(elemento);
      opzione.textContent = ottieniTesto(elemento);
      selectEl.appendChild(opzione);
    }
  }

  function chiudiModaleArticolo() {
    modaleArticolo.hidden = true;
  }

  async function creaArticolo() {
    const categoriaId = document.getElementById("articolo-categoria").value;
    const marcaId = document.getElementById("articolo-marca").value;
    const descrizione = document.getElementById("articolo-descrizione").value.trim();
    const scadenza = document.getElementById("articolo-scadenza").value || null;
    const urgente = document.getElementById("articolo-urgente").checked;
    const priorita = document.getElementById("articolo-priorita").checked;

    if (!categoriaId || !marcaId) {
      mostraErrore("Categoria e marca sono obbligatorie: se mancano, crearle prima nelle Anagrafiche.");
      return;
    }
    if (!descrizione) {
      mostraErrore("La descrizione dell'articolo è obbligatoria.");
      return;
    }

    nascondiErrore();
    try {
      const nuovoArticolo = await window.sciuscia.call("articoli.crea", {
        commessaId: commessaCorrenteId,
        categoriaId,
        marcaId,
        descrizione,
        scadenza,
        urgente,
        priorita,
      });
      chiudiModaleArticolo();
      await ricaricaDettaglioCommessa();
      // Dopo ogni nuovo articolo serve quasi sempre aggiungere almeno una lavorazione: si
      // propone subito la finestra, così non bisogna cercarla di nuovo nell'elenco. Se non
      // serve aggiungere nulla, si chiude semplicemente con "Annulla".
      apriModaleLavorazione(nuovoArticolo.itemId, nuovoArticolo.visibleCode);
    } catch (errore) {
      mostraErrore("Creazione articolo non riuscita: " + errore.message);
    }
  }

  // --- Aggiungi lavorazione ---

  async function apriModaleLavorazione(articoloId, articoloCodice) {
    nascondiErrore();
    articoloPerLavorazioneId = articoloId;
    articoloPerLavorazioneCodice = articoloCodice;
    document.getElementById("lavorazione-articolo-codice").textContent = "Articolo " + articoloCodice;
    document.getElementById("lavorazione-nome").value = "";
    document.getElementById("lavorazione-etichetta").value = "";
    document.getElementById("lavorazione-prezzo").value = "";
    checkboxEsterna.checked = false;
    campoArtigiano.hidden = true;
    document.getElementById("box-nuovo-artigiano").hidden = true;
    document.getElementById("nuovo-artigiano-nome").value = "";

    try {
      if (!listinoCache) {
        listinoCache = await window.sciuscia.call("anagrafiche.listino");
      }
      selectLavorazioneTipo.innerHTML = '<option value="">— nessuna, la scrivo io —</option>';
      for (const voce of listinoCache) {
        const opzione = document.createElement("option");
        opzione.value = voce.workTypeId;
        opzione.textContent = voce.name;
        selectLavorazioneTipo.appendChild(opzione);
      }
      campoFascia.hidden = true;
      selectLavorazioneFascia.innerHTML = "";
      modaleLavorazione.hidden = false;
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare il listino: " + errore.message);
    }
  }

  function chiudiModaleLavorazione() {
    modaleLavorazione.hidden = true;
  }

  async function selezionaVoceListino() {
    const workTypeId = selectLavorazioneTipo.value;
    if (!workTypeId) {
      campoFascia.hidden = true;
      return;
    }

    const voce = listinoCache.find((v) => v.workTypeId === workTypeId);
    if (!voce) {
      return;
    }

    document.getElementById("lavorazione-nome").value = voce.name;
    document.getElementById("lavorazione-etichetta").value = voce.labelText;
    checkboxEsterna.checked = voce.executionDefault === "ESTERNA";
    campoArtigiano.hidden = !checkboxEsterna.checked;
    if (checkboxEsterna.checked) {
      await popolaArtigiani();
    }

    if (voce.priceMode === "FASCE") {
      campoFascia.hidden = false;
      nascondiErrore();
      try {
        const fasce = await window.sciuscia.call("anagrafiche.fasceLavorazione", { workTypeId });
        selectLavorazioneFascia.innerHTML = "";
        for (const fascia of fasce) {
          const opzione = document.createElement("option");
          opzione.value = fascia.bandCode;
          opzione.dataset.prezzo = fascia.priceCents;
          opzione.textContent = "Fascia " + fascia.bandCode + " — " + formattaEuro(fascia.priceCents);
          selectLavorazioneFascia.appendChild(opzione);
        }
        aggiornaPrezzoDaFascia();
      } catch (errore) {
        mostraErrore("Non è stato possibile caricare le fasce di prezzo: " + errore.message);
      }
    } else {
      campoFascia.hidden = true;
      document.getElementById("lavorazione-prezzo").value = voce.standardPriceCents != null
        ? (voce.standardPriceCents / 100).toFixed(2)
        : "";
    }
  }

  function aggiornaPrezzoDaFascia() {
    const opzioneSelezionata = selectLavorazioneFascia.selectedOptions[0];
    if (opzioneSelezionata) {
      document.getElementById("lavorazione-prezzo").value = (Number(opzioneSelezionata.dataset.prezzo) / 100).toFixed(2);
    }
  }

  async function popolaArtigiani(artigianoDaSelezionare) {
    try {
      const artigiani = await window.sciuscia.call("anagrafiche.artigiani");
      popolaSelect(document.getElementById("lavorazione-artigiano"), artigiani, (a) => a.artisanId, (a) => a.displayName);
      if (artigianoDaSelezionare) {
        document.getElementById("lavorazione-artigiano").value = artigianoDaSelezionare;
      }
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare l'elenco artigiani: " + errore.message);
    }
  }

  async function creaArtigianoRapido() {
    const nome = document.getElementById("nuovo-artigiano-nome").value.trim();
    if (!nome) {
      mostraErrore("Il nome dell'artigiano è obbligatorio.");
      return;
    }

    nascondiErrore();
    try {
      const artigiano = await window.sciuscia.call("anagrafiche.creaArtigiano", { nome });
      await popolaArtigiani(artigiano.artisanId);
      document.getElementById("box-nuovo-artigiano").hidden = true;
      document.getElementById("nuovo-artigiano-nome").value = "";
    } catch (errore) {
      mostraErrore("Creazione artigiano non riuscita: " + errore.message);
    }
  }

  async function creaLavorazione() {
    const workTypeId = selectLavorazioneTipo.value || null;
    const nome = document.getElementById("lavorazione-nome").value.trim();
    // Se non viene scelta una voce del Listino, il testo del cartellino va digitato a mano:
    // se Mariagrazia lo lascia vuoto, usiamo il nome della lavorazione (così non deve scriverlo due volte).
    let testoEtichetta = document.getElementById("lavorazione-etichetta").value.trim();
    const prezzoTesto = document.getElementById("lavorazione-prezzo").value.trim();
    const fascia = campoFascia.hidden ? null : (selectLavorazioneFascia.value || null);
    const esterna = checkboxEsterna.checked;
    const artigianoId = esterna ? document.getElementById("lavorazione-artigiano").value : null;

    if (!nome) {
      mostraErrore("Il nome della lavorazione è obbligatorio.");
      return;
    }
    if (!testoEtichetta) {
      testoEtichetta = nome;
    }
    if (esterna && !artigianoId) {
      mostraErrore("Selezionare l'artigiano per una lavorazione esterna.");
      return;
    }

    const prezzoCents = prezzoTesto === "" ? null : Math.round(parseFloat(prezzoTesto.replace(",", ".")) * 100);

    // Se la lavorazione viene da una voce del Listino a prezzo fisso, ma nome/etichetta/prezzo
    // sono stati cambiati prima di confermare, chiediamo se la modifica deve valere solo per
    // questa commessa (comportamento di sempre) oppure anche per il Listino generale.
    let voceListinoDaAggiornare = null;
    if (workTypeId && listinoCache) {
      const voceOriginale = listinoCache.find((v) => v.workTypeId === workTypeId);
      if (voceOriginale && voceOriginale.priceMode === "FISSO") {
        const nomeCambiato = nome !== voceOriginale.name;
        const etichettaCambiata = testoEtichetta !== voceOriginale.labelText;
        const prezzoCambiato = prezzoCents !== voceOriginale.standardPriceCents;
        if (nomeCambiato || etichettaCambiata || prezzoCambiato) {
          const aggiornareListino = window.confirm(
            "Hai modificato nome, testo etichetta o prezzo rispetto alla voce del Listino.\n\n" +
            "Vuoi salvare questa modifica anche nel Listino generale, per ritrovarla pronta nelle prossime commesse?\n\n" +
            "Premi Annulla se deve valere solo per questa commessa."
          );
          if (aggiornareListino) {
            voceListinoDaAggiornare = voceOriginale;
          }
        }
      }
    }

    nascondiErrore();
    try {
      await window.sciuscia.call("articoli.aggiungiLavorazione", {
        articoloId: articoloPerLavorazioneId,
        workTypeId,
        nome,
        testoEtichetta,
        fascia,
        prezzoCents,
        esecuzione: esterna ? "ESTERNA" : "INTERNA",
        artigianoId,
      });

      if (voceListinoDaAggiornare) {
        try {
          await window.sciuscia.call("anagrafiche.modificaLavorazioneListino", {
            lavorazioneId: voceListinoDaAggiornare.workTypeId,
            nome,
            testoEtichetta,
            esecuzione: voceListinoDaAggiornare.executionDefault,
            modalitaPrezzo: voceListinoDaAggiornare.priceMode,
            prezzoStandardCents: prezzoCents,
            costoStandardCents: voceListinoDaAggiornare.standardCostCents,
            rowVersion: voceListinoDaAggiornare.rowVersion,
          });
          listinoCache = null;
        } catch (erroreListino) {
          mostraErrore("La lavorazione è stata applicata alla commessa, ma non è stato possibile aggiornare il Listino: " + erroreListino.message);
        }
      }

      chiudiModaleLavorazione();
      await ricaricaDettaglioCommessa();
    } catch (errore) {
      mostraErrore("Aggiunta lavorazione non riuscita: " + errore.message);
    }
  }

  // --- Fotografie ---

  function apriModaleFoto(articoloId, articoloCodice) {
    nascondiErrore();
    articoloPerFotoId = articoloId;
    document.getElementById("foto-articolo-codice").textContent = "Articolo " + articoloCodice;

    fotoCatturataDataUrl = null;
    fotoDaFileDataUrl = null;
    fotoDaFileNome = null;
    inputFileFoto.value = "";
    nomeFileFotoScelta.textContent = "Nessun file scelto.";
    anteprimaFile.hidden = true;
    anteprimaFile.removeAttribute("src");
    bottoneSalvaFoto.disabled = true;

    cambiaSchedaFoto("webcam");
    modaleFoto.hidden = false;
  }

  function cambiaSchedaFoto(scheda) {
    const suWebcam = scheda === "webcam";
    document.getElementById("foto-tab-webcam").hidden = !suWebcam;
    document.getElementById("foto-tab-file").hidden = suWebcam;
    document.getElementById("btn-tab-foto-webcam").classList.toggle("active", suWebcam);
    document.getElementById("btn-tab-foto-file").classList.toggle("active", !suWebcam);

    aggiornaStatoSalvaFoto();

    if (suWebcam) {
      avviaWebcam();
    } else {
      fermaWebcam();
    }
  }

  function aggiornaStatoSalvaFoto() {
    const suWebcam = !document.getElementById("foto-tab-webcam").hidden;
    bottoneSalvaFoto.disabled = suWebcam ? !fotoCatturataDataUrl : !fotoDaFileDataUrl;
  }

  async function avviaWebcam() {
    erroreWebcam.hidden = true;
    anteprimaWebcam.hidden = true;
    videoFoto.hidden = false;
    bottoneScattaFoto.hidden = false;
    bottoneRifaiFoto.hidden = true;
    fotoCatturataDataUrl = null;
    aggiornaStatoSalvaFoto();

    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      erroreWebcam.textContent = "Questo PC non permette l'accesso alla webcam da questa applicazione.";
      erroreWebcam.hidden = false;
      bottoneScattaFoto.hidden = true;
      return;
    }

    try {
      streamWebcamAttivo = await navigator.mediaDevices.getUserMedia({
        video: { width: { ideal: 1280 }, height: { ideal: 720 } },
        audio: false,
      });
      videoFoto.srcObject = streamWebcamAttivo;
    } catch (errore) {
      erroreWebcam.textContent = "Non è stato possibile accendere la webcam: " + errore.message + ". Verificare che sia collegata e che nessun'altra applicazione la stia già usando.";
      erroreWebcam.hidden = false;
      bottoneScattaFoto.hidden = true;
    }
  }

  function fermaWebcam() {
    if (streamWebcamAttivo) {
      for (const traccia of streamWebcamAttivo.getTracks()) {
        traccia.stop();
      }
      streamWebcamAttivo = null;
    }
    videoFoto.srcObject = null;
  }

  function scattaFoto() {
    if (!videoFoto.videoWidth) {
      return;
    }
    canvasFoto.width = videoFoto.videoWidth;
    canvasFoto.height = videoFoto.videoHeight;
    canvasFoto.getContext("2d").drawImage(videoFoto, 0, 0, canvasFoto.width, canvasFoto.height);
    fotoCatturataDataUrl = canvasFoto.toDataURL("image/jpeg", 0.85);

    anteprimaWebcam.src = fotoCatturataDataUrl;
    anteprimaWebcam.hidden = false;
    videoFoto.hidden = true;
    bottoneScattaFoto.hidden = true;
    bottoneRifaiFoto.hidden = false;
    fermaWebcam();
    aggiornaStatoSalvaFoto();
  }

  function rifaiFoto() {
    anteprimaWebcam.hidden = true;
    anteprimaWebcam.removeAttribute("src");
    fotoCatturataDataUrl = null;
    aggiornaStatoSalvaFoto();
    avviaWebcam();
  }

  function fotoDaFileScelta() {
    const file = inputFileFoto.files && inputFileFoto.files[0];
    if (!file) {
      fotoDaFileDataUrl = null;
      fotoDaFileNome = null;
      nomeFileFotoScelta.textContent = "Nessun file scelto.";
      anteprimaFile.hidden = true;
      aggiornaStatoSalvaFoto();
      return;
    }

    const lettore = new FileReader();
    lettore.onload = () => {
      fotoDaFileDataUrl = lettore.result;
      fotoDaFileNome = file.name;
      nomeFileFotoScelta.textContent = file.name;
      anteprimaFile.src = fotoDaFileDataUrl;
      anteprimaFile.hidden = false;
      aggiornaStatoSalvaFoto();
    };
    lettore.onerror = () => {
      mostraErrore("Non è stato possibile leggere il file scelto.");
    };
    lettore.readAsDataURL(file);
  }

  function chiudiModaleFoto() {
    modaleFoto.hidden = true;
    fermaWebcam();
    articoloPerFotoId = null;
    fotoCatturataDataUrl = null;
    fotoDaFileDataUrl = null;
    fotoDaFileNome = null;
  }

  function estensioneDaDataUrl(dataUrl) {
    const corrispondenza = /^data:image\/(jpeg|jpg|png|webp)/i.exec(dataUrl);
    const tipo = corrispondenza ? corrispondenza[1].toLowerCase() : "jpeg";
    return tipo === "jpg" ? "jpeg" : tipo;
  }

  async function salvaFoto() {
    const suWebcam = !document.getElementById("foto-tab-webcam").hidden;
    const immagineBase64 = suWebcam ? fotoCatturataDataUrl : fotoDaFileDataUrl;

    if (!articoloPerFotoId || !immagineBase64) {
      return;
    }

    nascondiErrore();
    try {
      await window.sciuscia.call("articoli.aggiungiFoto", {
        articoloId: articoloPerFotoId,
        immagineBase64,
        estensione: "." + estensioneDaDataUrl(immagineBase64),
        sorgente: suWebcam ? "WEBCAM" : "IMPORTAZIONE",
        nomeFile: suWebcam ? null : fotoDaFileNome,
      });
      chiudiModaleFoto();
      await ricaricaDettaglioCommessa();
    } catch (errore) {
      mostraErrore("Salvataggio fotografia non riuscito: " + errore.message);
    }
  }

  document.getElementById("btn-tab-foto-webcam").addEventListener("click", () => cambiaSchedaFoto("webcam"));
  document.getElementById("btn-tab-foto-file").addEventListener("click", () => cambiaSchedaFoto("file"));
  bottoneScattaFoto.addEventListener("click", scattaFoto);
  bottoneRifaiFoto.addEventListener("click", rifaiFoto);
  bottoneSceglieFileFoto.addEventListener("click", () => inputFileFoto.click());
  inputFileFoto.addEventListener("change", fotoDaFileScelta);
  document.getElementById("btn-annulla-foto").addEventListener("click", chiudiModaleFoto);
  bottoneSalvaFoto.addEventListener("click", salvaFoto);

  document.getElementById("btn-nuovo-pagamento").addEventListener("click", apriModalePagamento);
  document.getElementById("btn-annulla-pagamento").addEventListener("click", chiudiModalePagamento);
  document.getElementById("btn-salva-pagamento").addEventListener("click", salvaPagamento);
  document.getElementById("btn-annulla-correzione-pagamento").addEventListener("click", chiudiModaleCorrezionePagamento);
  document.getElementById("btn-salva-correzione-pagamento").addEventListener("click", salvaCorrezionePagamento);

  document.getElementById("btn-nuovo-ritiro").addEventListener("click", apriModaleRitiro);
  document.getElementById("btn-annulla-ritiro").addEventListener("click", chiudiModaleRitiro);
  document.getElementById("btn-salva-ritiro").addEventListener("click", salvaRitiro);

  document.getElementById("btn-nuovo-messaggio").addEventListener("click", () => apriModaleMessaggio());
  document.getElementById("btn-annulla-messaggio").addEventListener("click", chiudiModaleMessaggio);
  document.getElementById("btn-salva-messaggio").addEventListener("click", salvaMessaggio);
  document.getElementById("btn-apri-messaggio-esterno").addEventListener("click", apriMessaggioEsterno);
  document.getElementById("messaggio-tipo").addEventListener("change", aggiornaDestinatarioMessaggio);
  document.getElementById("messaggio-scopo").addEventListener("change", aggiornaTestoMessaggioDaSelezione);
  document.getElementById("messaggio-lista-articoli").addEventListener("change", aggiornaTestoMessaggioDaSelezione);
  document.getElementById("messaggio-seleziona-tutti").addEventListener("change", (evento) => {
    document.querySelectorAll("#messaggio-lista-articoli input[type=checkbox]").forEach((casella) => {
      casella.checked = evento.target.checked;
    });
    aggiornaTestoMessaggioDaSelezione();
  });

  document.getElementById("btn-stampa-ricevuta").addEventListener("click", () => {
    if (!dettaglioCorrente) {
      return;
    }
    stampaDocumenti(
      [{ tipoDocumento: "COPIA_CLIENTE", articoloId: null }],
      "Ricevuta — Commessa " + dettaglioCorrente.order.visibleCode,
      costruisciContenutoRicevuta(dettaglioCorrente)
    );
  });
  document.getElementById("btn-stampa-interna").addEventListener("click", () => {
    if (!dettaglioCorrente) {
      return;
    }
    stampaDocumenti(
      [{ tipoDocumento: "COPIA_INTERNA", articoloId: null }],
      "Copia interna — Commessa " + dettaglioCorrente.order.visibleCode,
      costruisciContenutoRicevuta(dettaglioCorrente)
    );
  });
  document.getElementById("btn-stampa-entrambe").addEventListener("click", () => {
    if (!dettaglioCorrente) {
      return;
    }
    const contenuto = costruisciContenutoRicevuta(dettaglioCorrente);
    stampaDocumenti(
      [
        { tipoDocumento: "COPIA_CLIENTE", articoloId: null },
        { tipoDocumento: "COPIA_INTERNA", articoloId: null },
      ],
      "Commessa " + dettaglioCorrente.order.visibleCode,
      '<div class="pagina-stampa"><h3>Copia per il cliente</h3>' + contenuto + "</div>" +
        '<div class="pagina-stampa pagina-stampa-successiva"><h3>Copia interna</h3>' + contenuto + "</div>"
    );
  });

  document.getElementById("btn-torna-elenco").addEventListener("click", () => {
    mostraVistaElenco();
    caricaCommesse();
  });
  document.getElementById("btn-nuovo-articolo").addEventListener("click", apriModaleArticolo);
  document.getElementById("btn-annulla-articolo").addEventListener("click", chiudiModaleArticolo);
  document.getElementById("btn-crea-articolo").addEventListener("click", creaArticolo);
  document.getElementById("btn-mostra-nuova-categoria").addEventListener("click", () => {
    document.getElementById("box-nuova-categoria").hidden = !document.getElementById("box-nuova-categoria").hidden;
  });
  document.getElementById("btn-crea-categoria").addEventListener("click", creaCategoriaRapida);
  document.getElementById("btn-mostra-nuova-marca").addEventListener("click", () => {
    document.getElementById("box-nuova-marca").hidden = !document.getElementById("box-nuova-marca").hidden;
  });
  document.getElementById("btn-crea-marca").addEventListener("click", creaMarcaRapida);

  document.getElementById("btn-annulla-lavorazione").addEventListener("click", chiudiModaleLavorazione);
  document.getElementById("btn-crea-lavorazione").addEventListener("click", creaLavorazione);
  document.getElementById("btn-chiudi-modifica-lavorazione").addEventListener("click", chiudiModaleModificaLavorazione);
  document.getElementById("btn-salva-modifica-lavorazione").addEventListener("click", salvaModificaLavorazione);
  document.getElementById("btn-annulla-lavorazione-applicata").addEventListener("click", annullaLavorazioneApplicata);
  selectLavorazioneTipo.addEventListener("change", selezionaVoceListino);
  selectLavorazioneFascia.addEventListener("change", aggiornaPrezzoDaFascia);
  checkboxEsterna.addEventListener("change", async () => {
    campoArtigiano.hidden = !checkboxEsterna.checked;
    if (checkboxEsterna.checked) {
      await popolaArtigiani();
    }
  });
  document.getElementById("btn-mostra-nuovo-artigiano").addEventListener("click", () => {
    document.getElementById("box-nuovo-artigiano").hidden = !document.getElementById("box-nuovo-artigiano").hidden;
  });
  document.getElementById("btn-crea-artigiano").addEventListener("click", creaArtigianoRapido);

  // --- Navigazione principale (Commesse / Anagrafiche) ---

  function mostraSchermataHome() {
    document.getElementById("nav-home").classList.add("active");
    document.getElementById("nav-commesse").classList.remove("active");
    document.getElementById("nav-agenda").classList.remove("active");
    document.getElementById("nav-pdr").classList.remove("active");
    document.getElementById("nav-pa").classList.remove("active");
    document.getElementById("nav-anagrafiche").classList.remove("active");
    document.getElementById("nav-impostazioni").classList.remove("active");
    vistaElenco.hidden = true;
    vistaDettaglio.hidden = true;
    vistaAgenda.hidden = true;
    vistaPdr.hidden = true;
    vistaPa.hidden = true;
    vistaAnagrafiche.hidden = true;
    vistaImpostazioni.hidden = true;
    vistaHome.hidden = false;
    nascondiErrore();
    caricaHome();
  }

  function mostraSchermataCommesse() {
    document.getElementById("nav-commesse").classList.add("active");
    document.getElementById("nav-home").classList.remove("active");
    document.getElementById("nav-agenda").classList.remove("active");
    document.getElementById("nav-pdr").classList.remove("active");
    document.getElementById("nav-pa").classList.remove("active");
    document.getElementById("nav-anagrafiche").classList.remove("active");
    document.getElementById("nav-impostazioni").classList.remove("active");
    vistaHome.hidden = true;
    vistaAgenda.hidden = true;
    vistaPdr.hidden = true;
    vistaPa.hidden = true;
    vistaAnagrafiche.hidden = true;
    vistaImpostazioni.hidden = true;
    mostraVistaElenco();
    caricaCommesse();
  }

  function mostraSchermataAnagrafiche() {
    document.getElementById("nav-anagrafiche").classList.add("active");
    document.getElementById("nav-home").classList.remove("active");
    document.getElementById("nav-commesse").classList.remove("active");
    document.getElementById("nav-agenda").classList.remove("active");
    document.getElementById("nav-pdr").classList.remove("active");
    document.getElementById("nav-pa").classList.remove("active");
    document.getElementById("nav-impostazioni").classList.remove("active");
    vistaHome.hidden = true;
    vistaElenco.hidden = true;
    vistaDettaglio.hidden = true;
    vistaAgenda.hidden = true;
    vistaPdr.hidden = true;
    vistaPa.hidden = true;
    vistaImpostazioni.hidden = true;
    vistaAnagrafiche.hidden = false;
    nascondiErrore();
    caricaClientiAnagrafica();
  }

  function mostraSchermataImpostazioni() {
    document.getElementById("nav-impostazioni").classList.add("active");
    document.getElementById("nav-home").classList.remove("active");
    document.getElementById("nav-commesse").classList.remove("active");
    document.getElementById("nav-agenda").classList.remove("active");
    document.getElementById("nav-pdr").classList.remove("active");
    document.getElementById("nav-pa").classList.remove("active");
    document.getElementById("nav-anagrafiche").classList.remove("active");
    vistaHome.hidden = true;
    vistaElenco.hidden = true;
    vistaDettaglio.hidden = true;
    vistaAgenda.hidden = true;
    vistaPdr.hidden = true;
    vistaPa.hidden = true;
    vistaAnagrafiche.hidden = true;
    vistaImpostazioni.hidden = false;
    nascondiErrore();
    if (!document.getElementById("calendario-da-data").value) {
      const oggi = new Date();
      const inNovantaGiorni = new Date(oggi.getTime() + 90 * 24 * 60 * 60 * 1000);
      document.getElementById("calendario-da-data").value = formattaDataPerInput(oggi);
      document.getElementById("calendario-a-data").value = formattaDataPerInput(inNovantaGiorni);
    }
    caricaCalendario();
    caricaCapacitaGiornaliera();
  }

  /// Capacità di lavoro interna di una giornata: un unico valore in minuti per tutto il
  /// laboratorio, mostrato in ore per semplicità (le mezze giornate dimezzano automaticamente).
  async function caricaCapacitaGiornaliera() {
    try {
      const risultato = await window.sciuscia.call("impostazioni.leggiCapacitaGiornaliera");
      document.getElementById("impostazioni-ore-giorno").value = risultato.capacitaGiornalieraMinuti / 60;
    } catch (errore) {
      mostraErrore("Non è stato possibile leggere la capacità giornaliera: " + errore.message);
    }
  }

  async function salvaCapacitaGiornaliera() {
    const oreTesto = document.getElementById("impostazioni-ore-giorno").value.trim();
    const ore = parseFloat(oreTesto.replace(",", "."));
    if (!oreTesto || isNaN(ore) || ore <= 0) {
      mostraErrore("Indica un numero di ore maggiore di zero.");
      return;
    }

    nascondiErrore();
    try {
      await window.sciuscia.call("impostazioni.impostaCapacitaGiornaliera", {
        capacitaGiornalieraMinuti: Math.round(ore * 60),
      });
      mostraConfermaPulsante(document.getElementById("btn-salva-ore-giorno"), "Salvato ✓");
    } catch (errore) {
      mostraErrore("Salvataggio non riuscito: " + errore.message);
    }
  }

  /// Conferma visibile e momentanea su un pulsante dopo un'azione riuscita (es. "Aggiorna",
  /// "Salva"): il testo del pulsante cambia per un istante, così si vede chiaramente che
  /// qualcosa è successo anche quando il contenuto della pagina non cambia visivamente.
  function mostraConfermaPulsante(bottone, testoConferma) {
    const testoOriginale = bottone.textContent;
    bottone.disabled = true;
    bottone.textContent = testoConferma;
    setTimeout(() => {
      bottone.textContent = testoOriginale;
      bottone.disabled = false;
    }, 1200);
  }

  function mostraSchermataAgenda() {
    document.getElementById("nav-agenda").classList.add("active");
    document.getElementById("nav-home").classList.remove("active");
    document.getElementById("nav-commesse").classList.remove("active");
    document.getElementById("nav-pdr").classList.remove("active");
    document.getElementById("nav-pa").classList.remove("active");
    document.getElementById("nav-anagrafiche").classList.remove("active");
    document.getElementById("nav-impostazioni").classList.remove("active");
    vistaHome.hidden = true;
    vistaElenco.hidden = true;
    vistaDettaglio.hidden = true;
    vistaPdr.hidden = true;
    vistaPa.hidden = true;
    vistaAnagrafiche.hidden = true;
    vistaImpostazioni.hidden = true;
    vistaAgenda.hidden = false;
    nascondiErrore();
    if (!document.getElementById("agenda-da-data").value) {
      const oggi = new Date();
      const inDueSettimane = new Date(oggi.getTime() + 13 * 24 * 60 * 60 * 1000);
      document.getElementById("agenda-da-data").value = formattaDataPerInput(oggi);
      document.getElementById("agenda-a-data").value = formattaDataPerInput(inDueSettimane);
    }
    caricaAgenda();
  }

  function mostraSchermataPdr() {
    document.getElementById("nav-pdr").classList.add("active");
    document.getElementById("nav-home").classList.remove("active");
    document.getElementById("nav-commesse").classList.remove("active");
    document.getElementById("nav-agenda").classList.remove("active");
    document.getElementById("nav-pa").classList.remove("active");
    document.getElementById("nav-anagrafiche").classList.remove("active");
    document.getElementById("nav-impostazioni").classList.remove("active");
    vistaHome.hidden = true;
    vistaElenco.hidden = true;
    vistaDettaglio.hidden = true;
    vistaAgenda.hidden = true;
    vistaPa.hidden = true;
    vistaAnagrafiche.hidden = true;
    vistaImpostazioni.hidden = true;
    vistaPdr.hidden = false;
    nascondiErrore();
    caricaPdr();
  }

  function mostraSchermataPa() {
    document.getElementById("nav-pa").classList.add("active");
    document.getElementById("nav-home").classList.remove("active");
    document.getElementById("nav-commesse").classList.remove("active");
    document.getElementById("nav-agenda").classList.remove("active");
    document.getElementById("nav-pdr").classList.remove("active");
    document.getElementById("nav-anagrafiche").classList.remove("active");
    document.getElementById("nav-impostazioni").classList.remove("active");
    vistaHome.hidden = true;
    vistaElenco.hidden = true;
    vistaDettaglio.hidden = true;
    vistaAgenda.hidden = true;
    vistaPdr.hidden = true;
    vistaAnagrafiche.hidden = true;
    vistaImpostazioni.hidden = true;
    vistaPa.hidden = false;
    nascondiErrore();
    caricaPa();
  }

  /// Riepilogo operativo (specifica, punto 5 della lista priorità): cinque riquadri che segnalano
  /// cosa richiede attenzione oggi. Ogni riga apre direttamente la commessa interessata, come già
  /// avviene dall'Agenda, per restare "semplice e intuitiva" come richiesto.
  async function caricaHome() {
    nascondiErrore();
    try {
      const riepilogo = await window.sciuscia.call("home.riepilogo");

      popolaRiquadroHome("lavori", riepilogo.lavoriUrgentiInRitardo, (riga) => ({
        testo: riga.customerName + " — " + riga.categoryName + (riga.isUrgent ? " (urgente)" : "") +
          " — " + riga.orderVisibleCode,
        dettaglio: riga.dueDate ? formattaDataItaliana(riga.dueDate) : "senza data",
        orderId: riga.orderId,
        itemId: riga.itemId,
      }));

      popolaRiquadroHome("foto", riepilogo.commesseSenzaFoto, (riga) => ({
        testo: riga.customerName + " — " + riga.orderVisibleCode,
        dettaglio: formattaData(riga.receivedAt),
        orderId: riga.orderId,
      }));

      popolaRiquadroHome("pronti", riepilogo.prontiDaAvvisare, (riga) => ({
        testo: riga.customerName + " — " + riga.categoryName + " — " + riga.orderVisibleCode,
        dettaglio: "",
        orderId: riga.orderId,
        itemId: riga.itemId,
      }));

      popolaRiquadroHome("preventivi", riepilogo.preventiviScaduti, (riga) => ({
        testo: riga.customerName + " — " + riga.categoryName + " (" + riga.lavorazioneNome + ") — " + riga.orderVisibleCode,
        dettaglio: riga.giorniAttesa + " gg",
        orderId: riga.orderId,
        itemId: riga.itemId,
      }));

      popolaRiquadroHome("ritiri", riepilogo.ritiriDaSollecitare, (riga) => ({
        testo: riga.customerName + " — " + riga.categoryName + " — " + riga.orderVisibleCode,
        dettaglio: riga.giorniAttesa + " gg",
        orderId: riga.orderId,
        itemId: riga.itemId,
      }));
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare la Home: " + errore.message);
    }
  }

  function popolaRiquadroHome(prefisso, righe, trasforma) {
    const numero = document.getElementById("home-" + prefisso + "-numero");
    const elenco = document.getElementById("home-" + prefisso + "-elenco");
    const vuoto = document.getElementById("home-" + prefisso + "-vuoto");

    numero.textContent = String(righe.length);
    numero.classList.toggle("con-segnalazioni", righe.length > 0);
    elenco.innerHTML = "";
    vuoto.hidden = righe.length > 0;

    const MASSIMO_VISIBILE = 5;
    for (const riga of righe.slice(0, MASSIMO_VISIBILE)) {
      const dati = trasforma(riga);
      const bottone = document.createElement("button");
      bottone.type = "button";
      bottone.className = "riquadro-home-riga";
      bottone.innerHTML =
        "<span>" + escapeHtml(dati.testo) + "</span>" +
        (dati.dettaglio ? '<span class="riquadro-home-riga-dettaglio">' + escapeHtml(dati.dettaglio) + "</span>" : "");
      bottone.addEventListener("click", () => apriCommessaDaHome(dati.orderId, dati.itemId || null));
      elenco.appendChild(bottone);
    }

    if (righe.length > MASSIMO_VISIBILE) {
      const altri = document.createElement("div");
      altri.className = "riquadro-home-altri";
      altri.textContent = "+ altri " + (righe.length - MASSIMO_VISIBILE);
      elenco.appendChild(altri);
    }
  }

  async function apriCommessaDaHome(orderId, itemId) {
    articoloDaEvidenziareId = itemId;
    document.getElementById("nav-commesse").classList.add("active");
    document.getElementById("nav-home").classList.remove("active");
    document.getElementById("nav-agenda").classList.remove("active");
    document.getElementById("nav-pdr").classList.remove("active");
    document.getElementById("nav-pa").classList.remove("active");
    document.getElementById("nav-anagrafiche").classList.remove("active");
    document.getElementById("nav-impostazioni").classList.remove("active");
    vistaHome.hidden = true;
    vistaAgenda.hidden = true;
    vistaPdr.hidden = true;
    vistaPa.hidden = true;
    vistaAnagrafiche.hidden = true;
    vistaImpostazioni.hidden = true;
    await apriDettaglioCommessa(orderId);
  }

  document.getElementById("nav-home").addEventListener("click", mostraSchermataHome);
  document.getElementById("btn-aggiorna-home").addEventListener("click", async () => {
    const bottone = document.getElementById("btn-aggiorna-home");
    await caricaHome();
    mostraConfermaPulsante(bottone, "Aggiornato ✓");
  });

  function formattaDataPerInput(data) {
    const anno = data.getFullYear();
    const mese = String(data.getMonth() + 1).padStart(2, "0");
    const giorno = String(data.getDate()).padStart(2, "0");
    return `${anno}-${mese}-${giorno}`;
  }

  document.getElementById("nav-commesse").addEventListener("click", mostraSchermataCommesse);
  document.getElementById("nav-anagrafiche").addEventListener("click", mostraSchermataAnagrafiche);
  document.getElementById("nav-impostazioni").addEventListener("click", mostraSchermataImpostazioni);
  document.getElementById("nav-agenda").addEventListener("click", mostraSchermataAgenda);
  document.getElementById("nav-pdr").addEventListener("click", mostraSchermataPdr);
  document.getElementById("btn-aggiorna-pdr").addEventListener("click", async () => {
    const bottone = document.getElementById("btn-aggiorna-pdr");
    await caricaPdr();
    mostraConfermaPulsante(bottone, "Aggiornato ✓");
  });
  document.getElementById("nav-pa").addEventListener("click", mostraSchermataPa);
  document.getElementById("btn-aggiorna-pa").addEventListener("click", async () => {
    const bottone = document.getElementById("btn-aggiorna-pa");
    await caricaPa();
    mostraConfermaPulsante(bottone, "Aggiornato ✓");
  });

  document.querySelectorAll("#view-anagrafiche .tab-btn").forEach((bottone) => {
    bottone.addEventListener("click", () => {
      document.querySelectorAll("#view-anagrafiche .tab-btn").forEach((b) => b.classList.remove("active"));
      document.querySelectorAll("#view-anagrafiche .tab-pane").forEach((p) => { p.hidden = true; });
      bottone.classList.add("active");
      const riquadro = document.getElementById("tab-" + bottone.dataset.tab);
      riquadro.hidden = false;

      if (bottone.dataset.tab === "clienti") caricaClientiAnagrafica();
      else if (bottone.dataset.tab === "categorie") caricaCategorieAnagrafica();
      else if (bottone.dataset.tab === "marche") caricaMarcheAnagrafica();
      else if (bottone.dataset.tab === "artigiani") caricaArtigianiAnagrafica();
      else if (bottone.dataset.tab === "listino") caricaListinoAnagrafica();
    });
  });

  document.querySelectorAll("#view-impostazioni [data-tab-impostazioni]").forEach((bottone) => {
    bottone.addEventListener("click", () => {
      document.querySelectorAll("#view-impostazioni [data-tab-impostazioni]").forEach((b) => b.classList.remove("active"));
      document.querySelectorAll("#view-impostazioni .tab-pane").forEach((p) => { p.hidden = true; });
      bottone.classList.add("active");
      const riquadro = document.getElementById("tab-impostazioni-" + bottone.dataset.tabImpostazioni);
      riquadro.hidden = false;

      if (bottone.dataset.tabImpostazioni === "calendario") caricaCalendario();
    });
  });

  // --- Clienti ---

  async function caricaClientiAnagrafica() {
    nascondiErrore();
    const query = document.getElementById("ricerca-clienti-anagrafica").value.trim();
    try {
      const clienti = await window.sciuscia.call("anagrafiche.clienti", { query });
      renderClientiAnagrafica(clienti || []);
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare i clienti: " + errore.message);
    }
  }

  function renderClientiAnagrafica(clienti) {
    const corpo = document.getElementById("corpo-tabella-clienti-anagrafica");
    corpo.innerHTML = "";
    for (const cliente of clienti) {
      const riga = document.createElement("tr");
      const nomeCompleto = [cliente.surname, cliente.firstName].filter(Boolean).join(" ");
      const statoClasse = cliente.active ? "stato-attivo" : "stato-inattivo";
      const statoTesto = cliente.active ? "Attivo" : "Disattivato";
      riga.innerHTML = `
        <td>${escapeHtml(nomeCompleto)}</td>
        <td>${escapeHtml(cliente.mobileOriginal || "")}</td>
        <td>${escapeHtml(cliente.email || "")}</td>
        <td class="${statoClasse}">${statoTesto}</td>
        <td class="azioni-riga">
          <button type="button" class="btn-modifica">Modifica</button>
          <button type="button" class="btn-attiva-disattiva">${cliente.active ? "Disattiva" : "Riattiva"}</button>
        </td>
      `;
      riga.querySelector(".btn-modifica").addEventListener("click", () => apriModaleCliente(cliente));
      riga.querySelector(".btn-attiva-disattiva").addEventListener("click", () => attivaDisattivaCliente(cliente));
      corpo.appendChild(riga);
    }
  }

  function pianificaRicercaClientiAnagrafica() {
    if (ricercaClientiAnagraficaInCorso) {
      clearTimeout(ricercaClientiAnagraficaInCorso);
    }
    ricercaClientiAnagraficaInCorso = setTimeout(caricaClientiAnagrafica, 300);
  }

  function apriModaleCliente(cliente) {
    nascondiErrore();
    clienteAnagraficaInModifica = cliente || null;
    document.getElementById("titolo-modal-cliente").textContent = cliente ? "Modifica cliente" : "Nuovo cliente";
    document.getElementById("cliente-anagrafica-cognome").value = cliente ? cliente.surname : "";
    document.getElementById("cliente-anagrafica-nome").value = cliente ? (cliente.firstName || "") : "";
    document.getElementById("cliente-anagrafica-cellulare").value = cliente ? (cliente.mobileOriginal || "") : "";
    document.getElementById("cliente-anagrafica-email").value = cliente ? (cliente.email || "") : "";
    document.getElementById("modal-cliente-anagrafica").hidden = false;
  }

  function chiudiModaleCliente() {
    document.getElementById("modal-cliente-anagrafica").hidden = true;
  }

  async function salvaClienteAnagrafica() {
    const cognome = document.getElementById("cliente-anagrafica-cognome").value.trim();
    const nome = document.getElementById("cliente-anagrafica-nome").value.trim() || null;
    const cellulare = document.getElementById("cliente-anagrafica-cellulare").value.trim() || null;
    const email = document.getElementById("cliente-anagrafica-email").value.trim() || null;

    if (!cognome) {
      mostraErrore("Il cognome del cliente è obbligatorio.");
      return;
    }

    nascondiErrore();
    try {
      if (clienteAnagraficaInModifica) {
        await window.sciuscia.call("anagrafiche.modificaCliente", {
          clienteId: clienteAnagraficaInModifica.customerId,
          cognome, nome, cellulare, email,
          statoPrivacy: clienteAnagraficaInModifica.privacyStatus,
          rowVersion: clienteAnagraficaInModifica.rowVersion,
        });
      } else {
        await window.sciuscia.call("anagrafiche.creaCliente", { cognome, nome, cellulare, email });
      }
      chiudiModaleCliente();
      await caricaClientiAnagrafica();
    } catch (errore) {
      mostraErrore("Salvataggio cliente non riuscito: " + errore.message);
    }
  }

  async function attivaDisattivaCliente(cliente) {
    nascondiErrore();
    try {
      const comando = cliente.active ? "anagrafiche.disattivaCliente" : "anagrafiche.attivaCliente";
      await window.sciuscia.call(comando, { clienteId: cliente.customerId, rowVersion: cliente.rowVersion });
      await caricaClientiAnagrafica();
    } catch (errore) {
      mostraErrore("Operazione non riuscita: " + errore.message);
    }
  }

  // --- Categorie ---

  async function caricaCategorieAnagrafica() {
    nascondiErrore();
    try {
      const categorie = await window.sciuscia.call("anagrafiche.categorie", { includiInattive: true });
      renderCategorieAnagrafica(categorie || []);
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare le categorie: " + errore.message);
    }
  }

  function renderCategorieAnagrafica(categorie) {
    const corpo = document.getElementById("corpo-tabella-categorie-anagrafica");
    corpo.innerHTML = "";
    for (const categoria of categorie) {
      const riga = document.createElement("tr");
      const statoClasse = categoria.active ? "stato-attivo" : "stato-inattivo";
      riga.innerHTML = `
        <td>${escapeHtml(categoria.name)}</td>
        <td class="${statoClasse}">${categoria.active ? "Attiva" : "Disattivata"}</td>
        <td class="azioni-riga">
          <button type="button" class="btn-modifica">Modifica</button>
          <button type="button" class="btn-attiva-disattiva">${categoria.active ? "Disattiva" : "Riattiva"}</button>
        </td>
      `;
      riga.querySelector(".btn-modifica").addEventListener("click", () => apriModaleCategoria(categoria));
      riga.querySelector(".btn-attiva-disattiva").addEventListener("click", () => attivaDisattivaCategoria(categoria));
      corpo.appendChild(riga);
    }
  }

  function apriModaleCategoria(categoria) {
    nascondiErrore();
    categoriaAnagraficaInModifica = categoria || null;
    document.getElementById("titolo-modal-categoria").textContent = categoria ? "Modifica categoria" : "Nuova categoria";
    document.getElementById("categoria-anagrafica-nome").value = categoria ? categoria.name : "";
    document.getElementById("modal-categoria-anagrafica").hidden = false;
  }

  function chiudiModaleCategoria() {
    document.getElementById("modal-categoria-anagrafica").hidden = true;
  }

  async function salvaCategoriaAnagrafica() {
    const nome = document.getElementById("categoria-anagrafica-nome").value.trim();
    if (!nome) {
      mostraErrore("Il nome della categoria è obbligatorio.");
      return;
    }

    nascondiErrore();
    try {
      if (categoriaAnagraficaInModifica) {
        await window.sciuscia.call("anagrafiche.modificaCategoria", {
          categoriaId: categoriaAnagraficaInModifica.categoryId,
          nome,
          ordine: categoriaAnagraficaInModifica.sortOrder,
          rowVersion: categoriaAnagraficaInModifica.rowVersion,
        });
      } else {
        await window.sciuscia.call("anagrafiche.creaCategoria", { nome });
      }
      chiudiModaleCategoria();
      await caricaCategorieAnagrafica();
    } catch (errore) {
      mostraErrore("Salvataggio categoria non riuscito: " + errore.message);
    }
  }

  async function attivaDisattivaCategoria(categoria) {
    nascondiErrore();
    try {
      const comando = categoria.active ? "anagrafiche.disattivaCategoria" : "anagrafiche.attivaCategoria";
      await window.sciuscia.call(comando, { categoriaId: categoria.categoryId, rowVersion: categoria.rowVersion });
      await caricaCategorieAnagrafica();
    } catch (errore) {
      mostraErrore("Operazione non riuscita: " + errore.message);
    }
  }

  // --- Marche ---

  async function caricaMarcheAnagrafica() {
    nascondiErrore();
    try {
      const marche = await window.sciuscia.call("anagrafiche.marche", { includiInattive: true });
      renderMarcheAnagrafica(marche || []);
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare le marche: " + errore.message);
    }
  }

  function renderMarcheAnagrafica(marche) {
    const corpo = document.getElementById("corpo-tabella-marche-anagrafica");
    corpo.innerHTML = "";
    for (const marca of marche) {
      const riga = document.createElement("tr");
      const statoClasse = marca.active ? "stato-attivo" : "stato-inattivo";
      const puoDisattivare = !marca.protected;
      riga.innerHTML = `
        <td>${escapeHtml(marca.canonicalName)}${marca.protected ? " <span class=\"hint\">(protetta)</span>" : ""}</td>
        <td class="${statoClasse}">${marca.active ? "Attiva" : "Disattivata"}</td>
        <td class="azioni-riga">
          ${puoDisattivare ? `<button type="button" class="btn-modifica">Modifica</button> <button type="button" class="btn-attiva-disattiva">${marca.active ? "Disattiva" : "Riattiva"}</button>` : ""}
        </td>
      `;
      const bottoneModifica = riga.querySelector(".btn-modifica");
      if (bottoneModifica) {
        bottoneModifica.addEventListener("click", () => apriModaleMarca(marca));
      }
      const bottoneAttiva = riga.querySelector(".btn-attiva-disattiva");
      if (bottoneAttiva) {
        bottoneAttiva.addEventListener("click", () => attivaDisattivaMarca(marca));
      }
      corpo.appendChild(riga);
    }
  }

  function apriModaleMarca(marca) {
    nascondiErrore();
    marcaAnagraficaInModifica = marca || null;
    document.getElementById("titolo-modal-marca-anagrafica").textContent = marca ? "Modifica marca" : "Nuova marca";
    document.getElementById("marca-anagrafica-nome").value = marca ? marca.canonicalName : "";
    document.getElementById("modal-marca-anagrafica").hidden = false;
  }

  function chiudiModaleMarca() {
    document.getElementById("modal-marca-anagrafica").hidden = true;
  }

  async function salvaMarcaAnagrafica() {
    const nome = document.getElementById("marca-anagrafica-nome").value.trim();
    if (!nome) {
      mostraErrore("Il nome della marca è obbligatorio.");
      return;
    }

    nascondiErrore();
    try {
      if (marcaAnagraficaInModifica) {
        await window.sciuscia.call("anagrafiche.modificaMarca", {
          marcaId: marcaAnagraficaInModifica.brandId,
          nome,
          rowVersion: marcaAnagraficaInModifica.rowVersion,
        });
      } else {
        await window.sciuscia.call("anagrafiche.creaMarca", { nome });
      }
      chiudiModaleMarca();
      await caricaMarcheAnagrafica();
    } catch (errore) {
      mostraErrore("Salvataggio marca non riuscito: " + errore.message);
    }
  }

  async function attivaDisattivaMarca(marca) {
    nascondiErrore();
    try {
      const comando = marca.active ? "anagrafiche.disattivaMarca" : "anagrafiche.attivaMarca";
      await window.sciuscia.call(comando, { marcaId: marca.brandId, rowVersion: marca.rowVersion });
      await caricaMarcheAnagrafica();
    } catch (errore) {
      mostraErrore("Operazione non riuscita: " + errore.message);
    }
  }

  // --- Artigiani ---

  async function caricaArtigianiAnagrafica() {
    nascondiErrore();
    try {
      const artigiani = await window.sciuscia.call("anagrafiche.artigiani", { includiInattive: true });
      renderArtigianiAnagrafica(artigiani || []);
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare gli artigiani: " + errore.message);
    }
  }

  function renderArtigianiAnagrafica(artigiani) {
    const corpo = document.getElementById("corpo-tabella-artigiani-anagrafica");
    corpo.innerHTML = "";
    for (const artigiano of artigiani) {
      const riga = document.createElement("tr");
      const statoClasse = artigiano.active ? "stato-attivo" : "stato-inattivo";
      riga.innerHTML = `
        <td>${escapeHtml(artigiano.displayName)}</td>
        <td>${escapeHtml(artigiano.phone || "")}</td>
        <td>${escapeHtml(artigiano.email || "")}</td>
        <td>${artigiano.solicitAfterDays ? artigiano.solicitAfterDays + " gg" : "—"}</td>
        <td class="${statoClasse}">${artigiano.active ? "Attivo" : "Disattivato"}</td>
        <td class="azioni-riga">
          <button type="button" class="btn-modifica">Modifica</button>
          <button type="button" class="btn-attiva-disattiva">${artigiano.active ? "Disattiva" : "Riattiva"}</button>
        </td>
      `;
      riga.querySelector(".btn-modifica").addEventListener("click", () => apriModaleArtigiano(artigiano));
      riga.querySelector(".btn-attiva-disattiva").addEventListener("click", () => attivaDisattivaArtigiano(artigiano));
      corpo.appendChild(riga);
    }
  }

  function apriModaleArtigiano(artigiano) {
    nascondiErrore();
    artigianoAnagraficaInModifica = artigiano || null;
    document.getElementById("titolo-modal-artigiano").textContent = artigiano ? "Modifica artigiano" : "Nuovo artigiano";
    document.getElementById("artigiano-anagrafica-nome").value = artigiano ? artigiano.displayName : "";
    document.getElementById("artigiano-anagrafica-contatto").value = artigiano ? (artigiano.contactName || "") : "";
    document.getElementById("artigiano-anagrafica-telefono").value = artigiano ? (artigiano.phone || "") : "";
    document.getElementById("artigiano-anagrafica-email").value = artigiano ? (artigiano.email || "") : "";
    document.getElementById("artigiano-anagrafica-indirizzo").value = artigiano ? (artigiano.address || "") : "";
    document.getElementById("artigiano-anagrafica-sollecito").value =
      artigiano && artigiano.solicitAfterDays ? artigiano.solicitAfterDays : "";
    document.getElementById("modal-artigiano-anagrafica").hidden = false;
  }

  function chiudiModaleArtigiano() {
    document.getElementById("modal-artigiano-anagrafica").hidden = true;
  }

  async function salvaArtigianoAnagrafica() {
    const nome = document.getElementById("artigiano-anagrafica-nome").value.trim();
    const contatto = document.getElementById("artigiano-anagrafica-contatto").value.trim() || null;
    const telefono = document.getElementById("artigiano-anagrafica-telefono").value.trim() || null;
    const email = document.getElementById("artigiano-anagrafica-email").value.trim() || null;
    const indirizzo = document.getElementById("artigiano-anagrafica-indirizzo").value.trim() || null;
    const soglieSollecitoTesto = document.getElementById("artigiano-anagrafica-sollecito").value.trim();
    const giorniSollecito = soglieSollecitoTesto ? parseInt(soglieSollecitoTesto, 10) : null;

    if (!nome) {
      mostraErrore("Il nome dell'artigiano è obbligatorio.");
      return;
    }
    if (giorniSollecito !== null && (!Number.isInteger(giorniSollecito) || giorniSollecito <= 0)) {
      mostraErrore("I giorni di sollecito devono essere un numero intero maggiore di zero.");
      return;
    }

    nascondiErrore();
    try {
      if (artigianoAnagraficaInModifica) {
        await window.sciuscia.call("anagrafiche.modificaArtigiano", {
          artigianoId: artigianoAnagraficaInModifica.artisanId,
          nome, contatto, telefono, email, indirizzo, giorniSollecito,
          rowVersion: artigianoAnagraficaInModifica.rowVersion,
        });
      } else {
        await window.sciuscia.call("anagrafiche.creaArtigiano", { nome, contatto, telefono, email, indirizzo, giorniSollecito });
      }
      chiudiModaleArtigiano();
      await caricaArtigianiAnagrafica();
    } catch (errore) {
      mostraErrore("Salvataggio artigiano non riuscito: " + errore.message);
    }
  }

  async function attivaDisattivaArtigiano(artigiano) {
    nascondiErrore();
    try {
      const comando = artigiano.active ? "anagrafiche.disattivaArtigiano" : "anagrafiche.attivaArtigiano";
      await window.sciuscia.call(comando, { artigianoId: artigiano.artisanId, rowVersion: artigiano.rowVersion });
      await caricaArtigianiAnagrafica();
    } catch (errore) {
      mostraErrore("Operazione non riuscita: " + errore.message);
    }
  }

  // --- Listino lavorazioni ---

  const ETICHETTE_MODALITA_PREZZO = { FISSO: "Fisso", FASCE: "A fasce", DA_DEFINIRE: "Da definire" };

  async function caricaListinoAnagrafica() {
    nascondiErrore();
    try {
      const listino = await window.sciuscia.call("anagrafiche.listino", { includiInattive: true });
      renderListinoAnagrafica(listino || []);
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare il listino: " + errore.message);
    }
  }

  // --- Importazione Listino/Marche/Categorie da Excel ---

  function fileExcelScelto(evento) {
    const file = evento.target.files && evento.target.files[0];
    evento.target.value = "";
    if (!file) {
      return;
    }
    const lettore = new FileReader();
    lettore.onload = async () => {
      nascondiErrore();
      try {
        const risultato = await window.sciuscia.call("anagrafiche.importaListino", { fileBase64: lettore.result });
        mostraRisultatoImportazioneListino(risultato);
        listinoCache = null;
        await caricaListinoAnagrafica();
        await caricaMarcheAnagrafica();
        await caricaCategorieAnagrafica();
      } catch (errore) {
        mostraErrore("Importazione non riuscita: " + errore.message);
      }
    };
    lettore.onerror = () => mostraErrore("Non è stato possibile leggere il file scelto.");
    lettore.readAsDataURL(file);
  }

  function mostraRisultatoImportazioneListino(risultato) {
    const riepilogo =
      `Marche: ${risultato.marcheNuove} nuove, ${risultato.marcheEsistenti} già presenti. ` +
      `Categorie: ${risultato.categorieNuove} nuove, ${risultato.categorieEsistenti} già presenti. ` +
      `Lavorazioni nel Listino: ${risultato.lavorazioniNuove} nuove, ${risultato.lavorazioniAggiornate} aggiornate.`;
    document.getElementById("importa-listino-riepilogo").textContent = riepilogo;

    const contenitoreAvvisi = document.getElementById("importa-listino-avvisi-contenitore");
    const listaAvvisi = document.getElementById("importa-listino-avvisi");
    listaAvvisi.innerHTML = "";
    if (risultato.avvisi && risultato.avvisi.length > 0) {
      for (const avviso of risultato.avvisi) {
        const voce = document.createElement("li");
        voce.textContent = avviso;
        listaAvvisi.appendChild(voce);
      }
      contenitoreAvvisi.hidden = false;
    } else {
      contenitoreAvvisi.hidden = true;
    }

    document.getElementById("modal-importa-listino").hidden = false;
  }

  function renderListinoAnagrafica(listino) {
    const corpo = document.getElementById("corpo-tabella-listino-anagrafica");
    corpo.innerHTML = "";
    for (const voce of listino) {
      const riga = document.createElement("tr");
      const statoClasse = voce.active ? "stato-attivo" : "stato-inattivo";
      let prezzoTesto = ETICHETTE_MODALITA_PREZZO[voce.priceMode] || voce.priceMode;
      if (voce.priceMode === "FISSO" && voce.standardPriceCents != null) {
        prezzoTesto = formattaEuro(voce.standardPriceCents);
      }
      let costoTesto = "—";
      if (voce.priceMode === "FISSO" && voce.standardCostCents != null) {
        costoTesto = formattaEuro(voce.standardCostCents);
      } else if (voce.priceMode === "FASCE") {
        costoTesto = "Vedi fasce";
      }
      const tempoTesto = voce.defaultDurationMinutes != null ? voce.defaultDurationMinutes + " min" : "—";
      riga.innerHTML = `
        <td>${escapeHtml(voce.name)}</td>
        <td>${escapeHtml(voce.labelText)}</td>
        <td>${voce.executionDefault === "ESTERNA" ? "Esterna" : "Interna"}</td>
        <td>${escapeHtml(prezzoTesto)}</td>
        <td>${escapeHtml(costoTesto)}</td>
        <td>${escapeHtml(tempoTesto)}</td>
        <td class="${statoClasse}">${voce.active ? "Attiva" : "Disattivata"}</td>
        <td class="azioni-riga">
          <button type="button" class="btn-modifica">Modifica</button>
          ${voce.priceMode === "FASCE" ? '<button type="button" class="btn-fasce">Fasce di prezzo</button>' : ""}
          <button type="button" class="btn-attiva-disattiva">${voce.active ? "Disattiva" : "Riattiva"}</button>
        </td>
      `;
      riga.querySelector(".btn-modifica").addEventListener("click", () => apriModaleListino(voce));
      const bottoneFasce = riga.querySelector(".btn-fasce");
      if (bottoneFasce) {
        bottoneFasce.addEventListener("click", () => apriModaleFasceListino(voce));
      }
      riga.querySelector(".btn-attiva-disattiva").addEventListener("click", () => attivaDisattivaLavorazioneListino(voce));
      corpo.appendChild(riga);
    }
  }

  function apriModaleListino(voce) {
    nascondiErrore();
    lavorazioneListinoInModifica = voce || null;
    document.getElementById("titolo-modal-listino").textContent = voce ? "Modifica lavorazione" : "Nuova lavorazione nel listino";
    document.getElementById("listino-nome").value = voce ? voce.name : "";
    document.getElementById("listino-etichetta").value = voce ? voce.labelText : "";
    document.getElementById("listino-esecuzione").value = voce ? voce.executionDefault : "INTERNA";
    document.getElementById("listino-modalita-prezzo").value = voce ? voce.priceMode : "DA_DEFINIRE";
    document.getElementById("listino-prezzo-fisso").value = voce && voce.standardPriceCents != null
      ? (voce.standardPriceCents / 100).toFixed(2)
      : "";
    document.getElementById("listino-costo-fisso").value = voce && voce.standardCostCents != null
      ? (voce.standardCostCents / 100).toFixed(2)
      : "";
    document.getElementById("listino-tempo-realizzo").value = voce && voce.defaultDurationMinutes != null
      ? voce.defaultDurationMinutes
      : "";
    aggiornaCampiModalitaPrezzoListino();
    document.getElementById("btn-salva-listino").textContent = voce ? "Salva" : "Crea lavorazione";
    document.getElementById("modal-lavorazione-listino").hidden = false;
  }

  function chiudiModaleListino() {
    document.getElementById("modal-lavorazione-listino").hidden = true;
  }

  function aggiornaCampiModalitaPrezzoListino() {
    const modalita = document.getElementById("listino-modalita-prezzo").value;
    document.getElementById("campo-listino-prezzo-fisso").hidden = modalita !== "FISSO";
    document.getElementById("campo-listino-costo-fisso").hidden = modalita !== "FISSO";
    document.getElementById("campo-listino-fasce").hidden = modalita !== "FASCE";
  }

  async function salvaLavorazioneListino() {
    const nome = document.getElementById("listino-nome").value.trim();
    const testoEtichetta = document.getElementById("listino-etichetta").value.trim();
    const esecuzione = document.getElementById("listino-esecuzione").value;
    const modalitaPrezzo = document.getElementById("listino-modalita-prezzo").value;
    const prezzoTesto = document.getElementById("listino-prezzo-fisso").value.trim();
    const costoTesto = document.getElementById("listino-costo-fisso").value.trim();
    const tempoRealizzoTesto = document.getElementById("listino-tempo-realizzo").value.trim();

    if (!nome || !testoEtichetta) {
      mostraErrore("Nome e testo etichetta sono obbligatori.");
      return;
    }

    const prezzoStandardCents = modalitaPrezzo === "FISSO" && prezzoTesto !== ""
      ? Math.round(parseFloat(prezzoTesto.replace(",", ".")) * 100)
      : null;
    const costoStandardCents = modalitaPrezzo === "FISSO" && costoTesto !== ""
      ? Math.round(parseFloat(costoTesto.replace(",", ".")) * 100)
      : null;
    const tempoRealizzoMinuti = tempoRealizzoTesto !== "" ? parseInt(tempoRealizzoTesto, 10) : null;

    nascondiErrore();
    try {
      if (lavorazioneListinoInModifica) {
        await window.sciuscia.call("anagrafiche.modificaLavorazioneListino", {
          lavorazioneId: lavorazioneListinoInModifica.workTypeId,
          nome, testoEtichetta, esecuzione, modalitaPrezzo, prezzoStandardCents,
          costoStandardCents, tempoRealizzoMinuti,
          rowVersion: lavorazioneListinoInModifica.rowVersion,
        });
      } else {
        await window.sciuscia.call("anagrafiche.creaLavorazioneListino", {
          nome, testoEtichetta, esecuzione, modalitaPrezzo, prezzoStandardCents, costoStandardCents,
          tempoRealizzoMinuti,
        });
      }
      listinoCache = null;
      chiudiModaleListino();
      await caricaListinoAnagrafica();
    } catch (errore) {
      mostraErrore("Salvataggio lavorazione non riuscito: " + errore.message);
    }
  }

  async function attivaDisattivaLavorazioneListino(voce) {
    nascondiErrore();
    try {
      const comando = voce.active ? "anagrafiche.disattivaLavorazione" : "anagrafiche.attivaLavorazione";
      await window.sciuscia.call(comando, { lavorazioneId: voce.workTypeId, rowVersion: voce.rowVersion });
      listinoCache = null;
      await caricaListinoAnagrafica();
    } catch (errore) {
      mostraErrore("Operazione non riuscita: " + errore.message);
    }
  }

  async function apriModaleFasceListino(voce) {
    nascondiErrore();
    lavorazioneListinoPerFasce = voce;
    document.getElementById("titolo-modal-fasce").textContent = "Fasce di prezzo";
    document.getElementById("sottotitolo-modal-fasce").textContent = voce.name;
    document.getElementById("fascia-listino-a").value = "";
    document.getElementById("fascia-listino-b").value = "";
    document.getElementById("fascia-listino-c").value = "";
    document.getElementById("fascia-listino-a-costo").value = "";
    document.getElementById("fascia-listino-b-costo").value = "";
    document.getElementById("fascia-listino-c-costo").value = "";

    try {
      const fasce = await window.sciuscia.call("anagrafiche.fasceLavorazione", { workTypeId: voce.workTypeId });
      for (const fascia of (fasce || [])) {
        const campo = document.getElementById("fascia-listino-" + fascia.bandCode.toLowerCase());
        if (campo) {
          campo.value = (fascia.priceCents / 100).toFixed(2);
        }
        const campoCosto = document.getElementById("fascia-listino-" + fascia.bandCode.toLowerCase() + "-costo");
        if (campoCosto && fascia.costCents != null) {
          campoCosto.value = (fascia.costCents / 100).toFixed(2);
        }
      }
      document.getElementById("modal-fasce-listino").hidden = false;
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare le fasce: " + errore.message);
    }
  }

  function chiudiModaleFasceListino() {
    document.getElementById("modal-fasce-listino").hidden = true;
  }

  async function salvaFasceListino() {
    if (!lavorazioneListinoPerFasce) {
      return;
    }

    const fasce = [
      {
        codice: "A",
        valore: document.getElementById("fascia-listino-a").value.trim(),
        valoreCosto: document.getElementById("fascia-listino-a-costo").value.trim(),
      },
      {
        codice: "B",
        valore: document.getElementById("fascia-listino-b").value.trim(),
        valoreCosto: document.getElementById("fascia-listino-b-costo").value.trim(),
      },
      {
        codice: "C",
        valore: document.getElementById("fascia-listino-c").value.trim(),
        valoreCosto: document.getElementById("fascia-listino-c-costo").value.trim(),
      },
    ].filter((f) => f.valore !== "");

    if (fasce.length === 0) {
      mostraErrore("Inserisci almeno il prezzo di una fascia.");
      return;
    }

    nascondiErrore();
    try {
      for (const fascia of fasce) {
        const prezzoCents = Math.round(parseFloat(fascia.valore.replace(",", ".")) * 100);
        const costoCents = fascia.valoreCosto !== ""
          ? Math.round(parseFloat(fascia.valoreCosto.replace(",", ".")) * 100)
          : null;
        await window.sciuscia.call("anagrafiche.impostaFasciaLavorazione", {
          lavorazioneId: lavorazioneListinoPerFasce.workTypeId,
          fascia: fascia.codice,
          prezzoCents,
          costoCents,
        });
      }
      chiudiModaleFasceListino();
      await caricaListinoAnagrafica();
    } catch (errore) {
      mostraErrore("Salvataggio fasce non riuscito: " + errore.message);
    }
  }

  document.getElementById("ricerca-clienti-anagrafica").addEventListener("input", pianificaRicercaClientiAnagrafica);
  document.getElementById("btn-nuovo-cliente-anagrafica").addEventListener("click", () => apriModaleCliente(null));
  document.getElementById("btn-annulla-cliente-anagrafica").addEventListener("click", chiudiModaleCliente);
  document.getElementById("btn-salva-cliente-anagrafica").addEventListener("click", salvaClienteAnagrafica);

  document.getElementById("btn-nuova-categoria-anagrafica").addEventListener("click", () => apriModaleCategoria(null));
  document.getElementById("btn-annulla-categoria-anagrafica").addEventListener("click", chiudiModaleCategoria);
  document.getElementById("btn-salva-categoria-anagrafica").addEventListener("click", salvaCategoriaAnagrafica);

  document.getElementById("btn-nuova-marca-anagrafica").addEventListener("click", () => apriModaleMarca());
  document.getElementById("btn-annulla-marca-anagrafica").addEventListener("click", chiudiModaleMarca);
  document.getElementById("btn-salva-marca-anagrafica").addEventListener("click", salvaMarcaAnagrafica);

  document.getElementById("btn-nuovo-artigiano-anagrafica").addEventListener("click", () => apriModaleArtigiano(null));
  document.getElementById("btn-annulla-artigiano-anagrafica").addEventListener("click", chiudiModaleArtigiano);
  document.getElementById("btn-salva-artigiano-anagrafica").addEventListener("click", salvaArtigianoAnagrafica);

  document.getElementById("btn-nuova-lavorazione-listino").addEventListener("click", () => apriModaleListino());
  document.getElementById("btn-importa-listino").addEventListener("click", () =>
    document.getElementById("input-file-importa-listino").click()
  );
  document.getElementById("input-file-importa-listino").addEventListener("change", fileExcelScelto);
  document.getElementById("btn-chiudi-importa-listino").addEventListener("click", () => {
    document.getElementById("modal-importa-listino").hidden = true;
  });
  document.getElementById("btn-annulla-listino").addEventListener("click", chiudiModaleListino);
  document.getElementById("btn-salva-listino").addEventListener("click", salvaLavorazioneListino);
  document.getElementById("listino-modalita-prezzo").addEventListener("change", aggiornaCampiModalitaPrezzoListino);

  document.getElementById("btn-annulla-fasce-listino").addEventListener("click", chiudiModaleFasceListino);
  document.getElementById("btn-salva-fasce-listino").addEventListener("click", salvaFasceListino);

  // --- Calendario di lavoro (Impostazioni) ---

  const NOMI_GIORNI_SETTIMANA = ["Domenica", "Lunedì", "Martedì", "Mercoledì", "Giovedì", "Venerdì", "Sabato"];

  function testoEsitoGiorno(capacityFactor) {
    if (capacityFactor === 1) return "Lavorativo";
    if (capacityFactor === 0.5) return "Mezza giornata";
    return "Chiuso";
  }

  function classeEsitoGiorno(capacityFactor) {
    if (capacityFactor === 1) return "stato-attivo";
    if (capacityFactor === 0.5) return "stato-parziale";
    return "stato-inattivo";
  }

  async function caricaCalendario() {
    nascondiErrore();
    const daData = document.getElementById("calendario-da-data").value;
    const aData = document.getElementById("calendario-a-data").value;
    if (!daData || !aData) {
      return;
    }
    if (aData < daData) {
      mostraErrore("La data finale non può essere prima della data iniziale.");
      return;
    }
    try {
      const giorni = await window.sciuscia.call("impostazioni.calendario", { daData, aData });
      renderCalendario(giorni || []);
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare il calendario: " + errore.message);
    }
  }

  function renderCalendario(giorni) {
    const corpo = document.getElementById("corpo-tabella-calendario");
    corpo.innerHTML = "";
    for (const giorno of giorni) {
      const data = new Date(giorno.date + "T00:00:00");
      const riga = document.createElement("tr");
      riga.innerHTML = `
        <td>${escapeHtml(formattaDataItaliana(giorno.date))}</td>
        <td>${escapeHtml(NOMI_GIORNI_SETTIMANA[data.getDay()])}</td>
        <td class="${classeEsitoGiorno(giorno.capacityFactor)}">${testoEsitoGiorno(giorno.capacityFactor)}</td>
        <td>${escapeHtml(giorno.reason || "")}</td>
        <td class="azioni-riga">${giorno.calendarExceptionId ? '<button type="button" class="btn-elimina-eccezione-calendario">Elimina</button>' : ""}</td>
      `;
      if (giorno.calendarExceptionId) {
        riga.querySelector(".btn-elimina-eccezione-calendario").addEventListener("click", () =>
          eliminaEccezioneCalendario(giorno.calendarExceptionId)
        );
      }
      corpo.appendChild(riga);
    }
  }

  function formattaDataItaliana(dataIso) {
    const [anno, mese, giorno] = dataIso.split("-");
    return `${giorno}/${mese}/${anno}`;
  }

  function apriModaleChiusuraCalendario() {
    document.getElementById("chiusura-da-data").value = document.getElementById("calendario-da-data").value || "";
    document.getElementById("chiusura-a-data").value = document.getElementById("calendario-da-data").value || "";
    document.getElementById("chiusura-mezza-giornata").checked = false;
    document.getElementById("chiusura-descrizione").value = "";
    document.getElementById("modal-chiusura-calendario").hidden = false;
  }

  function chiudiModaleChiusuraCalendario() {
    document.getElementById("modal-chiusura-calendario").hidden = true;
  }

  async function salvaChiusuraCalendario() {
    nascondiErrore();
    const daData = document.getElementById("chiusura-da-data").value;
    const aData = document.getElementById("chiusura-a-data").value;
    const mezzaGiornata = document.getElementById("chiusura-mezza-giornata").checked;
    const descrizione = document.getElementById("chiusura-descrizione").value.trim();
    if (!daData || !aData) {
      mostraErrore("Indicare le date di inizio e fine della chiusura.");
      return;
    }
    if (aData < daData) {
      mostraErrore("La data finale non può essere prima della data iniziale.");
      return;
    }
    if (mezzaGiornata && daData !== aData) {
      mostraErrore("La mezza giornata è consentita solo per una chiusura di un unico giorno.");
      return;
    }
    if (!descrizione) {
      mostraErrore("Indicare il motivo della chiusura.");
      return;
    }
    try {
      await window.sciuscia.call("impostazioni.creaChiusuraCalendario", { daData, aData, mezzaGiornata, descrizione });
      chiudiModaleChiusuraCalendario();
      await caricaCalendario();
    } catch (errore) {
      mostraErrore("Salvataggio chiusura non riuscito: " + errore.message);
    }
  }

  function apriModaleAperturaCalendario() {
    document.getElementById("apertura-data").value = document.getElementById("calendario-da-data").value || "";
    document.getElementById("apertura-descrizione").value = "";
    document.getElementById("modal-apertura-calendario").hidden = false;
  }

  function chiudiModaleAperturaCalendario() {
    document.getElementById("modal-apertura-calendario").hidden = true;
  }

  async function salvaAperturaCalendario() {
    nascondiErrore();
    const data = document.getElementById("apertura-data").value;
    const descrizione = document.getElementById("apertura-descrizione").value.trim();
    if (!data) {
      mostraErrore("Indicare la data dell'apertura straordinaria.");
      return;
    }
    if (!descrizione) {
      mostraErrore("Indicare il motivo dell'apertura straordinaria.");
      return;
    }
    try {
      await window.sciuscia.call("impostazioni.creaAperturaCalendario", { data, descrizione });
      chiudiModaleAperturaCalendario();
      await caricaCalendario();
    } catch (errore) {
      mostraErrore("Salvataggio apertura straordinaria non riuscito: " + errore.message);
    }
  }

  async function eliminaEccezioneCalendario(eccezioneId) {
    if (!window.confirm("Eliminare questa eccezione dal calendario? Il giorno tornerà a seguire la regola ordinaria.")) {
      return;
    }
    nascondiErrore();
    try {
      await window.sciuscia.call("impostazioni.eliminaEccezioneCalendario", { eccezioneId });
      await caricaCalendario();
    } catch (errore) {
      mostraErrore("Eliminazione non riuscita: " + errore.message);
    }
  }

  document.getElementById("btn-mostra-calendario").addEventListener("click", caricaCalendario);
  document.getElementById("btn-salva-ore-giorno").addEventListener("click", salvaCapacitaGiornaliera);
  document.getElementById("btn-nuova-chiusura-calendario").addEventListener("click", apriModaleChiusuraCalendario);
  document.getElementById("btn-nuova-apertura-calendario").addEventListener("click", apriModaleAperturaCalendario);
  document.getElementById("btn-annulla-chiusura-calendario").addEventListener("click", chiudiModaleChiusuraCalendario);
  document.getElementById("btn-salva-chiusura-calendario").addEventListener("click", salvaChiusuraCalendario);
  document.getElementById("btn-annulla-apertura-calendario").addEventListener("click", chiudiModaleAperturaCalendario);
  document.getElementById("btn-salva-apertura-calendario").addEventListener("click", salvaAperturaCalendario);

  // --- Agenda lavori ---

  async function caricaAgenda() {
    nascondiErrore();
    try {
      agendaCache = await window.sciuscia.call("agenda.elenco");
      await renderAgenda();
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare l'agenda: " + errore.message);
    }
  }

  /// Le prossime `numeroGiorni` date lavorative da oggi (capacità > 0), per evidenziare gli
  /// articoli che entrano nella soglia senza spostarli dal loro giorno (vedi la specifica,
  /// "la soglia serve a evidenziarli, non a spostarli").
  function estraiProssimiGiorniLavorativi(giorniCalendario, oggiTesto, numeroGiorni) {
    const risultato = new Set();
    for (const giorno of giorniCalendario) {
      if (giorno.date < oggiTesto) continue;
      if (giorno.capacityFactor > 0) {
        risultato.add(giorno.date);
        if (risultato.size >= numeroGiorni) break;
      }
    }
    return risultato;
  }

  async function renderAgenda() {
    const daData = document.getElementById("agenda-da-data").value;
    const aData = document.getElementById("agenda-a-data").value;
    const raggruppa = document.getElementById("agenda-raggruppa-lavorazione").checked;
    const contenitore = document.getElementById("agenda-contenuto");
    contenitore.innerHTML = "";

    if (!daData || !aData) {
      return;
    }

    const oggiTesto = formattaDataPerInput(new Date());
    const inizioCalendario = daData < oggiTesto ? daData : oggiTesto;
    const margineLavorativi = formattaDataPerInput(new Date(new Date(oggiTesto + "T00:00:00").getTime() + 20 * 24 * 60 * 60 * 1000));
    const fineCalendario = aData > margineLavorativi ? aData : margineLavorativi;

    let giorniCalendario = [];
    try {
      giorniCalendario = await window.sciuscia.call("impostazioni.calendario", { daData: inizioCalendario, aData: fineCalendario }) || [];
    } catch (errore) {
      mostraErrore("Non è stato possibile leggere il calendario aperture: " + errore.message);
    }

    const infoGiorno = new Map(giorniCalendario.map((g) => [g.date, g]));
    const prossimiLavorativi = estraiProssimiGiorniLavorativi(giorniCalendario, oggiTesto, 5);

    const articoli = agendaCache.map((articolo) => {
      const dataPrevista = articolo.item.dueDate;
      const scaduto = !!dataPrevista && dataPrevista < oggiTesto;
      const inScadenza = !scaduto && !!dataPrevista && prossimiLavorativi.has(dataPrevista);
      return { ...articolo, scaduto, inScadenza };
    });

    const inPeriodo = articoli.filter((articolo) => {
      if (articolo.scaduto || !articolo.item.dueDate) {
        return true;
      }
      return articolo.item.dueDate >= daData && articolo.item.dueDate <= aData;
    });

    // Stesso grafico a barre già presente in "Consulta carico", qui sul periodo Dal/Al scelto
    // in Agenda, così il carico si vede subito senza dover aprire un'altra finestra.
    const giorniGraficoAgenda = giorniCalendario.filter((g) => g.date >= daData && g.date <= aData);
    const carichiPerGiornoAgenda = costruisciCarichiPerGiorno(articoli);
    try {
      const capacita = await window.sciuscia.call("impostazioni.leggiCapacitaGiornaliera");
      costruisciGraficoCarico(giorniGraficoAgenda, carichiPerGiornoAgenda, "agenda-grafico-carico", capacita.capacitaGiornalieraMinuti);
    } catch (errore) {
      costruisciGraficoCarico(giorniGraficoAgenda, carichiPerGiornoAgenda, "agenda-grafico-carico", 0);
    }

    document.getElementById("agenda-stato-vuoto").hidden = inPeriodo.length > 0;
    if (inPeriodo.length === 0) {
      return;
    }

    if (raggruppa) {
      renderAgendaRaggruppata(contenitore, inPeriodo);
    } else {
      renderAgendaPerGiorno(contenitore, inPeriodo, infoGiorno);
    }
  }

  function renderAgendaPerGiorno(contenitore, articoli, infoGiorno) {
    const arretrati = articoli.filter((a) => a.scaduto);
    const senzaData = articoli.filter((a) => !a.scaduto && !a.item.dueDate);
    const perData = new Map();
    for (const articolo of articoli) {
      if (articolo.scaduto || !articolo.item.dueDate) {
        continue;
      }
      if (!perData.has(articolo.item.dueDate)) {
        perData.set(articolo.item.dueDate, []);
      }
      perData.get(articolo.item.dueDate).push(articolo);
    }

    if (arretrati.length > 0) {
      contenitore.appendChild(costruisciGruppoAgenda("Arretrati", arretrati, null, false));
    }

    for (const data of Array.from(perData.keys()).sort()) {
      const giorno = infoGiorno.get(data);
      const titolo = `${NOMI_GIORNI_SETTIMANA[new Date(data + "T00:00:00").getDay()]} ${formattaDataItaliana(data)}`;
      const nota = giorno && giorno.capacityFactor < 1 ? giorno.reason : null;
      contenitore.appendChild(costruisciGruppoAgenda(titolo, perData.get(data), nota, false));
    }

    if (senzaData.length > 0) {
      contenitore.appendChild(costruisciGruppoAgenda("Senza data prevista", senzaData, null, false));
    }
  }

  function renderAgendaRaggruppata(contenitore, articoli) {
    const gruppi = new Map();
    for (const articolo of articoli) {
      const lavorazioni = articolo.works.length > 0
        ? articolo.works
        : [{ etichetta: "Senza lavorazione interna indicata", ordine: Number.MAX_SAFE_INTEGER }];
      for (const lavorazione of lavorazioni) {
        if (!gruppi.has(lavorazione.etichetta)) {
          gruppi.set(lavorazione.etichetta, { ordine: lavorazione.ordine, voci: [] });
        }
        gruppi.get(lavorazione.etichetta).voci.push(articolo);
      }
    }

    const etichette = Array.from(gruppi.keys()).sort((a, b) => {
      const differenza = gruppi.get(a).ordine - gruppi.get(b).ordine;
      return differenza !== 0 ? differenza : a.localeCompare(b, "it");
    });

    for (const etichetta of etichette) {
      contenitore.appendChild(costruisciGruppoAgenda(etichetta, gruppi.get(etichetta).voci, null, true));
    }
  }

  function costruisciGruppoAgenda(titolo, voci, nota, mostraData) {
    const gruppo = document.createElement("div");
    gruppo.className = "agenda-gruppo";
    const notaHtml = nota ? `<span class="chiusura-giorno">— ${escapeHtml(nota)}</span>` : "";
    gruppo.innerHTML = `<h3 class="agenda-gruppo-titolo">${escapeHtml(titolo)} ${notaHtml}</h3>`;
    for (const articolo of voci) {
      gruppo.appendChild(costruisciSchedaAgenda(articolo, mostraData));
    }
    return gruppo;
  }

  function costruisciSchedaAgenda(articolo, mostraData) {
    const item = articolo.item;
    const scheda = document.createElement("div");
    scheda.className = "panel scheda-agenda";
    if (articolo.scaduto) {
      scheda.classList.add("scaduta");
    } else if (articolo.inScadenza) {
      scheda.classList.add("in-scadenza");
    }

    const bollini = [];
    if (item.isUrgent) bollini.push('<span class="badge urgente">Urgente</span>');
    if (item.isPriority) bollini.push('<span class="badge priorita">Priorità</span>');
    if (articolo.scaduto) bollini.push('<span class="badge arretrato">Arretrato</span>');

    const dataTesto = mostraData ? (item.dueDate ? formattaDataItaliana(item.dueDate) : "Senza data") : "";
    const nomiLavorazioni = articolo.works.length > 0
      ? articolo.works.map((w) => w.etichetta).join(", ")
      : "Nessuna lavorazione interna indicata";

    scheda.innerHTML = `
      <div class="scheda-agenda-intestazione">
        <div>
          <strong>${escapeHtml(item.visibleCode)}</strong> — ${escapeHtml(articolo.categoryName)} / ${escapeHtml(articolo.brandName)}
          ${bollini.join(" ")}
          <div class="descrizione-articolo">${escapeHtml(articolo.customerName)} — ${escapeHtml(item.description)}</div>
          <div class="scheda-agenda-lavorazioni">${escapeHtml(nomiLavorazioni)}</div>
        </div>
        ${dataTesto ? `<div class="scheda-agenda-data">${escapeHtml(dataTesto)}</div>` : ""}
      </div>
    `;
    scheda.addEventListener("click", () => apriAnteprimaAgenda(articolo));
    return scheda;
  }

  async function apriAnteprimaAgenda(articolo) {
    nascondiErrore();
    anteprimaAgendaCorrente = articolo;
    document.getElementById("anteprima-agenda-titolo").textContent =
      articolo.item.visibleCode + " — " + articolo.categoryName + " / " + articolo.brandName;
    document.getElementById("anteprima-agenda-sottotitolo").textContent =
      articolo.orderVisibleCode + " · " + articolo.customerName;
    document.getElementById("anteprima-agenda-lavorazioni").textContent =
      articolo.works.length > 0 ? articolo.works.map((w) => w.etichetta).join(", ") : "Nessuna lavorazione interna indicata.";
    const contenitoreFoto = document.getElementById("anteprima-agenda-foto");
    contenitoreFoto.innerHTML = "";
    document.getElementById("modal-anteprima-agenda").hidden = false;

    try {
      const dettaglio = await window.sciuscia.call("commesse.dettaglio", { commessaId: articolo.orderId });
      const dettaglioArticolo = (dettaglio.items || []).find((d) => d.item.itemId === articolo.item.itemId);
      if (dettaglioArticolo) {
        anteprimaAgendaCorrente = { ...articolo, item: dettaglioArticolo.item };
        contenitoreFoto.appendChild(costruisciGalleriaFotoSoloLettura(dettaglioArticolo.photos || []));
      }
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare le fotografie: " + errore.message);
    }
  }

  function chiudiModaleAnteprimaAgenda() {
    document.getElementById("modal-anteprima-agenda").hidden = true;
    anteprimaAgendaCorrente = null;
  }

  async function apriCommessaDaAgenda(orderId, itemId) {
    chiudiModaleAnteprimaAgenda();
    articoloDaEvidenziareId = itemId;
    document.getElementById("nav-commesse").classList.add("active");
    document.getElementById("nav-home").classList.remove("active");
    document.getElementById("nav-agenda").classList.remove("active");
    document.getElementById("nav-pdr").classList.remove("active");
    document.getElementById("nav-pa").classList.remove("active");
    document.getElementById("nav-anagrafiche").classList.remove("active");
    document.getElementById("nav-impostazioni").classList.remove("active");
    vistaHome.hidden = true;
    vistaAgenda.hidden = true;
    vistaPdr.hidden = true;
    vistaPa.hidden = true;
    vistaAnagrafiche.hidden = true;
    vistaImpostazioni.hidden = true;
    await apriDettaglioCommessa(orderId);
  }

  async function apriCommessaDaPdr(orderId, itemId) {
    articoloDaEvidenziareId = itemId;
    document.getElementById("nav-commesse").classList.add("active");
    document.getElementById("nav-home").classList.remove("active");
    document.getElementById("nav-agenda").classList.remove("active");
    document.getElementById("nav-pdr").classList.remove("active");
    document.getElementById("nav-pa").classList.remove("active");
    document.getElementById("nav-anagrafiche").classList.remove("active");
    document.getElementById("nav-impostazioni").classList.remove("active");
    vistaHome.hidden = true;
    vistaAgenda.hidden = true;
    vistaPdr.hidden = true;
    vistaPa.hidden = true;
    vistaAnagrafiche.hidden = true;
    vistaImpostazioni.hidden = true;
    await apriDettaglioCommessa(orderId);
  }

  /// Carica l'elenco "Pronti da ritirare" (specifica, sezione 6): articoli pronti per il cliente
  /// (ancora da avvisare o già avvisati) delle commesse aperte, raggruppati per commessa/cliente.
  async function caricaPdr() {
    nascondiErrore();
    try {
      pdrCache = await window.sciuscia.call("ritiri.prontiDaRitirare");
      renderPdr();
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare i pronti da ritirare: " + errore.message);
    }
  }

  function renderPdr() {
    const contenuto = document.getElementById("pdr-contenuto");
    contenuto.innerHTML = "";

    const gruppiPerCommessa = new Map();
    for (const riga of pdrCache) {
      if (!gruppiPerCommessa.has(riga.orderId)) {
        gruppiPerCommessa.set(riga.orderId, {
          orderId: riga.orderId,
          orderVisibleCode: riga.orderVisibleCode,
          customerName: riga.customerName,
          customerMobile: riga.customerMobile,
          righe: [],
        });
      }
      gruppiPerCommessa.get(riga.orderId).righe.push(riga);
    }

    const gruppi = Array.from(gruppiPerCommessa.values());
    for (const gruppo of gruppi) {
      gruppo.daAvvisare = gruppo.righe.filter((r) => r.item.workflowStatus === "PRONTO_DA_AVVISARE");
      gruppo.completamento = gruppo.righe.reduce(
        (minimo, r) => (!minimo || r.updatedAt < minimo ? r.updatedAt : minimo), null);
    }

    // Prima i clienti ancora da avvisare, poi la data di completamento (specifica, sezione 6).
    gruppi.sort((a, b) => {
      const aDaAvvisare = a.daAvvisare.length > 0 ? 0 : 1;
      const bDaAvvisare = b.daAvvisare.length > 0 ? 0 : 1;
      if (aDaAvvisare !== bDaAvvisare) {
        return aDaAvvisare - bDaAvvisare;
      }
      return (a.completamento || "").localeCompare(b.completamento || "");
    });

    document.getElementById("pdr-stato-vuoto").hidden = gruppi.length > 0;
    for (const gruppo of gruppi) {
      contenuto.appendChild(costruisciSchedaPdr(gruppo));
    }
  }

  function costruisciSchedaPdr(gruppo) {
    // Gli articoli ancora da avvisare sono evidenziati e vengono mostrati prima degli avvisati.
    const righeOrdinate = gruppo.righe.slice().sort((a, b) => {
      const aDaAvvisare = a.item.workflowStatus === "PRONTO_DA_AVVISARE" ? 0 : 1;
      const bDaAvvisare = b.item.workflowStatus === "PRONTO_DA_AVVISARE" ? 0 : 1;
      return aDaAvvisare - bDaAvvisare;
    });

    const scheda = document.createElement("div");
    scheda.className = "pdr-gruppo" + (gruppo.daAvvisare.length > 0 ? " pdr-gruppo-da-avvisare" : "");

    const intestazione = document.createElement("div");
    intestazione.className = "pdr-gruppo-intestazione";
    intestazione.innerHTML =
      '<div class="pdr-gruppo-cliente">' +
      "<strong>" + escapeHtml(gruppo.customerName) + "</strong>" +
      '<span class="pdr-gruppo-cellulare">' + escapeHtml(gruppo.customerMobile || "Cellulare non registrato") + "</span>" +
      "</div>" +
      '<div class="pdr-gruppo-stato">' + (gruppo.daAvvisare.length > 0 ? "Da avvisare" : "Tutti avvisati") + "</div>";

    const azioni = document.createElement("div");
    azioni.className = "pdr-gruppo-azioni";

    const bottoneApri = document.createElement("button");
    bottoneApri.type = "button";
    bottoneApri.textContent = "Apri commessa " + gruppo.orderVisibleCode;
    bottoneApri.addEventListener("click", () => apriCommessaDaPdr(gruppo.orderId, righeOrdinate[0].item.itemId));
    azioni.appendChild(bottoneApri);

    const bottoneWhatsApp = document.createElement("button");
    bottoneWhatsApp.type = "button";
    bottoneWhatsApp.className = "primary";
    bottoneWhatsApp.textContent = "Prepara avviso WhatsApp";
    bottoneWhatsApp.disabled = gruppo.daAvvisare.length === 0;
    bottoneWhatsApp.addEventListener("click", () => preparaAvvisoWhatsAppPdr(gruppo));
    azioni.appendChild(bottoneWhatsApp);

    intestazione.appendChild(azioni);
    scheda.appendChild(intestazione);

    const elencoArticoli = document.createElement("div");
    elencoArticoli.className = "pdr-gruppo-articoli";
    for (const riga of righeOrdinate) {
      const daAvvisare = riga.item.workflowStatus === "PRONTO_DA_AVVISARE";
      const bottoneArticolo = document.createElement("button");
      bottoneArticolo.type = "button";
      bottoneArticolo.className = "pdr-articolo-riga" + (daAvvisare ? " pdr-articolo-da-avvisare" : "");
      bottoneArticolo.innerHTML =
        "<span>" + escapeHtml(riga.item.visibleCode + " — " + riga.categoryName + " — " + riga.item.description) + "</span>" +
        '<span class="pdr-articolo-stato">' + (daAvvisare ? "Da avvisare" : "Avvisato") + "</span>";
      bottoneArticolo.addEventListener("click", () => apriCommessaDaPdr(gruppo.orderId, riga.item.itemId));
      elencoArticoli.appendChild(bottoneArticolo);
    }
    scheda.appendChild(elencoArticoli);

    return scheda;
  }

  /// Apre direttamente la finestra "Nuovo messaggio al cliente" con tutti gli articoli ancora da
  /// avvisare già preselezionati (specifica, sezione 6: "consentendo un avviso parziale" se poi
  /// l'operatore deseleziona qualcosa prima di registrare).
  async function preparaAvvisoWhatsAppPdr(gruppo) {
    const idsDaAvvisare = gruppo.daAvvisare.map((r) => r.item.itemId);
    if (idsDaAvvisare.length === 0) {
      return;
    }
    await apriCommessaDaPdr(gruppo.orderId, idsDaAvvisare[0]);
    apriModaleMessaggio("AVVISO_PRONTO", idsDaAvvisare);
  }

  // --- Presso artigiani ---

  async function caricaPa() {
    nascondiErrore();
    try {
      paCache = await window.sciuscia.call("artigiani.pressoArtigiani");
      renderPa();
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare gli articoli presso gli artigiani: " + errore.message);
    }
  }

  // Raggruppa le righe piatte (una per articolo+lavorazione esterna) per articolo, raccogliendo
  // tutte le lavorazioni affidate in un unico elenco (specifica, sezione 7: "lavorazioni esterne
  // affidate", al plurale), e calcola per ciascun articolo lo stato di ritardo/sollecito.
  function costruisciArticoliPa(righe) {
    const perArticolo = new Map();
    for (const riga of righe) {
      if (!perArticolo.has(riga.item.itemId)) {
        perArticolo.set(riga.item.itemId, {
          item: riga.item,
          orderId: riga.orderId,
          orderVisibleCode: riga.orderVisibleCode,
          categoryName: riga.categoryName,
          externalAssignmentId: riga.externalAssignmentId,
          assignmentRowVersion: riga.assignmentRowVersion,
          artisanId: riga.artisanId,
          artisanName: riga.artisanName,
          entrustedAt: riga.entrustedAt,
          expectedReturnDate: riga.expectedReturnDate,
          solicitAfterDays: riga.solicitAfterDays,
          lavorazioni: [],
        });
      }
      if (riga.externalWorkName) {
        perArticolo.get(riga.item.itemId).lavorazioni.push(riga.externalWorkName);
      }
    }

    const articoli = Array.from(perArticolo.values());
    for (const articolo of articoli) {
      const stato = calcolaStatoRitardoPa(articolo);
      articolo.giorniRitardo = stato.giorniRitardo;
      articolo.daSollecitare = stato.daSollecitare;
    }
    return articoli;
  }

  // Giorni di ritardo sulla data prevista di rientro (se indicata); in assenza di ritardo,
  // "da sollecitare" se sono passati almeno i giorni di soglia dell'artigiano dall'affidamento
  // (soglia facoltativa, specifica a ogni artigiano). Non cambia l'ordinamento richiesto dalla
  // specifica: è solo un avviso aggiuntivo.
  function calcolaStatoRitardoPa(articolo) {
    const oggi = new Date();
    oggi.setHours(0, 0, 0, 0);

    let giorniRitardo = null;
    if (articolo.expectedReturnDate) {
      const scadenza = new Date(articolo.expectedReturnDate.substring(0, 10) + "T00:00:00");
      const diffGiorni = Math.round((oggi - scadenza) / 86400000);
      if (diffGiorni > 0) {
        giorniRitardo = diffGiorni;
      }
    }

    let daSollecitare = false;
    if (!giorniRitardo && articolo.solicitAfterDays) {
      const affidato = new Date(articolo.entrustedAt.substring(0, 10) + "T00:00:00");
      const diffGiorni = Math.round((oggi - affidato) / 86400000);
      if (diffGiorni >= articolo.solicitAfterDays) {
        daSollecitare = true;
      }
    }

    return { giorniRitardo, daSollecitare };
  }

  function renderPa() {
    const contenuto = document.getElementById("pa-contenuto");
    contenuto.innerHTML = "";

    const articoli = costruisciArticoliPa(paCache);

    const gruppiPerArtigiano = new Map();
    for (const articolo of articoli) {
      if (!gruppiPerArtigiano.has(articolo.artisanId)) {
        gruppiPerArtigiano.set(articolo.artisanId, {
          artisanId: articolo.artisanId,
          artisanName: articolo.artisanName,
          articoli: [],
        });
      }
      gruppiPerArtigiano.get(articolo.artisanId).articoli.push(articolo);
    }

    const gruppi = Array.from(gruppiPerArtigiano.values());
    gruppi.sort((a, b) => a.artisanName.localeCompare(b.artisanName, "it"));

    // Ordinamento articoli (specifica, sezione 7): prima i ritardi (dal maggiore), poi la data
    // prevista di rientro; gli articoli senza data prevista sono in fondo.
    for (const gruppo of gruppi) {
      gruppo.articoli.sort((a, b) => {
        const aRitardo = a.giorniRitardo || 0;
        const bRitardo = b.giorniRitardo || 0;
        if (aRitardo !== bRitardo) {
          return bRitardo - aRitardo;
        }
        if (!a.expectedReturnDate && !b.expectedReturnDate) {
          return 0;
        }
        if (!a.expectedReturnDate) {
          return 1;
        }
        if (!b.expectedReturnDate) {
          return -1;
        }
        return a.expectedReturnDate.localeCompare(b.expectedReturnDate);
      });
    }

    document.getElementById("pa-stato-vuoto").hidden = gruppi.length > 0;
    for (const gruppo of gruppi) {
      contenuto.appendChild(costruisciSchedaArtigianoGruppo(gruppo));
    }
  }

  function costruisciSchedaArtigianoGruppo(gruppo) {
    const scheda = document.createElement("div");
    scheda.className = "pa-gruppo";

    const intestazione = document.createElement("div");
    intestazione.className = "pa-gruppo-intestazione";
    intestazione.innerHTML =
      '<div class="pa-gruppo-nome"><strong>' + escapeHtml(gruppo.artisanName) + "</strong></div>" +
      '<span class="pa-gruppo-conteggio">' + gruppo.articoli.length +
      (gruppo.articoli.length === 1 ? " articolo" : " articoli") + "</span>";
    scheda.appendChild(intestazione);

    const elencoArticoli = document.createElement("div");
    elencoArticoli.className = "pa-gruppo-articoli";
    for (const articolo of gruppo.articoli) {
      elencoArticoli.appendChild(costruisciRigaArtigiano(articolo));
    }
    scheda.appendChild(elencoArticoli);

    return scheda;
  }

  function costruisciRigaArtigiano(articolo) {
    const senzaData = !articolo.expectedReturnDate;
    const riga = document.createElement("button");
    riga.type = "button";
    riga.className = "pa-articolo-riga" +
      (articolo.giorniRitardo ? " pa-articolo-ritardo" : "") +
      (senzaData ? " pa-articolo-senza-data" : "");

    const bollini = [];
    if (articolo.giorniRitardo) {
      bollini.push(
        '<span class="badge arretrato">' + articolo.giorniRitardo +
        (articolo.giorniRitardo === 1 ? " giorno di ritardo" : " giorni di ritardo") + "</span>");
    }
    if (articolo.daSollecitare) {
      bollini.push('<span class="badge sollecito">Da sollecitare</span>');
    }

    const dataTesto = senzaData ? "Senza data prevista" : formattaDataItaliana(articolo.expectedReturnDate);
    const lavorazioniTesto = articolo.lavorazioni.length > 0
      ? articolo.lavorazioni.join(", ")
      : "Nessuna lavorazione esterna indicata";

    riga.innerHTML =
      '<div class="pa-articolo-info">' +
      "<strong>" + escapeHtml(articolo.item.visibleCode) + "</strong> — " +
      escapeHtml(articolo.categoryName) + " — " + escapeHtml(articolo.item.description) + " " +
      bollini.join(" ") +
      '<div class="pa-articolo-lavorazioni">' + escapeHtml(lavorazioniTesto) + "</div>" +
      "</div>" +
      '<div class="pa-articolo-destra"><span class="pa-articolo-data">' + escapeHtml(dataTesto) + "</span></div>";

    riga.addEventListener("click", () => apriAnteprimaArtigiani(articolo));
    return riga;
  }

  async function apriAnteprimaArtigiani(articolo) {
    nascondiErrore();
    anteprimaArtigianiCorrente = articolo;
    document.getElementById("anteprima-artigiani-titolo").textContent =
      articolo.item.visibleCode + " — " + articolo.categoryName + " — " + articolo.artisanName;
    document.getElementById("anteprima-artigiani-sottotitolo").textContent =
      articolo.orderVisibleCode + " · affidato il " + formattaDataItaliana(articolo.entrustedAt.substring(0, 10));
    document.getElementById("anteprima-artigiani-lavorazioni").textContent =
      articolo.lavorazioni.length > 0 ? articolo.lavorazioni.join(", ") : "Nessuna lavorazione esterna indicata.";
    document.getElementById("anteprima-artigiani-data-rientro").value = new Date().toISOString().substring(0, 10);

    const contenitoreFoto = document.getElementById("anteprima-artigiani-foto");
    contenitoreFoto.innerHTML = "";
    document.getElementById("modal-anteprima-artigiani").hidden = false;

    try {
      const dettaglio = await window.sciuscia.call("commesse.dettaglio", { commessaId: articolo.orderId });
      const dettaglioArticolo = (dettaglio.items || []).find((d) => d.item.itemId === articolo.item.itemId);
      if (dettaglioArticolo) {
        anteprimaArtigianiCorrente = { ...articolo, item: dettaglioArticolo.item };
        contenitoreFoto.appendChild(costruisciGalleriaFotoSoloLettura(dettaglioArticolo.photos || []));
      }
    } catch (errore) {
      mostraErrore("Non è stato possibile caricare le fotografie: " + errore.message);
    }
  }

  function chiudiModaleAnteprimaArtigiani() {
    document.getElementById("modal-anteprima-artigiani").hidden = true;
    anteprimaArtigianiCorrente = null;
  }

  async function apriCommessaDaArtigiani(orderId, itemId) {
    chiudiModaleAnteprimaArtigiani();
    articoloDaEvidenziareId = itemId;
    document.getElementById("nav-commesse").classList.add("active");
    document.getElementById("nav-home").classList.remove("active");
    document.getElementById("nav-agenda").classList.remove("active");
    document.getElementById("nav-pdr").classList.remove("active");
    document.getElementById("nav-pa").classList.remove("active");
    document.getElementById("nav-anagrafiche").classList.remove("active");
    document.getElementById("nav-impostazioni").classList.remove("active");
    vistaHome.hidden = true;
    vistaAgenda.hidden = true;
    vistaPdr.hidden = true;
    vistaPa.hidden = true;
    vistaAnagrafiche.hidden = true;
    vistaImpostazioni.hidden = true;
    await apriDettaglioCommessa(orderId);
  }

  // "Segna Rientrato" (specifica, sezione 7): la data propone oggi ma resta modificabile, per
  // registrare un rientro avvenuto prima. Non cambia da solo lo stato di lavorazione
  // dell'articolo: resta una scelta dell'operatore dal dettaglio della commessa.
  async function segnaRientratoDaArtigiani() {
    if (!anteprimaArtigianiCorrente) {
      return;
    }
    nascondiErrore();
    const articolo = anteprimaArtigianiCorrente;
    const dataRientro = document.getElementById("anteprima-artigiani-data-rientro").value || null;
    try {
      await window.sciuscia.call("artigiani.segnaRientrato", {
        affidamentoId: articolo.externalAssignmentId,
        rowVersion: articolo.assignmentRowVersion,
        dataRientro,
      });
      chiudiModaleAnteprimaArtigiani();
      await caricaPa();
    } catch (errore) {
      mostraErrore("Non è stato possibile registrare il rientro: " + errore.message);
    }
  }

  async function segnaProntoDaAgenda() {
    if (!anteprimaAgendaCorrente) {
      return;
    }
    nascondiErrore();
    const articolo = anteprimaAgendaCorrente;
    try {
      await window.sciuscia.call("articoli.cambiaStato", {
        articoloId: articolo.item.itemId,
        rowVersion: articolo.item.rowVersion,
        nuovoStato: "PRONTO_DA_AVVISARE",
      });
      chiudiModaleAnteprimaAgenda();
      await caricaAgenda();
      if (window.confirm("Articolo segnato come pronto. Preparare subito il messaggio di avviso al cliente?")) {
        await apriCommessaDaAgenda(articolo.orderId, articolo.item.itemId);
        apriModaleMessaggioPerArticolo(articolo.item.itemId);
      }
    } catch (errore) {
      mostraErrore("Non è stato possibile segnare l'articolo come pronto: " + errore.message);
    }
  }

  /// Stampa esattamente quello che si vede a schermo nell'Agenda (stesso intervallo di date e
  /// stessa modalità, raggruppata o per giorno): una copia su carta da consultare lavorando.
  /// Non è legata a una commessa, quindi non viene registrata tra le stampe come etichette/ricevute.
  function stampaAgenda() {
    const contenuto = document.getElementById("agenda-contenuto");
    if (!contenuto.innerHTML.trim()) {
      mostraErrore("Non c'è nulla da stampare: aggiorna prima l'agenda.");
      return;
    }
    const daData = document.getElementById("agenda-da-data").value;
    const aData = document.getElementById("agenda-a-data").value;
    const sottotitolo = daData && aData
      ? `<p>Dal ${escapeHtml(formattaDataItaliana(daData))} al ${escapeHtml(formattaDataItaliana(aData))}</p>`
      : "";
    stampaDocumenti([], "Agenda lavori", sottotitolo + contenuto.innerHTML);
  }

  document.getElementById("btn-aggiorna-agenda").addEventListener("click", async () => {
    const bottone = document.getElementById("btn-aggiorna-agenda");
    await caricaAgenda();
    mostraConfermaPulsante(bottone, "Aggiornato ✓");
  });
  document.getElementById("agenda-raggruppa-lavorazione").addEventListener("change", renderAgenda);
  document.getElementById("btn-stampa-agenda").addEventListener("click", stampaAgenda);
  document.getElementById("btn-chiudi-anteprima-agenda").addEventListener("click", chiudiModaleAnteprimaAgenda);
  document.getElementById("btn-apri-commessa-agenda").addEventListener("click", () => {
    if (anteprimaAgendaCorrente) {
      apriCommessaDaAgenda(anteprimaAgendaCorrente.orderId, anteprimaAgendaCorrente.item.itemId);
    }
  });
  document.getElementById("btn-segna-pronto-agenda").addEventListener("click", segnaProntoDaAgenda);
  document.getElementById("btn-chiudi-anteprima-artigiani").addEventListener("click", chiudiModaleAnteprimaArtigiani);
  document.getElementById("btn-apri-commessa-artigiani").addEventListener("click", () => {
    if (anteprimaArtigianiCorrente) {
      apriCommessaDaArtigiani(anteprimaArtigianiCorrente.orderId, anteprimaArtigianiCorrente.item.itemId);
    }
  });
  document.getElementById("btn-segna-rientrato-artigiani").addEventListener("click", segnaRientratoDaArtigiani);

  // --- Consulta carico delle prossime settimane (specifica, sezione 3) ---

  /// `contesto` è null quando la finestra è aperta dal modulo "Nuovo articolo" (la data scelta va
  /// solo scritta nel campo, l'articolo non esiste ancora), oppure { articoloEsistente, commessaId }
  /// quando è apribile dalla scheda di un articolo già creato (la data va salvata subito).
  let modaleNuovoArticoloEraAperto = false;

  async function apriConsultaCarico(contesto) {
    nascondiErrore();
    consultaCaricoContesto = contesto || null;
    document.getElementById("carico-applica-tutti-contenitore").hidden = !(contesto && contesto.commessaId);
    document.getElementById("carico-applica-tutti").checked = false;

    if (!document.getElementById("carico-da-data").value) {
      const oggi = new Date();
      const inSetteSettimane = new Date(oggi.getTime() + 48 * 24 * 60 * 60 * 1000);
      document.getElementById("carico-da-data").value = formattaDataPerInput(oggi);
      document.getElementById("carico-a-data").value = formattaDataPerInput(inSetteSettimane);
    }

    // Se la finestra viene aperta da "Nuovo articolo" (entrambe le finestre hanno la stessa
    // sovrapposizione), la nascondiamo senza annullarla, così non si perde quanto già compilato:
    // torna visibile, con i suoi campi intatti, alla chiusura di Consulta carico.
    modaleNuovoArticoloEraAperto = !modaleArticolo.hidden;
    if (modaleNuovoArticoloEraAperto) {
      modaleArticolo.hidden = true;
    }

    document.getElementById("modal-consulta-carico").hidden = false;
    await caricaConsultaCarico();
  }

  function chiudiConsultaCarico() {
    document.getElementById("modal-consulta-carico").hidden = true;
    consultaCaricoContesto = null;
    if (modaleNuovoArticoloEraAperto) {
      modaleArticolo.hidden = false;
      modaleNuovoArticoloEraAperto = false;
    }
  }

  async function caricaConsultaCarico() {
    const daData = document.getElementById("carico-da-data").value;
    const aData = document.getElementById("carico-a-data").value;
    const corpo = document.getElementById("corpo-tabella-carico");
    corpo.innerHTML = "";
    if (!daData || !aData) {
      return;
    }

    nascondiErrore();
    try {
      const [articoli, giorniCalendario, capacita] = await Promise.all([
        window.sciuscia.call("agenda.elenco"),
        window.sciuscia.call("impostazioni.calendario", { daData, aData }),
        window.sciuscia.call("impostazioni.leggiCapacitaGiornaliera"),
      ]);

      const oggiTesto = formattaDataPerInput(new Date());
      const arretrati = articoli.filter((a) => a.item.dueDate && a.item.dueDate < oggiTesto);
      document.getElementById("carico-arretrati-totale").textContent =
        arretrati.length > 0
          ? `Arretrati (consegna già scaduta, articolo non ancora pronto): ${arretrati.length}`
          : "Nessun arretrato al momento.";

      const carichiPerGiorno = costruisciCarichiPerGiorno(articoli);

      for (const giorno of giorniCalendario || []) {
        corpo.appendChild(costruisciRigaCarico(giorno, carichiPerGiorno.get(giorno.date)));
      }
      costruisciGraficoCarico(giorniCalendario || [], carichiPerGiorno, "carico-grafico", capacita.capacitaGiornalieraMinuti);
    } catch (errore) {
      mostraErrore("Non è stato possibile consultare il carico: " + errore.message);
    }
  }

  /// Somma i tempi di realizzo (minuti) delle lavorazioni interne ancora da fare di un articolo
  /// (vedi il campo "Tempo di realizzo" nel Listino). Le lavorazioni senza un tempo impostato non
  /// contano nel totale (così come indicato all'operatore nel Listino).
  function minutiLavorazioniInterne(articolo) {
    return (articolo.works || []).reduce((somma, lavorazione) => somma + (lavorazione.durationMinutes || 0), 0);
  }

  /// Raggruppa gli articoli per data prevista, con sia il conteggio (usato dalla tabella di
  /// "Consulta carico") sia i minuti totali di lavorazioni interne (usati dal grafico a ore).
  function costruisciCarichiPerGiorno(articoli) {
    const carichiPerGiorno = new Map();
    for (const articolo of articoli) {
      const data = articolo.item.dueDate;
      if (!data) {
        continue;
      }
      if (!carichiPerGiorno.has(data)) {
        carichiPerGiorno.set(data, { totale: 0, urgenti: 0, prioritari: 0, minuti: 0 });
      }
      const voce = carichiPerGiorno.get(data);
      voce.totale += 1;
      if (articolo.item.isUrgent) voce.urgenti += 1;
      if (articolo.item.isPriority) voce.prioritari += 1;
      voce.minuti += minutiLavorazioniInterne(articolo);
    }
    return carichiPerGiorno;
  }

  /// Un grafico a barre semplice (senza librerie esterne), un po' com'è già stata scritta
  /// l'importazione Excel: ogni colonna è un giorno, l'altezza è proporzionale al numero di
  /// articoli già previsti quel giorno, il colore segue l'esito del giorno (lavorativo, mezza
  /// giornata, chiuso). Se il tempo previsto delle lavorazioni (quando impostato nel Listino)
  /// supera la capacità giornaliera di quel giorno, la colonna viene segnalata chiaramente
  /// (barra e numero in rosso, con un'icona di avviso): è solo un segnale, non impedisce nulla.
  /// Stessa funzione usata sia da "Consulta carico" sia dall'Agenda lavori, passando il
  /// contenitore giusto in `containerId`.
  function costruisciGraficoCarico(giorniCalendario, carichiPerGiorno, containerId, capacitaGiornalieraMinuti) {
    const contenitore = document.getElementById(containerId);
    contenitore.innerHTML = "";
    if (giorniCalendario.length === 0) {
      return;
    }

    const massimo = Math.max(1, ...giorniCalendario.map((g) => (carichiPerGiorno.get(g.date) || { totale: 0 }).totale));

    for (const giorno of giorniCalendario) {
      const carico = carichiPerGiorno.get(giorno.date) || { totale: 0, urgenti: 0, prioritari: 0, minuti: 0 };
      const sogliaMinuti = (capacitaGiornalieraMinuti || 0) * giorno.capacityFactor;
      const superato = giorno.capacityFactor > 0 && sogliaMinuti > 0 && carico.minuti > sogliaMinuti;
      const classeBarra = (giorno.capacityFactor === 0 ? "chiuso" : giorno.capacityFactor === 0.5 ? "mezza" : "") + (superato ? " superato" : "");
      const altezza = Math.max(2, Math.round((carico.totale / massimo) * 100));
      const motivoChiusura = giorno.capacityFactor < 1 ? ` (${giorno.reason})` : "";
      const oreTempoTesto = carico.minuti > 0 ? ` — tempo previsto ${formattaOreMinuti(carico.minuti)} su ${formattaOreMinuti(sogliaMinuti)} disponibili` : "";
      const titolo = `${formattaDataItaliana(giorno.date)} — ${carico.totale} previst${carico.totale === 1 ? "o" : "i"}${oreTempoTesto}${motivoChiusura}${superato ? " — CARICO SUPERATO" : ""}`;

      const colonna = document.createElement("div");
      colonna.className = "grafico-carico-colonna";
      colonna.title = titolo;
      colonna.innerHTML = `
        <div class="grafico-carico-valore${superato ? " superato" : ""}">${superato ? "⚠ " : ""}${carico.totale > 0 ? carico.totale : ""}</div>
        <div class="grafico-carico-traccia"><div class="grafico-carico-barra ${classeBarra}" style="height:${altezza}%"></div></div>
        <div class="grafico-carico-giorno">${new Date(giorno.date + "T00:00:00").getDate()}</div>
      `;
      contenitore.appendChild(colonna);
    }
  }

  /// Minuti in formato leggibile "1h 30min" (o solo "45min" sotto l'ora), per i messaggi del
  /// grafico del carico.
  function formattaOreMinuti(minuti) {
    const totale = Math.round(minuti);
    const ore = Math.floor(totale / 60);
    const resto = totale % 60;
    if (ore === 0) {
      return `${resto}min`;
    }
    return resto === 0 ? `${ore}h` : `${ore}h ${resto}min`;
  }

  function costruisciRigaCarico(giorno, carico) {
    const riga = document.createElement("tr");
    if (giorno.capacityFactor === 0) {
      riga.classList.add("riga-carico-chiuso");
    }

    const titoloGiorno = NOMI_GIORNI_SETTIMANA[new Date(giorno.date + "T00:00:00").getDay()];
    const esitoTesto = giorno.capacityFactor === 1 ? "Lavorativo" : giorno.capacityFactor === 0.5 ? "Mezza giornata" : "Chiuso";
    const notaEsito = giorno.capacityFactor < 1 ? ` — ${escapeHtml(giorno.reason)}` : "";
    const caricoTesto = carico
      ? `${carico.totale} previst${carico.totale === 1 ? "o" : "i"}`
        + (carico.urgenti > 0 ? `, ${carico.urgenti} urgent${carico.urgenti === 1 ? "e" : "i"}` : "")
        + (carico.prioritari > 0 ? `, ${carico.prioritari} con priorità` : "")
      : "Nessuno";

    riga.innerHTML = `
      <td>${escapeHtml(formattaDataItaliana(giorno.date))}</td>
      <td>${escapeHtml(titoloGiorno)}</td>
      <td>${escapeHtml(esitoTesto)}${notaEsito}</td>
      <td>${escapeHtml(caricoTesto)}</td>
      <td class="azioni-riga"><button type="button" class="btn-scegli-data-carico">Scegli questa data</button></td>
    `;
    riga.querySelector(".btn-scegli-data-carico").addEventListener("click", () => scegliDataCarico(giorno));
    return riga;
  }

  async function scegliDataCarico(giorno) {
    if (giorno.capacityFactor === 0 && !window.confirm(`Questo giorno è chiuso (${giorno.reason}). Usare comunque questa data?`)) {
      return;
    }

    if (!consultaCaricoContesto || !consultaCaricoContesto.articoloEsistente) {
      document.getElementById("articolo-scadenza").value = giorno.date;
      chiudiConsultaCarico();
      return;
    }

    nascondiErrore();
    const applicaATutti = document.getElementById("carico-applica-tutti").checked && !!consultaCaricoContesto.commessaId;
    try {
      if (applicaATutti) {
        await window.sciuscia.call("commesse.impostaScadenzaTutti", {
          commessaId: consultaCaricoContesto.commessaId,
          scadenza: giorno.date,
        });
      } else {
        await window.sciuscia.call("articoli.impostaScadenza", {
          articoloId: consultaCaricoContesto.articoloEsistente.itemId,
          scadenza: giorno.date,
          rowVersion: consultaCaricoContesto.articoloEsistente.rowVersion,
        });
      }
      chiudiConsultaCarico();
      await ricaricaDettaglioCommessa();
    } catch (errore) {
      mostraErrore("Non è stato possibile impostare la data: " + errore.message);
    }
  }

  document.getElementById("btn-consulta-carico-nuovo-articolo").addEventListener("click", () => apriConsultaCarico(null));
  document.getElementById("btn-aggiorna-carico").addEventListener("click", caricaConsultaCarico);
  document.getElementById("btn-chiudi-consulta-carico").addEventListener("click", chiudiConsultaCarico);

  caricaHome();
})();
