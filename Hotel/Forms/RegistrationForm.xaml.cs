using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;

namespace Hotel
{
    public partial class RegistrationForm : System.Windows.Window
    {
        public static int CurrentUserId { get; private set; }

        public RegistrationForm()
        {
            InitializeComponent();
            Loaded += (s, e) => textBox_Name.Focus();
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string firstName = textBox_Name.Text.Trim();
            string surname = textBox_surname.Text.Trim();
            string email = textBox_Email.Text.Trim();
            string password = textBox_Password.Password;

            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(surname) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Пожалуйста, заполните все обязательные поля.");
                return;
            }

            try
            {
                using (var context = DatabaseContent.GetContext())
                {
                    if (await context.Users.AnyAsync(u => u.Email == email))
                    {
                        MessageBox.Show("Пользователь с таким email уже существует.");
                        return;
                    }

                    string hashedPassword = HashingPassword.HashPassword(password);

                    var newUser = new Users
                    {
                        Email = email,
                        Password = hashedPassword,
                        Firstname = firstName,
                        Surname = surname,
                        IdJob = 3
                    };

                    context.Users.Add(newUser);
                    await context.SaveChangesAsync();

                    CurrentUserId = newUser.Id;
                    MessageBox.Show($"Регистрация прошла успешно! Добро пожаловать, {firstName}!");
                    MainWindow mainWindow = new MainWindow(CurrentUserId);
                    mainWindow.Show();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var loginForm = new LoginForm();
            loginForm.Show();
            this.Close();
        }

        private void CloseWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        private void textBox_Password_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                RegisterButton_Click(sender, e);
            }
        }
    }
}