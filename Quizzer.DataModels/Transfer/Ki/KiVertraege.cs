using Quizzer.DataModels.Enumerations;

namespace Quizzer.DataModels.Transfer.Ki
{
    /// <summary>Welche KI der Server anspricht (M31, 2026-10-23).</summary>
    public enum KiAnbieter
    {
        /// <summary>Keine KI eingerichtet.</summary>
        Aus = 0,

        /// <summary>Anthropic Claude über die Messages-API.</summary>
        Claude = 1,

        /// <summary>Jede OpenAI-kompatible Schnittstelle: OpenAI, Ollama, LM Studio, vLLM.</summary>
        OpenAiKompatibel = 2,
    }

    /// <summary>
    /// Die KI-Einstellungen, wie der Admin sie sieht und setzt.
    /// <para>
    /// <b>Der Schlüssel geht nur hinein, nie heraus.</b> Der Server meldet nur
    /// <see cref="SchluesselGesetzt"/>; <see cref="NeuerSchluessel"/> ist beim Lesen immer leer.
    /// </para>
    /// </summary>
    public sealed class KiEinstellungenDto
    {
        public KiAnbieter Anbieter { get; set; }

        /// <summary>Basisadresse - bei Claude leer (Vorgabe), bei OpenAI-kompatibel etwa <c>http://ollama:11434/v1</c>.</summary>
        public string? Adresse { get; set; }

        public string Modell { get; set; } = string.Empty;

        public bool SchluesselGesetzt { get; set; }

        /// <summary><c>null</c>: unverändert. Leer: löschen. Sonst: neuer Schlüssel.</summary>
        public string? NeuerSchluessel { get; set; }

        /// <summary>Entwürfe je Konto und Tag (Aufrufe, nicht Fragen).</summary>
        public int Tageslimit { get; set; } = 20;

        /// <summary>Ob der Server Links abrufen darf (mit Schutz gegen Heimnetz-Adressen).</summary>
        public bool LinksAbrufen { get; set; } = true;

        /// <summary>Die Konten, die Entwürfe anfordern dürfen. Der Admin darf immer.</summary>
        public List<Guid> FreigegebeneKonten { get; set; } = new();
    }

    /// <summary>Was ein Konto über die KI wissen muss, bevor es den Dialog öffnet.</summary>
    public sealed record KiStatusDto(bool Verfuegbar, string Grund, int VerbleibendHeute, bool LinksAbrufen, string Beschreibung);

    /// <summary>Ein mitgeschicktes Bild: als Vorlage für die KI und, wenn gewünscht, als Medium der Frage.</summary>
    public sealed class KiBild
    {
        public string Name { get; set; } = string.Empty;

        /// <summary><c>image/png</c>, <c>image/jpeg</c>, <c>image/gif</c> oder <c>image/webp</c>.</summary>
        public string MediaType { get; set; } = string.Empty;

        public string Base64 { get; set; } = string.Empty;
    }

    /// <summary>Die Bitte um einen Entwurf.</summary>
    public sealed class KiEntwurfAnfrage
    {
        /// <summary>Das Thema in eigenen Worten.</summary>
        public string Thema { get; set; } = string.Empty;

        /// <summary><c>null</c>: die KI wählt den passenden Typ.</summary>
        public QuestionType? Typ { get; set; }

        public int Anzahl { get; set; } = 1;

        public Difficulty? Schwierigkeit { get; set; }

        /// <summary>Öffentliche Seiten, die der Server abruft (wenn erlaubt).</summary>
        public List<string> Links { get; set; } = new();

        /// <summary>Eingefügter Text: Notizen, ein Artikel, eine Liste.</summary>
        public string? Text { get; set; }

        public List<KiBild> Bilder { get; set; } = new();

        /// <summary>Weitere Wünsche („für Kinder", „nicht zu leicht").</summary>
        public string? Wuensche { get; set; }

        public const int MaxAnzahl = 5;
        public const int MaxLinks = 5;
        public const int MaxBilder = 4;
        public const int MaxTextZeichen = 40_000;
        public const int MaxBildBytes = 5 * 1024 * 1024;
    }

    /// <summary>Eine Zeile eines Entwurfs - was sie bedeutet, sagt der Fragetyp (siehe <see cref="KiFrageEntwurf.Zeilen"/>).</summary>
    public sealed class KiZeile
    {
        public string Text { get; set; } = string.Empty;

        public bool Richtig { get; set; }

        /// <summary>Nur Zuordnung: das Gegenstück zu <see cref="Text"/>.</summary>
        public string? Partner { get; set; }
    }

    /// <summary>
    /// Ein Fragenentwurf, wie ihn die KI liefert - bewusst flach und typunabhängig, damit ihn
    /// jedes Modell zuverlässig ausfüllen kann. In eine echte Frage verwandelt ihn
    /// <c>Quizzer.Logic.Ki.KiEntwurfUebersetzer</c>.
    /// </summary>
    public sealed class KiFrageEntwurf
    {
        public QuestionType Typ { get; set; }

        public string Fragetext { get; set; } = string.Empty;

        public string Bezeichnung { get; set; } = string.Empty;

        public string Kurz { get; set; } = string.Empty;

        public int Punkte { get; set; } = 100;

        public int Schwierigkeit { get; set; } = 1;

        /// <summary>
        /// Standard- und Eigenschaftsfrage: Hinweise, vom schweren zum leichten. Multiple Choice:
        /// Antworten, <see cref="KiZeile.Richtig"/> markiert die richtigen. Reihenfolge: Elemente in
        /// der richtigen Folge. Zuordnung: Begriff (<see cref="KiZeile.Text"/>) und Gegenstück
        /// (<see cref="KiZeile.Partner"/>). Wahr oder falsch: Aussagen, Richtig = wahr.
        /// </summary>
        public List<KiZeile> Zeilen { get; set; } = new();

        /// <summary>Was am Ende auf dem Beamer steht.</summary>
        public string Aufloesung { get; set; } = string.Empty;

        /// <summary>Schätzfrage mit Zahl.</summary>
        public double? Sollwert { get; set; }

        /// <summary>Schätzfrage: Name aus <see cref="AppreciateUnit"/> (etwa <c>Meter</c>).</summary>
        public string? Einheit { get; set; }

        /// <summary>Schätzfrage mit Datum, als <c>yyyy-MM-dd</c>.</summary>
        public string? SollDatum { get; set; }

        /// <summary>Welches der mitgeschickten Bilder (ab 0) die Frage zeigt - Pflicht bei der Aufdeckfrage.</summary>
        public int? BildIndex { get; set; }

        /// <summary>Für den Spielleiter: was zu prüfen ist, Hintergrund.</summary>
        public string Notizen { get; set; } = string.Empty;

        public List<string> Quellen { get; set; } = new();
    }

    public sealed class KiEntwurfAntwort
    {
        public List<KiFrageEntwurf> Entwuerfe { get; set; } = new();

        /// <summary>Was der Server dazu sagt: nicht abrufbare Links, abgelehnte Entwürfe.</summary>
        public List<string> Hinweise { get; set; } = new();

        public int VerbleibendHeute { get; set; }
    }
}
