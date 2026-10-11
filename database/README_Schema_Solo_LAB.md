# Gestionale Sciuscià — Schema database Solo LAB

Versione 1.0 — 12 settembre 2026

## Risultato

Il file `001_schema_solo_lab.sql` costituisce la prima struttura fisica del database SQLite del gestionale Solo LAB. Traduce il modello dati approvato in tabelle, collegamenti, controlli, viste e regole automatiche verificabili.

## Scelte tecniche definitive

- Gli identificativi tecnici sono UUID e non vengono mostrati all’operatore.
- I codici visibili, per esempio `LAB-126` e `LAB-126/2`, restano separati dagli UUID.
- Gli importi sono memorizzati in centesimi, evitando errori di arrotondamento.
- Le date sono memorizzate in formato ISO; l’interfaccia le mostrerà nel formato italiano.
- Prezzi, costi, nomi e testi etichetta sono congelati nella lavorazione della commessa.
- Le fotografie restano file esterni; il database conserva percorso relativo, impronta e metadati.
- Pagamenti, ritiri, messaggi, stampe e cronologia sono registrazioni non riscrivibili.
- Annullamenti e correzioni mantengono lo storico.
- Il modello include da subito UUID, `row_version` e sede, preparando REM senza attivarla.

## Gruppi di dati

| Gruppo | Tabelle principali | Funzione |
|---|---|---|
| Configurazione | `sites`, `installation_settings`, `printer_profiles`, `number_sequences` | Sede LAB, intestazione, avvertenza ricevuta, percorsi, stampanti e numerazioni |
| Anagrafiche | `customers`, `categories`, `brands`, `artisans`, `work_types` | Clienti, categorie, marche, artigiani e Listino |
| Commessa | `orders`, `items`, `item_works` | Commesse, singoli articoli e lavorazioni applicate |
| Esterni | `external_assignments` | Affidamento e rientro dagli artigiani |
| Fotografie | `photos` | Collegamento sicuro tra articolo e file fotografici |
| Economia | `payment_events`, `v_order_financials` | Pagamenti, correzioni, totale e residuo |
| Consegna | `pickups`, `pickup_items` | Ritiri completi o parziali |
| Comunicazioni | `messages`, `message_items` | WhatsApp, preventivi, solleciti e altri avvisi |
| Stampe | `print_events` | Ricevute, fotografie ed etichette con revisione |
| Controllo | `history_events`, `backup_runs`, `legacy_maps` | Cronologia, backup e migrazione Access |

## Controlli già affidati al database

1. Il cellulare normalizzato non può appartenere a due clienti diversi.
2. Il progressivo commessa è univoco per sede e anno.
3. Il sottocodice articolo è univoco dentro la commessa.
4. Può esistere un solo affidamento esterno attivo per articolo.
5. Una lavorazione esterna può collegarsi soltanto a un affidamento dello stesso articolo.
6. Un articolo non può diventare pronto se manca il costo di una lavorazione esterna approvata.
7. Una modifica a consegna o lavorazioni incrementa la revisione dell’etichetta.
8. Una lavorazione si annulla ma non si elimina.
9. Un articolo può essere consegnato una sola volta.
10. Pagamenti e registrazioni storiche non possono essere modificati o cancellati.

## Totali e prezzi da definire

La vista `v_order_financials` calcola:

- totale delle lavorazioni approvate, valorizzate e non annullate;
- pagato come somma degli eventi economici con segno;
- residuo come totale meno pagato;
- numero delle lavorazioni ancora senza prezzo.

Una lavorazione con prezzo da definire resta visibile ma non entra nel totale provvisorio.

## Etichette

Ogni articolo conserva:

- `label_revision`, incrementata dopo variazioni rilevanti;
- `last_printed_label_revision`, aggiornata dopo una stampa riuscita.

La vista `v_items_to_reprint_label` individua immediatamente le etichette mai stampate o da ristampare. Il contenuto stampato comprenderà codice articolo, data di consegna e lavorazioni, senza dati personali o economici.

## Fotografie

La tabella `photos` memorizza soltanto metadati e percorsi relativi. La struttura prevista è:

```text
Fotografie/Anno/Commessa/Articolo/file.jpg
```

Il programma creerà le cartelle e rinominerà i file automaticamente. L’eliminazione sposterà il file in un cestino controllato e registrerà il nuovo percorso.

## Avvertenza ricevuta iniziale

Alla prima configurazione verrà inserito questo testo approvato:

> La presente ricevuta deve essere esibita al momento del ritiro. Il bene dovrà essere ritirato dal cliente presso il nostro negozio entro sei mesi dalla comunicazione di avvenuta ultimazione della lavorazione. Decorso tale termine, non potrà essere garantita la conservazione del bene nelle medesime condizioni.

Il testo resterà modificabile nelle Impostazioni; ogni ricevuta conserverà la versione effettivamente stampata.

## Prossimo blocco

Il passo successivo è il prototipo tecnico **Fondazioni**:

1. creazione del database al primo avvio;
2. configurazione iniziale della sede LAB;
3. gestione sicura della numerazione;
4. cartelle fotografie e backup;
5. registro degli errori;
6. prova di chiusura anomala e riapertura.

La migrazione verrà sviluppata solo dopo aver ricevuto una copia chiusa del database Access e averne censito tabelle, campi e collegamenti.
