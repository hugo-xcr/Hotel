using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;

namespace Hotel
{
    public partial class LoginForm : System.Windows.Window
    {
        public static int CurrentUserId { get; set; } 

        public LoginForm()
        {
            InitializeComponent();
            Loaded += (s, e) => textBox_Email.Focus();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var registrationForm = new RegistrationForm();
            registrationForm.Show();
            this.Close();
        }

        private void CloseWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await AuthenticateUser();
        }

        private async Task AuthenticateUser()
        {
            string email = textBox_Email.Text.Trim();
            string password = textBox_Password.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Введите email и пароль");
                return;
            }

            try
            {
                using (var context = DatabaseContent.GetContext())
                {
                    var user = await context.Users
                        .FirstOrDefaultAsync(u => u.Email == email);

                    if (user != null && HashingPassword.VerifyPassword(password, user.Password))
                    {
                        CurrentUserId = user.Id;
                        MessageBox.Show($"Добро пожаловать, {user.Firstname} {user.Surname}!");

                        MainWindow mainWindow = new MainWindow(CurrentUserId);
                        mainWindow.Show();
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Неверный email или пароль");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка авторизации: {ex.Message}");
            }
        }

        private void textBox_Password_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AuthenticateUser().ConfigureAwait(false);
            }
        }

        private void textBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
        }
    }
}