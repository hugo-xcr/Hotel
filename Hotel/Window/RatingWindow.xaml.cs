using System;
using System.Windows;
using Npgsql;

namespace Hotel.Window
{
    public partial class RatingWindow : System.Windows.Window
    {
        private readonly int _userId;
        private readonly long _roomId;
        private const string ConnectionString = "Host=172.20.7.53;Database=db3996_17;Username=root;Password=root;Timeout=300";

        public RatingWindow(int userId, long roomId)
        {
            InitializeComponent();
            _userId = userId;
            _roomId = roomId;
        }

        private int GetSelectedRating()
        {
            if (rb1.IsChecked == true) return 1;
            if (rb2.IsChecked == true) return 2;
            if (rb3.IsChecked == true) return 3;
            if (rb4.IsChecked == true) return 4;
            return 5;
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            int rating = GetSelectedRating();
            string comment = txtComment.Text;

            if (string.IsNullOrWhiteSpace(comment))
            {
                MessageBox.Show("Пожалуйста, оставьте комментарий");
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // 1. Проверяем существующую оценку пользователя для этого номера
                            var checkCmd = new NpgsqlCommand(
                                @"SELECT COUNT(*) FROM hotel.rating r
                        JOIN hotel.information_room ir ON r.id = ir.id_rating
                        JOIN hotel.room rm ON rm.id_information = ir.id
                        WHERE r.id_user = @userId AND rm.id = @roomId",
                                connection, transaction);

                            checkCmd.Parameters.AddWithValue("userId", _userId);
                            checkCmd.Parameters.AddWithValue("roomId", _roomId);

                            var existingRating = (long)await checkCmd.ExecuteScalarAsync();

                            if (existingRating > 0)
                            {
                                MessageBox.Show("Вы уже оценивали этот номер ранее");
                                return;
                            }

                            // 2. Обновляем существующую оценку пользователя (если есть)
                            var updateCmd = new NpgsqlCommand(
                                @"UPDATE hotel.rating SET
                        rating = @rating,
                        comments = @comment
                        WHERE id_user = @userId
                        RETURNING id",
                                connection, transaction);

                            updateCmd.Parameters.AddWithValue("rating", rating);
                            updateCmd.Parameters.AddWithValue("comment", comment);
                            updateCmd.Parameters.AddWithValue("userId", _userId);

                            var updatedRows = await updateCmd.ExecuteNonQueryAsync();
                            if (updatedRows == 0)
                            {
                                var maxIdCmd = new NpgsqlCommand(
                                    "SELECT COALESCE(MAX(id), 0) FROM hotel.rating",
                                    connection, transaction);
                                long newId = (long)await maxIdCmd.ExecuteScalarAsync() + 1;

                                var insertCmd = new NpgsqlCommand(
                                    @"INSERT INTO hotel.rating 
                                    (id, rating, comments, id_user) 
                                    VALUES (@id, @rating, @comment, @userId)
                                    RETURNING id",
                                    connection, transaction);

                                insertCmd.Parameters.AddWithValue("id", newId);
                                insertCmd.Parameters.AddWithValue("rating", rating);
                                insertCmd.Parameters.AddWithValue("comment", comment);
                                insertCmd.Parameters.AddWithValue("userId", _userId);

                                await insertCmd.ExecuteNonQueryAsync();
                            }

                            var linkCmd = new NpgsqlCommand(
                                @"UPDATE hotel.information_room 
                        SET id_rating = (SELECT id FROM hotel.rating WHERE id_user = @userId)
                        FROM hotel.room 
                        WHERE hotel.room.id_information = hotel.information_room.id 
                        AND hotel.room.id = @roomId",
                                connection, transaction);

                            linkCmd.Parameters.AddWithValue("userId", _userId);
                            linkCmd.Parameters.AddWithValue("roomId", _roomId);

                            await linkCmd.ExecuteNonQueryAsync();

                            transaction.Commit();
                            MessageBox.Show("Спасибо за вашу оценку!");
                            DialogResult = true;
                            Close();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show($"Ошибка при сохранении оценки: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}