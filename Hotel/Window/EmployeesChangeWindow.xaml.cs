using System;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Npgsql;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace Hotel.Window
{
    public partial class EmployeesChangeWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private readonly int _currentUserId;
        private ObservableCollection<EmployeeViewModel> _employees;
        private ObservableCollection<JobTitle> _jobTitles;
        private const string ConnectionString = "Host=172.20.7.53;Database=db3996_17;Username=root;Password=root;Timeout=300";

        public ObservableCollection<EmployeeViewModel> Employees
        {
            get => _employees;
            set
            {
                _employees = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<JobTitle> JobTitles
        {
            get => _jobTitles;
            set
            {
                _jobTitles = value;
                OnPropertyChanged();
            }
        }

        public EmployeesChangeWindow(int userId)
        {
            InitializeComponent();
            _currentUserId = userId;
            DataContext = this;
            LoadJobTitles();
            LoadEmployees();
        }

        private void LoadEmployees()
        {
            try
            {
                Employees = new ObservableCollection<EmployeeViewModel>();

                string query = @"
    SELECT 
        u.id, 
        u.email, 
        u.firstname, 
        u.surname, 
        u.id_job,
        COALESCE(NULLIF(jt.responsibilities->>'title', ''), 'Гость') as job_title,
        CASE 
            WHEN jt.responsibilities ? 'обязанности' 
            THEN string_to_array(jt.responsibilities->>'обязанности', ',') 
            ELSE ARRAY[]::text[] 
        END as job_responsibilities
    FROM hotel.user u
    LEFT JOIN hotel.job_title jt ON u.id_job = jt.id";

                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new NpgsqlCommand(query, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var employee = new EmployeeViewModel
                                {
                                    Id = reader.GetInt64(0),
                                    Email = reader.IsDBNull(1) ? null : reader.GetString(1),
                                    Firstname = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Surname = reader.IsDBNull(3) ? null : reader.GetString(3), // Добавлено
                                    JobId = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                                    JobTitle = reader.IsDBNull(5) ? "Гость" : reader.GetString(5),
                                    JobResponsibilities = reader.IsDBNull(6) ?
        "Нет обязанностей" :
        string.Join(", ", reader.GetFieldValue<List<string>>(6))
                                };
                                Employees.Add(employee);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке пользователей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadJobTitles()
        {
            try
            {
                JobTitles = new ObservableCollection<JobTitle>();
                string query = @"
                    SELECT id, 
                           COALESCE(
                               NULLIF(responsibilities->>'title', ''),
                               'Не указано'
                           ) as title 
                    FROM hotel.job_title";

                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new NpgsqlCommand(query, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                JobTitles.Add(new JobTitle
                                {
                                    Id = reader.GetInt64(0),
                                    Title = reader.IsDBNull(1) ? "Не указано" : reader.GetString(1)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке должностей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveChanges()
        {
            try
            {
                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    connection.Open();

                    foreach (var employee in Employees)
                    {
                        string updateQuery = @"UPDATE hotel.user 
                                            SET email = @email, 
                                                firstname = @firstname, 
                                                surname = @surname, 
                                                id_job = @id_job 
                                            WHERE id = @id";

                        using (var cmd = new NpgsqlCommand(updateQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@email", employee.Email ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@firstname", employee.Firstname ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@surname", employee.Surname);
                            cmd.Parameters.AddWithValue("@id_job", employee.JobId);
                            cmd.Parameters.AddWithValue("@id", employee.Id);

                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                MessageBox.Show("Изменения успешно сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadEmployees(); // Обновляем данные после сохранения
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении изменений: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSaveEmployeers_Click(object sender, RoutedEventArgs e)
        {
            SaveChanges();
        }

        private void btnRegisterEmployee_Click(object sender, RoutedEventArgs e)
        {
            var registrationWindow = new EmployeesRegistrationWindow(_currentUserId);
            registrationWindow.Show();
            this.Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                Close();
        }

        private void MinimizeWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                WindowState = WindowState.Minimized;
        }

        private void ProfileButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var mainWindow = new MainWindow(_currentUserId);
                mainWindow.Show();
                this.Close();
            }
        }
        private void btnDeleteEmployee_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var deleteWindow = new EmployeesDeleteWindow(_currentUserId);
                deleteWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии окна удаления сотрудников: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void BookRoom_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var bookingWindow = new AddRoomWindow(_currentUserId);
                bookingWindow.Show();
                this.Close();
            }
        }

        private void ViewBookedRooms_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var bookingWindow = new BookingRoomWindow(_currentUserId);
                bookingWindow.Show();
                this.Close();
            }
        }

        private void CheckBooking_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var CheckBooking = new ChekingBookingWindow(_currentUserId);
                CheckBooking.Show();
                this.Close();
            }
        }

        private void BookRoom_Click(object sender, RoutedEventArgs e)
        {
            NavigateToBookingWindow();
        }
        private void NavigateToRegistrationWindow()
        {
            try
            {
                var registrationWindow = new EmployeesRegistrationWindow(_currentUserId);
                registrationWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии окна регистрации: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToBookingWindow()
        {
            try
            {
                var bookingWindow = new BookingRoomWindow(_currentUserId);
                bookingWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии окна бронирования: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void ViewEmployees_Click(object sender, MouseButtonEventArgs e)
        {
        }

        

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void btnRegisterEmployee_Click_1(object sender, RoutedEventArgs e)
        {

            NavigateToRegistrationWindow();
        }

        private void btnEmployeeInfo_Click(object sender, RoutedEventArgs e)
        {

        }
    }

    public class EmployeeViewModel : INotifyPropertyChanged
    {
        private long _id;
        private string _email;
        private string _firstname;
        private string _surname;
        private long _jobId;
        private string _jobTitle;
        private string _jobResponsibilities;

        public long Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string Firstname
        {
            get => _firstname;
            set { _firstname = value; OnPropertyChanged(); }
        }

        public string Surname
        {
            get => _surname;
            set { _surname = value; OnPropertyChanged(); }
        }

        public long JobId
        {
            get => _jobId;
            set { _jobId = value; OnPropertyChanged(); }
        }

        public string JobTitle
        {
            get => _jobTitle;
            set { _jobTitle = value; OnPropertyChanged(); }
        }

        public string JobResponsibilities
        {
            get => _jobResponsibilities;
            set { _jobResponsibilities = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class JobTitle
    {
        public long Id { get; set; }
        public string Title { get; set; }
    }
}