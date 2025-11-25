using Golovin_Vibornov_422.services;
using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Golovin_Vibornov_422.Pages
{
    /// <summary>
    /// Логика взаимодействия для AdEditPage.xaml
    /// </summary>
    public partial class AdEditPage : Page
    {
        private AdsDatabaseEntities _context;
        private ads_data _ad;
        private bool _isNew;
        private bool _isLoading = false;

        // Кэш для справочных данных
        private static System.Collections.Generic.List<city> _cachedCities;
        private static System.Collections.Generic.List<category> _cachedCategories;
        private static System.Collections.Generic.List<type> _cachedTypes;
        private static System.Collections.Generic.List<status> _cachedStatuses;
        private static DateTime _lastCacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

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
                ad_status_id = 1 // Активно по умолчанию
            };
        }

        private void InitializeExistingAd(ads_data ad)
        {
            // Создаем контекст только один раз
            _context = new AdsDatabaseEntities();

            // Явно загружаем связанные данные
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
        }

        private async void InitializePage()
        {
            _isLoading = true;
            ShowLoadingState("Загрузка данных...");

            try
            {
                // Для нового объявления создаем контекст
                if (_isNew && _context == null)
                {
                    _context = new AdsDatabaseEntities();
                }

                // Загружаем справочники
                await LoadReferenceData();

                lblTitle.Text = _isNew ? "Создание нового объявления" : "Редактирование объявления";

                // Устанавливаем дату только для нового объявления
                if (_isNew)
                {
                    dpDate.SelectedDate = _ad.ad_post_date;
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
                // Проверяем, нужно ли обновить кэш
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

                    // Сохраняем в кэш
                    _cachedCities = citiesTask.Result;
                    _cachedCategories = categoriesTask.Result;
                    _cachedTypes = typesTask.Result;
                    _cachedStatuses = statusesTask.Result;
                    _lastCacheTime = DateTime.Now;
                }

                // Устанавливаем данные в UI потоке
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

                // Используем Dispatcher для установки значений после загрузки комбобоксов
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        SetComboBoxValueSafely(cmbCity, _ad.city_id);
                        SetComboBoxValueSafely(cmbCategory, _ad.category);
                        SetComboBoxValueSafely(cmbType, _ad.ad_type_id);
                        SetComboBoxValueSafely(cmbStatus, _ad.ad_status_id);

                        CheckStatusForProfitPanel();
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
                // Ждем пока комбобокс заполнится данными
                if (comboBox.Items.Count > 0)
                {
                    comboBox.SelectedValue = value;
                }
                else
                {
                    // Если данные еще не загружены, откладываем установку
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        comboBox.SelectedValue = value;
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            }
        }

        private void CheckStatusForProfitPanel()
        {
            try
            {
                // Показываем панель ввода прибыли только если статус "Завершено" (ID=2)
                if (_ad.ad_status_id == 2)
                {
                    profitPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    profitPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка проверки статуса: {ex.Message}");
            }
        }

        private void cmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoading && cmbStatus.SelectedItem is status selectedStatus)
            {
                // Показываем панель ввода прибыли при выборе статуса "Завершено"
                if (selectedStatus.id == 2)
                {
                    profitPanel.Visibility = Visibility.Visible;
                    // Предзаполняем ценой из объявления, если поле пустое
                    if (string.IsNullOrEmpty(txtProfit.Text) && _ad.price > 0)
                    {
                        txtProfit.Text = ((int)_ad.price).ToString();
                    }
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

                // Собираем данные из формы
                _ad.ad_title = txtTitle.Text.Trim();
                _ad.ad_description = txtDescription.Text.Trim();
                _ad.ad_post_date = dpDate.SelectedDate.Value;
                _ad.city_id = GetSelectedId(cmbCity);
                _ad.category = GetSelectedId(cmbCategory);
                _ad.ad_type_id = GetSelectedId(cmbType);
                _ad.ad_status_id = GetSelectedId(cmbStatus);
                _ad.price = decimal.Parse(txtPrice.Text);

                // Обработка прибыли при завершении объявления
                if (_ad.ad_status_id == 2 && !string.IsNullOrEmpty(txtProfit.Text))
                {
                    await ProcessProfit();
                }

                // Сохранение в базу данных
                if (_isNew)
                {
                    _context.ads_data.Add(_ad);
                }

                await _context.SaveChangesAsync();

                // Очищаем кэш после успешного сохранения
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

        private async Task ProcessProfit()
        {
            if (int.TryParse(txtProfit.Text, out int profitAmount) && profitAmount >= 0)
            {
                try
                {
                    // Ищем существующую запись о прибыли пользователя
                    var profit = await _context.profit
                        .FirstOrDefaultAsync(p => p.user_id == AuthService.CurrentUser.id);

                    if (profit != null)
                    {
                        // Обновляем существующую запись
                        profit.profit1 += profitAmount;
                        _context.Entry(profit).State = EntityState.Modified;
                    }
                    else
                    {
                        // Создаем новую запись
                        profit = new profit
                        {
                            user_id = AuthService.CurrentUser.id,
                            profit1 = profitAmount
                        };
                        _context.profit.Add(profit);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка обработки прибыли: {ex.Message}");
                }
            }
        }

        private int GetSelectedId(ComboBox comboBox)
        {
            return comboBox.SelectedValue != null ? (int)comboBox.SelectedValue : 0;
        }

        private void ClearCache()
        {
            // Очищаем кэш при необходимости
            _cachedCities = null;
            _cachedCategories = null;
            _cachedTypes = null;
            _cachedStatuses = null;
            _lastCacheTime = DateTime.MinValue;
        }

        private bool ValidateInput()
        {
            HideError();

            // Проверка обязательных полей
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                ShowError("Заголовок объявления является обязательным полем. Пожалуйста, введите заголовок.");
                txtTitle.Focus();
                return false;
            }

            if (dpDate.SelectedDate == null)
            {
                ShowError("Дата публикации является обязательным полем. Пожалуйста, выберите дату.");
                dpDate.Focus();
                return false;
            }

            if (dpDate.SelectedDate > DateTime.Today)
            {
                ShowError("Дата публикации не может быть в будущем. Пожалуйста, выберите корректную дату.");
                dpDate.Focus();
                return false;
            }

            if (cmbCity.SelectedItem == null)
            {
                ShowError("Город является обязательным полем. Пожалуйста, выберите город из списка.");
                cmbCity.Focus();
                return false;
            }

            if (cmbCategory.SelectedItem == null)
            {
                ShowError("Категория является обязательным полем. Пожалуйста, выберите категорию из списка.");
                cmbCategory.Focus();
                return false;
            }

            if (cmbType.SelectedItem == null)
            {
                ShowError("Тип объявления является обязательным полем. Пожалуйста, выберите тип из списка.");
                cmbType.Focus();
                return false;
            }

            if (cmbStatus.SelectedItem == null)
            {
                ShowError("Статус является обязательным полем. Пожалуйста, выберите статус из списка.");
                cmbStatus.Focus();
                return false;
            }

            // Проверка цены
            if (!decimal.TryParse(txtPrice.Text, out decimal price) || price < 0)
            {
                ShowError("Цена должна быть положительным числом. Пожалуйста, введите корректное значение цены.");
                txtPrice.Focus();
                return false;
            }

            // Проверка прибыли при завершенном статусе
            if (cmbStatus.SelectedItem is status status && status.id == 2)
            {
                if (string.IsNullOrWhiteSpace(txtProfit.Text))
                {
                    ShowError("Для завершенного объявления необходимо указать полученную сумму.");
                    txtProfit.Focus();
                    return false;
                }

                if (!int.TryParse(txtProfit.Text, out int profit) || profit < 0)
                {
                    ShowError("Полученная сумма должна быть целым неотрицательным числом. Пожалуйста, введите корректное значение.");
                    txtProfit.Focus();
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
            // Подтверждение отмены при наличии изменений
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
            // Простая проверка на наличие изменений
            if (_isNew) return true;

            return txtTitle.Text != _ad.ad_title ||
                   txtDescription.Text != _ad.ad_description ||
                   dpDate.SelectedDate != _ad.ad_post_date ||
                   (cmbCity.SelectedValue != null && (int)cmbCity.SelectedValue != _ad.city_id) ||
                   (cmbCategory.SelectedValue != null && (int)cmbCategory.SelectedValue != _ad.category) ||
                   (cmbType.SelectedValue != null && (int)cmbType.SelectedValue != _ad.ad_type_id) ||
                   (cmbStatus.SelectedValue != null && (int)cmbStatus.SelectedValue != _ad.ad_status_id) ||
                   (decimal.TryParse(txtPrice.Text, out decimal currentPrice) && currentPrice != _ad.price);
        }

        // Обработчики для улучшения UX
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

        // Валидация ввода чисел
        private void txtPrice_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только цифры и точку
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c) && c != '.')
                {
                    e.Handled = true;
                    return;
                }
            }

            // Проверяем, что точка только одна
            var textBox = (TextBox)sender;
            if (e.Text == "." && textBox.Text.Contains('.'))
            {
                e.Handled = true;
            }
        }

        private void txtProfit_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только цифры для поля прибыли
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        // Обработчик выхода со страницы
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // Аккуратно освобождаем ресурсы
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