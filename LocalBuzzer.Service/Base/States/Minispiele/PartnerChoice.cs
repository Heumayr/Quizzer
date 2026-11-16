using Quizzer.DataModels.Models.Base;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon bei der Spielerwahl abgibt. Feldname = <c>spielerwahl.js</c>.</summary>
    public sealed class PlayerChoicePayload
    {
        public Guid? Chosen { get; set; }
    }

    /// <summary>Eine Taste der Spielerwahl. Die Id darf ans Telefon (E4) - der Server prueft die Wahl trotzdem.</summary>
    public sealed class MinigamePlayerOption
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Der Ausloeser waehlt einen Mitspieler (M35 C2) - Duell und Teilen oder Nehmen teilen sich das.
    /// Vor der Wahl spielt nur er, danach die beiden. <b>Gewaehlt werden darf nur ein Mitspieler des
    /// laufenden Spiels, nie der Waehlende selbst</b> - die Wahl kommt aus dem WLAN.
    /// </summary>
    public sealed class PartnerChoice
    {
        private readonly MinigameRulesContext kontext;
        private readonly string partnerWort;

        /// <param name="ausloeser">Wer das Feld aufgedeckt hat; kein Mitspieler heisst: der Spielleiter waehlt.</param>
        /// <param name="partnerWort">„Gegner" oder „Partner" - fuer die Zuschauer.</param>
        public PartnerChoice(Guid? ausloeser, string partnerWort, MinigameRulesContext kontext)
        {
            this.kontext = kontext;
            this.partnerWort = partnerWort;
            Chooser = IstMitspieler(ausloeser) ? ausloeser : null;
        }

        public Guid? Chooser { get; private set; }

        public Guid? Partner { get; private set; }

        public bool Chosen => Chooser != null && Partner != null;

        public string? ChooserName => Chooser is { } id ? Name(id) : null;

        public string? PartnerName => Partner is { } id ? Name(id) : null;

        /// <summary>Wer gerade abgeben darf: vor der Wahl nur der Waehlende, danach die beiden.</summary>
        public bool Plays(Guid spieler) => Chosen ? spieler == Chooser || spieler == Partner : spieler == Chooser;

        public bool ChoosesNow(Guid spieler) => !Chosen && spieler == Chooser;

        /// <summary>Nimmt die Wahl des Ausloesers an; <c>null</c> heisst verworfen.</summary>
        public PlayerChosen? Accept(Player player, JsonElement payload)
        {
            if (!ChoosesNow(player.Id))
                return null;

            if (MinigamePayload.Read<PlayerChoicePayload>(payload)?.Chosen is not { } gewaehlt
                || gewaehlt == player.Id || !IstMitspieler(gewaehlt))
                return null;

            Partner = gewaehlt;
            return new PlayerChosen(player.Id, gewaehlt);
        }

        /// <summary>Der Spielleiter legt das Paar fest (E1); ohne Partner waehlt danach der Ausloeser am Telefon.</summary>
        public bool SetPair(Guid chooser, Guid? partner)
        {
            if (!IstMitspieler(chooser) || partner is { } p && (p == chooser || !IstMitspieler(p)))
                return false;

            Chooser = chooser;
            Partner = partner;
            return true;
        }

        /// <summary>Zuruecksetzen waehlt neu - wer waehlt, bleibt.</summary>
        public void Clear() => Partner = null;

        /// <summary>Die Tasten mit den Spielernamen - nur fuer den, der gerade waehlt.</summary>
        public List<MinigamePlayerOption> OptionsFor(Player? player)
            => player != null && ChoosesNow(player.Id)
                ? kontext.Mitspieler()
                    .Where(p => p.Id != player.Id)
                    .Select(p => new MinigamePlayerOption { Id = p.Id, Name = Name(p.Id) })
                    .OrderBy(o => o.Name, StringComparer.CurrentCulture)
                    .ToList()
                : new();

        public string WatcherText
            => Chooser == null ? "Der Spielleiter wählt, wer spielt – du schaust zu."
                : !Chosen ? $"{ChooserName} wählt einen {partnerWort} …"
                : $"{ChooserName} und {PartnerName} spielen – du schaust zu.";

        private bool IstMitspieler(Guid? id) => id is { } gesucht && kontext.Mitspieler().Any(p => p.Id == gesucht);

        private string Name(Guid id) => kontext.NameVon(id) ?? "?";
    }
}
