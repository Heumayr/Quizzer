// Ein Mindest-DOM fuer die Layout-Module der Telefonseite.
//
// Bewusst KEIN jsdom: das Projekt hat keine npm-Abhaengigkeiten, und eine dafuer
// aufzunehmen waere eine Entscheidung, keine Testfrage. Nachgesehen wird nur, was die
// Module wirklich benutzen - createElement, appendChild, querySelector(All),
// classList, dataset, addEventListener, innerHTML mit festen Vorlagen.

class ClassList {
    constructor(el) { this.el = el; this.werte = new Set(); }
    add(...n) { n.forEach(x => this.werte.add(x)); }
    remove(...n) { n.forEach(x => this.werte.delete(x)); }
    toggle(n, an) { if (an) this.werte.add(n); else this.werte.delete(n); }
    contains(n) { return this.werte.has(n); }
    get value() { return [...this.werte].join(" "); }
}

class Element {
    constructor(tag) {
        this.tagName = String(tag).toUpperCase();
        this.children = [];
        this.attribute = new Map();
        this.dataset = {};
        this.classList = new ClassList(this);
        this.textContent = "";
        this.disabled = false;
        this.type = "";
        this.hoerer = {};
        this.style = { setProperty() {} };
    }

    set className(wert) {
        this.classList.werte = new Set(String(wert).split(/\s+/).filter(Boolean));
    }

    get className() { return this.classList.value; }

    set innerHTML(html) {
        // Nur die zwei festen Vorlagen der Layouts, kein Parser.
        this.children = [];

        for (const treffer of String(html).matchAll(/<(\w+)[^>]*class="([^"]+)"[^>]*>(?:([^<]*)<\/\1>)?/g)) {
            const kind = new Element(treffer[1]);

            kind.className = treffer[2];
            kind.textContent = (treffer[3] ?? "").trim();

            this.children.push(kind);
        }
    }

    get innerHTML() { return ""; }

    appendChild(kind) { this.children.push(kind); return kind; }
    append(...kinder) { kinder.forEach(k => this.children.push(k)); }
    focus() { }
    setAttribute(name, wert) { this.attribute.set(name, String(wert)); }
    getAttribute(name) { return this.attribute.get(name) ?? null; }
    removeAttribute(name) { this.attribute.delete(name); }
    addEventListener(name, fn) { (this.hoerer[name] ??= []).push(fn); }
    removeEventListener(name, fn) {
        this.hoerer[name] = (this.hoerer[name] ?? []).filter(f => f !== fn);
    }

    click() { (this.hoerer.click ?? []).forEach(fn => fn({})); }

    *alle() {
        for (const kind of this.children) { yield kind; yield* kind.alle(); }
    }

    querySelector(auswahl) { return this.querySelectorAll(auswahl)[0] ?? null; }

    querySelectorAll(auswahl) {
        const klasse = auswahl.replace(/^\./, "");
        return [...this.alle()].filter(e => e.classList.contains(klasse));
    }
}

export function baueDom() {
    // helpers.js liest beim Laden fuenf Elemente ueber getElementById - ohne sie wirft schon
    // der Import. Sie werden nur gehalten, nicht geprueft.
    const nachName = new Map();

    const dokument = {
        createElement: tag => new Element(tag),
        getElementById(id) {
            if (!nachName.has(id)) nachName.set(id, new Element("div"));
            return nachName.get(id);
        },
        cookie: ""
    };

    globalThis.document = dokument;
    globalThis.window = { show_toast() {} };

    return { dokument, wurzel: new Element("div") };
}
