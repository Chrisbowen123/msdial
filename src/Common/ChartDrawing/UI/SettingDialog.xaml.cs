﻿using System.Windows;
using System.Windows.Input;

namespace CompMs.Graphics.UI
{
    /// <summary>
    /// Interaction logic for SettingDialog.xaml
    /// </summary>
    public partial class SettingDialog : System.Windows.Window
    {
        public SettingDialog() {
            InitializeComponent();
        }

        public static readonly DependencyProperty ApplyCommandProperty =
            DependencyProperty.Register(
                nameof(ApplyCommand),
                typeof(ICommand),
                typeof(SettingDialog));

        public ICommand ApplyCommand {
            get => (ICommand)GetValue(ApplyCommandProperty);
            set => SetValue(ApplyCommandProperty, value);
        }

        public static readonly DependencyProperty FinishCommandProperty =
            DependencyProperty.Register(
                nameof(FinishCommand),
                typeof(ICommand),
                typeof(SettingDialog));

        public ICommand FinishCommand {
            get => (ICommand)GetValue(FinishCommandProperty);
            set => SetValue(FinishCommandProperty, value);
        }

        public static readonly DependencyProperty CancelCommandProperty =
            DependencyProperty.Register(
                nameof(CancelCommand),
                typeof(ICommand),
                typeof(SettingDialog));

        public ICommand CancelCommand {
            get => (ICommand)GetValue(CancelCommandProperty);
            set => SetValue(CancelCommandProperty, value);
        }

        private void FinishClose(object sender, RoutedEventArgs e) {
            if (System.Windows.Interop.ComponentDispatcher.IsThreadModal) {
                DialogResult = true;
            }
            if (DataContext is SettingDialogViewModel vm) {
                vm.Result = DialogResult;
            }
            Close();
        }

        private void CancelClose(object sender, RoutedEventArgs e) {
            if (System.Windows.Interop.ComponentDispatcher.IsThreadModal) {
                DialogResult = false;
            }
            if (DataContext is SettingDialogViewModel vm) {
                vm.Result = DialogResult;
            }
            Close();
        }
    }
}
