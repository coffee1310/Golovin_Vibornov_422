using Golovin_Vibornov_422.services;
using Microsoft.Win32;
using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Golovin_Vibornov_422.Pages
{
    /// <summary>
    /// Логика страницы для управления услугами
    /// </summary>
    /// 
    public partial class AdEditPage : Page
    {
        private AdsDatabaseEntities _context;
        private ads_data _ad;
        private bool _isNew;
        private bool _isLoading = false;
        private string _selectedImagePath;
        private string _currentImagePath;

        private static System.Collections.Generic.List<city> _cachedCities;
        private static System.Collections.Generic.List<category> _cachedCategories;
        private static System.Collections.Generic.List<type> _cachedTypes;
        private static System.Collections.Generic.List<status> _cachedStatuses;
        private static DateTime _lastCacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

        private bool _imageRemoved = false;

        private const int COMPLETED_STATUS_ID = 2; 

        public event Action AdSaved;

        public AdEditPage()
        {
            InitializeComponent();
            _isNew = true;
            InitializeNewAd();
            InitializePage();
        }

        public AdEditPage(ads_data ad)
        {
            InitializeComponent();
            _isNew = false;
            InitializeExistingAd(ad);
            InitializePage();
            if (!_isNew)
            {
                LoadAdData();
            }
        }

        private void InitializeNewAd()
        {
            _ad = new ads_data
            {
                user_id = AuthService.CurrentUser.id,
                ad_post_date = DateTime.Today,
                ad_status_id = 1, 
                profit = null 
            };
        }

        private void InitializeExistingAd(ads_data ad)
        {
            _context = new AdsDatabaseEntities();
            _ad = _context.ads_data
                .Include(a => a.city)
                .Include(a => a.category1)
                .Include(a => a.type)
                .Include(a => a.status)
                .FirstOrDefault(a => a.id == ad.id);

            if (_ad == null)
            {
                _isNew = true;
                InitializeNewAd();
            }
            else
            {
                _currentImagePath = _ad.ad_image_path;
            }
        }

        private async void InitializePage()
        {
            _isLoading = true;
            ShowLoadingState("Загрузка данных...");

            try
            {
                if (_isNew && _context == null)
                {
                    _context = new AdsDatabaseEntities();
                }

                await LoadReferenceData();

                lblTitle.Text = _isNew ? "Создание нового объявления" : "Редактирование объявления";

                if (_isNew)
                {
                    dpDate.SelectedDate = _ad.ad_post_date;
                }

                if (!_isNew && !string.IsNullOrEmpty(_currentImagePath))
                {
                    LoadImagePreview(_currentImagePath);
                }

                if (!_isNew && _ad.profit.HasValue)
                {
                    txtProfit.Text = _ad.profit.Value.ToString("F0");
                }

                HideLoadingState();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка инициализации: {ex.Message}");
                HideLoadingState();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task LoadReferenceData()
        {
            try
            {
                bool shouldRefreshCache = _cachedCities == null ||
                                         _cachedCategories == null ||
                                         _cachedTypes == null ||
                                         _cachedStatuses == null ||
                                         (DateTime.Now - _lastCacheTime) > CacheDuration;

                if (shouldRefreshCache)
                {
                    var citiesTask = _context.city.AsNoTracking().ToListAsync();
                    var categoriesTask = _context.category.AsNoTracking().ToListAsync();
                    var typesTask = _context.type.AsNoTracking().ToListAsync();
                    var statusesTask = _context.status.AsNoTracking().ToListAsync();

                    await Task.WhenAll(citiesTask, categoriesTask, typesTask, statusesTask);

                    _cachedCities = citiesTask.Result;
                    _cachedCategories = categoriesTask.Result;
                    _cachedTypes = typesTask.Result;
                    _cachedStatuses = statusesTask.Result;
                    _lastCacheTime = DateTime.Now;
                }

                Dispatcher.Invoke(() =>
                {
                    SetComboBoxData(cmbCity, _cachedCities, "id", "city1");
                    SetComboBoxData(cmbCategory, _cachedCategories, "id", "name");
                    SetComboBoxData(cmbType, _cachedTypes, "id", "type1");
                    SetComboBoxData(cmbStatus, _cachedStatuses, "id", "status1");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    ShowError($"Ошибка загрузки справочников: {ex.Message}");
                });
            }
        }

        private void SetComboBoxData(ComboBox comboBox, System.Collections.IEnumerable items, string valuePath, string displayPath)
        {
            comboBox.ItemsSource = items;
            comboBox.SelectedValuePath = valuePath;
            comboBox.DisplayMemberPath = displayPath;
        }

        private void LoadAdData()
        {
            try
            {
                txtTitle.Text = _ad.ad_title ?? "";
                txtDescription.Text = _ad.ad_description ?? "";

                if (_ad.ad_post_date != DateTime.MinValue)
                {
                    dpDate.SelectedDate = _ad.ad_post_date;
                }
                else
                {
                    dpDate.SelectedDate = DateTime.Today;
                }

                txtPrice.Text = _ad.price.ToString("F2");

                if (_ad.profit.HasValue)
                {
                    txtProfit.Text = _ad.profit.Value.ToString("F0");
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        SetComboBoxValueSafely(cmbCity, _ad.city_id);
                        SetComboBoxValueSafely(cmbCategory, _ad.category);
                        SetComboBoxValueSafely(cmbType, _ad.ad_type_id);
                        SetComboBoxValueSafely(cmbStatus, _ad.ad_status_id);

                        UpdateProfitPanelVisibility();
                    }
                    catch (Exception ex)
                    {
                        ShowError($"Ошибка установки значений: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка загрузки данных объявления: {ex.Message}");
            }
        }

        private void SetComboBoxValueSafely(ComboBox comboBox, object value)
        {
            if (value != null && Convert.ToInt32(value) > 0)
            {
                if (comboBox.Items.Count > 0)
                {
                    comboBox.SelectedValue = value;
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        comboBox.SelectedValue = value;
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            }
        }

        private void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Изображения (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|Все файлы (*.*)|*.*",
                    Title = "Выберите изображение для объявления",
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var selectedFile = openFileDialog.FileName;

                    var fileInfo = new FileInfo(selectedFile);
                    if (fileInfo.Length > 5 * 1024 * 1024)
                    {
                        ShowError("Размер изображения не должен превышать 5 МБ");
                        return;
                    }

                    _selectedImagePath = selectedFile;
                    LoadImagePreview(_selectedImagePath);

                    lblImageInfo.Text = $"Файл: {Path.GetFileName(selectedFile)}\nРазмер: {(fileInfo.Length / 1024.0):F1} КБ";
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при выборе изображения: {ex.Message}");
            }
        }

        private void RemoveImageButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedImagePath = null;
            _imageRemoved = true;
            imgPreview.Source = new BitmapImage(new Uri("pack://application:,,,/Images/no-image.jpg"));
            lblImageInfo.Text = "Изображение не выбрано";
        }


        private void LoadImagePreview(string imagePath)
        {
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
                    imgPreview.Source = bitmap;

                    var fileInfo = new FileInfo(fullPath);
                    lblImageInfo.Text = $"Файл: {Path.GetFileName(fullPath)}\nРазмер: {(fileInfo.Length / 1024.0):F1} КБ";
                }
                else
                {
                    imgPreview.Source = new BitmapImage(new Uri("pack://application:,,,/Images/no-image.jpg"));
                    lblImageInfo.Text = "Изображение не найдено";
                    System.Diagnostics.Debug.WriteLine($"Файл изображения не найден: {fullPath}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка загрузки изображения: {ex.Message}");
                imgPreview.Source = new BitmapImage(new Uri("pack://application:,,,/Images/no-image.jpg"));
                lblImageInfo.Text = "Ошибка загрузки изображения";
            }
        }

        private string SaveImageToFolder(string sourceImagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceImagePath) || !File.Exists(sourceImagePath))
                    return null;

                string projectDirectory = GetProjectDirectory();
                string imagesFolder = Path.Combine(projectDirectory, "Images", "ads");

                if (!Directory.Exists(imagesFolder))
                {
                    Directory.CreateDirectory(imagesFolder);
                }

                string fileExtension = Path.GetExtension(sourceImagePath);
                string fileName = $"ad_{DateTime.Now:yyyyMMddHHmmssfff}{fileExtension}";
                string destinationPath = Path.Combine(imagesFolder, fileName);

                File.Copy(sourceImagePath, destinationPath, true);

                return Path.Combine("ads", fileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения изображения: {ex.Message}");
                return null;
            }
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

        private void DeleteOldImage(string oldImagePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(oldImagePath))
                {
                    string fullPath = GetFullImagePath(oldImagePath);
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }

                    string alternativePath = FindImageInAlternativeLocations(oldImagePath);
                    if (!string.IsNullOrEmpty(alternativePath) && File.Exists(alternativePath))
                    {
                        File.Delete(alternativePath);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления старого изображения: {ex.Message}");
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

        private void cmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            UpdateProfitPanelVisibility();

            if (cmbStatus.SelectedItem is status selectedStatus && selectedStatus.id == COMPLETED_STATUS_ID)
            {
                if (string.IsNullOrEmpty(txtProfit.Text) && decimal.TryParse(txtPrice.Text, out decimal price))
                {
                    txtProfit.Text = ((int)price).ToString();
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    txtProfit.Focus();
                    txtProfit.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else if (cmbStatus.SelectedItem is status newStatus && newStatus.id != COMPLETED_STATUS_ID)
            {
                if (!string.IsNullOrEmpty(txtProfit.Text) && int.TryParse(txtProfit.Text, out int profit) && profit > 0)
                {
                    txtPrice.Text = profit.ToString("F2");
                }
            }
        }

        private void UpdateProfitPanelVisibility()
        {
            if (cmbStatus.SelectedItem is status selectedStatus)
            {
                if (selectedStatus.id == COMPLETED_STATUS_ID)
                {
                    profitPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    profitPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
                return;

            try
            {
                ShowLoadingState("Сохранение данных...");
                btnSave.IsEnabled = false;

                _ad.ad_title = txtTitle.Text.Trim();
                _ad.ad_description = txtDescription.Text.Trim();
                _ad.ad_post_date = dpDate.SelectedDate.Value;
                _ad.city_id = GetSelectedId(cmbCity);
                _ad.category = GetSelectedId(cmbCategory);
                _ad.ad_type_id = GetSelectedId(cmbType);
                _ad.ad_status_id = GetSelectedId(cmbStatus);

                if (cmbStatus.SelectedItem is status selectedStatus && selectedStatus.id == COMPLETED_STATUS_ID)
                {
                    _ad.price = decimal.Parse(txtPrice.Text);

                    if (int.TryParse(txtProfit.Text, out int profitAmount) && profitAmount >= 0)
                    {
                        _ad.profit = profitAmount;
                    }
                    else
                    {
                        _ad.profit = null;
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(txtProfit.Text) && int.TryParse(txtProfit.Text, out int profitFromField) && profitFromField > 0)
                    {
                        _ad.price = profitFromField;
                    }
                    else
                    {
                        _ad.price = decimal.Parse(txtPrice.Text);
                    }

                    _ad.profit = null;
                }

                if (!string.IsNullOrEmpty(_selectedImagePath))
                {
                    if (!string.IsNullOrEmpty(_currentImagePath))
                    {
                        DeleteOldImage(_currentImagePath);
                    }

                    string savedImagePath = SaveImageToFolder(_selectedImagePath);
                    if (!string.IsNullOrEmpty(savedImagePath))
                    {
                        _ad.ad_image_path = savedImagePath;
                    }
                    _imageRemoved = false; 
                }
                else if (_imageRemoved)
                {
                    if (!string.IsNullOrEmpty(_currentImagePath))
                    {
                        DeleteOldImage(_currentImagePath);
                    }
                    _ad.ad_image_path = null;
                    _imageRemoved = false; 
                }

                if (_isNew)
                {
                    _context.ads_data.Add(_ad);
                }
                else
                {
                    _context.Entry(_ad).State = EntityState.Modified;
                }

                await _context.SaveChangesAsync();

                ClearCache();
                AdSaved?.Invoke();

                MessageBox.Show(
                    _isNew ? "Объявление успешно создано!" : "Изменения успешно сохранены!",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                NavigationService?.GoBack();
            }
            catch (DbUpdateException ex)
            {
                string errorMessage = $"Ошибка сохранения в базу данных: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\nДетали: {ex.InnerException.Message}";
                }
                ShowError(errorMessage);
            }
            catch (Exception ex)
            {
                ShowError($"Неожиданная ошибка при сохранении: {ex.Message}");
            }
            finally
            {
                HideLoadingState();
                btnSave.IsEnabled = true;
            }
        }

        private int GetSelectedId(ComboBox comboBox)
        {
            return comboBox.SelectedValue != null ? (int)comboBox.SelectedValue : 0;
        }

        private void ClearCache()
        {
            _cachedCities = null;
            _cachedCategories = null;
            _cachedTypes = null;
            _cachedStatuses = null;
            _lastCacheTime = DateTime.MinValue;
        }

        private bool ValidateInput()
        {
            HideError();

            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                ShowError("Заголовок объявления является обязательным полем.");
                txtTitle.Focus();
                return false;
            }

            if (dpDate.SelectedDate == null)
            {
                ShowError("Дата публикации является обязательным полем.");
                dpDate.Focus();
                return false;
            }

            if (dpDate.SelectedDate > DateTime.Today)
            {
                ShowError("Дата публикации не может быть в будущем.");
                dpDate.Focus();
                return false;
            }

            if (cmbCity.SelectedItem == null)
            {
                ShowError("Город является обязательным полем.");
                cmbCity.Focus();
                return false;
            }

            if (cmbCategory.SelectedItem == null)
            {
                ShowError("Категория является обязательным полем.");
                cmbCategory.Focus();
                return false;
            }

            if (cmbType.SelectedItem == null)
            {
                ShowError("Тип объявления является обязательным полем.");
                cmbType.Focus();
                return false;
            }

            if (cmbStatus.SelectedItem == null)
            {
                ShowError("Статус является обязательным полем.");
                cmbStatus.Focus();
                return false;
            }

            if (cmbStatus.SelectedItem is status selectedStatus && selectedStatus.id == COMPLETED_STATUS_ID)
            {
                if (string.IsNullOrWhiteSpace(txtProfit.Text))
                {
                    ShowError("Для завершенного объявления необходимо указать полученную сумму.");
                    txtProfit.Focus();
                    return false;
                }

                if (!int.TryParse(txtProfit.Text, out int profit) || profit < 0)
                {
                    ShowError("Прибыль должна быть целым неотрицательным числом.");
                    txtProfit.Focus();
                    return false;
                }

                if (profit == 0)
                {
                    var result = MessageBox.Show(
                        "Вы указали нулевую прибыль. Вы уверены?",
                        "Подтверждение",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                    {
                        txtProfit.Focus();
                        return false;
                    }
                }

                if (!decimal.TryParse(txtPrice.Text, out decimal price) || price < 0)
                {
                    ShowError("Цена должна быть положительным числом.");
                    txtPrice.Focus();
                    return false;
                }
            }
            else
            {
                if (!decimal.TryParse(txtPrice.Text, out decimal price) || price < 0)
                {
                    ShowError("Цена должна быть положительным числом.");
                    txtPrice.Focus();
                    return false;
                }
            }

            return true;
        }

        private void ShowLoadingState(string message)
        {
            btnSave.Content = "⏳ Сохранение...";
        }

        private void HideLoadingState()
        {
            btnSave.Content = "💾 Сохранить";
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (HasUnsavedChanges())
            {
                var result = MessageBox.Show(
                    "У вас есть несохраненные изменения. Вы уверены, что хотите отменить?",
                    "Подтверждение отмены",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);

                if (result == MessageBoxResult.No)
                    return;
            }

            NavigationService?.GoBack();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            CancelButton_Click(sender, e);
        }

        private bool HasUnsavedChanges()
        {
            if (_isNew) return true;

            bool hasChanges = txtTitle.Text != _ad.ad_title ||
                   txtDescription.Text != _ad.ad_description ||
                   dpDate.SelectedDate != _ad.ad_post_date ||
                   (cmbCity.SelectedValue != null && (int)cmbCity.SelectedValue != _ad.city_id) ||
                   (cmbCategory.SelectedValue != null && (int)cmbCategory.SelectedValue != _ad.category) ||
                   (cmbType.SelectedValue != null && (int)cmbType.SelectedValue != _ad.ad_type_id) ||
                   (cmbStatus.SelectedValue != null && (int)cmbStatus.SelectedValue != _ad.ad_status_id) ||
                   (decimal.TryParse(txtPrice.Text, out decimal currentPrice) && currentPrice != _ad.price);

            if (_ad.profit.HasValue)
            {
                if (int.TryParse(txtProfit.Text, out int currentProfit))
                {
                    hasChanges = hasChanges || currentProfit != _ad.profit.Value;
                }
                else
                {
                    hasChanges = hasChanges || !string.IsNullOrEmpty(txtProfit.Text);
                }
            }
            else
            {
                hasChanges = hasChanges || !string.IsNullOrEmpty(txtProfit.Text);
            }

            bool imageChanged = _selectedImagePath != null ||
                               (_selectedImagePath == null && !string.IsNullOrEmpty(_currentImagePath));

            return hasChanges || imageChanged;
        }

        private void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            HideError();
            if (sender is Control control)
            {
                control.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            }
        }

        private void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                control.BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204));
            }
        }

        private void txtPrice_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c) && c != '.')
                {
                    e.Handled = true;
                    return;
                }
            }

            var textBox = (TextBox)sender;
            if (e.Text == "." && textBox.Text.Contains('.'))
            {
                e.Handled = true;
            }
        }

        private void txtProfit_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isNew)
            {
                txtTitle.Focus();
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _context?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при освобождении контекста: {ex.Message}");
            }
        }
    }
}