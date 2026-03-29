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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Quizzer.Views.GameViews.QuestionViews.Typed
{
    /// <summary>
    /// Interaction logic for DefaultStepView.xaml
    /// </summary>
    public partial class DefaultStepView : UserControlStepViewBase
    {
        public DefaultStepView()
        {
            InitializeComponent();
        }

        //public override void RefreshView()
        //{
        //    StackPanleIsResult.Visibility = IsMasterView ? Visibility.Visible : Visibility.Collapsed;
        //    StackPanleIsFinish.Visibility = IsMasterView ? Visibility.Visible : Visibility.Collapsed;

        //    base.RefreshView();
        //}
    }
}