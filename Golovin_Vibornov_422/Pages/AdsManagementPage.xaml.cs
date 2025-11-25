using Golovin_Vibornov_422.services;
using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Golovin_Vibornov_422.Pages
{
    /// <summary>
    /// Логика взаимодействия для AdsManagementPage.xaml
    /// </summary>
    public partial class AdsManagementPage : Page
    {
        private AdsDatabaseEntities _context;
        private bool _isLoading = false;

        public AdsManagementPage()
        {
            InitializeComponent();
            InitializePage();
        }

        private void InitializePage()
        {
            try
            {
                _context = new AdsDatabaseEntities();
                LoadUserInfo();
                LoadAds();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка инициализации страницы: {ex.Message}", "Ошибка загрузки");
            }
        }

        private void LoadUserInfo()
        {
            // Отображаем информацию о текущем пользователе
            lblUserInfo.Text = $"Пользователь: {AuthService.CurrentUser.user_login}";
        }

        private async void LoadAds()
        {
            // Защита от множественных одновременных загрузок
            if (_isLoading) return;

            _isLoading = true;
            ShowLoadingState("Загрузка объявлений...");

            try
            {
                // Загружаем объявления без Include
                var userAds = await _context.ads_data
                    .Where(a => a.user_id == AuthService.CurrentUser.id)
                    .OrderByDescending(a => a.ad_post_date)
                    .ToListAsync();

                // Загружаем справочники отдельно
                var cities = await _context.city.ToListAsync();
                var categories = await _context.category.ToListAsync();
                var types = await _context.type.ToListAsync();
                var statuses = await _context.status.ToListAsync();

                // Создаем временный список для отображения с связанными данными
                var adsWithReferences = userAds.Select(ad => new
                {
                    // Основные свойства объявления
                    ad.id,
                    ad.ad_title,
                    ad.ad_description,
                    ad.ad_post_date,
                    ad.city_id,
                    ad.category,
                    ad.ad_type_id,
                    ad.ad_status_id,
                    ad.price,
                    ad.user_id,

                    // Связанные данные
                    City = cities.FirstOrDefault(c => c.id == ad.city_id),
                    Category = categories.FirstOrDefault(c => c.id == ad.category),
                    AdType = types.FirstOrDefault(t => t.id == ad.ad_type_id),
                    AdStatus = statuses.FirstOrDefault(s => s.id == ad.ad_status_id)
                }).ToList();

                // Обновляем UI в основном потоке
                Dispatcher.Invoke(() =>
                {
                    dgAds.ItemsSource = adsWithReferences;
                    UpdateUIState(adsWithReferences.Count);
                    lblStatus.Text = "Данные успешно загружены";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    ShowErrorMessage($"Не удалось загрузить объявления: {ex.Message}", "Ошибка загрузки");
                    UpdateUIState(0);
                });
            }
            finally
            {
                _isLoading = false;
                HideLoadingState();
            }
        }

        private void UpdateUIState(int adsCount)
        {
            lblCount.Text = $"Объявлений: {adsCount}";

            // Показываем/скрываем сообщение при отсутствии данных
            emptyStatePanel.Visibility = adsCount == 0 ? Visibility.Visible : Visibility.Collapsed;
            dgAds.Visibility = adsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowLoadingState(string message)
        {
            loadingPanel.Visibility = Visibility.Visible;
            lblLoading.Text = message;
            dgAds.IsEnabled = false;
        }

        private void HideLoadingState()
        {
            loadingPanel.Visibility = Visibility.Collapsed;
            dgAds.IsEnabled = true;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // Переход на страницу создания нового объявления
            try
            {
                var editPage = new AdEditPage();
                editPage.AdSaved += OnAdSaved;
                NavigationService.Navigate(editPage);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось открыть форму создания: {ex.Message}", "Ошибка");
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            EditSelectedAd();
        }

        private void dgAds_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Редактирование при двойном клике по объявлению
            EditSelectedAd();
        }

        private void EditSelectedAd()
        {
            var selectedItem = dgAds.SelectedItem;
            if (selectedItem != null)
            {
                try
                {
                    // Получаем ID выбранного объявления из анонимного типа
                    var idProperty = selectedItem.GetType().GetProperty("id");
                    if (idProperty != null)
                    {
                        int adId = (int)idProperty.GetValue(selectedItem);

                        // Находим полное объявление в контексте
                        var fullAd = _context.ads_data.Find(adId);
                        if (fullAd != null)
                        {
                            // Переход на страницу редактирования выбранного объявления
                            var editPage = new AdEditPage(fullAd);
                            editPage.AdSaved += OnAdSaved;
                            NavigationService.Navigate(editPage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"Не удалось открыть объявление для редактирования: {ex.Message}", "Ошибка");
                }
            }
            else
            {
                ShowInfoMessage("Пожалуйста, выберите объявление для редактирования", "Выбор объявления");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = dgAds.SelectedItem;
            if (selectedItem != null)
            {
                try
                {
                    // Получаем данные выбранного объявления из анонимного типа
                    var idProperty = selectedItem.GetType().GetProperty("id");
                    var titleProperty = selectedItem.GetType().GetProperty("ad_title");
                    var dateProperty = selectedItem.GetType().GetProperty("ad_post_date");
                    var priceProperty = selectedItem.GetType().GetProperty("price");

                    if (idProperty != null && titleProperty != null && dateProperty != null && priceProperty != null)
                    {
                        string title = (string)titleProperty.GetValue(selectedItem);
                        DateTime date = (DateTime)dateProperty.GetValue(selectedItem);
                        decimal price = (decimal)priceProperty.GetValue(selectedItem);

                        // Подтверждение удаления с подробной информацией
                        var result = MessageBox.Show(
                            $"Вы уверены, что хотите удалить объявление?\n\n" +
                            $"Заголовок: {title}\n" +
                            $"Дата: {date:dd.MM.yyyy}\n" +
                            $"Цена: {price:C}\n\n" +
                            $"Это действие нельзя отменить.",
                            "Подтверждение удаления",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning,
                            MessageBoxResult.No);

                        if (result == MessageBoxResult.Yes)
                        {
                            int adId = (int)idProperty.GetValue(selectedItem);
                            var fullAd = _context.ads_data.Find(adId);
                            if (fullAd != null)
                            {
                                PerformDeleteAd(fullAd);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"Ошибка при удалении: {ex.Message}", "Ошибка");
                }
            }
            else
            {
                ShowInfoMessage("Пожалуйста, выберите объявление для удаления", "Выбор объявления");
            }
        }

        private async void PerformDeleteAd(ads_data ad)
        {
            ShowLoadingState("Удаление объявления...");

            try
            {
                _context.ads_data.Remove(ad);
                await _context.SaveChangesAsync();

                LoadAds(); // Обновляем список
                ShowSuccessMessage("Объявление успешно удалено");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось удалить объявление: {ex.Message}", "Ошибка удаления");
            }
            finally
            {
                HideLoadingState();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadAds();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы уверены, что хотите выйти из системы?",
                "Подтверждение выхода",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result == MessageBoxResult.Yes)
            {
                AuthService.Logout();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
            else
            {
                // Если нельзя вернуться назад, выходим из системы
                LogoutButton_Click(sender, e);
            }
        }

        private void OnAdSaved()
        {
            // Обновляем данные после сохранения изменений
            LoadAds();
        }

        private void dgAds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Можно добавить дополнительную логику при изменении выбора
        }

        // Вспомогательные методы для показа сообщений
        private void ShowErrorMessage(string message, string title = "Ошибка")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            lblStatus.Text = "Произошла ошибка";
        }

        private void ShowInfoMessage(string message, string title = "Информация")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowSuccessMessage(string message)
        {
            lblStatus.Text = message;
        }

        // Обработчик выхода со страницы
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // Очистка ресурсов при уходе со страницы
            _context?.Dispose();
        }
    }
}