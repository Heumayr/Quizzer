// Eine Uhr zum Vorstellen fuer die Minispiel-Module (M35 B3): setTimeout und die Zeit des
// Telefons laufen nur, wenn der Test sie vorstellt. So laesst sich "nach 2,5 s gruen, nach
// weiteren 321 ms getippt" genau pruefen, ohne zu warten.

export function baueUhr() {
    let jetzt = 0;
    let naechste = 1;
    const offen = new Map();

    return {
        timer: {
            setTimeout(fn, ms) {
                const id = naechste++;
                offen.set(id, { fn, bei: jetzt + ms });
                return id;
            },
            clearTimeout(id) { offen.delete(id); }
        },

        now: () => jetzt,

        // Stellt die Uhr vor und laesst alles laufen, was bis dahin faellig wird - in Reihenfolge.
        vor(ms) {
            const ziel = jetzt + ms;

            for (;;) {
                const faellig = [...offen.entries()]
                    .filter(([, t]) => t.bei <= ziel)
                    .sort((a, b) => a[1].bei - b[1].bei)[0];

                if (!faellig) break;

                offen.delete(faellig[0]);
                jetzt = faellig[1].bei;
                faellig[1].fn();
            }

            jetzt = ziel;
        },

        offen: () => offen.size
    };
}
