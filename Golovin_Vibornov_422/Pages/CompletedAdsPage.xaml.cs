using Golovin_Vibornov_422.services;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Golovin_Vibornov_422.Pages
{
    /// <summary>
    /// Логика страницы для отображения завершенных услуг
    /// </summary>
    public partial class CompletedAdsPage : Page
    {
        private AdsDatabaseEntities _context;
        private bool _isLoading = false;
        private decimal _totalProfit = 0;

        public CompletedAdsPage()
        {
            InitializeComponent();
            InitializePage();
        }

        private void InitializePage()
        {
            try
            {
                _context = new AdsDatabaseEntities();
                LoadCompletedAds();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка инициализации страницы: {ex.Message}", "Ошибка загрузки");
            }
        }

        private async void LoadCompletedAds()
        {
            if (_isLoading) return;

            _isLoading = true;
            ShowLoadingState("Загрузка завершенных объявлений...");

            try
            {
                var completedAds = await _context.ads_data
                    .Include(a => a.city)
                    .Include(a => a.category1)
                    .Include(a => a.type)
                    .Include(a => a.status)
                    .Where(a => a.user_id == AuthService.CurrentUser.id && a.ad_status_id == 2)
                    .OrderByDescending(a => a.ad_post_date)
                    .ToListAsync();

                _totalProfit = completedAds
                    .Where(ad => ad.profit.HasValue)
                    .Sum(ad => ad.profit.Value);

                var adsWithDetails = completedAds.Select(ad => new
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
                    ad.profit, 
                    ad.user_id,
                    ad.ad_image_path,

                    City = ad.city,
                    Category = ad.category1,
                    AdType = ad.type,
                    AdStatus = ad.status,

                    HasImage = !string.IsNullOrEmpty(ad.ad_image_path),
                    ImageSource = LoadImageFromPath(ad.ad_image_path),

                    ProfitAmount = ad.profit.HasValue ? ad.profit.Value : ad.price, 

                    StatusColor = new SolidColorBrush(Color.FromRgb(40, 167, 69))
                }).ToList();

                Dispatcher.Invoke(() =>
                {
                    itemsCompletedAds.ItemsSource = adsWithDetails;
                    UpdateUIState(adsWithDetails.Count, _totalProfit);
                    lblStatus.Text = "Данные успешно загружены";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    ShowErrorMessage($"Не удалось загрузить завершенные объявления: {ex.Message}", "Ошибка загрузки");
                    UpdateUIState(0, 0);
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

            if (System.IO.Path.IsPathRooted(imagePath))
                return imagePath;

            string projectDirectory = GetProjectDirectory();
            string cleanPath = imagePath.TrimStart('\\', '/');
            string fullPath = System.IO.Path.Combine(projectDirectory, "Images", cleanPath);

            return fullPath;
        }

        private string FindImageInAlternativeLocations(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return null;

            string cleanPath = imagePath.TrimStart('\\', '/');

            string[] possiblePaths = {
                System.IO.Path.Combine(GetProjectDirectory(), "Images", cleanPath),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", cleanPath),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                           "Golovin_Vibornov_422", "Images", cleanPath),
                System.IO.Path.Combine(GetProjectDirectory(), "Images", "ads", System.IO.Path.GetFileName(cleanPath)),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "ads", System.IO.Path.GetFileName(cleanPath))
            };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine($"Изображение найдено: {path}");
                    return path;
                }
            }

            return null;
        }

        private string GetProjectDirectory()
        {
            try
            {
                string executablePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string binDebugDirectory = System.IO.Path.GetDirectoryName(executablePath);

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

        private void UpdateUIState(int adsCount, decimal totalProfit)
        {
            lblCount.Text = $"Завершенных объявлений: {adsCount}";

            string formattedProfit = totalProfit.ToString("N0");
            lblTotalProfit.Text = $"Общая прибыль: {formattedProfit}₽";
            lblProfitInfo.Text = $"Общая прибыль: {formattedProfit}₽";

            emptyStatePanel.Visibility = adsCount == 0 ? Visibility.Visible : Visibility.Collapsed;
            itemsCompletedAds.Visibility = adsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowLoadingState(string message)
        {
            loadingPanel.Visibility = Visibility.Visible;
            lblLoading.Text = message;
            itemsCompletedAds.IsEnabled = false;
        }

        private void HideLoadingState()
        {
            loadingPanel.Visibility = Visibility.Collapsed;
            itemsCompletedAds.IsEnabled = true;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadCompletedAds();
        }

        private void ManageAdsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var managePage = new AdsManagementPage();
                NavigationService.Navigate(managePage);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось открыть страницу управления: {ex.Message}", "Ошибка");
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
                ManageAdsButton_Click(sender, e);
            }
        }

        private void ShowErrorMessage(string message, string title = "Ошибка")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            lblStatus.Text = "Произошла ошибка";
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCompletedAds();
        }
    }
}