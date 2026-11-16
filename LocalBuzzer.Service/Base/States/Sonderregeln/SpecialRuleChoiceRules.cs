using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Models.Base;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Sonderregeln
{
    /// <summary>Was ein Telefon bei einer Sonderregel mit Spielerwahl sieht (Wette, Experte, Allianz). Feldnamen = <c>wahl.js</c>.</summary>
    public sealed class ChoiceClientInfo
    {
        /// <summary>Die Tasten - nur fuer den, der gerade waehlt; die Id darf ans Telefon (E4).</summary>
        public List<MinigamePlayerOption> Options { get; set; } = new();

        /// <summary>Seine Wahl, sobald abgegeben - auch nach dem Wiederverbinden, nie eine fremde.</summary>
        public Guid? OwnChoice { get; set; }

        /// <summary>Die Frage ueber den Tasten, etwa „Auf wen wettest du?".</summary>
        public string Prompt { get; set; } = string.Empty;
    }

    /// <summary>
    /// Vorphase mit Spielerwahl (M35 C3b): jeder, der spielt, waehlt genau einen Mitspieler. <b>Gewaehlt werden darf
    /// nur ein Mitspieler des laufenden Spiels</b>, sich selbst nur, wo die Regel es erlaubt - die Wahl kommt aus dem
    /// WLAN. Abgegeben wird <see cref="PlayerChoicePayload"/>, wie bei der Spielerwahl der Minispiele.
    /// </summary>
    public abstract class SpecialRuleChoiceRules : SpecialRuleRules<Guid>
    {
        protected SpecialRuleChoiceRules(MinigameRulesContext kontext)
        {
            ArgumentNullException.ThrowIfNull(kontext);
            Kontext = kontext;
        }

        protected MinigameRulesContext Kontext { get; }

        /// <summary>Ob man sich selbst waehlen darf - bei der Wette ja.</summary>
        protected abstract bool SelfAllowed { get; }

        /// <summary>Die Frage am Telefon.</summary>
        protected abstract string Prompt { get; }

        /// <summary>Die Beschriftung einer Taste.</summary>
        protected virtual string OptionName(Player spieler) => Kontext.NameVon(spieler.Id) ?? "?";

        protected override Guid? Read(Player player, JsonElement payload)
        {
            if (!Plays(player.Id) || MinigamePayload.Read<PlayerChoicePayload>(payload)?.Chosen is not { } gewaehlt)
                return null;

            return Waehlbar(player.Id, gewaehlt) ? gewaehlt : null;
        }

        protected override object? Info(Player? player, Guid? eigene) => new ChoiceClientInfo
        {
            Options = player != null && Plays(player.Id) ? Optionen(player) : new(),
            OwnChoice = eigene,
            Prompt = Prompt,
        };

        private bool Waehlbar(Guid waehler, Guid gewaehlt)
            => (SelfAllowed || gewaehlt != waehler) && Kontext.Mitspieler().Any(p => p.Id == gewaehlt);

        private List<MinigamePlayerOption> Optionen(Player player)
            => Kontext.Mitspieler()
                .Where(p => SelfAllowed || p.Id != player.Id)
                .Select(p => new MinigamePlayerOption { Id = p.Id, Name = OptionName(p) })
                .OrderBy(o => o.Name, StringComparer.CurrentCulture)
                .ToList();
    }
}
