using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Quizzer.Base
{
    /// <summary>Wie ein Element hereinkommt.</summary>
    public enum BewegungsStil
    {
        /// <summary>Nur einblenden.</summary>
        Blenden,

        /// <summary>Einblenden und ein Stück von unten heraufgleiten.</summary>
        Gleiten,

        /// <summary>Einblenden und aus leicht verkleinert auf volle Größe.</summary>
        Zoomen,
    }

    /// <summary>
    /// Die Bewegungen in den Abläufen (Nutzerwunsch 2026-10-21: „generell können in Abläufe
    /// Animation eingebaut werden ... passend ohne den Spielfluss zu stören").
    /// <para>
    /// <b>Die Regeln, an die sich jede Stelle hält:</b> kurz (bis 0,3 s), nie blockierend - es wird
    /// nur Deckkraft und <c>RenderTransform</c> bewegt, nie Layout, und keine Eingabe wartet -, nur
    /// bei einem echten Wechsel, nie in Schleife. Und abschaltbar: <see cref="Aktiv"/> folgt dem
    /// Schalter in den Einstellungen <b>und</b> Windows ("Animationen in Windows anzeigen").
    /// </para>
    /// <para>
    /// <b>Attached Properties statt Storyboards in jeder Ansicht:</b> so steht die Dauer an einer
    /// Stelle, und der Schalter wirkt überall gleich. Eine Ansicht sagt nur <i>dass</i> und
    /// <i>wie</i> sich etwas bewegt.
    /// </para>
    /// </summary>
    public static class Bewegung
    {
        /// <summary>Die gewöhnliche Dauer eines Auftritts.</summary>
        public static readonly Duration Dauer = new(TimeSpan.FromMilliseconds(260));

        private static readonly IEasingFunction Auslaufend = Gefroren(new CubicEase { EasingMode = EasingMode.EaseOut });

        private static bool? aktiv;

        /// <summary>
        /// Ob überhaupt bewegt wird: der eigene Schalter (<c>UserSettings.Animationen</c>) und die
        /// Windows-Einstellung. Wer am Rechner Animationen abgeschaltet hat, will sie auch hier nicht.
        /// </summary>
        public static bool Aktiv
        {
            get => aktiv ??= DataModels.UserSettings.Load().Animationen && SystemParameters.ClientAreaAnimation;
            set => aktiv = value;
        }

        // --- Einblenden, sobald das Element sichtbar wird ---

        public static readonly DependencyProperty EinblendenProperty = DependencyProperty.RegisterAttached(
            "Einblenden", typeof(bool), typeof(Bewegung), new PropertyMetadata(false, EinblendenGeaendert));

        public static bool GetEinblenden(DependencyObject d) => (bool)d.GetValue(EinblendenProperty);

        public static void SetEinblenden(DependencyObject d, bool value) => d.SetValue(EinblendenProperty, value);

        public static readonly DependencyProperty StilProperty = DependencyProperty.RegisterAttached(
            "Stil", typeof(BewegungsStil), typeof(Bewegung), new PropertyMetadata(BewegungsStil.Gleiten));

        public static BewegungsStil GetStil(DependencyObject d) => (BewegungsStil)d.GetValue(StilProperty);

        public static void SetStil(DependencyObject d, BewegungsStil value) => d.SetValue(StilProperty, value);

        private static void EinblendenGeaendert(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element)
                return;

            element.IsVisibleChanged -= SichtbarGeworden;
            element.Loaded -= Geladen;

            if (e.NewValue is true)
            {
                element.IsVisibleChanged += SichtbarGeworden;
                element.Loaded += Geladen;
            }
        }

        private static void Geladen(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.IsVisible)
                Auftritt(element, GetStil(element));
        }

        private static void SichtbarGeworden(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is FrameworkElement element && e.NewValue is true && element.IsLoaded)
                Auftritt(element, GetStil(element));
        }

        // --- Einblenden nur, wenn eine Bedingung beim Laden gilt (etwa: diese Kachel ist neu) ---

        public static readonly DependencyProperty EinblendenWennProperty = DependencyProperty.RegisterAttached(
            "EinblendenWenn", typeof(bool), typeof(Bewegung), new PropertyMetadata(false, EinblendenWennGeaendert));

        public static bool GetEinblendenWenn(DependencyObject d) => (bool)d.GetValue(EinblendenWennProperty);

        public static void SetEinblendenWenn(DependencyObject d, bool value) => d.SetValue(EinblendenWennProperty, value);

        private static void EinblendenWennGeaendert(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement element || e.NewValue is not true)
                return;

            if (element.IsLoaded)
                Auftritt(element, GetStil(element));
            else
                element.Loaded += EinmalGeladen;
        }

        private static void EinmalGeladen(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element)
                return;

            element.Loaded -= EinmalGeladen;

            if (GetEinblendenWenn(element))
                Auftritt(element, GetStil(element));
        }

        // --- Kurz aufblenden, wenn sich ein Wert ändert (etwa: der nächste Schritt) ---

        public static readonly DependencyProperty BeiWechselProperty = DependencyProperty.RegisterAttached(
            "BeiWechsel", typeof(object), typeof(Bewegung), new PropertyMetadata(null, WertGewechselt));

        public static object? GetBeiWechsel(DependencyObject d) => d.GetValue(BeiWechselProperty);

        public static void SetBeiWechsel(DependencyObject d, object? value) => d.SetValue(BeiWechselProperty, value);

        private static void WertGewechselt(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Der erste Wert ist kein Wechsel - beim Aufbau soll nichts zucken.
            if (d is FrameworkElement element && element.IsLoaded && e.OldValue != null && !Equals(e.OldValue, e.NewValue))
                Auftritt(element, BewegungsStil.Blenden, TimeSpan.FromMilliseconds(180));
        }

        // --- Ein kurzer Puls, wenn ein Zustand eintritt (etwa: Zelle gerade abgeschlossen) ---

        public static readonly DependencyProperty PulsProperty = DependencyProperty.RegisterAttached(
            "Puls", typeof(bool), typeof(Bewegung), new PropertyMetadata(false, PulsGeaendert));

        public static bool GetPuls(DependencyObject d) => (bool)d.GetValue(PulsProperty);

        public static void SetPuls(DependencyObject d, bool value) => d.SetValue(PulsProperty, value);

        private static void PulsGeaendert(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Nur der Uebergang falsch -> wahr an einem schon stehenden Element. Beim Laden eines
            // Spiels mit lauter erledigten Zellen pulsiert so nichts.
            if (d is not FrameworkElement element || e.OldValue is not false || e.NewValue is not true || !element.IsLoaded || !Aktiv)
                return;

            var skala = Skala(element);

            if (skala == null)
                return;

            var puls = new DoubleAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.FromMilliseconds(420)) };
            puls.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            puls.KeyFrames.Add(new EasingDoubleKeyFrame(1.08, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160)), Auslaufend));
            puls.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420)), Auslaufend));
            puls.Freeze();

            skala.BeginAnimation(ScaleTransform.ScaleXProperty, puls);
            skala.BeginAnimation(ScaleTransform.ScaleYProperty, puls);
        }

        // --- Die eine Stelle, die bewegt ---

        /// <summary>
        /// Der Auftritt: Deckkraft von 0 auf den Wert, den das Element hat, dazu Gleiten oder Zoom.
        /// <para>
        /// <b>Die Deckkraft wird nicht festgehalten</b> (<c>FillBehavior.Stop</c>, danach der
        /// eigene Wert): sonst ueberstimmte die Animation fuer immer eine Bindung auf
        /// <c>Opacity</c>, etwa <c>SlotOpacity</c> der Antwortkacheln.
        /// </para>
        /// </summary>
        public static void Auftritt(FrameworkElement element, BewegungsStil stil, TimeSpan? dauer = null)
        {
            if (!Aktiv)
                return;

            var zeit = dauer.HasValue ? new Duration(dauer.Value) : Dauer;
            var ziel = element.Opacity;

            if (ziel > 0)
            {
                var blende = new DoubleAnimation(0, ziel, zeit) { EasingFunction = Auslaufend, FillBehavior = FillBehavior.Stop };
                blende.Freeze();
                element.BeginAnimation(UIElement.OpacityProperty, blende);
            }

            switch (stil)
            {
                case BewegungsStil.Gleiten when Verschiebung(element) is { } schub:
                    {
                        var gleiten = new DoubleAnimation(18, 0, zeit) { EasingFunction = Auslaufend };
                        gleiten.Freeze();
                        schub.BeginAnimation(TranslateTransform.YProperty, gleiten);
                        break;
                    }

                case BewegungsStil.Zoomen when Skala(element) is { } skala:
                    {
                        var zoom = new DoubleAnimation(0.94, 1, zeit) { EasingFunction = Auslaufend };
                        zoom.Freeze();
                        skala.BeginAnimation(ScaleTransform.ScaleXProperty, zoom);
                        skala.BeginAnimation(ScaleTransform.ScaleYProperty, zoom);
                        break;
                    }
            }
        }

        /// <summary>
        /// Die Verschiebung des Elements - angelegt, wenn es noch keine Transformation hat.
        /// <b>Eine fremde Transformation wird nicht angefasst</b>: dann nur Blenden.
        /// </summary>
        private static TranslateTransform? Verschiebung(FrameworkElement element)
        {
            switch (element.RenderTransform)
            {
                case TranslateTransform eigene when !eigene.IsFrozen:
                    return eigene;

                case Transform t when t == Transform.Identity || t.Value.IsIdentity && t.IsFrozen:
                    var neu = new TranslateTransform();
                    element.RenderTransform = neu;
                    return neu;

                default:
                    return null;
            }
        }

        private static ScaleTransform? Skala(FrameworkElement element)
        {
            switch (element.RenderTransform)
            {
                case ScaleTransform eigene when !eigene.IsFrozen:
                    return eigene;

                case Transform t when t == Transform.Identity || t.Value.IsIdentity && t.IsFrozen:
                    var neu = new ScaleTransform(1, 1);
                    element.RenderTransformOrigin = new Point(0.5, 0.5);
                    element.RenderTransform = neu;
                    return neu;

                default:
                    return null;
            }
        }

        private static T Gefroren<T>(T freezable) where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }
    }
}
