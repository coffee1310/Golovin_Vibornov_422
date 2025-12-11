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
    /// <summary>
    /// Логика главной страницы
    /// </summary>
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
                var ads = await _context.ads_data
                    .Include(a => a.city)
                    .Include(a => a.category1)
                    .Include(a => a.type)
                    .Include(a => a.status)
                    .Include(a => a.user)
                    .OrderByDescending(a => a.ad_post_date)
                    .ToListAsync();

                _allAds = ads.Select(ad => new
                {
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

                    City = ad.city,
                    Category = ad.category1,
                    AdType = ad.type,
                    AdStatus = ad.status,
                    User = ad.user,

                    HasImage = !string.IsNullOrEmpty(ad.ad_image_path),
                    ImageSource = LoadImageFromPath(ad.ad_image_path),
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
                else
                {
                    string alternativePath = FindImageInAlternativeLocations(imagePath);
                    if (!string.IsNullOrEmpty(alternativePath) && File.Exists(alternativePath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(alternativePath, UriKind.Absolute);
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
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
            if (string.IsNullOrEmpty(imagePath))
                return null;

            if (Path.IsPathRooted(imagePath))
                return imagePath;

            string projectDirectory = GetProjectDirectory();
            string cleanPath = imagePath.TrimStart('\\', '/');
            string fullPath = Path.Combine(projectDirectory, "Images", cleanPath);

            return fullPath;
        }

        private string FindImageInAlternativeLocations(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return null;

            string cleanPath = imagePath.TrimStart('\\', '/');

            string[] possiblePaths = {
                Path.Combine(GetProjectDirectory(), "Images", cleanPath),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", cleanPath),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                           "Golovin_Vibornov_422", "Images", cleanPath),
                Path.Combine(GetProjectDirectory(), "Images", "ads", Path.GetFileName(cleanPath)),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "ads", Path.GetFileName(cleanPath))
            };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine($"Изображение найдено: {path}");
                    return path;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Изображение не найдено: {imagePath}");
            return null;
        }

        private string GetProjectDirectory()
        {
            try
            {
                string executablePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string binDebugDirectory = Path.GetDirectoryName(executablePath);

                DirectoryInfo binDirectory = Directory.GetParent(binDebugDirectory);
                if (binDirectory != null)
                {
                    DirectoryInfo projectDirectory = binDirectory.Parent;
                    if (projectDirectory != null)
                    {
                        return projectDirectory.FullName;
                    }
                }

                string currentDirectory = Directory.GetCurrentDirectory();
                if (currentDirectory.Contains("bin\\Debug") || currentDirectory.Contains("bin\\Release"))
                {
                    DirectoryInfo currentDir = Directory.GetParent(currentDirectory);
                    if (currentDir != null && currentDir.Parent != null)
                    {
                        return currentDir.Parent.FullName;
                    }
                }

                return currentDirectory;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка получения пути проекта: {ex.Message}");
                return Directory.GetCurrentDirectory();
            }
        }

        private void ApplyFilters()
        {
            if (_allAds == null) return;

            var filteredAds = _allAds.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                var searchTerm = txtSearch.Text.ToLower();
                filteredAds = filteredAds.Where(ad =>
                    ad.ad_title.ToLower().Contains(searchTerm) ||
                    (ad.ad_description != null && ad.ad_description.ToLower().Contains(searchTerm))
                );
            }

            if (cmbCity.SelectedItem is city selectedCity && selectedCity.id != 0)
            {
                filteredAds = filteredAds.Where(ad => ad.city_id == selectedCity.id);
            }

            if (cmbCategory.SelectedItem is category selectedCategory && selectedCategory.id != 0)
            {
                filteredAds = filteredAds.Where(ad => ad.category == selectedCategory.id);
            }

            if (cmbType.SelectedItem is type selectedType && selectedType.id != 0)
            {
                filteredAds = filteredAds.Where(ad => ad.ad_type_id == selectedType.id);
            }

            if (cmbStatus.SelectedItem is status selectedStatus && selectedStatus.id != 0)
            {
                filteredAds = filteredAds.Where(ad => ad.ad_status_id == selectedStatus.id);
            }

            itemsAds.ItemsSource = filteredAds.ToList();
            UpdateStatusBar(filteredAds.Count());
        }

        private void UpdateStatusBar(int count)
        {
            lblCount.Text = $"Найдено объявлений: {count}";

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
                var loginPage = new LoginPage();
                NavigationService.Navigate(loginPage);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось открыть страницу авторизации: {ex.Message}", "Ошибка");
            }
        }
    }
}