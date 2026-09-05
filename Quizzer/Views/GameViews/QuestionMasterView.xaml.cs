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
    /// Interaction logic for QuestionMasterView.xaml
    /// </summary>
    public partial class QuestionMasterView : WindowBase
    {
        /// <summary>
        /// Escape schliesst die laufende Frage nicht. Zum Beenden gibt es die Knoepfe unten
        /// rechts - die schreiben den Spielstand mit.
        /// </summary>
        public override bool CloseOnEscape => false;

        public QuestionMasterView()
        {
            InitializeComponent();
        }
    }
}