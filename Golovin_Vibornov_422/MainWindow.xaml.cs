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

            services.NavigationService.Initialize(MainFrame);

            AuthService.UserLoggedIn += OnUserLoggedIn;
            AuthService.UserLoggedOut += OnUserLoggedOut;

            NavigateToLoginPage();
        }

        private void NavigateToLoginPage()
        {
            services.NavigationService.NavigateTo(new MainPage());
        }

        private void OnUserLoggedIn(user user)
        {
            Dispatcher.Invoke(() =>
            {
                services.NavigationService.NavigateTo(new AdsManagementPage());
            });
        }

        private void OnUserLoggedOut()
        {
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
            AuthService.UserLoggedIn -= OnUserLoggedIn;
            AuthService.UserLoggedOut -= OnUserLoggedOut;
            base.OnClosed(e);
        }
    }
}
