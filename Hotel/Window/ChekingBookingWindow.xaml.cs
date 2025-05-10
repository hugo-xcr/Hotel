using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Npgsql;
using System.Data;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using Border = System.Windows.Controls.Border;
using Style = System.Windows.Style;

namespace Hotel.Window
{
    public partial class ChekingBookingWindow : System.Windows.Window
    {
        private const string ConnectionString = "Host=172.20.7.53;Database=db3996_17;Username=root;Password=root;Timeout=300";
        private readonly int _currentUserId;
        private readonly CultureInfo _usCulture = new CultureInfo("en-US");
        private string _userRole;

        public ChekingBookingWindow(int userId)
        {
            InitializeComponent();
            _currentUserId = userId;
            Loaded += ChekingBookingWindow_Loaded;
            BookingRoomsDataGrid.SelectionChanged += BookingRoomsDataGrid_SelectionChanged;
        }

        private void ShowAccessDeniedMessage()
        {
            MessageBox.Show("У вас недостаточно прав для выполнения этого действия.",
                          "Ограничение доступа",
                          MessageBoxButton.OK,
                          MessageBoxImage.Warning);
        }

        private async void ChekingBookingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadUserRoleAsync();
            UpdateUIForRole();
            LoadBookingData();
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

        private void SetAdminAccess()
        {

            var borders = new[] { ProfileButton, BorderAddRoom, BorderViewEmployees };
            foreach (var border in borders)
            {
                border.Opacity = 1;
                border.IsEnabled = true;
                border.Cursor = Cursors.Hand;
                border.Background = Brushes.Transparent;
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
            var borders = new[] { ProfileButton, BorderAddRoom, BorderCheckBooking, BorderViewEmployees };
            foreach (var border in borders)
            {
                if (border == BorderCheckBooking) continue;

                border.Background = Brushes.Transparent;
                border.Opacity = 1;
                border.IsEnabled = true;
                border.Cursor = Cursors.Hand;

                var textBlock = FindVisualChildren<TextBlock>(border).FirstOrDefault();
                if (textBlock != null) textBlock.Foreground = Brushes.White;
            }
        }

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


        private void AddBookRoom_Click(object sender, MouseButtonEventArgs e)
        {
            if (_userRole != "Администратор" && _userRole != "Менеджер")
            {
                ShowAccessDeniedMessage();
                return;
            }

            var addbookingWindow = new AddRoomWindow(_currentUserId);
            addbookingWindow.Show();
            Close();
        }

        private void ViewEmployees_Click(object sender, MouseButtonEventArgs e)
        {
            if (_userRole != "Администратор")
            {
                ShowAccessDeniedMessage();
                return;
            }

            var ViewEmployees = new EmployeesChangeWindow(_currentUserId);
            ViewEmployees.Show();
            this.Close();
        }

        // Остальной код остается без изменений
        private void BookingRoomsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BookingRoomsDataGrid.SelectedItem is DataRowView selectedRow)
            {
                txtUserId.Text = selectedRow["user_id"].ToString();
                txtRoomNumber.Text = selectedRow["room_number"].ToString();
                txtStartDate.Text = Convert.ToDateTime(selectedRow["start_date"]).ToString("dd.MM.yyyy");
                txtEndDate.Text = Convert.ToDateTime(selectedRow["end_date"]).ToString("dd.MM.yyyy");
                txtTotalAmount.Text = FormatAsDollars(Convert.ToDecimal(selectedRow["summ"]));
            }
        }

        private string FormatAsDollars(decimal amount)
        {
            return amount.ToString("C", _usCulture);
        }

        private void LoadBookingData()
        {
            try
            {
                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    connection.Open();

                    string query = @"
                        SELECT 
                            b.id as booking_id,
                            u.id as user_id,
                            r.room_number,
                            b.start_date,
                            b.end_date,
                            b.summ
                        FROM hotel.booking b
                        JOIN hotel.user u ON b.user_id = u.id
                        JOIN hotel.room r ON b.room_id = r.id
                        ORDER BY b.start_date";

                    var adapter = new NpgsqlDataAdapter(query, connection);
                    var dataSet = new DataSet();
                    adapter.Fill(dataSet);

                    if (dataSet.Tables.Count > 0)
                    {
                        dataSet.Tables[0].Columns["summ"].ExtendedProperties.Add("Format", "$#,##0.00");
                        BookingRoomsDataGrid.ItemsSource = dataSet.Tables[0].DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private void NumberValidation(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void DecimalValidation(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.,]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Логика при закрытии окна
        }

        private void btnOtchet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (BookingRoomsDataGrid.SelectedItem == null)
                {
                    MessageBox.Show("Пожалуйста, выберите бронирование для формирования отчета");
                    return;
                }

                CreateDetailedWordReport();
                MessageBox.Show("Отчет успешно создан!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании отчета: {ex.Message}");
            }
        }

        private void CreateDetailedWordReport()
        {
            if (!(BookingRoomsDataGrid.SelectedItem is DataRowView selectedRow)) return;

            string fileName = $"Отчет_о_бронировании_{DateTime.Now:yyyyMMddHHmmss}.docx";
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

            using (WordprocessingDocument wordDocument = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();
                Body body = mainPart.Document.AppendChild(new Body());

                AddHeader(body);

                // Детали бронирования
                AddBookingDetails(body, selectedRow);

                // Дополнительные расчеты
                AddCalculations(body, selectedRow);

                // Подпись внизу
                AddFooter(body);
            }

            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }

        private void AddHeader(Body body)
        {
            // Название отеля и заголовок
            Paragraph hotelPara = body.AppendChild(new Paragraph());
            Run hotelRun = hotelPara.AppendChild(new Run());
            hotelRun.AppendChild(new Text("Отчет о бронировании Grand Hotel"));
            hotelRun.PrependChild(new RunProperties(new Bold(), new FontSize() { Val = "32" }));
            hotelPara.ParagraphProperties = new ParagraphProperties(
                new Justification() { Val = JustificationValues.Center });

            // Дата отчета
            Paragraph datePara = body.AppendChild(new Paragraph());
            Run dateRun = datePara.AppendChild(new Run());
            dateRun.AppendChild(new Text($"Отчет сформирован: {DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm", new CultureInfo("ru-RU"))}"));
            datePara.ParagraphProperties = new ParagraphProperties(
                new Justification() { Val = JustificationValues.Right });

            body.AppendChild(new Paragraph(new Run(new Text(""))));
        }

        private void AddBookingDetails(Body body, DataRowView selectedRow)
        {

            AddSectionTitle(body, "Детали бронирования");
            Table table = body.AppendChild(new Table());
            TableProperties tableProps = new TableProperties(
                new TableBorders(
                    new TopBorder() { Val = BorderValues.Single, Size = 4 },
                    new BottomBorder() { Val = BorderValues.Single, Size = 4 },
                    new LeftBorder() { Val = BorderValues.Single, Size = 4 },
                    new RightBorder() { Val = BorderValues.Single, Size = 4 }
                ),
                new TableWidth() { Width = "100%", Type = TableWidthUnitValues.Pct }
            );
            table.AppendChild(tableProps);

            AddTableRow(table, "ID бронирования:", selectedRow["booking_id"].ToString());
            AddTableRow(table, "ID гостя:", selectedRow["user_id"].ToString());
            AddTableRow(table, "Номер комнаты:", selectedRow["room_number"].ToString());
            AddTableRow(table, "Дата заезда:", Convert.ToDateTime(selectedRow["start_date"]).ToString("dddd, dd MMMM yyyy", new CultureInfo("ru-RU")));
            AddTableRow(table, "Дата выезда:", Convert.ToDateTime(selectedRow["end_date"]).ToString("dddd, dd MMMM yyyy", new CultureInfo("ru-RU")));
            AddTableRow(table, "Общая сумма:", FormatAsDollars(Convert.ToDecimal(selectedRow["summ"])));

            body.AppendChild(new Paragraph(new Run(new Text(""))));
        }

        private void AddCalculations(Body body, DataRowView selectedRow)
        {
            AddSectionTitle(body, "Дополнительная информация");

            DateTime startDate = Convert.ToDateTime(selectedRow["start_date"]);
            DateTime endDate = Convert.ToDateTime(selectedRow["end_date"]);
            int days = (endDate - startDate).Days;
            decimal totalAmount = Convert.ToDecimal(selectedRow["summ"]);
            decimal dailyRate = totalAmount / days;

            Table calcTable = body.AppendChild(new Table());
            TableProperties calcTableProps = new TableProperties(
                new TableBorders(
                    new TopBorder() { Val = BorderValues.Single, Size = 4 },
                    new BottomBorder() { Val = BorderValues.Single, Size = 4 },
                    new LeftBorder() { Val = BorderValues.Single, Size = 4 },
                    new RightBorder() { Val = BorderValues.Single, Size = 4 }
                ),
                new TableWidth() { Width = "100%", Type = TableWidthUnitValues.Pct }
            );
            calcTable.AppendChild(calcTableProps);

            AddTableRow(calcTable, "Продолжительность проживания:", $"{days} {(days == 1 ? "день" : "дней")}");
            AddTableRow(calcTable, "Стоимость за день:", FormatAsDollars(dailyRate));
            AddTableRow(calcTable, "Общая сумма:", FormatAsDollars(totalAmount), true);

            body.AppendChild(new Paragraph(new Run(new Text(""))));
        }

        private void AddFooter(Body body)
        {
            Paragraph footerPara = body.AppendChild(new Paragraph());
            Run footerRun = footerPara.AppendChild(new Run());
            footerRun.AppendChild(new Text("Благодарим за выбор Grand Hotel!"));
            footerRun.PrependChild(new RunProperties(new Italic()));

            Paragraph signaturePara = body.AppendChild(new Paragraph());
            signaturePara.ParagraphProperties = new ParagraphProperties(
                new Justification() { Val = JustificationValues.Right });
            Run signatureRun = signaturePara.AppendChild(new Run());
            signatureRun.AppendChild(new Text("Менеджер отеля"));
            signatureRun.PrependChild(new RunProperties(new Bold()));

            body.AppendChild(new Paragraph(new Run(new Text(""))));
            Paragraph contactPara = body.AppendChild(new Paragraph());
            Run contactRun = contactPara.AppendChild(new Run());
            contactRun.AppendChild(new Text("Контакты: hugolol596@gmail.com | Телефон: +7 (951) 725 7876"));
        }

        private void AddSectionTitle(Body body, string title)
        {
            Paragraph titlePara = body.AppendChild(new Paragraph());
            Run titleRun = titlePara.AppendChild(new Run());
            titleRun.AppendChild(new Text(title));
            titleRun.PrependChild(new RunProperties(new Bold(), new FontSize() { Val = "20" }));
            titlePara.ParagraphProperties = new ParagraphProperties(
                new Justification() { Val = JustificationValues.Left },
                new SpacingBetweenLines() { Before = "200", After = "100" });
        }

        private void AddTableRow(Table table, string label, string value, bool bold = false)
        {
            TableRow row = table.AppendChild(new TableRow());

            // Ячейка с меткой
            TableCell labelCell = row.AppendChild(new TableCell());
            Paragraph labelPara = labelCell.AppendChild(new Paragraph());
            Run labelRun = labelPara.AppendChild(new Run());
            labelRun.AppendChild(new Text(label));
            if (bold) labelRun.PrependChild(new RunProperties(new Bold()));

            // Ячейка со значением
            TableCell valueCell = row.AppendChild(new TableCell());
            Paragraph valuePara = valueCell.AppendChild(new Paragraph());
            Run valueRun = valuePara.AppendChild(new Run());
            valueRun.AppendChild(new Text(value));
            if (bold) valueRun.PrependChild(new RunProperties(new Bold()));
        }

        private void CloseWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void MinimizeWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ProfileButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var profileWindow = new MainWindow(_currentUserId);
            profileWindow.Show();
            Close();
        }

        private void BookRoom_Click(object sender, MouseButtonEventArgs e)
        {
            var bookingWindow = new BookingRoomWindow(_currentUserId);
            bookingWindow.Show();
            Close();
        }

        private void ViewBookedRooms_Click(object sender, MouseButtonEventArgs e)
        {
            var ViewEmployees = new BookingRoomWindow(_currentUserId);
            ViewEmployees.Show();
            this.Close();
        }
    }
}