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

    function initReveal() {
        if (typeof IntersectionObserver === "undefined") return;

        const targets = document.querySelectorAll(".card-wrapper, .fade-image, .chiSono, .fade-slide");
        const observer = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (entry.isIntersecting) {
                    entry.target.classList.add("show");
                    entry.target.classList.add("visible");
                }
            });
        }, { threshold: 0.2 });

        targets.forEach((el) => observer.observe(el));
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

    // Blazor sostituisce il DOM dopo l'interattività: ripeti l'osservazione.
    document.addEventListener("blazor:enhancedload", initReveal);
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

    const top = sezione.getBoundingClientRect().top + window.scrollY - offset;
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

// Usato dai componenti Blazor per distinguere telefono e desktop senza ricorrere a eval.
window.asCoachIsMobile = function () {
    return window.matchMedia("(max-width: 767.98px)").matches;
};

// Ricerca luogo tramite Google Places + Geocoder (parità con GestioneIndirizzi React).
window.asCoachSearchPlace = function (query) {
    return new Promise((resolve) => {
        let tentativi = 0;

        function cerca() {
            if (!(window.google && google.maps && google.maps.places)) {
                if (tentativi++ < 50) {
                    setTimeout(cerca, 100);
                } else {
                    resolve(null);
                }
                return;
            }

            const service = new google.maps.places.PlacesService(document.createElement("div"));
            service.findPlaceFromQuery(
                { query: query, fields: ["formatted_address", "geometry", "place_id"] },
                (results, status) => {
                    if (status !== "OK" || !results || results.length === 0) {
                        resolve(null);
                        return;
                    }

                    const risultato = results[0];
                    const lat = risultato.geometry.location.lat();
                    const lng = risultato.geometry.location.lng();
                    const geocoder = new google.maps.Geocoder();

                    geocoder.geocode({ location: { lat: lat, lng: lng } }, (res, geoStatus) => {
                        if (geoStatus !== "OK" || !res || res.length === 0) {
                            resolve({ formattedAddress: risultato.formatted_address, lat: lat, lng: lng });
                            return;
                        }

                        const componenti = res[0].address_components || [];
                        const get = (tipo, breve) =>
                            (componenti.find((c) => c.types.includes(tipo)) || {})[breve ? "short_name" : "long_name"] || "";

                        resolve({
                            formattedAddress: risultato.formatted_address,
                            via: get("route", false),
                            numeroCivico: get("street_number", false),
                            citta: get("locality", false) || get("postal_town", false) || get("administrative_area_level_3", false),
                            provincia: get("administrative_area_level_2", true),
                            lat: lat,
                            lng: lng
                        });
                    });
                }
            );
        }

        cerca();
    });
};
