using Quizzer.DataModels;
using Quizzer.Logic.Demo;
using Quizzer.Logic.Vorrat;

// Startprojekt fuer die EF-Werkzeuge - und ein schmaler Weg, Demodaten anzulegen oder wieder
// zu entfernen, ohne die Oberflaeche zu bemuehen.
//
//   dotnet run --project Quizzer.EF -- demo-anlegen
//   dotnet run --project Quizzer.EF -- demo-entfernen
//
// Ohne Argument passiert nichts ausser dieser Hilfe. Die Verbindungszeichenfolge kommt aus
// derselben appsettings.json wie in der Anwendung, zeigt also auf die *echte* Spieldatenbank.

// Die Ausgaben tragen Umlaute. Ohne das schreibt .NET in der Codepage der Konsole (850: "Ü" = 0x9A),
// und wer die Ausgabe als UTF-8 liest, sieht "?" (V13).
Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length == 0)
{
    Console.WriteLine("Quizzer.EF - Startprojekt fuer die EF-Werkzeuge.");
    Console.WriteLine();
    Console.WriteLine("  demo-anlegen     legt den Demo-Quizabend, 'Alle Fragetypen' und den Minispielabend an");
    Console.WriteLine("  demo-typen       legt nur das Spiel 'Alle Fragetypen' an (je Typ eine Spalte)");
    Console.WriteLine("  demo-minispiele  legt nur den Minispielabend an (jede Minispiel-Art und Sonderregel)");
    Console.WriteLine("  demo-vorrat      legt den Vorrat geteilter Minispiele an - echter Bestand, ohne Marke");
    Console.WriteLine("  demo-design      legt ein zweites Design mit eigenen Texturen an");
    Console.WriteLine("  demo-entfernen   entfernt alles mit der Marke " + DemoDataSeeder.Marke);
    return 0;
}

Settings.LoadSettings();

Console.WriteLine($"Datenbank: {Settings.ConnectionString}");
Console.WriteLine();

switch (args[0])
{
    case "demo-anlegen":
        {
            var e = await DemoDataSeeder.CreateAsync();

            Console.WriteLine($"Angelegt: \"{e.Spiel.Designation}\"");
            Console.WriteLine($"  Kategorien:  {e.Kategorien}");
            Console.WriteLine($"  Fragen:      {e.Fragen}");
            // Hier stand "(davon 1 Moderator)" als eingetippte Zahl - schon vor dem 2026-09-06
            // falsch, es waren zwei. Eine Zahl im Fliesstext altert lautlos.
            Console.WriteLine($"  Mitspieler:  {e.Mitspieler}");
            Console.WriteLine($"  Belegte Zellen: {e.Zellen}");
            Console.WriteLine();

            await TypenschauAsync();
            Console.WriteLine();

            return await MinispielabendAsync();
        }

    case "demo-typen":
        return await TypenschauAsync();

    case "demo-minispiele":
        return await MinispielabendAsync();

    case "demo-vorrat":
        {
            // Ohne Anmeldung gibt es keinen Besitzer - wie QuestionOwnership.ClaimIfUnowned.
            var v = await MinispielVorrat.CreateAsync(besitzer: null);

            Console.WriteLine($"Minispiel-Vorrat: {v.Neu} neu angelegt, {v.Vorhanden} schon vorhanden (unverändert).");
            Console.WriteLine("  Er bleibt bei demo-entfernen stehen - Spiele dürfen darauf verweisen.");
            return 0;
        }

    case "demo-design":
        {
            var d = await DemoDataSeeder.CreateThemeAsync();

            Console.WriteLine(d == null
                ? "Das Demo-Design gab es schon."
                : $"Angelegt: Design \"{d.Designation}\", Ordner Themes/{d.FolderName}");
            return 0;
        }

    case "demo-entfernen":
        {
            var e = await DemoDataRemover.RemoveAsync();

            Console.WriteLine("Entfernt:");
            Console.WriteLine($"  Spiele:      {e.Spiele}");
            Console.WriteLine($"  Fragen:      {e.Fragen}");
            Console.WriteLine($"  Mitspieler:  {e.Mitspieler}");
            Console.WriteLine($"  Kategorien:  {e.Kategorien}");
            Console.WriteLine($"  Designs:     {e.Designs}");

            if (e.BehalteneDesigns > 0)
                Console.WriteLine(
                    $"  {e.BehalteneDesigns} Demo-Design(s) blieben stehen: es steht noch ein "
                    + "Spiel darauf. Das Löschen wäre am Fremdschlüssel gescheitert.");

            if (e.BehalteneKategorien > 0)
                Console.WriteLine(
                    $"  {e.BehalteneKategorien} Demo-Kategorie(n) blieben stehen: es liegt noch "
                    + "eine fremde Frage darin. Das Löschen hätte sie mitgenommen.");

            return 0;
        }

    default:
        Console.Error.WriteLine($"Unbekannter Befehl: {args[0]}");
        return 1;
}

static async Task<int> TypenschauAsync()
{
    var t = await DemoDataSeeder.CreateTypenschauAsync();

    Console.WriteLine($"Angelegt: \"{t.Spiel.Designation}\"");
    Console.WriteLine($"  Fragen:      {t.Fragen}");
    Console.WriteLine($"  Fragetypen:  {string.Join(", ", t.Typen.Select(Quizzer.DataModels.Questions.QuestionTypeProfiles.For).Select(p => p.DisplayName))}");

    if (t.OhneBilder)
        Console.WriteLine("  Hinweis: der Ressourcenordner war nicht beschreibbar - Bilderfragen und "
            + "Aufdeckfragen fehlen. Pfad steht in appsettings.json (FilePathQuizzer).");

    return 0;
}

static async Task<int> MinispielabendAsync()
{
    var m = await DemoDataSeeder.CreateMinispielabendAsync();

    Console.WriteLine($"Angelegt: \"{m.Spiel.Designation}\"");
    Console.WriteLine($"  Fragen:      {m.Fragen}");
    Console.WriteLine($"  Joker:       {m.Joker.Count} offen (Grund \"{DemoDataSeeder.DemoJokerGrund}\")");

    return 0;
}
