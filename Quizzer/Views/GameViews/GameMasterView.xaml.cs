using Quizzer.Base;
using Quizzer.Views.Base;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Interaction logic for GameMasterView.xaml
    /// </summary>
    public partial class GameMasterView : WindowBase
    {
        /// <summary>
        /// Escape schliesst dieses Fenster nicht: es traegt das laufende Spiel, und ein
        /// versehentlicher Druck haette den ganzen Spielstand vom Bildschirm genommen.
        /// </summary>
        public override bool CloseOnEscape => false;

        public GameMasterView()
        {
            InitializeComponent();

            WindowTyp = WindowTyp.Game;
        }
    }
}