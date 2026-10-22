// Haelt den Bildschirm des Telefons wach (Verbesserung V5, 2026-10-22): sonst geht er mitten in
// der Frage aus, und wer buzzern will, entsperrt erst.
//
// Die Screen Wake Lock API gibt es nur in einem sicheren Kontext (https). Die Buzzer-Seite laeuft
// bewusst ueber http im Heim-WLAN - ein Zertifikat fuer eine LAN-Adresse kann man den Gaesten
// nicht zumuten, und ohne https geht auch kein Kennwort mit (M10). Deshalb, wo Wake Lock fehlt,
// der bekannte Kniff aus NoSleep.js: ein stummes Mini-Video in Schleife. Ein laufendes Video
// haelt den Bildschirm auf Android und iOS wach.
//
// Beides braucht einen ersten Tipp (Abspielen nur nach einer Geste), und beides faellt weg,
// wenn die Seite in den Hintergrund geht - beim Zurueckkommen wird es neu angefordert.

export function wachHalten({ doc = globalThis.document, nav = globalThis.navigator, win = globalThis } = {}) {
    const sicher = !!win.isSecureContext && !!nav && "wakeLock" in nav;
    let gestartet = false;
    let lock = null;
    let video = null;
    let modus = "aus";

    function baueVideo() {
        const v = doc.createElement("video");
        v.setAttribute("playsinline", "");
        v.setAttribute("muted", "");
        v.setAttribute("aria-hidden", "true");
        v.setAttribute("tabindex", "-1");
        v.muted = true;
        v.loop = true;
        v.className = "wach-video";

        for (const [src, type] of [["/media/wach.webm", "video/webm"], ["/media/wach.mp4", "video/mp4"]]) {
            const quelle = doc.createElement("source");
            quelle.setAttribute("src", src);
            quelle.setAttribute("type", type);
            v.appendChild(quelle);
        }

        doc.body.appendChild(v);
        return v;
    }

    async function an() {
        gestartet = true;

        if (sicher) {
            try {
                lock = await nav.wakeLock.request("screen");
                modus = "wakelock";
                return modus;
            } catch {
                // Abgelehnt (Energiesparen, Richtlinie) - dann das Video.
            }
        }

        // play() muss noch in der Geste laufen: vor diesem Aufruf steht im unsicheren Kontext
        // kein await.
        video ??= baueVideo();

        try {
            await video.play();
            modus = "video";
        } catch {
            modus = "aus";
        }

        return modus;
    }

    const ersterTipp = () => {
        doc.removeEventListener("pointerdown", ersterTipp, true);
        doc.removeEventListener("click", ersterTipp, true);
        if (!gestartet) an();
    };

    doc.addEventListener("pointerdown", ersterTipp, true);
    doc.addEventListener("click", ersterTipp, true);

    doc.addEventListener("visibilitychange", () => {
        if (doc.visibilityState === "visible" && gestartet && (lock == null || lock.released || modus === "video"))
            an();
    });

    return {
        an,
        get modus() { return modus; },
    };
}
