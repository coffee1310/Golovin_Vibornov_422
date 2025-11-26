using Golovin_Vibornov_422.services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Golovin_Vibornov_422.Pages
{
    /// <summary>
    /// Логика взаимодействия для LoginPage.xaml
    /// </summary>
    public partial class LoginPage : Page
    {
        private bool _isLoggingIn = false;

        public LoginPage()
        {
            InitializeComponent();
            txtLogin.Focus();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoggingIn) return;

            string login = txtLogin.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(login))
            {
                ShowError("Пожалуйста, введите логин");
                txtLogin.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Пожалуйста, введите пароль");
                txtPassword.Focus();
                return;
            }

            SetLoginState(true, sender);

            try
            {
                bool success = await Task.Run(() => AuthService.Login(login, password));

                if (!success)
                {
                    ShowError("Неверный логин или пароль. Проверьте правильность введенных данных и попробуйте снова.");
                    txtPassword.Password = "";
                    txtPassword.Focus();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Произошла ошибка при подключении к базе данных: {ex.Message}. " +
                         "Проверьте подключение к серверу и повторите попытку.");
            }
            finally
            {
                SetLoginState(false, sender);
            }
        }

        private void SetLoginState(bool loggingIn, object sender)
        {
            _isLoggingIn = loggingIn;
            var button = sender as Button;

            if (loggingIn)
            {
                txtLogin.IsEnabled = false;
                txtPassword.IsEnabled = false;
                if (button != null)
                {
                    button.Content = "Выполняется вход...";
                    button.IsEnabled = false;
                    button.Background = Brushes.Gray;
                }
            }
            else
            {
                txtLogin.IsEnabled = true;
                txtPassword.IsEnabled = true;
                if (button != null)
                {
                    button.Content = "Войти";
                    button.IsEnabled = true;
                    button.Background = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                }
            }
        }

        private void ShowError(string message)
        {
            lblError.Text = message;
            errorBorder.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            errorBorder.Visibility = Visibility.Collapsed;
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            HideError();
            if (sender is TextBox textBox)
            {
                textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204));
            }
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            HideError();
            if (sender is PasswordBox passwordBox)
            {
                passwordBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            }
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                passwordBox.BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204));
            }
        }

        private void txtLogin_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                txtPassword.Focus();
            }
        }

        private void txtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_isLoggingIn)
            {
                LoginButton_Click(sender, e);
            }
        }
    }
}