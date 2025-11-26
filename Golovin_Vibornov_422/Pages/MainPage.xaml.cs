using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace Golovin_Vibornov_422.Pages
{
    public partial class MainPage : Page
    {
        private AdsDatabaseEntities _context;
        private List<dynamic> _allAds;
        private bool _isLoading = false;

        public MainPage()
        {
            InitializeComponent();
            InitializePage();
        }

        private async void InitializePage()
        {
            try
            {
                _context = new AdsDatabaseEntities();
                await LoadFilters();
                await LoadAds();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка инициализации страницы: {ex.Message}", "Ошибка загрузки");
            }
        }

        private async System.Threading.Tasks.Task LoadFilters()
        {
            try
            {
                var cities = await _context.city.OrderBy(c => c.city1).ToListAsync();
                var categories = await _context.category.OrderBy(c => c.name).ToListAsync();
                var types = await _context.type.OrderBy(t => t.type1).ToListAsync();
                var statuses = await _context.status.OrderBy(s => s.status1).ToListAsync();

                // Добавляем пустой элемент для каждого фильтра
                var allCities = new List<city> { new city { id = 0, city1 = "Все города" } };
                allCities.AddRange(cities);

                var allCategories = new List<category> { new category { id = 0, name = "Все категории" } };
                allCategories.AddRange(categories);

                var allTypes = new List<type> { new type { id = 0, type1 = "Все типы" } };
                allTypes.AddRange(types);

                var allStatuses = new List<status> { new status { id = 0, status1 = "Все статусы" } };
                allStatuses.AddRange(statuses);

                cmbCity.ItemsSource = allCities;
                cmbCategory.ItemsSource = allCategories;
                cmbType.ItemsSource = allTypes;
                cmbStatus.ItemsSource = allStatuses;

                // Выбираем первые элементы
                cmbCity.SelectedIndex = 0;
                cmbCategory.SelectedIndex = 0;
                cmbType.SelectedIndex = 0;
                cmbStatus.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка загрузки фильтров: {ex.Message}", "Ошибка");
            }
        }

        private async System.Threading.Tasks.Task LoadAds()
        {
            if (_isLoading) return;

            _isLoading = true;
            ShowLoadingState("Загрузка объявлений...");

            try
            {
                // Загружаем все активные объявления
                var ads = await _context.ads_data
                    .Include(a => a.city)
                    .Include(a => a.category1)
                    .Include(a => a.type)
                    .Include(a => a.status)
                    .Include(a => a.user)
                    .Where(a => a.status.status1 == "Активно") // Только активные объявления
                    .OrderByDescending(a => a.ad_post_date)
                    .ToListAsync();

                // Создаем список для отображения с дополнительными свойствами
                _allAds = ads.Select(ad => new
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
                    ad.ad_image_path, // Добавляем путь к изображению

                    // Связанные данные
                    City = ad.city,
                    Category = ad.category1,
                    AdType = ad.type,
                    AdStatus = ad.status,
                    User = ad.user,

                    // Дополнительные свойства для UI
                    HasImage = !string.IsNullOrEmpty(ad.ad_image_path), // Проверяем наличие пути к изображению
                    ImageSource = LoadImageFromPath(ad.ad_image_path), // Загружаем изображение
                    StatusColor = ad.status.status1 == "Активно" ?
                        new SolidColorBrush(Color.FromRgb(76, 175, 80)) :
                        new SolidColorBrush(Color.FromRgb(158, 158, 158))
                }).Cast<dynamic>().ToList();

                ApplyFilters();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось загрузить объявления: {ex.Message}", "Ошибка загрузки");
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
                // Проверяем разные варианты путей
                string fullPath = GetFullImagePath(imagePath);

                if (File.Exists(fullPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze(); // Рекомендуется для производительности
                    return bitmap;
                }
                else
                {
                    // Если файл не найден, логируем для отладки
                    System.Diagnostics.Debug.WriteLine($"Файл изображения не найден: {fullPath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                // В случае ошибки загрузки изображения
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки изображения {imagePath}: {ex.Message}");
                return null;
            }
        }

        private string GetFullImagePath(string imagePath)
        {
            // Если путь уже абсолютный
            if (Path.IsPathRooted(imagePath))
                return imagePath;

            // Если путь относительный, добавляем базовую директорию
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Убираем начальные слеши из относительного пути
            string cleanPath = imagePath.TrimStart('\\', '/');

            // Формируем полный путь
            string fullPath = Path.Combine(baseDirectory, "Images", "ads", cleanPath);

            return fullPath;
        }

        private void ApplyFilters()
        {
            if (_allAds == null) return;

            var filteredAds = _allAds.AsEnumerable();

            // Применяем текстовый поиск
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                var searchTerm = txtSearch.Text.ToLower();
                filteredAds = filteredAds.Where(ad =>
                    ad.ad_title.ToLower().Contains(searchTerm) ||
                    (ad.ad_description != null && ad.ad_description.ToLower().Contains(searchTerm))
                );
            }

            // Применяем фильтр по городу
            if (cmbCity.SelectedItem is city selectedCity && selectedCity.id != 0)
            {
                filteredAds = filteredAds.Where(ad => ad.city_id == selectedCity.id);
            }

            // Применяем фильтр по категории
            if (cmbCategory.SelectedItem is category selectedCategory && selectedCategory.id != 0)
            {
                filteredAds = filteredAds.Where(ad => ad.category == selectedCategory.id);
            }

            // Применяем фильтр по типу
            if (cmbType.SelectedItem is type selectedType && selectedType.id != 0)
            {
                filteredAds = filteredAds.Where(ad => ad.ad_type_id == selectedType.id);
            }

            // Применяем фильтр по статусу
            if (cmbStatus.SelectedItem is status selectedStatus && selectedStatus.id != 0)
            {
                filteredAds = filteredAds.Where(ad => ad.ad_status_id == selectedStatus.id);
            }

            // Обновляем UI
            itemsAds.ItemsSource = filteredAds.ToList();
            UpdateStatusBar(filteredAds.Count());
        }

        private void UpdateStatusBar(int count)
        {
            lblCount.Text = $"Найдено объявлений: {count}";

            // Информация о примененных фильтрах
            var filters = new List<string>();

            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
                filters.Add($"поиск: \"{txtSearch.Text}\"");

            if (cmbCity.SelectedItem is city city && city.id != 0)
                filters.Add($"город: {city.city1}");

            if (cmbCategory.SelectedItem is category category && category.id != 0)
                filters.Add($"категория: {category.name}");

            if (cmbType.SelectedItem is type type && type.id != 0)
                filters.Add($"тип: {type.type1}");

            if (cmbStatus.SelectedItem is status status && status.id != 0)
                filters.Add($"статус: {status.status1}");

            lblFilterInfo.Text = filters.Any() ?
                $"Фильтры: {string.Join(", ", filters)}" :
                "Фильтры не применены";
        }

        // Обработчики событий
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allAds != null)
                ApplyFilters();
        }

        private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_allAds != null)
                ApplyFilters();
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = string.Empty;
            cmbCity.SelectedIndex = 0;
            cmbCategory.SelectedIndex = 0;
            cmbType.SelectedIndex = 0;
            cmbStatus.SelectedIndex = 0;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadAds();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        // Вспомогательные методы
        private void ShowLoadingState(string message)
        {
            lblStatus.Text = message;
        }

        private void HideLoadingState()
        {
            lblStatus.Text = "Готово";
        }

        private void ShowErrorMessage(string message, string title = "Ошибка")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            lblStatus.Text = "Произошла ошибка";
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Переход на страницу авторизации
                var loginPage = new LoginPage(); // Замените на вашу страницу авторизации
                NavigationService.Navigate(loginPage);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось открыть страницу авторизации: {ex.Message}", "Ошибка");
            }
        }

    }
}