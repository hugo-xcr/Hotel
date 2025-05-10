using System;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Npgsql;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Windows.Controls;

namespace Hotel.Window
{
    public partial class EmployeesDeleteWindow : System.Windows.Window, INotifyPropertyChanged
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

        public EmployeesDeleteWindow(int userId)
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

                string query = @"SELECT 
                        u.id, 
                        u.email, 
                        u.firstname, 
                        u.surname, 
                        u.id_job,
                        COALESCE(
                            NULLIF(jt.responsibilities->>'title', ''),
                            'Гость'
                        ) as job_title,
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
                                    Surname = reader.IsDBNull(3) ? null : reader.GetString(3),
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
                MessageBox.Show(
                    $"Ошибка при загрузке пользователей: {ex.Message}\n\n{ex.StackTrace}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void LoadJobTitles()
        {
            try
            {
                JobTitles = new ObservableCollection<JobTitle>();

                // Исправленный запрос с обработкой NULL
                string query = @"SELECT 
                        id, 
                        COALESCE(
                            NULLIF(responsibilities->>'title', ''),
                            'Без названия'
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
                MessageBox.Show(
                    $"Ошибка при загрузке должностей: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
        private void DeleteEmployee(long employeeId)
        {
            try
            {
                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    connection.Open();
                    string deleteQuery = "DELETE FROM hotel.user WHERE id = @id";

                    using (var cmd = new NpgsqlCommand(deleteQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", employeeId);
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Сотрудник успешно удален!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadEmployees();
                            txtSelectedId.Text = "";
                        }
                        else
                        {
                            MessageBox.Show("Сотрудник с указанным ID не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении сотрудника: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EmployeesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EmployeesDataGrid.SelectedItem is EmployeeViewModel selectedEmployee)
            {
                txtSelectedId.Text = selectedEmployee.Id.ToString();
            }
            else
            {
                txtSelectedId.Text = string.Empty;
            }
        }

        private void btnDeleteEmployeers_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtSelectedId.Text))
            {
                MessageBox.Show("Выберите сотрудника для удаления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (long.TryParse(txtSelectedId.Text, out long employeeId))
            {
                var result = MessageBox.Show($"Вы уверены, что хотите удалить сотрудника с ID {employeeId}?",
                                           "Подтверждение удаления",
                                           MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    DeleteEmployee(employeeId);
                }
            }
            else
            {
                MessageBox.Show("Некорректный ID сотрудника.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void btnRegisterEmployee_Click_1(object sender, RoutedEventArgs e)
        {
            var registrationWindow = new EmployeesRegistrationWindow(_currentUserId);
            registrationWindow.Show();
            this.Close();
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
        private void btnEmployeeInfo_Click(object sender, RoutedEventArgs e)
        {
            var changeWindow = new EmployeesChangeWindow(_currentUserId);
            changeWindow.Show();
            this.Close();
        }



        private void ViewEmployees_Click(object sender, MouseButtonEventArgs e)
        {
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
        private void MinimizeWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                WindowState = WindowState.Minimized;
        }

        private void CloseButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                Close();
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


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    
}