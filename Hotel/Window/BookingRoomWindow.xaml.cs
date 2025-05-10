using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Npgsql;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Hotel.Window
{
    public partial class BookingRoomWindow : System.Windows.Window
    {
        private const string ConnectionString = "Host=172.20.7.53;Database=db3996_17;Username=root;Password=root;Timeout=300";
        private readonly int _currentUserId;
        private DataTable _roomsData;
        private string _userRole;

        public BookingRoomWindow(int currentUserId)
        {
            InitializeComponent();
            _currentUserId = currentUserId;
            txtUserId.Text = currentUserId.ToString();
            Loaded += async (s, e) =>
            {
                await LoadUserRoleAsync();
                UpdateUIForRole();
                await LoadRoomsDataAsync();
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
                _userRole = "Гость"; // По умолчанию
            }
        }

        // BookingRoomWindow.xaml.cs
        private async Task<string> DetermineUserRole(NpgsqlConnection connection)
        {
            const string query = @"SELECT id_job FROM hotel.user WHERE id = @userId";
            using (var cmd = new NpgsqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("userId", _currentUserId);
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

        // В методе SetAdminAccess измените:
        private void SetAdminAccess()
        {
            var borders = new[] { ProfileButton, BorderAddRoom, BorderViewEmployees };
            foreach (var border in borders)
            {
                border.Opacity = 1;
                border.IsEnabled = true;
                border.Cursor = Cursors.Hand;
                border.Background = Brushes.Transparent; // Уберите установку белого фона здесь
            }

            // Обновляем только цвет текста
            var textBlock = FindVisualChildren<TextBlock>(BorderAddRoom).FirstOrDefault();
            if (textBlock != null)
            {
                textBlock.Foreground = Brushes.White; // Белый текст для админа
            }
        }

        private void SetManagerAccess()
        {
            BorderViewEmployees.Opacity = 0.5;
            BorderViewEmployees.IsEnabled = false;
            BorderViewEmployees.Cursor = Cursors.No;

            BorderAddRoom.Opacity = 1;
            BorderAddRoom.IsEnabled = true;
        }

        private void SetGuestAccess()
        {
            var borders = new[] { BorderAddRoom, BorderViewEmployees };
            foreach (var border in borders)
            {
                border.Opacity = 0.5;
                border.IsEnabled = false;
                border.Cursor = Cursors.No;
            }
        }

        private void ResetAllBorders()
        {
            var borders = new[] { ProfileButton, BorderAddRoom, BorderViewEmployees };
            foreach (var border in borders)
            {
                border.Background = Brushes.Transparent;
                border.Opacity = 1;
                border.IsEnabled = true;
                border.Cursor = Cursors.Hand;

                var textBlock = FindVisualChildren<TextBlock>(border).FirstOrDefault();
                if (textBlock != null) textBlock.Foreground = Brushes.White;
            }
        }

        // Добавьте этот метод в класс, если его нет
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) yield return t;

                foreach (var childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }

        private void ShowAccessDeniedMessage()
        {
            MessageBox.Show("У вас недостаточно прав для выполнения этого действия.",
                          "Ограничение доступа",
                          MessageBoxButton.OK,
                          MessageBoxImage.Warning);
        }

        // Обработчики кликов с проверкой прав
        private void AddRoom_Click(object sender, MouseButtonEventArgs e)
        {
            if (_userRole != "Администратор" && _userRole != "Менеджер")
            {
                ShowAccessDeniedMessage();
                return;
            }

            var addRoomWindow = new AddRoomWindow(_currentUserId);
            addRoomWindow.Show();
            this.Close();
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
            this.Close();
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
    r.id AS room_id,
    r.room_number,
    ir.name AS room_name,
    (ir.type_seats_room->>'type') || ' (' || 
    (ir.type_seats_room->>'capacity') || ' мест)' AS room_type_capacity,
    ir.price_per_night,
    ir.status
FROM hotel.room r
JOIN hotel.information_room ir ON r.id_information = ir.id";

                    var adapter = new NpgsqlDataAdapter(query, connection);
                    _roomsData = new DataTable();
                    adapter.Fill(_roomsData);

                    RoomsDataGrid.ItemsSource = _roomsData.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}\n\nПодробности:\n{ex.StackTrace}");
                Debug.WriteLine(ex.ToString());
            }
        }

        private async void btnRateRoom_Click(object sender, RoutedEventArgs e)
        {
            if (RoomsDataGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите номер для оценки");
                return;
            }

            try
            {
                var selectedRow = (DataRowView)RoomsDataGrid.SelectedItem;

                if (!selectedRow.Row.Table.Columns.Contains("room_id") ||
                    !selectedRow.Row.Table.Columns.Contains("room_number"))
                {
                    MessageBox.Show("Отсутствуют необходимые данные о номере");
                    return;
                }

                if (!long.TryParse(selectedRow["room_id"].ToString(), out long roomId) ||
                    !int.TryParse(selectedRow["room_number"].ToString(), out int roomNumber))
                {
                    MessageBox.Show("Некорректные данные о номере");
                    return;
                }

                var ratingWindow = new RatingWindow(_currentUserId, roomId); 
                if (ratingWindow.ShowDialog() == true)
                {
                    await LoadRoomsDataAsync();
                    MessageBox.Show($"Спасибо за оценку номера {roomNumber}!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при оценке номера: {ex.Message}");
                Debug.WriteLine(ex.ToString());
            }
        }

        private void CalculateTotalAmount()
        {
            if (RoomsDataGrid.SelectedItem == null ||
                string.IsNullOrEmpty(txtStartDate.Text) ||
                string.IsNullOrEmpty(txtEndDate.Text))
                return;

            try
            {
                var selectedRow = (DataRowView)RoomsDataGrid.SelectedItem;
                decimal pricePerNight = Convert.ToDecimal(selectedRow["price_per_night"]);

                if (!DateTime.TryParseExact(txtStartDate.Text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime startDate) ||
                    !DateTime.TryParseExact(txtEndDate.Text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime endDate))
                {
                    txtTotalAmount.Text = "0";
                    return;
                }

                if (endDate <= startDate)
                {
                    txtTotalAmount.Text = "0";
                    return;
                }

                int days = (endDate - startDate).Days;
                decimal totalAmount = pricePerNight * days;

                txtTotalAmount.Text = totalAmount.ToString("C");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка расчета суммы: {ex.Message}");
            }
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (RoomsDataGrid.SelectedItem == null)
            {
                MessageBox.Show("Выберите номер для бронирования");
                return;
            }

            if (!ValidateBookingFields())
                return;

            try
            {
                var selectedRow = (DataRowView)RoomsDataGrid.SelectedItem;
                long roomId = Convert.ToInt64(selectedRow["room_id"]);
                int roomNumber = Convert.ToInt32(selectedRow["room_number"]);
                decimal pricePerNight = Convert.ToDecimal(selectedRow["price_per_night"]);

                DateTime startDate = DateTime.ParseExact(txtStartDate.Text, "dd.MM.yyyy", CultureInfo.InvariantCulture);
                DateTime endDate = DateTime.ParseExact(txtEndDate.Text, "dd.MM.yyyy", CultureInfo.InvariantCulture);
                int days = (endDate - startDate).Days;
                decimal totalAmount = pricePerNight * days;

                if (await IsRoomBooked(roomId, startDate, endDate))
                {
                    MessageBox.Show("Номер уже забронирован на выбранные даты");
                    return;
                }

                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            var cmd = new NpgsqlCommand(
                                @"INSERT INTO hotel.booking 
                        (user_id, room_id, start_date, end_date, summ) 
                        VALUES (@userId, @roomId, @startDate, @endDate, @summ)
                        RETURNING id",
                                connection, transaction);

                            cmd.Parameters.AddWithValue("userId", _currentUserId);
                            cmd.Parameters.AddWithValue("roomId", roomId);
                            cmd.Parameters.AddWithValue("startDate", startDate);
                            cmd.Parameters.AddWithValue("endDate", endDate);
                            cmd.Parameters.AddWithValue("summ", totalAmount);

                            long newBookingId = (long)await cmd.ExecuteScalarAsync();

                            var updateCmd = new NpgsqlCommand(
                                @"UPDATE hotel.information_room 
                        SET status = 'занят' 
                        FROM hotel.room 
                        WHERE hotel.room.id_information = hotel.information_room.id 
                        AND hotel.room.id = @roomId",
                                connection, transaction);

                            updateCmd.Parameters.AddWithValue("roomId", roomId);
                            await updateCmd.ExecuteNonQueryAsync();

                            transaction.Commit();
                            MessageBox.Show($"Номер {roomNumber} успешно забронирован!\nСумма: {totalAmount:C}\nНомер брони: {newBookingId}");
                            await LoadRoomsDataAsync();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show($"Ошибка при бронировании: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private async Task<bool> IsRoomBooked(long roomId, DateTime startDate, DateTime endDate)
        {
            try
            {
                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();

                    var cmd = new NpgsqlCommand(
                        @"SELECT COUNT(*) FROM hotel.booking 
                        WHERE room_id = @roomId 
                        AND NOT (end_date <= @startDate OR start_date >= @endDate)",
                        connection);

                    cmd.Parameters.AddWithValue("roomId", roomId);
                    cmd.Parameters.AddWithValue("startDate", startDate);
                    cmd.Parameters.AddWithValue("endDate", endDate);

                    var count = (long)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
            }
            catch
            {
                return true;
            }
        }

        private bool ValidateBookingFields()
        {
            bool isValid = true;

            if (string.IsNullOrWhiteSpace(txtStartDate.Text) ||
                !DateTime.TryParseExact(txtStartDate.Text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                txtStartDate.Background = Brushes.LightPink;
                isValid = false;
            }
            else
            {
                txtStartDate.Background = Brushes.White;
            }

            if (string.IsNullOrWhiteSpace(txtEndDate.Text) ||
                !DateTime.TryParseExact(txtEndDate.Text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                txtEndDate.Background = Brushes.LightPink;
                isValid = false;
            }
            else
            {
                txtEndDate.Background = Brushes.White;
            }

            if (!isValid)
                MessageBox.Show("Проверьте правильность введенных дат (формат: ДД.ММ.ГГГГ)");

            return isValid;
        }


        private void BookRoom_Click(object sender, MouseButtonEventArgs e)
        {
            var bookedRoomsWindow = new BookingRoomWindow(_currentUserId);
            bookedRoomsWindow.Show();
            this.Close();
        }

        private void CheckBooking_Click(object sender, MouseButtonEventArgs e)
        {
            var viewBookingWindow = new ChekingBookingWindow(_currentUserId);
            viewBookingWindow.Show();
            this.Close();
        }


        private void ProfileButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            new MainWindow(_currentUserId).Show();
            Close();
        }

        private void RoomsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RoomsDataGrid.SelectedItem != null)
            {
                var selectedRow = (DataRowView)RoomsDataGrid.SelectedItem;
                txtRoomNumber.Text = selectedRow["room_number"].ToString();
            }
            CalculateTotalAmount();
        }

        private void CloseWindow_MouseDown(object sender, MouseButtonEventArgs e) => Close();
        private void MinimizeWindow_MouseDown(object sender, MouseButtonEventArgs e) => WindowState = WindowState.Minimized;
        private void Window_Closing(object sender, CancelEventArgs e) { }

        private void NumberValidation(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        } 

        private void DecimalValidation(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !decimal.TryParse(((TextBox)sender).Text + e.Text, out _);
        }

        private void DateValidation(object sender, TextCompositionEventArgs e)
        {
            var textBox = (TextBox)sender;
            var fullText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            e.Handled = !DateTime.TryParseExact(fullText, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        }
    }
}