using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using WPFWechatDemo.Models;
using WPFWechatDemo.ViewModels;

namespace WPFWechatDemo
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.Messages.CollectionChanged += (s, args) =>
                {
                    // 消息列表更新时自动滚动到底部
                    MessageScrollViewer?.ScrollToEnd();
                };
            }
        }

        private void ContactItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is Contact contact)
            {
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.SelectContactCommand.Execute(contact);
                }
            }
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Ctrl+Enter 发送消息
                if (DataContext is MainViewModel viewModel && viewModel.SendMessageCommand.CanExecute(null))
                {
                    viewModel.SendMessageCommand.Execute(null);
                }
                e.Handled = true;
            }
        }
    }
}

