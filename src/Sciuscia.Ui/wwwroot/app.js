(function () {
  const corpoTabella = document.getElementById("corpo-tabella-commesse");
  const statoVuoto = document.getElementById("stato-vuoto");
  const bannerErrore = document.getElementById("error-banner");

  const modale = document.getElementById("modal-nuova-commessa");
  const inputRicercaCliente = document.getElementById("ricerca-cliente");
  const risultatiClienti = document.getElementById("risultati-clienti");
  const boxNuovoCliente = document.getElementById("box-nuovo-cliente");

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

  const ETICHETTE_STATO = {
    DA_LAVORARE: "Da lavorare",
    PRESSO_ARTIGIANO: "Presso l'artigiano",
    PRONTO_DA_AVVISARE: "Pronto, da avvisare",
    AVVISATO: "Avvisato",
    CONSEGNATO: "Consegnato",
    ANNULLATO: "Annullato",
  };

  const vistaAnagrafiche = document.getElementById("view-anagrafiche");
  let ricercaClientiAnagraficaInCorso = null;

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
      return;
    }
    dettaglioStatoVuoto.hidden = true;

    for (const dettaglioArticolo of dettaglio.items) {
      listaArticoli.appendChild(costruisciSchedaArticolo(dettaglioArticolo));
    }
  }

  function costruisciSchedaArticolo(dettaglioArticolo) {
    const articolo = dettaglioArticolo.item;
    const scheda = document.createElement("div");
    scheda.className = "panel scheda-articolo";

    const etichettaStato = ETICHETTE_STATO[articolo.workflowStatus] || articolo.workflowStatus;
    const bollini = [];
    if (articolo.isUrgent) bollini.push('<span class="badge urgente">Urgente</span>');
    if (articolo.isPriority) bollini.push('<span class="badge priorita">Priorità</span>');

    scheda.innerHTML = `
      <div class="scheda-articolo-intestazione">
        <div>
          <strong>${escapeHtml(articolo.visibleCode)}</strong> — ${escapeHtml(dettaglioArticolo.categoryName)} / ${escapeHtml(dettaglioArticolo.brandName)}
          ${bollini.join(" ")}
          <div class="descrizione-articolo">${escapeHtml(articolo.description)}</div>
        </div>
        <div class="scheda-articolo-azioni">
          <select class="select-stato-articolo"></select>
          <button type="button" class="btn-aggiungi-lavorazione">+ Lavorazione</button>
        </div>
      </div>
      <div class="lista-lavorazioni"></div>
    `;

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

    const listaLavorazioni = scheda.querySelector(".lista-lavorazioni");
    if (dettaglioArticolo.works.length === 0) {
      listaLavorazioni.innerHTML = '<div class="nessuna-lavorazione">Nessuna lavorazione applicata.</div>';
    } else {
      for (const dettaglioLavorazione of dettaglioArticolo.works) {
        listaLavorazioni.appendChild(costruisciRigaLavorazione(dettaglioLavorazione));
      }
    }

    return scheda;
  }

  function costruisciRigaLavorazione(dettaglioLavorazione) {
    const lavorazione = dettaglioLavorazione.work;
    const riga = document.createElement("div");
    riga.className = "riga-lavorazione";

    const prezzoTesto = lavorazione.priceStatus === "DEFINITO" ? formattaEuro(lavorazione.priceCents) : "Prezzo da definire";
    const esecuzioneTesto = lavorazione.executionType === "ESTERNA"
      ? "Esterna" + (dettaglioLavorazione.artisanName ? " — " + escapeHtml(dettaglioLavorazione.artisanName) : "")
      : "Interna";

    riga.innerHTML = `
      <span class="nome-lavorazione">${escapeHtml(lavorazione.appliedName)}</span>
      <span class="esecuzione-lavorazione">${esecuzioneTesto}</span>
      <span class="prezzo-lavorazione">${prezzoTesto}</span>
    `;
    return riga;
  }

  async function cambiaStatoArticolo(articoloId, rowVersion, nuovoStato) {
    nascondiErrore();
    try {
      await window.sciuscia.call("articoli.cambiaStato", { articoloId, rowVersion, nuovoStato });
      await ricaricaDettaglioCommessa();
    } catch (errore) {
      mostraErrore("Cambio stato non riuscito: " + errore.message);
      await ricaricaDettaglioCommessa();
    }
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
      await window.sciuscia.call("articoli.crea", {
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
    const testoEtichetta = document.getElementById("lavorazione-etichetta").value.trim();
    const prezzoTesto = document.getElementById("lavorazione-prezzo").value.trim();
    const fascia = campoFascia.hidden ? null : (selectLavorazioneFascia.value || null);
    const esterna = checkboxEsterna.checked;
    const artigianoId = esterna ? document.getElementById("lavorazione-artigiano").value : null;

    if (!nome || !testoEtichetta) {
      mostraErrore("Nome e testo etichetta della lavorazione sono obbligatori.");
      return;
    }
    if (esterna && !artigianoId) {
      mostraErrore("Selezionare l'artigiano per una lavorazione esterna.");
      return;
    }

    const prezzoCents = prezzoTesto === "" ? null : Math.round(parseFloat(prezzoTesto.replace(",", ".")) * 100);

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
      chiudiModaleLavorazione();
      await ricaricaDettaglioCommessa();
    } catch (errore) {
      mostraErrore("Aggiunta lavorazione non riuscita: " + errore.message);
    }
  }

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

  function mostraSchermataCommesse() {
    document.getElementById("nav-commesse").classList.add("active");
    document.getElementById("nav-anagrafiche").classList.remove("active");
    vistaAnagrafiche.hidden = true;
    mostraVistaElenco();
    caricaCommesse();
  }

  function mostraSchermataAnagrafiche() {
    document.getElementById("nav-anagrafiche").classList.add("active");
    document.getElementById("nav-commesse").classList.remove("active");
    vistaElenco.hidden = true;
    vistaDettaglio.hidden = true;
    vistaAnagrafiche.hidden = false;
    nascondiErrore();
    caricaClientiAnagrafica();
  }

  document.getElementById("nav-commesse").addEventListener("click", mostraSchermataCommesse);
  document.getElementById("nav-anagrafiche").addEventListener("click", mostraSchermataAnagrafiche);

  document.querySelectorAll(".tab-btn").forEach((bottone) => {
    bottone.addEventListener("click", () => {
      document.querySelectorAll(".tab-btn").forEach((b) => b.classList.remove("active"));
      document.querySelectorAll(".tab-pane").forEach((p) => { p.hidden = true; });
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

    if (!nome) {
      mostraErrore("Il nome dell'artigiano è obbligatorio.");
      return;
    }

    nascondiErrore();
    try {
      if (artigianoAnagraficaInModifica) {
        await window.sciuscia.call("anagrafiche.modificaArtigiano", {
          artigianoId: artigianoAnagraficaInModifica.artisanId,
          nome, contatto, telefono, email, indirizzo,
          rowVersion: artigianoAnagraficaInModifica.rowVersion,
        });
      } else {
        await window.sciuscia.call("anagrafiche.creaArtigiano", { nome, contatto, telefono, email, indirizzo });
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
      riga.innerHTML = `
        <td>${escapeHtml(voce.name)}</td>
        <td>${escapeHtml(voce.labelText)}</td>
        <td>${voce.executionDefault === "ESTERNA" ? "Esterna" : "Interna"}</td>
        <td>${escapeHtml(prezzoTesto)}</td>
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
    document.getElementById("campo-listino-fasce").hidden = modalita !== "FASCE";
  }

  async function salvaLavorazioneListino() {
    const nome = document.getElementById("listino-nome").value.trim();
    const testoEtichetta = document.getElementById("listino-etichetta").value.trim();
    const esecuzione = document.getElementById("listino-esecuzione").value;
    const modalitaPrezzo = document.getElementById("listino-modalita-prezzo").value;
    const prezzoTesto = document.getElementById("listino-prezzo-fisso").value.trim();

    if (!nome || !testoEtichetta) {
      mostraErrore("Nome e testo etichetta sono obbligatori.");
      return;
    }

    const prezzoStandardCents = modalitaPrezzo === "FISSO" && prezzoTesto !== ""
      ? Math.round(parseFloat(prezzoTesto.replace(",", ".")) * 100)
      : null;

    nascondiErrore();
    try {
      if (lavorazioneListinoInModifica) {
        await window.sciuscia.call("anagrafiche.modificaLavorazioneListino", {
          lavorazioneId: lavorazioneListinoInModifica.workTypeId,
          nome, testoEtichetta, esecuzione, modalitaPrezzo, prezzoStandardCents,
          costoStandardCents: lavorazioneListinoInModifica.standardCostCents,
          rowVersion: lavorazioneListinoInModifica.rowVersion,
        });
      } else {
        await window.sciuscia.call("anagrafiche.creaLavorazioneListino", {
          nome, testoEtichetta, esecuzione, modalitaPrezzo, prezzoStandardCents,
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

    try {
      const fasce = await window.sciuscia.call("anagrafiche.fasceLavorazione", { workTypeId: voce.workTypeId });
      for (const fascia of (fasce || [])) {
        const campo = document.getElementById("fascia-listino-" + fascia.bandCode.toLowerCase());
        if (campo) {
          campo.value = (fascia.priceCents / 100).toFixed(2);
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
      { codice: "A", valore: document.getElementById("fascia-listino-a").value.trim() },
      { codice: "B", valore: document.getElementById("fascia-listino-b").value.trim() },
      { codice: "C", valore: document.getElementById("fascia-listino-c").value.trim() },
    ].filter((f) => f.valore !== "");

    if (fasce.length === 0) {
      mostraErrore("Inserisci almeno il prezzo di una fascia.");
      return;
    }

    nascondiErrore();
    try {
      for (const fascia of fasce) {
        const prezzoCents = Math.round(parseFloat(fascia.valore.replace(",", ".")) * 100);
        await window.sciuscia.call("anagrafiche.impostaFasciaLavorazione", {
          lavorazioneId: lavorazioneListinoPerFasce.workTypeId,
          fascia: fascia.codice,
          prezzoCents,
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
  document.getElementById("btn-annulla-listino").addEventListener("click", chiudiModaleListino);
  document.getElementById("btn-salva-listino").addEventListener("click", salvaLavorazioneListino);
  document.getElementById("listino-modalita-prezzo").addEventListener("change", aggiornaCampiModalitaPrezzoListino);

  document.getElementById("btn-annulla-fasce-listino").addEventListener("click", chiudiModaleFasceListino);
  document.getElementById("btn-salva-fasce-listino").addEventListener("click", salvaFasceListino);

  caricaCommesse();
})();
