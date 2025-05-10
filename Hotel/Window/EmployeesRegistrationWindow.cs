using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using Npgsql;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace Hotel.Window
{
    public partial class EmployeesRegistrationWindow : System.Windows.Window
    {
        private const string ConnectionString = "Host=172.20.7.53;Database=db3996_17;Username=root;Password=root;Timeout=300";
        private readonly int _currentUserId;

        public class JobTitle
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public override string ToString() => Title;
        }

        public EmployeesRegistrationWindow(int userId)
        {
            InitializeComponent();
            _currentUserId = userId;
            LoadJobTitles();
            InitializePasswordField();
            InitializeEventHandlers();
        }

        public static class PasswordHasher
        {
            private const int SaltSize = 32; 
            private const int HashSize = 64; 
            private const int Iterations = 10000;

            public static string HashPassword(string password)
            {
                byte[] salt;
                new RNGCryptoServiceProvider().GetBytes(salt = new byte[SaltSize]);

                var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA512);
                byte[] hash = pbkdf2.GetBytes(HashSize);

                byte[] hashBytes = new byte[SaltSize + HashSize];
                Array.Copy(salt, 0, hashBytes, 0, SaltSize);
                Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

                return Convert.ToBase64String(hashBytes);
            }

            public static bool VerifyPassword(string inputPassword, string hashedPassword)
            {
                byte[] hashBytes = Convert.FromBase64String(hashedPassword);

                byte[] salt = new byte[SaltSize];
                Array.Copy(hashBytes, 0, salt, 0, SaltSize);

                var pbkdf2 = new Rfc2898DeriveBytes(inputPassword, salt, Iterations, HashAlgorithmName.SHA512);
                byte[] hash = pbkdf2.GetBytes(HashSize);

                for (int i = 0; i < HashSize; i++)
                {
                    if (hashBytes[i + SaltSize] != hash[i])
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private void InitializePasswordField()
        {
            txtPassword.PasswordChar = '•';
            txtPassword.MaxLength = 50;
        }

        private void InitializeEventHandlers()
        {
            btnRegisterEmployee.Click += btnRegisterEmployee_Click;
            btnEmployeeInfo.Click += btnEmployeeInfo_Click;
        }

        private void LoadJobTitles()
        {
            try
            {
                cmbPosition.Items.Clear();
                cmbPosition.ItemsSource = GetJobTitles();
                cmbPosition.DisplayMemberPath = "Title";
                cmbPosition.SelectedValuePath = "Id";
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка загрузки должностей: {ex.Message}");
            }
        }

        private List<JobTitle> GetJobTitles()
        {
            var jobTitles = new List<JobTitle>();

            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                connection.Open();
                const string query = @"SELECT id, responsibilities->>'title' as title 
                                    FROM hotel.job_title
                                    WHERE responsibilities->>'title' != 'Гость'";

                using (var cmd = new NpgsqlCommand(query, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        jobTitles.Add(new JobTitle
                        {
                            Id = reader.GetInt32(0),
                            Title = reader.GetString(1)
                        });
                    }
                }
            }

            return jobTitles;
        }

        private bool ValidateFields()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(txtFirstName.Text))
                errors.Add("Введите имя сотрудника");

            if (string.IsNullOrWhiteSpace(txtLastName.Text))
                errors.Add("Введите фамилию сотрудника");

            if (string.IsNullOrWhiteSpace(txtEmail.Text) || !new EmailAddressAttribute().IsValid(txtEmail.Text))
                errors.Add("Введите корректный email");

            if (cmbPosition.SelectedItem == null)
                errors.Add("Выберите должность");

            if (string.IsNullOrWhiteSpace(txtPassword.Password) || txtPassword.Password.Length < 8)
                errors.Add("Пароль должен содержать минимум 8 символов");

            if (errors.Any())
            {
                ShowError(string.Join("\n", errors));
                return false;
            }

            return true;
        }

        private void btnRegisterEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateFields()) return;

            try
            {
                RegisterEmployee();
                ShowSuccess("Сотрудник успешно зарегистрирован");
                ClearFields();
            }
            catch (NpgsqlException ex) when (ex.SqlState == "23505")
            {
                ShowError("Пользователь с таким email уже существует");
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка регистрации: {ex.Message}");
            }
        }

        private void RegisterEmployee()
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                connection.Open();
                const string query = @"INSERT INTO hotel.user 
                                     (email, firstname, surname, id_job, password) 
                                     VALUES (@email, @firstName, @lastName, @jobId, @password) 
                                     RETURNING id";

                using (var cmd = new NpgsqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@email", txtEmail.Text);
                    cmd.Parameters.AddWithValue("@firstName", txtFirstName.Text);
                    cmd.Parameters.AddWithValue("@lastName", txtLastName.Text);
                    cmd.Parameters.AddWithValue("@jobId", (int)cmbPosition.SelectedValue);
                    string hashedPassword = PasswordHasher.HashPassword(txtPassword.Password);
                    cmd.Parameters.AddWithValue("@password", hashedPassword);

                    var newId = cmd.ExecuteScalar();
                }
            }
        }
        

        private void ClearFields()
        {
            txtFirstName.Text = string.Empty;
            txtLastName.Text = string.Empty;
            txtEmail.Text = string.Empty;
            txtPassword.Password = string.Empty;
            cmbPosition.SelectedIndex = -1;
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowSuccess(string message)
        {
            MessageBox.Show(message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }



        private void ProfileButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var ProfileWindow = new MainWindow(_currentUserId);
                ProfileWindow.Show();
                this.Close();
            }
        }
        private void btnEmployeeInfo_Click(object sender, RoutedEventArgs e)
        {
            NavigateToChangeWindow();
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
        private void ViewBookedRooms_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var bookingWindow = new BookingRoomWindow(_currentUserId);
                bookingWindow.Show();
                this.Close();
            }
        }
        

        private void ViewEmployees_Click(object sender, MouseButtonEventArgs e)
        {

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
        private void NavigateToChangeWindow()
        {
            var changeWindow = new EmployeesChangeWindow(_currentUserId);
            changeWindow.Show();
            Close();
        }

        // Обработчики событий окна
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
    }
}