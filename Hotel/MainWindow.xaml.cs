using System;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using Polly;
using System.ComponentModel;
using System.Windows.Controls;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Windows.Media;

namespace Hotel
{
    public partial class MainWindow : System.Windows.Window
    {
        private const string ConnectionString = "Host=172.20.7.53;Database=db3996_17;Username=root;Password=root;Timeout=300";
        private readonly int _currentUserId;
        private bool _isEditMode;
        private bool _isDataChanged;
        private string _userRole;

        public MainWindow(int userId)
        {
            InitializeComponent();
            _currentUserId = userId;
            InitializeValidation();
            Loaded += MainWindow_Loaded;
        }

        private void InitializeValidation()
        {
            txtEmail.LostFocus += ValidateEmailHandler;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUserData();
        }

        private async void LoadUserData()
        {
            try
            {
                await ExecuteWithRetryPolicy(LoadUserDataAsync);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteWithRetryPolicy(Func<Task> action)
        {
            var retryPolicy = Policy
                .Handle<NpgsqlException>(ex => ex.IsTransient)
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            await retryPolicy.ExecuteAsync(action);
        }

        private async Task LoadUserDataAsync()
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                await connection.OpenAsync();

                var userData = await GetUserData(connection);
                if (userData.UserId == 0)
                {
                    MessageBox.Show("Пользователь не найден", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var jobTitle = await GetJobTitle(connection, userData.JobId);
                _userRole = DetermineUserRole(userData.JobId);

                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateUI(userData, jobTitle);
                    UpdateUIForRole();
                });
            }
        }

        private async Task<(int UserId, string Email, string FirstName, string LastName, int JobId)> GetUserData(NpgsqlConnection connection)
        {
            const string query = @"SELECT id, email, firstname, surname, id_job 
                           FROM hotel.user 
                           WHERE id = @userId";

            using (var cmd = new NpgsqlCommand(query, connection))
            {
                cmd.Parameters.Add("@userId", NpgsqlDbType.Integer).Value = _currentUserId;
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return (
                            reader.GetInt32(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.GetInt32(4)
                        );
                    }
                    return (0, null, null, null, 0);
                }
            }
        }

        private string DetermineUserRole(int jobId)
        {
            return jobId switch
            {
                4 => "Администратор",
                1 => "Менеджер",
                2 => "Менеджер",
                _ => "Гость"
            };
        }

        private async Task<string> GetJobTitle(NpgsqlConnection connection, int jobId)
        {
            if (jobId <= 0)
                return FormatJobTitle("Гость", new List<string>());

            try
            {
                const string query = @"SELECT responsibilities FROM hotel.job_title WHERE id = @jobId";
                using (var cmd = new NpgsqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@jobId", jobId);
                    var json = (await cmd.ExecuteScalarAsync())?.ToString();

                    if (string.IsNullOrWhiteSpace(json))
                        return FormatJobTitle("Сотрудник", new List<string>());

                    try
                    {
                        var jobInfo = JObject.Parse(json);

                        // Извлечение названия должности
                        var title = jobInfo["title"]?.ToString()
                                  ?? jobInfo["Title"]?.ToString()
                                  ?? "Должность не указана";

                        // Извлечение обязанностей
                        var responsibilities = jobInfo["обязанности"]?.ToObject<List<string>>()
                                             ?? jobInfo["Обязанности"]?.ToObject<List<string>>()
                                             ?? new List<string>();

                        return FormatJobTitle(title, responsibilities);
                    }
                    catch
                    {
                        return FormatJobTitle("Ошибка формата данных", new List<string>());
                    }
                }
            }
            catch
            {
                return FormatJobTitle("Ошибка загрузки", new List<string>());
            }
        }

        private string FormatJobTitle(string title, List<string> responsibilities)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ДОЛЖНОСТЬ: {title.Trim()}");

            if (responsibilities.Any())
            {
                sb.AppendLine("ОБЯЗАННОСТИ:");
                foreach (var responsibility in responsibilities)
                {
                    sb.AppendLine($"• {responsibility.Trim()}");
                }
            }
            else
            {
                sb.AppendLine("ОБЯЗАННОСТИ: не указаны");
            }

            return sb.ToString();
        }

        private void UpdateUIForRole()
        {
            ResetAllBorders();

            switch (_userRole)
            {
                case "Администратор":
                    SetAdminAccess();
                    break;
                case "Менеджер":
                    SetManagerAccess();
                    break;
                default:
                    SetGuestAccess();
                    break;
            }
        }

        private void SetAdminAccess()
        {
            AddRoomBorder.Opacity = 1;
            AddRoomBorder.IsEnabled = true;
            AddRoomBorder.Cursor = Cursors.Hand;

            ViewEmployeesBorder.Opacity = 1;
            ViewEmployeesBorder.IsEnabled = true;
            ViewEmployeesBorder.Cursor = Cursors.Hand;
        }

        private void SetManagerAccess()
        {
            AddRoomBorder.Opacity = 1;
            AddRoomBorder.IsEnabled = true;
            AddRoomBorder.Cursor = Cursors.Hand;

            ViewEmployeesBorder.Opacity = 0.5;
            ViewEmployeesBorder.IsEnabled = false;
            ViewEmployeesBorder.Cursor = Cursors.Arrow;
        }

        private void SetGuestAccess()
        {
            AddRoomBorder.Opacity = 0.5;
            AddRoomBorder.IsEnabled = false;
            AddRoomBorder.Cursor = Cursors.Arrow;

            ViewEmployeesBorder.Opacity = 0.5;
            ViewEmployeesBorder.IsEnabled = false;
            ViewEmployeesBorder.Cursor = Cursors.Arrow;
        }

        private void ResetAllBorders()
        {
            var borders = new[] { AddRoomBorder, BookRoomBorder, CheckRoomBorder, ViewEmployeesBorder };
            foreach (var border in borders)
            {
                border.Opacity = 1;
                border.IsEnabled = true;
                border.Cursor = Cursors.Hand;
            }
        }

        // Удалить метод ShowAccessDeniedMessage и все его вызовы


        private void UpdateUI(
            (int UserId, string Email, string FirstName, string LastName, int JobId) userData,
            string jobTitle)
        {
            txtId.Text = userData.UserId.ToString();
            txtFirstName.Text = userData.FirstName;
            txtLastName.Text = userData.LastName;
            txtEmail.Text = userData.Email;
            txtJobTitle.Text = jobTitle;

            ResetEditState();
        }




        // Вспомогательный метод для поиска дочерних элементов
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                {
                    yield return t;
                }

                foreach (T childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }

        private void EnableAdminAccess()
        {
            // Админ получает полный доступ — все кнопки активны и не серые
            AddRoomBorder.IsEnabled = true;
            BookRoomBorder.IsEnabled = true;
            CheckRoomBorder.IsEnabled = true;
            ViewEmployeesBorder.IsEnabled = true;

            // Убираем серый фон (если вдруг остался)
            AddRoomBorder.Background = Brushes.Transparent;
            BookRoomBorder.Background = Brushes.Transparent;
            CheckRoomBorder.Background = Brushes.Transparent;
            ViewEmployeesBorder.Background = Brushes.Transparent;
        }

       

        private void AddRoom_Click(object sender, MouseButtonEventArgs e)
        {

            if (_userRole != "Администратор" && _userRole != "Менеджер")
            {
                ShowAccessDeniedMessage();
                return;
            }

            var addRoomWindow = new Window.AddRoomWindow(_currentUserId);
            addRoomWindow.Show();
            Close();
        }

        private void BookRoom_Click(object sender, MouseButtonEventArgs e)
        {
            // Бронирование доступно всем
            var bookingWindow = new Window.BookingRoomWindow(_currentUserId);
            bookingWindow.Show();
            Close();
        }

        private void CheckRoom_Click(object sender, MouseButtonEventArgs e)
        {
            // Проверка доступна всем
            var checkRoomWindow = new Window.ChekingBookingWindow(_currentUserId);
            checkRoomWindow.Show();
            Close();
        }

        private void ViewEmployees_Click(object sender, MouseButtonEventArgs e)
        {
            if (_userRole == "Администратор" || _currentUserId == 4)
            {
                var employeesWindow = new Window.EmployeesRegistrationWindow(_currentUserId);
                employeesWindow.Show();
                Close();
            }
            else
            {
                ShowAccessDeniedMessage();
            }
        }

        private void btnEditData_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditMode)
            {
                if (ValidateAllFields())
                {
                    SaveChanges();
                }
            }
            else
            {
                EnableEditMode();
            }
        }

        private void EnableEditMode()
        {
            _isEditMode = true;
            UpdateEditState();
            _isDataChanged = false;
        }

        private void ResetEditState()
        {
            _isEditMode = false;
            UpdateEditState();
            _isDataChanged = false;
        }

        private void UpdateEditState()
        {
            txtFirstName.IsReadOnly = !_isEditMode;
            txtLastName.IsReadOnly = !_isEditMode;
            txtEmail.IsReadOnly = !_isEditMode;
            txtJobTitle.IsReadOnly = true;
            btnEditData.Content = _isEditMode ? "Сохранить изменения" : "Изменить данные";
        }


        private bool ValidateAllFields()
        {
            return ValidateEmail();
        }

        private void ValidateEmailHandler(object sender, RoutedEventArgs e)
        {
            ValidateEmail();
        }

        private bool ValidateEmail()
        {
            var email = txtEmail.Text;
            var isValid = !string.IsNullOrWhiteSpace(email) &&
                         new EmailAddressAttribute().IsValid(email);

            txtEmail.Background = isValid ? Brushes.White : Brushes.LightPink;
            return isValid;
        }

        private async void SaveChanges()
        {
            try
            {
                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            await UpdateUserData(connection);
                            await transaction.CommitAsync();
                            MessageBox.Show("Данные успешно обновлены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadUserData();
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка соединения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdateUserData(NpgsqlConnection connection)
        {
            const string query = @"UPDATE hotel.user 
                                SET firstname = @firstName,
                                    surname = @lastName,
                                    email = @email 
                                WHERE id = @userId";

            using (var cmd = new NpgsqlCommand(query, connection))
            {
                cmd.Parameters.Add("@firstName", NpgsqlDbType.Varchar, 50).Value = txtFirstName.Text;
                cmd.Parameters.Add("@lastName", NpgsqlDbType.Varchar, 50).Value = txtLastName.Text;
                cmd.Parameters.Add("@email", NpgsqlDbType.Varchar, 100).Value = txtEmail.Text;
                cmd.Parameters.Add("@userId", NpgsqlDbType.Integer).Value = _currentUserId;

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isEditMode) _isDataChanged = true;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_isDataChanged)
            {
                var result = MessageBox.Show("Есть несохраненные изменения. Закрыть окно?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                e.Cancel = result == MessageBoxResult.No;
            }
        }

        private void MinimizeWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void ShowAccessDeniedMessage()
        {
            MessageBox.Show("У вас недостаточно прав для выполнения этого действия.",
                          "Ограничение доступа",
                          MessageBoxButton.OK,
                          MessageBoxImage.Warning);
        }
    }
}