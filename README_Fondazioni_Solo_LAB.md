# Gestionale Sciuscià — Fondazioni Solo LAB

Questa è la prima base tecnica del gestionale approvato. Non è ancora l’interfaccia grafica definitiva: contiene i servizi essenziali sui quali verranno costruite le schermate già collaudate nel prototipo.

## Funzioni già implementate

- creazione automatica del database SQLite al primo avvio;
- controllo di integrità a ogni avvio;
- configurazione iniziale della sede LAB;
- inserimento dell’avvertenza approvata per la copia cliente;
- numerazione progressiva e sicura delle commesse;
- creazione automatica delle cartelle fotografiche;
- importazione e rinomina progressiva delle fotografie;
- impronta SHA-256 per controllare i file fotografici;
- snapshot coerente del database e copia delle fotografie nel backup;
- manifest del backup con data, dimensione e impronta del database;
- registro tecnico degli errori;
- autotest integrato.

## Cartella operativa prevista su Windows

Al primo avvio vengono create automaticamente queste cartelle:

```text
%LOCALAPPDATA%\Sciuscia\GestionaleLAB\
├── Dati\sciuscia-lab.db
├── Fotografie\
├── Backup\
└── Log\errori.jsonl
```

Le fotografie saranno organizzate come:

```text
Fotografie\2026\LAB-126\LAB-126-2\LAB-126-2_01.jpg
```

L’operatore non dovrà aprire o gestire manualmente queste cartelle.

## Compilazione Windows

Il progetto richiede **.NET 10 SDK**. Da PowerShell, nella cartella del progetto:

```powershell
.\build-windows.ps1
```

Lo script esegue prima l’autotest e crea poi un’applicazione Windows autonoma nella cartella `dist\Fondazioni-Solo-LAB-win-x64`.

## Compilazione automatica

Il progetto comprende anche una procedura GitHub Actions in `.github/workflows/build-windows.yml`. Dopo il caricamento in un archivio GitHub privato, la procedura:

1. installa .NET 10 in un computer Windows temporaneo;
2. compila il progetto con gli avvisi trattati come errori;
3. esegue l’autotest Fondazioni;
4. produce l’eseguibile Windows autonomo;
5. rende disponibile il pacchetto ZIP compilato per 30 giorni.

Questa modalità evita di installare strumenti di sviluppo sul PC LAB. Il collegamento a GitHub è necessario soltanto per caricare il progetto e avviare la compilazione automatica.

## Stato del collaudo

Lo schema SQLite e le sue regole sono stati verificati nell'ambiente di sviluppo. Tre compilazioni reali su Windows (GitHub Actions) hanno già superato build e autotest: Fondazioni + Anagrafiche (commit `9686d31`), il blocco Commesse (commit `b7e427f`), e poi anche il blocco Operazioni. Della nuova interfaccia grafica (Sciuscia.Ui), le prime due schermate (elenco/creazione commesse, e dettaglio di una commessa con articoli e lavorazioni) sono state aperte e provate davvero su un PC Windows, in ogni loro funzione, con esito positivo. La terza schermata (Anagrafiche) è appena stata realizzata e non ancora collaudata. Vedere la cronologia nel progetto Claude per lo stato aggiornato prima di consegnare un eseguibile al PC LAB.

## Struttura del codice

Il codice sorgente è diviso in sei progetti:

- **Sciuscia.Data**: le fondamenta condivise (percorsi, apertura del database, opzioni di installazione, numerazione progressiva). Non ha una propria interfaccia: serve a tutti gli altri progetti.
- **Sciuscia.Foundations**: il programma a riga di comando usato finora per gli autotest (fotografie, backup, registro errori, autotest di tutti i blocchi).
- **Sciuscia.Registry**: il blocco **Anagrafiche** — clienti, categorie, marche (con fusione dei duplicati), artigiani e listino lavorazioni.
- **Sciuscia.Orders**: il blocco **Commesse** — ordini, articoli, lavorazioni applicate (con prezzo e testo etichetta "congelati" al momento dell'inserimento) e affidamenti esterni agli artigiani, incluso il blocco automatico imposto dal database quando manca il costo di una lavorazione esterna prima di segnare un articolo come Pronto.
- **Sciuscia.Operations**: il blocco **Operazioni** — pagamenti (corretti solo con nuovi eventi, mai riscritti), ritiri (un articolo non si può ritirare due volte, e il ritiro lo segna automaticamente come Consegnato), messaggi al cliente collegati agli articoli, e stampe (con aggiornamento automatico di quale versione dell'etichetta è stata effettivamente stampata).
- **Sciuscia.Ui**: la **finestra vera e propria** che vedrà l'operatore, con dentro un controllo WebView2 che mostra le schermate (pagine HTML/CSS/JS nella cartella `wwwroot`). Le pagine non parlano mai direttamente con il database: passano sempre da un "ponte" in C# (`Bridge.cs`) che usa gli stessi servizi già collaudati degli altri blocchi. Schermate realizzate finora: elenco delle commesse aperte con apertura di una nuova commessa (ricerca cliente esistente o creazione al volo di uno nuovo); dettaglio di una commessa, con l'elenco dei suoi articoli, l'aggiunta di un nuovo articolo, l'aggiunta di lavorazioni dal listino o scritte a mano (con fasce di prezzo o affidamento a un artigiano esterno) e il cambio dello stato di lavorazione di ciascun articolo; **Anagrafiche**, una schermata unica con cinque schede (Clienti, Categorie, Marche, Artigiani, Listino lavorazioni) per creare, modificare e attivare/disattivare ciascuna di queste voci in un posto solo, invece di doverle creare "al volo" dentro le altre schermate.

Gli autotest di Anagrafiche, Commesse e Operazioni girano automaticamente insieme a quello di Fondazioni quando si lancia `--self-test`. L'interfaccia grafica non ha un autotest a riga di comando (è una finestra): la build automatica verifica solo che compili e si pubblichi correttamente; il collaudo vero e proprio si fa aprendo l'eseguibile su un PC Windows.

## Passo successivo

Aggiungere via via le altre schermate dell'interfaccia grafica (pagamenti/ritiri/messaggi/stampe collegati a una commessa), una alla volta, ciascuna collaudata su Windows prima di passare alla successiva.
