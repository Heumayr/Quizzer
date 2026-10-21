import { COOKIE_DAYS, COOKIE_NAME } from "./constants.js";

export const dom = {
    name: document.getElementById("name"),
    connection: document.getElementById("connection"),
    status: document.getElementById("status"),
    error: document.getElementById("error_display"),
    host: document.getElementById("layout_host")
};

// Wartezeiten fuer die ersten Wiederverbindungsversuche; danach gilt RETRY_DELAY_STEADY_MS ohne Ende.
const RETRY_DELAYS_MS = [0, 1000, 2000, 5000];
const RETRY_DELAY_STEADY_MS = 10000;

const CONNECTION_LABELS = Object.freeze({
    connected: "● verbunden",
    reconnecting: "○ verbinde neu …",
    disconnected: "○ getrennt"
});

export function setCookie(name, value, days = COOKIE_DAYS) {
    const expires = new Date(Date.now() + days * 864e5).toUTCString();
    document.cookie = `${encodeURIComponent(name)}=${encodeURIComponent(value)}; Expires=${expires}; Path=/; SameSite=Lax`;
}

export function getCookie(name) {
    const key = encodeURIComponent(name) + "=";
    return document.cookie
        .split(";")
        .map(s => s.trim())
        .find(s => s.startsWith(key))
        ?.slice(key.length) ?? null;
}

export function isGuid(s) {
    return typeof s === "string" &&
        /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(s.trim());
}

// Der Zugangsschluessel: 22 Zeichen Base64url (BuzzerAccessKeys). Eine Spieler-Id (GUID)
// ist ausdruecklich KEIN Schluessel mehr - sie stand frueher in QR-Codes und Cookies.
export function isAccessKey(s) {
    return typeof s === "string" && !isGuid(s) && /^[A-Za-z0-9_-]{16,64}$/.test(s.trim());
}

export function resolveAccessKey() {
    const params = new URLSearchParams(location.search);
    const urlKey = params.get("key")?.trim() ?? null;

    if (urlKey && isAccessKey(urlKey)) {
        setCookie(COOKIE_NAME, urlKey, COOKIE_DAYS);
        return urlKey;
    }

    const cookieRaw = getCookie(COOKIE_NAME);
    const cookieKey = cookieRaw ? decodeURIComponent(cookieRaw).trim() : null;

    if (cookieKey && isAccessKey(cookieKey)) {
        return cookieKey;
    }

    // M10: ohne Kennung meldet man sich mit Namen an (join.js) - statt eine GUID einzutippen.
    return null;
}

export function clearPlayerCookie() {
    document.cookie = `${encodeURIComponent(COOKIE_NAME)}=; Expires=Thu, 01 Jan 1970 00:00:00 GMT; Path=/; SameSite=Lax`;
}

export function showError(msg) {
    dom.error.textContent = msg ?? "";
    dom.error.classList.toggle("show", !!msg);
}

export function getErrMsg(e) {
    const m = e?.message ?? String(e ?? "");
    const match = m.match(/HubException:\s*(.*)$/);
    return (match ? match[1] : m).trim();
}

export function toast(type, msg, options) {
    if (typeof window.show_toast === "function") {
        window.show_toast(type, msg, options);
    }
}

// Rueckfallregel fuer withAutomaticReconnect: liefert nie null, versucht es also ohne Ende weiter.
export function nextRetryDelay(retryContext) {
    const count = retryContext?.previousRetryCount ?? 0;
    return count < RETRY_DELAYS_MS.length ? RETRY_DELAYS_MS[count] : RETRY_DELAY_STEADY_MS;
}

// Verbindungsanzeige in der Kopfzeile: Punkt und Wort, nicht nur Farbe.
export function setConnectionIndicator(kind) {
    const key = CONNECTION_LABELS[kind] ? kind : "disconnected";
    if (!dom.connection) return;
    dom.connection.textContent = CONNECTION_LABELS[key];
    dom.connection.dataset.state = key;
}
