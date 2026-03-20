using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Quizzer.Views.GameViews.QuestionViews.Typed
{
    public class UserControlStepViewBase : UserControl
    {
        public bool IsMasterView
        {
            get => (bool)GetValue(IsMasterViewProperty);
            set => SetValue(IsMasterViewProperty, value);
        }

        public static readonly DependencyProperty IsMasterViewProperty =
            DependencyProperty.Register(
                nameof(IsMasterView),
                typeof(bool),
                typeof(UserControlStepViewBase),
                new PropertyMetadata(false));

        public UserControlStepViewBase() : base()
        {
            DataContextChanged += Contextchange;
        }

        private void Contextchange(object sender, DependencyPropertyChangedEventArgs e)
        {
            //if (e.OldValue is INotifyPropertyChanged oldNpc)
            //    oldNpc.PropertyChanged -= Step_PropertyChanged;

            //if (e.NewValue is INotifyPropertyChanged newNpc)
            //    newNpc.PropertyChanged += Step_PropertyChanged;

            RefreshView();
        }

        public virtual void RefreshView()
        {
        }
    }
}