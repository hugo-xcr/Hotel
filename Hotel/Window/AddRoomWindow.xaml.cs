using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Npgsql;
using System.Windows.Media;
using System.Data;
using System.Threading.Tasks;
using NpgsqlTypes;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Hotel.Window
{
    public partial class AddRoomWindow : System.Windows.Window
    {
        private const string ConnectionString = "Host=172.20.7.53;Database=db3996_17;Username=root;Password=root;Timeout=300";
        private readonly int _currentUserId;
        private bool _isDataDirty;
        private DataTable _dataTable;
        private DataTable _servicesDataTable;
        private List<string> _roomStatuses = new List<string> { "свободен", "занят", "на ремонте" };
        private string _userRole;

        public AddRoomWindow(int currentUserId)
        {
            InitializeComponent();
            _currentUserId = currentUserId;
            Loaded += async (s, e) =>
            {
                await LoadUserRoleAsync();
                UpdateUIForRole();
                await LoadRoomsDataAsync();
                await LoadServicesDataAsync();
                cmbStatus.ItemsSource = _roomStatuses;
                cmbStatus.SelectedIndex = 0;
            };
        }

        private async Task LoadUserRoleAsync()
        {
            try
            {
                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();
                    _userRole = await DetermineUserRole(connection);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки роли: {ex.Message}");
                _userRole = "Гость";
            }
        }

        private async Task<string> DetermineUserRole(NpgsqlConnection connection)
        {
            const string query = @"SELECT id_job FROM hotel.user WHERE id = @userId";
            using (var cmd = new NpgsqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("userId", _currentUserId);

                // Используем long вместо int
                long jobId = (long)await cmd.ExecuteScalarAsync();

                return jobId switch
                {
                    4 => "Администратор",
                    1 or 2 => "Менеджер",
                    _ => "Гость"
                };
            }
        }

        private void UpdateUIForRole()
        {
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
            // Активируем все элементы
            AddRoomBorder.Background = Brushes.White;
            AddRoomBorder.Opacity = 1;
            AddRoomBorder.IsEnabled = true;

            ViewEmployeesBorder.Opacity = 1;
            ViewEmployeesBorder.IsEnabled = true;

            // Настройка полей ввода
            UpdateInputFieldsAccess(true);
        }

        private void SetManagerAccess()
        {
            // Блокируем просмотр сотрудников
            ViewEmployeesBorder.Opacity = 0.5;
            ViewEmployeesBorder.IsEnabled = false;

            // Настройка полей ввода
            UpdateInputFieldsAccess(true);
        }

        private void SetGuestAccess()
        {
            // Блокируем всё кроме просмотра
            AddRoomBorder.Opacity = 0.5;
            AddRoomBorder.IsEnabled = false;

            ViewEmployeesBorder.Opacity = 0.5;
            ViewEmployeesBorder.IsEnabled = false;

            // Настройка полей ввода
            UpdateInputFieldsAccess(false);
        }

        private void UpdateInputFieldsAccess(bool canEdit)
        {
            var opacity = canEdit ? 1.0 : 0.5;
            var isEnabled = canEdit;

            txtRoomNumber.IsEnabled = isEnabled;
            txtRoomName.IsEnabled = isEnabled;
            txtRoomTypeAndCapacity.IsEnabled = isEnabled;
            cmbStatus.IsEnabled = isEnabled;
            txtPrice.IsEnabled = isEnabled;
            txtServices.IsEnabled = isEnabled;
            btnShowServices.IsEnabled = isEnabled;
            btnSave.IsEnabled = isEnabled;

            txtRoomNumber.Opacity = opacity;
            txtRoomName.Opacity = opacity;
            txtRoomTypeAndCapacity.Opacity = opacity;
            cmbStatus.Opacity = opacity;
            txtPrice.Opacity = opacity;
            txtServices.Opacity = opacity;
            btnShowServices.Opacity = opacity;
            btnSave.Opacity = opacity;
        }
        private async Task SomeMethod()
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                var cmd = new NpgsqlCommand("SELECT id_job FROM hotel.user WHERE id = @id", connection);
                cmd.Parameters.AddWithValue("id", _currentUserId);

                // Используйте long
                long jobId = (long)await cmd.ExecuteScalarAsync();
            }
        }
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

        private void ResetAllBorders()
        {
            var borders = new[] { ProfileBorder, AddRoomBorder, BookRoomBorder, CheckBookingBorder, ViewEmployeesBorder };

            foreach (var border in borders)
            {
                border.Background = Brushes.Transparent;
                border.IsEnabled = true;
                border.Opacity = 1;
                border.Cursor = Cursors.Hand;


                var textBlock = FindVisualChildren<TextBlock>(border).FirstOrDefault();
                if (textBlock != null)
                {
                    textBlock.Foreground = Brushes.White;
                }
            }
        }



        private void ShowAccessDeniedMessage()
        {
            MessageBox.Show("У вас недостаточно прав для выполнения этого действия.",
                          "Ограничение доступа",
                          MessageBoxButton.OK,
                          MessageBoxImage.Warning);
        }

        private async Task LoadServicesDataAsync()
        {
            try
            {
                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();
                    var cmd = new NpgsqlCommand("SELECT id, name, description, price FROM hotel.type_service", connection);
                    _servicesDataTable = new DataTable();
                    _servicesDataTable.Load(await cmd.ExecuteReaderAsync());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки сервисов: {ex.Message}");
            }
        }


        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_userRole == "Гость")
            {
                ShowAccessDeniedMessage();
                return;
            }

            if (!ValidateFields()) return;

            try
            {
                var match = Regex.Match(txtRoomTypeAndCapacity.Text, @"^(.*?)\s*\((\d+)\s*места?\)$");
                if (!match.Success)
                {
                    MessageBox.Show("Используйте формат: 'Тип (X мест)', например: 'Deluxe (2 места)'");
                    return;
                }

                string roomType = match.Groups[1].Value.Trim();
                int capacity = int.Parse(match.Groups[2].Value);

                // Получаем список ID сервисов
                List<long> serviceIds = new List<long>();
                if (!string.IsNullOrWhiteSpace(txtServices.Text))
                {
                    serviceIds = txtServices.Text.Split(',')
                        .Select(s => long.Parse(s.Trim()))
                        .ToList();
                }

                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            var maxIdCmd = new NpgsqlCommand(
                                "SELECT COALESCE(MAX(id), 0) + 1 FROM hotel.information_room",
                                connection, transaction);
                            var newInfoId = (long)await maxIdCmd.ExecuteScalarAsync();

                            var servicesArray = serviceIds.Any()
                                ? new JArray(serviceIds).ToString()
                                : null;
                            var infoCmd = new NpgsqlCommand(
                                @"INSERT INTO hotel.information_room 
                        (id, status, price_per_night, name, type_seats_room, id_services_all) 
                        VALUES (@id, @status, @price, @name, @type_seats, @services)",
                                connection, transaction);

                            infoCmd.Parameters.AddWithValue("id", newInfoId);
                            infoCmd.Parameters.AddWithValue("status", cmbStatus.SelectedItem.ToString());
                            infoCmd.Parameters.AddWithValue("price", decimal.Parse(txtPrice.Text));
                            infoCmd.Parameters.AddWithValue("name", txtRoomName.Text);
                            infoCmd.Parameters.AddWithValue("type_seats", NpgsqlDbType.Jsonb,
                                $"{{\"type\":\"{roomType}\",\"capacity\":{capacity}}}");

                            if (servicesArray != null)
                            {
                                infoCmd.Parameters.AddWithValue("services", NpgsqlDbType.Jsonb, servicesArray);
                            }
                            else
                            {
                                infoCmd.Parameters.AddWithValue("services", DBNull.Value);
                            }

                            await infoCmd.ExecuteNonQueryAsync();

                            var maxRoomIdCmd = new NpgsqlCommand(
                                "SELECT COALESCE(MAX(id), 0) + 1 FROM hotel.room",
                                connection, transaction);
                            var newRoomId = (long)await maxRoomIdCmd.ExecuteScalarAsync();

                            // 5. Вставляем данные в room
                            var roomCmd = new NpgsqlCommand(
                                @"INSERT INTO hotel.room 
                        (id, room_number, id_information) 
                        VALUES (@id, @num, @infoId)",
                                connection, transaction);

                            roomCmd.Parameters.AddWithValue("id", newRoomId);
                            roomCmd.Parameters.AddWithValue("num", int.Parse(txtRoomNumber.Text));
                            roomCmd.Parameters.AddWithValue("infoId", newInfoId);

                            await roomCmd.ExecuteNonQueryAsync();

                            transaction.Commit();
                            MessageBox.Show("Номер успешно добавлен!");
                            await LoadRoomsDataAsync();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show($"Ошибка при сохранении: {ex.Message}\n\nПодробности: {ex.InnerException?.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private async Task LoadRoomsDataAsync()
        {
            try
            {
                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
SELECT 
    r.room_number,
    ir.name AS room_name,
    ir.status,
    ir.price_per_night,
    (ir.type_seats_room->>'type') || ' (' || (ir.type_seats_room->>'capacity') || ' мест)' AS room_type_capacity,
    COALESCE(rt.rating::text, 'Нет рейтинга') AS rating,
    COALESCE(
        (
            SELECT string_agg(ts.name, ', ')
            FROM jsonb_array_elements_text(ir.id_services_all) AS service_id
            JOIN hotel.type_service ts ON service_id::BIGINT = ts.id
        ),
        'Нет сервисов'
    ) AS services
FROM hotel.room r
JOIN hotel.information_room ir ON r.id_information = ir.id
LEFT JOIN hotel.rating rt ON ir.id_rating = rt.id;
";

                    var adapter = new NpgsqlDataAdapter(query, connection);
                    _dataTable = new DataTable();
                    adapter.Fill(_dataTable);

                    RoomsDataGrid.ItemsSource = _dataTable.DefaultView;

                    // Настройка колонок
                    RoomsDataGrid.Columns.Clear();
                    RoomsDataGrid.AutoGenerateColumns = false;

                    var columns = new[]
                    {
                new DataGridTextColumn { Header = "№", Binding = new Binding("room_number"), Width = 50 },
                new DataGridTextColumn { Header = "Название", Binding = new Binding("room_name"), Width = 120 },
                new DataGridTextColumn { Header = "Тип и места", Binding = new Binding("room_type_capacity"), Width = 150 },
                new DataGridTextColumn { Header = "Статус", Binding = new Binding("status"), Width = 100 },
                new DataGridTextColumn { Header = "Цена/ночь", Binding = new Binding("price_per_night"), Width = 100 },
                new DataGridTextColumn { Header = "Рейтинг", Binding = new Binding("rating"), Width = 100 },
                new DataGridTextColumn { Header = "Сервисы", Binding = new Binding("services"), Width = 200 }
            };

                    foreach (var column in columns)
                    {
                        RoomsDataGrid.Columns.Add(column);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private bool ValidateFields()
        {
            var valid = true;
            ResetFieldColors();

            // Проверка формата "Тип (X мест)"
            if (string.IsNullOrWhiteSpace(txtRoomTypeAndCapacity.Text) ||
                !Regex.IsMatch(txtRoomTypeAndCapacity.Text, @"^.*?\s*\(\d+\s*места?\)$"))
            {
                txtRoomTypeAndCapacity.Background = Brushes.LightPink;
                txtRoomTypeAndCapacity.ToolTip = "Используйте формат: 'Тип (X мест)', например: 'Deluxe (2 места)'";
                valid = false;
            }

            // Остальные проверки
            if (string.IsNullOrWhiteSpace(txtRoomNumber.Text))
            {
                txtRoomNumber.Background = Brushes.LightPink;
                valid = false;
            }
            if (string.IsNullOrWhiteSpace(txtRoomName.Text))
            {
                txtRoomName.Background = Brushes.LightPink;
                valid = false;
            }
            if (!decimal.TryParse(txtPrice.Text, out _))
            {
                txtPrice.Background = Brushes.LightPink;
                valid = false;
            }
            if (!string.IsNullOrWhiteSpace(txtServices.Text) &&
                !txtServices.Text.Split(',').All(s => int.TryParse(s.Trim(), out _)))
            {
                txtServices.Background = Brushes.LightPink;
                valid = false;
            }

            return valid;
        }

        private async Task<bool> CheckServicesExist(List<int> serviceIds, NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM hotel.type_service WHERE id = ANY(@ids)",
                connection, transaction);

            cmd.Parameters.AddWithValue("ids", serviceIds.ToArray());
            var count = (long)await cmd.ExecuteScalarAsync();

            return count == serviceIds.Count;
        }

        private void btnShowServices_Click(object sender, RoutedEventArgs e)
        {
            if (_servicesDataTable == null || _servicesDataTable.Rows.Count == 0)
            {
                MessageBox.Show("Список сервисов не загружен");
                return;
            }

            var servicesWindow = new System.Windows.Window
            {
                Title = "Доступные сервисы (ID, Название, Описание, Цена)",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = true,
                ItemsSource = _servicesDataTable.DefaultView,
                Margin = new Thickness(10)
            };

            servicesWindow.Content = dataGrid;
            servicesWindow.ShowDialog();
        }

        private void ResetFieldColors()
        {
            txtRoomNumber.Background = Brushes.White;
            txtRoomName.Background = Brushes.White;
            txtRoomTypeAndCapacity.Background = Brushes.White;
            txtPrice.Background = Brushes.White;
            txtServices.Background = Brushes.White;
        }

        private void NumberValidation(object sender, TextCompositionEventArgs e) =>
            e.Handled = !int.TryParse(e.Text, out _);

        private void DecimalValidation(object sender, TextCompositionEventArgs e) =>
            e.Handled = !decimal.TryParse(((TextBox)sender).Text + e.Text, out _);

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_isDataDirty)
            {
                var result = MessageBox.Show("Закрыть без сохранения?", "Подтверждение", MessageBoxButton.YesNo);
                e.Cancel = result == MessageBoxResult.No;
            }
        }

        private void Field_Changed(object sender, TextChangedEventArgs e) => _isDataDirty = true;

        private void CloseWindow_MouseDown(object sender, MouseButtonEventArgs e) => Close();
        private void MinimizeWindow_MouseDown(object sender, MouseButtonEventArgs e) => WindowState = WindowState.Minimized;
        private void ProfileButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            new MainWindow(_currentUserId).Show();
            Close();
        }

        private void BookRoom_Click(object sender, MouseButtonEventArgs e)
        {
            // Бронирование доступно всем
            var bookingWindow = new BookingRoomWindow(_currentUserId);
            bookingWindow.Show();
            Close();
        }

        private void CheckBooking_Click(object sender, MouseButtonEventArgs e)
        {
            // Проверка броней доступна всем
            var viewBookingWindow = new ChekingBookingWindow(_currentUserId);
            viewBookingWindow.Show();
            Close();
        }

        private void ViewEmployees_Click(object sender, MouseButtonEventArgs e)
        {
            if (_userRole != "Администратор")
            {
                ShowAccessDeniedMessage();
                return;
            }
            var viewEmployees = new EmployeesRegistrationWindow(_currentUserId);
            viewEmployees.Show();
            Close();
        }

        private void AddRoom_Click(object sender, MouseButtonEventArgs e)
        {
            if (_userRole != "Администратор" && _userRole != "Менеджер")
            {
                ShowAccessDeniedMessage();
                return;
            }
            // Окно уже открыто
        }

        private void RoomsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Обработка изменения выбранной строки (если нужно)
        }
    }
}