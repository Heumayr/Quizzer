// Die Team-Abstimmung am Telefon (M37 T7, F-M37-13): vor dem Schluss nur die eigene Stimme und wie viele des eigenen Teams
// abgestimmt haben, danach die Antwort des eigenen Teams. Der Server schickt nur das eigene Team; hier wird nur Text gesetzt.

export function abstimmungsText(vote) {
    if (!vote) return "";

    const stand = `${vote.voted ?? 0} von ${vote.of ?? 0} haben abgestimmt`;

    if (vote.closed) {
        return vote.result ? `Euer Team: ${vote.result}` : `Abstimmung zu – ${stand}`;
    }

    const eigene = Array.isArray(vote.own) && vote.own.length > 0
        ? `Deine Stimme: ${vote.own.join(" + ")}`
        : "Du hast noch nicht abgestimmt";

    return `${eigene} · ${stand}`;
}

export function zeigeAbstimmung(ziel, vote) {
    if (!ziel) return;

    const text = abstimmungsText(vote);
    ziel.textContent = text;
    ziel.hidden = text === "";
}
