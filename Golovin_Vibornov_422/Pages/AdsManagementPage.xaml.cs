using Golovin_Vibornov_422.services;
using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace Golovin_Vibornov_422.Pages
{
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
            lblUserInfo.Text = $"Пользователь: {AuthService.CurrentUser.user_login}";
        }

        private async void LoadAds()
        {
            if (_isLoading) return;

            _isLoading = true;
            ShowLoadingState("Загрузка объявлений...");

            try
            {
                // Загружаем объявления с Include для связанных данных
                var userAds = await _context.ads_data
                    .Include(a => a.city)
                    .Include(a => a.category1)
                    .Include(a => a.type)
                    .Include(a => a.status)
                    .Where(a => a.user_id == AuthService.CurrentUser.id)
                    .OrderByDescending(a => a.ad_post_date)
                    .ToListAsync();

                // Создаем список для отображения с дополнительными свойствами
                var adsWithDetails = userAds.Select(ad => new
                {
                    // Основные свойства
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
                    ad.ad_image_path,

                    // Связанные данные
                    City = ad.city,
                    Category = ad.category1,
                    AdType = ad.type,
                    AdStatus = ad.status,

                    // Дополнительные свойства для UI
                    HasImage = !string.IsNullOrEmpty(ad.ad_image_path),
                    ImageSource = LoadImageFromPath(ad.ad_image_path),
                    StatusColor = ad.status.status1 == "Активно" ?
                        new SolidColorBrush(Color.FromRgb(76, 175, 80)) :
                        new SolidColorBrush(Color.FromRgb(158, 158, 158))
                }).ToList();

                // Обновляем UI
                Dispatcher.Invoke(() =>
                {
                    itemsAds.ItemsSource = adsWithDetails;
                    UpdateUIState(adsWithDetails.Count);
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

        private ImageSource LoadImageFromPath(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return null;

            try
            {
                string fullPath = GetFullImagePath(imagePath);

                if (File.Exists(fullPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки изображения {imagePath}: {ex.Message}");
                return null;
            }
        }

        private string GetFullImagePath(string imagePath)
        {
            if (Path.IsPathRooted(imagePath))
                return imagePath;

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string cleanPath = imagePath.TrimStart('\\', '/');
            string fullPath = Path.Combine(baseDirectory, "Images", "ads", cleanPath);

            return fullPath;
        }

        private void UpdateUIState(int adsCount)
        {
            lblCount.Text = $"Объявлений: {adsCount}";

            // Показываем/скрываем сообщение при отсутствии данных
            emptyStatePanel.Visibility = adsCount == 0 ? Visibility.Visible : Visibility.Collapsed;
            itemsAds.Visibility = adsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowLoadingState(string message)
        {
            loadingPanel.Visibility = Visibility.Visible;
            lblLoading.Text = message;
            itemsAds.IsEnabled = false;
        }

        private void HideLoadingState()
        {
            loadingPanel.Visibility = Visibility.Collapsed;
            itemsAds.IsEnabled = true;
        }

        // Обработчики кликов по карточкам
        private void AdCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) // Двойной клик
            {
                var border = sender as Border;
                if (border != null)
                {
                    EditAdFromCard(border.DataContext);
                }
            }
        }

        private void EditCardButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag != null)
            {
                EditAdById((int)button.Tag);
            }
        }

        private void DeleteCardButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag != null)
            {
                DeleteAdById((int)button.Tag);
            }
        }

        private void EditAdFromCard(object dataContext)
        {
            try
            {
                var idProperty = dataContext.GetType().GetProperty("id");
                if (idProperty != null)
                {
                    int adId = (int)idProperty.GetValue(dataContext);
                    EditAdById(adId);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось открыть объявление для редактирования: {ex.Message}", "Ошибка");
            }
        }

        private void EditAdById(int adId)
        {
            try
            {
                var fullAd = _context.ads_data.Find(adId);
                if (fullAd != null)
                {
                    var editPage = new AdEditPage(fullAd);
                    editPage.AdSaved += OnAdSaved;
                    NavigationService.Navigate(editPage);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось открыть объявление для редактирования: {ex.Message}", "Ошибка");
            }
        }

        private void DeleteAdById(int adId)
        {
            try
            {
                var fullAd = _context.ads_data.Find(adId);
                if (fullAd != null)
                {
                    // Подтверждение удаления
                    var result = MessageBox.Show(
                        $"Вы уверены, что хотите удалить объявление?\n\n" +
                        $"Заголовок: {fullAd.ad_title}\n" +
                        $"Дата: {fullAd.ad_post_date:dd.MM.yyyy}\n" +
                        $"Цена: {fullAd.price}₽\n\n" +
                        $"Это действие нельзя отменить.",
                        "Подтверждение удаления",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning,
                        MessageBoxResult.No);

                    if (result == MessageBoxResult.Yes)
                    {
                        PerformDeleteAd(fullAd);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка при удалении: {ex.Message}", "Ошибка");
            }
        }

        // Существующие методы (без изменений)
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
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

        private async void PerformDeleteAd(ads_data ad)
        {
            ShowLoadingState("Удаление объявления...");

            try
            {
                _context.ads_data.Remove(ad);
                await _context.SaveChangesAsync();

                LoadAds();
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
                LogoutButton_Click(sender, e);
            }
        }

        private void OnAdSaved()
        {
            LoadAds();
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

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _context?.Dispose();
        }

        private void AllAdsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var allAdsPage = new MainPage();
                NavigationService.Navigate(allAdsPage);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось открыть страницу всех объявлений: {ex.Message}", "Ошибка");
            }
        }
    }
}