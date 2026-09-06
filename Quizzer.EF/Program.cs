using Quizzer.DataModels;
using Quizzer.Logic.Demo;

// Startprojekt fuer die EF-Werkzeuge - und ein schmaler Weg, Demodaten anzulegen oder wieder
// zu entfernen, ohne die Oberflaeche zu bemuehen.
//
//   dotnet run --project Quizzer.EF -- demo-anlegen
//   dotnet run --project Quizzer.EF -- demo-entfernen
//
// Ohne Argument passiert nichts ausser dieser Hilfe. Die Verbindungszeichenfolge kommt aus
// derselben appsettings.json wie in der Anwendung, zeigt also auf die *echte* Spieldatenbank.

if (args.Length == 0)
{
    Console.WriteLine("Quizzer.EF - Startprojekt fuer die EF-Werkzeuge.");
    Console.WriteLine();
    Console.WriteLine("  demo-anlegen     legt einen vollstaendigen Demo-Quizabend an");
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
            Console.WriteLine($"  Mitspieler:  {e.Mitspieler} (davon 1 Moderator)");
            Console.WriteLine($"  Belegte Zellen: {e.Zellen}");
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
            return 0;
        }

    default:
        Console.Error.WriteLine($"Unbekannter Befehl: {args[0]}");
        return 1;
}
