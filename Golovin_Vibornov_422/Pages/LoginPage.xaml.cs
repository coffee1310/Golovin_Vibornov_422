using Golovin_Vibornov_422.services;
using System;
using System.Diagnostics;
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

            HideError();

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                ShowError("Пожалуйста, введите логин и пароль");

                if (string.IsNullOrEmpty(login))
                    txtLogin.Focus();
                else
                    txtPassword.Focus();

                return;
            }

            SetLoginState(true, sender);

            try
            {
                bool success = await Task.Run(() => AuthService.Login(login, password));

                if (!success)
                {
                    ShowError("Неверный логин или пароль. Проверьте правильность введенных данных.");
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
            Debug.WriteLine($"Показать ошибку: {message}");

            lblError.Text = message;
            errorBorder.Visibility = Visibility.Visible;

            errorBorder.InvalidateVisual();
            errorBorder.UpdateLayout();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                errorBorder.Visibility = Visibility.Visible;
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void HideError()
        {
            errorBorder.Visibility = Visibility.Collapsed;

            errorBorder.InvalidateVisual();
            errorBorder.UpdateLayout();
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
                if (string.IsNullOrEmpty(txtLogin.Text.Trim()))
                {
                    ShowError("Пожалуйста, введите логин");
                    return;
                }

                txtPassword.Focus();
                HideError();
            }
        }

        private void txtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_isLoggingIn)
            {
                if (string.IsNullOrEmpty(txtPassword.Password))
                {
                    ShowError("Пожалуйста, введите пароль");
                    return;
                }

                LoginButton_Click(sender, e);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService?.CanGoBack == true)
            {
                NavigationService.GoBack();
            }
        }

        public void ClearFields()
        {
            txtLogin.Text = "";
            txtPassword.Password = "";
            HideError();
            txtLogin.Focus();
        }
    }
}