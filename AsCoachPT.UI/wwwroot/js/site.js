// Animazioni e cursore personalizzato, pari comportamento dell'app React.
(function () {
    function initCursor() {
        if (!document.getElementById("cursorFollower")) return;

        let inCoda = false;
        let ultimoX = 0;
        let ultimoY = 0;

        // Aggiorna una sola volta per frame con transform (composito su GPU):
        // evita layout ripetuti e rende il cursore fluido. L'elemento viene
        // ricercato a ogni frame perché Blazor può sostituire il nodo dopo l'hydration.
        function aggiorna() {
            const follower = document.getElementById("cursorFollower");
            if (follower) {
                follower.style.transform = "translate3d(" + ultimoX + "px," + ultimoY + "px,0) translate(-50%, -50%)";
            }
            inCoda = false;
        }

        window.addEventListener("mousemove", (e) => {
            ultimoX = e.clientX;
            ultimoY = e.clientY;
            if (!inCoda) {
                inCoda = true;
                requestAnimationFrame(aggiorna);
            }
        }, { passive: true });
    }

    // Comparsa progressiva degli elementi mentre si scorre. Blazor sostituisce
    // il DOM a ogni navigazione: oltre all'osservazione iniziale serve un
    // MutationObserver, altrimenti tornando sulla home le sezioni restano a
    // opacity 0. Se manca IntersectionObserver il documento torna visibile.
    const selettoreReveal = ".reveal, .card-wrapper, .service-row, .info-panel, .form-card, .section-head";

    let osservatoreReveal = null;
    let osservazioneProgrammata = false;

    function osservaReveal() {
        osservazioneProgrammata = false;

        if (!osservatoreReveal) {
            return;
        }

        // Riparte da zero: elimina i nodi rimossi dalla pagina e osserva i nuovi.
        osservatoreReveal.disconnect();

        document.querySelectorAll(selettoreReveal).forEach((el) => {
            if (el.dataset.revealed !== "true") {
                osservatoreReveal.observe(el);
            }
        });
    }

    function programmaOsservazione() {
        if (osservazioneProgrammata) {
            return;
        }

        osservazioneProgrammata = true;
        requestAnimationFrame(osservaReveal);
    }

    function initReveal() {
        if (typeof IntersectionObserver === "undefined") {
            document.documentElement.classList.remove("js-reveal");
            return;
        }

        if (osservatoreReveal) {
            programmaOsservazione();
            return;
        }

        window.__asCoachRevealAttivo = true;

        osservatoreReveal = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (entry.isIntersecting) {
                    entry.target.dataset.revealed = "true";
                    osservatoreReveal.unobserve(entry.target);
                }
            });
        }, { threshold: 0.12, rootMargin: "0px 0px -6% 0px" });

        osservaReveal();

        // Blazor aggiunge o sostituisce nodi durante le navigazioni interne.
        new MutationObserver(programmaOsservazione).observe(document.body, {
            childList: true,
            subtree: true
        });

        // Dopo l'idratazione alcune parti possono essere riscritte da Blazor.
        setTimeout(programmaOsservazione, 400);
        setTimeout(programmaOsservazione, 1400);
    }

    function boot() {
        initCursor();
        initReveal();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }

    // Navigazione migliorata di Blazor: ricalcola subito gli elementi da osservare.
    document.addEventListener("blazor:enhancedload", programmaOsservazione);
})();

// Scroll fluido verso una sezione. Usa un offset solo se la navbar è
// effettivamente sovrapposta al contenuto (fixed/sticky): in home è assoluta
// sull'hero e scorre via, quindi l'offset deve essere 0 per non tagliare
// la parte bassa della sezione di destinazione.
window.asCoachScrollTo = function (id) {
    const sezione = document.getElementById(id);
    if (!sezione) return;

    const navbar = document.querySelector(".navbar");
    let offset = 0;
    if (navbar) {
        const posizione = window.getComputedStyle(navbar).position;
        if (posizione === "fixed" || posizione === "sticky") {
            offset = navbar.getBoundingClientRect().height;
        }
    }

    const rettangolo = sezione.getBoundingClientRect();
    const spazio = window.innerHeight - offset;
    const carte = [...sezione.querySelectorAll(".service-row")];

    // Contenuto utile: dall'inizio della sezione all'ultima riga servizio.
    const basso = carte.length
        ? Math.max(...carte.map((carta) => carta.getBoundingClientRect().bottom))
        : rettangolo.bottom;
    const altezza = Math.min(rettangolo.height, basso - rettangolo.top);

    let top;
    if (altezza <= spazio) {
        // Tutto visibile: centra titolo e righe nello schermo.
        top = rettangolo.top + window.scrollY - offset - (spazio - altezza) / 2;
    } else {
        // Sezione più alta dello schermo: parti dal titolo, così le righe
        // non vengono tagliate fuori dall'area visibile.
        top = rettangolo.top + window.scrollY - offset;
    }

    window.scrollTo({ top: Math.max(0, top), behavior: "smooth" });
};

// Dialoghi della sezione servizi: blocca lo scroll della pagina mentre sono aperti
// e alla chiusura riporta il focus sull'elemento che li ha aperti.
window.asCoachModal = function (apri) {
    const radice = document.documentElement;

    if (apri) {
        window.__asCoachFuocoPrecedente = document.activeElement;
        radice.style.overflow = "hidden";
        return;
    }

    radice.style.overflow = "";

    const precedente = window.__asCoachFuocoPrecedente;
    window.__asCoachFuocoPrecedente = null;
    if (precedente && typeof precedente.focus === "function") {
        try {
            precedente.focus({ preventScroll: true });
        } catch {
            precedente.focus();
        }
    }
};

// Copia un testo negli appunti; restituisce false se il browser non lo consente.
window.asCoachCopiaTesto = async function (testo) {
    try {
        if (navigator.clipboard && window.isSecureContext) {
            await navigator.clipboard.writeText(testo);
            return true;
        }

        const area = document.createElement("textarea");
        area.value = testo;
        area.setAttribute("readonly", "");
        area.style.position = "fixed";
        area.style.opacity = "0";
        document.body.appendChild(area);
        area.select();
        const copiato = document.execCommand("copy");
        document.body.removeChild(area);
        return copiato;
    } catch {
        return false;
    }
};

// Usato dai componenti Blazor per distinguere telefono e desktop senza ricorrere a eval.
window.asCoachIsMobile = function () {
    return window.matchMedia("(max-width: 767.98px)").matches;
};

// ---------------------------------------------------------------------------
// Ricerca indirizzi: suggerimenti Google sotto l'input, dettagli del luogo
// scelto e selezione diretta dalla mappa. Componenti e coordinate arrivano
// sempre dallo stesso risultato Google.
// ---------------------------------------------------------------------------
(function () {
    const mappeSelettore = new Map();

    function componente(componenti, tipo, breve) {
        const trovato = (componenti || []).find((c) => c.types.includes(tipo));
        return trovato ? (breve ? trovato.short_name : trovato.long_name) : "";
    }

    function risultatoDa(luogo) {
        const componenti = luogo.address_components || [];
        return {
            formattedAddress: luogo.formatted_address || "",
            via: componente(componenti, "route", false),
            numeroCivico: componente(componenti, "street_number", false),
            citta: componente(componenti, "locality", false)
                || componente(componenti, "postal_town", false)
                || componente(componenti, "administrative_area_level_3", false),
            provincia: componente(componenti, "administrative_area_level_2", true),
            lat: luogo.geometry.location.lat(),
            lng: luogo.geometry.location.lng()
        };
    }

    // Il reverse geocoding restituisce anche fermate e attività senza civico:
    // preferisce l'indirizzo stradale più preciso, non il primo risultato.
    function scegliPreciso(risultati) {
        function punteggio(luogo) {
            const tipi = luogo.types || [];
            const componenti = luogo.address_components || [];
            const ha = (tipo) => componenti.some((c) => c.types.includes(tipo));

            if (tipi.includes("street_address")) return 0;
            if (tipi.includes("premise")) return 1;
            if (ha("route") && ha("street_number")) return 2;
            if (ha("route")) return 3;
            if (tipi.includes("establishment") || tipi.includes("point_of_interest")) return 4;
            return 5;
        }

        return risultati.reduce(
            (migliore, candidato) => punteggio(candidato) < punteggio(migliore) ? candidato : migliore,
            risultati[0]);
    }

    // Restituisce fino a sei suggerimenti: la scelta resta all'utente.
    window.asCoachPlaceSuggestions = function (query) {
        return new Promise((resolve) => {
            let tentativi = 0;

            function cerca() {
                if (!(window.google && google.maps && google.maps.places && google.maps.places.AutocompleteService)) {
                    if (tentativi++ < 50) {
                        setTimeout(cerca, 100);
                    } else {
                        resolve([]);
                    }
                    return;
                }

                const servizio = new google.maps.places.AutocompleteService();
                servizio.getPlacePredictions(
                    {
                        input: query,
                        componentRestrictions: { country: "it" },
                        language: "it"
                    },
                    (previsioni, stato) => {
                        if (stato !== "OK" || !previsioni) {
                            resolve([]);
                            return;
                        }

                        resolve(previsioni.slice(0, 6).map((p) => ({
                            placeId: p.place_id,
                            primaryText: p.structured_formatting?.main_text || p.description || "",
                            secondaryText: p.structured_formatting?.secondary_text || ""
                        })));
                    }
                );
            }

            cerca();
        });
    };

    window.asCoachPlaceDetails = function (placeId) {
        return new Promise((resolve) => {
            if (!(window.google && google.maps && google.maps.places && google.maps.places.PlacesService)) {
                resolve(null);
                return;
            }

            const servizio = new google.maps.places.PlacesService(document.createElement("div"));
            servizio.getDetails(
                {
                    placeId: placeId,
                    fields: ["formatted_address", "geometry", "address_components"],
                    language: "it"
                },
                (luogo, stato) => {
                    resolve(stato === "OK" && luogo ? risultatoDa(luogo) : null);
                }
            );
        });
    };

    // Usato solo per il clic sulla mappa: qui il reverse geocoding è corretto
    // perché il punto scelto è la fonte della posizione.
    window.asCoachReverseGeocode = function (lat, lng) {
        return new Promise((resolve) => {
            if (!(window.google && google.maps && google.maps.Geocoder)) {
                resolve(null);
                return;
            }

            new google.maps.Geocoder().geocode(
                { location: { lat: lat, lng: lng }, region: "it" },
                (risultati, stato) => {
                    if (stato !== "OK" || !risultati || risultati.length === 0) {
                        resolve(null);
                        return;
                    }

                    resolve(risultatoDa(scegliPreciso(risultati)));
                }
            );
        });
    };

    window.asCoachInitMapPicker = function (elementId, dotnetRef, lat, lng) {
        return new Promise((resolve) => {
            let tentativi = 0;

            function avvia() {
                if (!(window.google && google.maps && google.maps.Map)) {
                    if (tentativi++ < 50) {
                        setTimeout(avvia, 100);
                    } else {
                        resolve(false);
                    }
                    return;
                }

                const elemento = document.getElementById(elementId);
                if (!elemento) {
                    if (tentativi++ < 50) {
                        setTimeout(avvia, 100);
                    } else {
                        resolve(false);
                    }
                    return;
                }

                window.asCoachDisposeMapPicker(elementId);

                const coordinateValide = typeof lat === "number" && typeof lng === "number";
                const centro = coordinateValide ? { lat: lat, lng: lng } : { lat: 42.5636, lng: 12.6427 };

                const mappa = new google.maps.Map(elemento, {
                    center: centro,
                    zoom: coordinateValide ? 16 : 11,
                    mapTypeControl: false,
                    streetViewControl: false,
                    fullscreenControl: false,
                    clickableIcons: false
                });

                const indicatore = new google.maps.Marker({
                    map: mappa,
                    position: centro,
                    visible: coordinateValide
                });

                mappa.addListener("click", (evento) => {
                    const posizione = evento.latLng;
                    indicatore.setPosition(posizione);
                    indicatore.setVisible(true);

                    window.asCoachReverseGeocode(posizione.lat(), posizione.lng()).then((risultato) => {
                        if (risultato && dotnetRef) {
                            dotnetRef.invokeMethodAsync("AggiornaDaMappa", risultato);
                        }
                    });
                });

                mappeSelettore.set(elementId, { mappa, indicatore });
                resolve(true);
            }

            avvia();
        });
    };

    window.asCoachSetMapPickerPosition = function (elementId, lat, lng) {
        const selettore = mappeSelettore.get(elementId);
        if (!selettore || typeof lat !== "number" || typeof lng !== "number") {
            return;
        }

        const posizione = { lat: lat, lng: lng };
        selettore.mappa.setCenter(posizione);
        selettore.mappa.setZoom(16);
        selettore.indicatore.setPosition(posizione);
        selettore.indicatore.setVisible(true);
    };

    window.asCoachDisposeMapPicker = function (elementId) {
        const selettore = mappeSelettore.get(elementId);
        if (!selettore) {
            return;
        }

        google.maps.event.clearInstanceListeners(selettore.mappa);
        selettore.indicatore.setMap(null);
        mappeSelettore.delete(elementId);
    };
})();

// ---------------------------------------------------------------------------
// Scadenza assoluta della sessione: 30 minuti dal login, indipendentemente
// dalle interazioni. Il cookie di sessione sparisce alla chiusura del browser;
// il riavvio dell'applicazione invalida comunque il cookie lato server.
// ---------------------------------------------------------------------------
(function () {
    var DURATA_SESSIONE = 30 * 60 * 1000;
    var timer = null;
    var inizioFallback = null;

    function utenteAutenticato() {
        return !!document.querySelector('form[action="/auth/logout"]');
    }

    function inizioSessione() {
        var corrispondenza = document.cookie.match(/(?:^|;\s*)ascoach\.session_start=(\d+)/);
        return corrispondenza ? Number(corrispondenza[1]) * 1000 : null;
    }

    function disconnetti() {
        if (!utenteAutenticato()) {
            return;
        }

        fetch("/auth/logout", {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/x-www-form-urlencoded" }
        })
            .catch(function () { /* anche se la chiamata fallisce, torniamo al login */ })
            .then(function () {
                window.location.replace("/login?timeout=1");
            });
    }

    function pianifica() {
        window.clearTimeout(timer);

        if (!utenteAutenticato()) {
            return;
        }

        var inizio = inizioSessione();
        if (!inizio) {
            if (!inizioFallback) {
                inizioFallback = Date.now();
            }
            inizio = inizioFallback;
        }

        var resta = DURATA_SESSIONE - (Date.now() - inizio);
        if (resta <= 0) {
            disconnetti();
            return;
        }

        timer = window.setTimeout(pianifica, resta);
    }

    // Ricalcola dopo una navigazione Blazor o quando la scheda torna visibile.
    document.addEventListener("blazor:enhancedload", pianifica);
    document.addEventListener("visibilitychange", function () {
        if (document.visibilityState === "visible") {
            pianifica();
        }
    });

    pianifica();
})();
