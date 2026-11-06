// Animazioni e cursore personalizzato, pari comportamento dell'app React.
(function () {
    function initCursor() {
        const follower = document.getElementById("cursorFollower");
        if (!follower) return;
        window.addEventListener("mousemove", (e) => {
            follower.style.left = e.clientX + "px";
            follower.style.top = e.clientY + "px";
        });
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
