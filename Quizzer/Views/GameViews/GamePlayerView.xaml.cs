using Quizzer.Base;
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
    /// Interaction logic for GamePlayerView.xaml
    /// </summary>
    public partial class GamePlayerView : WindowBase
    {
        /// <summary>Escape schliesst den Spielerbildschirm nicht - er haengt am Beamer.</summary>
        public override bool CloseOnEscape => false;

        public GamePlayerView()
        {
            InitializeComponent();
        }
    }
}