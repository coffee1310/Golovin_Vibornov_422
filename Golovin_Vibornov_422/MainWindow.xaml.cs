using Golovin_Vibornov_422.Pages;
using Golovin_Vibornov_422.services;
using System;
using System.Windows;
using System.Windows.Navigation;


namespace Golovin_Vibornov_422
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Инициализация сервиса навигации
            services.NavigationService.Initialize(MainFrame);

            // Подписка на события авторизации
            AuthService.UserLoggedIn += OnUserLoggedIn;
            AuthService.UserLoggedOut += OnUserLoggedOut;

            // Начальная страница - авторизация
            NavigateToLoginPage();
        }

        private void NavigateToLoginPage()
        {
            services.NavigationService.NavigateTo(new LoginPage());
        }

        private void OnUserLoggedIn(user user)
        {
            // Переход на главную страницу после успешной авторизации
            Dispatcher.Invoke(() =>
            {
                services.NavigationService.NavigateTo(new AdsManagementPage());
            });
        }

        private void OnUserLoggedOut()
        {
            // Очистка истории навигации и возврат на страницу авторизации
            Dispatcher.Invoke(() =>
            {
                while (MainFrame.CanGoBack)
                {
                    MainFrame.RemoveBackEntry();
                }
                NavigateToLoginPage();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            // Отписка от событий при закрытии приложения
            AuthService.UserLoggedIn -= OnUserLoggedIn;
            AuthService.UserLoggedOut -= OnUserLoggedOut;
            base.OnClosed(e);
        }
    }
}
