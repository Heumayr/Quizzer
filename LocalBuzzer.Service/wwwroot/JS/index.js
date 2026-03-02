const nameEl = document.getElementById('name');
const statusEl = document.getElementById('status');
const btn = document.getElementById('buzz');
const errorEl = document.getElementById('error_display');
let locked = false;

// ---- cookie helpers ----
const COOKIE_NAME = "playerId";
const COOKIE_DAYS = 365;

function setCookie(name, value, days) {
    const expires = new Date(Date.now() + days * 864e5).toUTCString();
    document.cookie = `${encodeURIComponent(name)}=${encodeURIComponent(value)}; Expires=${expires}; Path=/; SameSite=Lax`;
}

function getCookie(name) {
    const key = encodeURIComponent(name) + "=";
    return document.cookie
        .split(";")
        .map(s => s.trim())
        .find(s => s.startsWith(key))
        ?.slice(key.length) ?? null;
}

function isGuid(s) {
    // simple GUID v4-ish / general GUID format check
    return typeof s === "string" &&
        /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(s.trim());
}

// ---- resolve id: URL -> cookie -> prompt ----
function resolvePlayerId() {
    const params = new URLSearchParams(location.search);
    const urlIdRaw = params.get("id");
    const urlId = urlIdRaw?.trim() ?? null;

    if (urlId && isGuid(urlId)) {
        setCookie(COOKIE_NAME, urlId, COOKIE_DAYS);
        return urlId;
    }

    const cookieIdRaw = getCookie(COOKIE_NAME);
    const cookieId = cookieIdRaw ? decodeURIComponent(cookieIdRaw).trim() : null;

    if (cookieId && isGuid(cookieId)) {
        return cookieId;
    }

    // blocking popup: user must provide a GUID or we won't connect
    const input = prompt("Player-ID (GUID) fehlt. Bitte einfügen:", "");
    const typed = input?.trim() ?? "";

    if (typed && isGuid(typed)) {
        setCookie(COOKIE_NAME, typed, COOKIE_DAYS);
        return typed;
    }

    return null;
}

// ---- error display helpers ----
function showError(msg) {
    errorEl.textContent = msg ?? "";
    errorEl.classList.toggle("show", !!msg);
}

function getErrMsg(e) {
    const m = e?.message ?? String(e ?? "");
    // make it nicer: keep only HubException text if present
    const match = m.match(/HubException:\s*(.*)$/);
    return (match ? match[1] : m).trim();
}

// ---- toast integration (expects show_toast from earlier snippet) ----
function toast(type, msg) {
    if (typeof window.show_toast === "function") {
        window.show_toast(type, msg);
    }
}

async function start() {
    btn.disabled = true;
    statusEl.textContent = "Verbinde…";

    const id = resolvePlayerId();
    if (!id) {
        statusEl.textContent = "Nicht verbunden: keine gültige Player-ID.";
        nameEl.textContent = "—";
        const m = "Nicht verbunden: keine gültige Player-ID.";
        showError(m);
        toast("error", m);
        return; // do NOT connect
    }

    const conn = new signalR.HubConnectionBuilder()
        .withUrl(`/hub?playerid=${encodeURIComponent(id)}`)
        .withAutomaticReconnect()
        .build();

    conn.on("Assigned", (name, round, isLocked, winner) => {
        showError(""); // clear any previous errors
        nameEl.textContent = name;
        locked = isLocked;
        statusEl.textContent = winner ? `Runde ${round}: ${winner}` : `Runde ${round}: bereit`;
        btn.disabled = locked;

        // optional small toast on (re)assign
        toast("info", `${name} verbunden`);
    });

    conn.on("Winner", (winner, round) => {
        showError("");
        locked = true;
        statusEl.textContent = `Runde ${round}: Gewinner: ${winner}`;
        btn.disabled = true;
        toast("warning", `Runde ${round}: Gewinner: ${winner}`);
    });

    conn.on("Reset", (round) => {
        showError("");
        locked = false;
        statusEl.textContent = `Runde ${round}: bereit`;
        btn.disabled = false;
        toast("info", `Runde ${round}: bereit`);
    });

    conn.onreconnecting(() => {
        const m = "Verbindung verloren… reconnecting";
        statusEl.textContent = m;
        btn.disabled = true;
        toast("warning", m);
    });

    conn.onreconnected(() => {
        const m = "Wieder verbunden";
        statusEl.textContent = m;
        toast("info", m);
        // button state will be corrected by next Assigned/Reset/Winner
    });

    conn.on("Error", (errorMessage) => {
        showError(errorMessage);
        toast("error", errorMessage);
    });

    conn.onclose((closeErr) => {
        const m = closeErr ? getErrMsg(closeErr) : "Verbindung geschlossen.";
        showError(m);
        statusEl.textContent = "Nicht verbunden: Verbindung geschlossen.";
        btn.disabled = true;
        toast("error", m);
    });

    btn.addEventListener('click', async () => {
        try {
            await conn.invoke("Buzz");
        } catch (e) {
            console.error("Buzz failed", e);
            statusEl.textContent = "Buzz failed";
            const m = getErrMsg(e);
            showError(m);
            toast("error", m);
        }
    });

    try {
        await conn.start();
        showError("");                 // clear on success
        statusEl.textContent = "Bereit";
        btn.disabled = locked;
        toast("debug", "Bereit");
    } catch (e) {
        console.error("Connect failed", e);
        const m = getErrMsg(e);
        showError(m);                  // <-- THIS is what you want
        statusEl.textContent = "Nicht verbunden: Verbindung fehlgeschlagen.";
        btn.disabled = true;
        toast("error", m);
    }
}

start();