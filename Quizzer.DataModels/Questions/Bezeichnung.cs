namespace Quizzer.DataModels.Questions
{
    /// <summary>
    /// Bezeichnung und Kurzbezeichnung aus dem Fragetext (E1, M31, 2026-10-23) - der Editor
    /// trägt sie ein, bis jemand sie von Hand ändert.
    /// </summary>
    public static class Bezeichnung
    {
        public const int Lang = 80;
        public const int Kurz = 24;

        /// <summary>Die erste Zeile ohne Fragezeichen am Ende, an einer Wortgrenze gekürzt.</summary>
        public static string AusFragetext(string? text, int laenge)
        {
            var zeile = (text ?? string.Empty).Split('\n')[0].Trim().TrimEnd('?', ':', '.', '!').Trim();

            if (zeile.Length <= laenge)
                return zeile;

            var schnitt = zeile.LastIndexOf(' ', laenge);
            return (schnitt > laenge / 2 ? zeile[..schnitt] : zeile[..laenge]).TrimEnd(',', ';', ' ') + " …";
        }
    }
}
