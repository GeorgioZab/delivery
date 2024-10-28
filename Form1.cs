using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace delivery
{
    public partial class Form1 : Form
    {
        private string connectionString = "Data Source=DeliveryOrdersDB.db;Version=3;";
        private string logFilePath;
        private string resultFilePath = "_deliveryOrder.txt"; // Файл для записи результатов

        public Form1()
        {
            InitializeComponent();
            logFilePath = GetLogFilePath(); // Получение пути к файлу логов из аргументов командной строки
            LogAction("Программа запущена"); // Логирование запуска программы
        }

        // Метод для получения пути к файлу логов из аргументов командной строки
        private string GetLogFilePath()
        {
            var args = Environment.GetCommandLineArgs();
            var logArg = args.FirstOrDefault(arg => arg.StartsWith("_deliveryLog="));
            return logArg != null ? logArg.Split('=')[1] : "_deliveryLog.txt"; // Значение по умолчанию
        }

        // Метод для установления подключения к базе данных
        private SQLiteConnection GetConnection()
        {
            LogAction("Установление подключения к базе данных.");
            return new SQLiteConnection(connectionString);
        }

        // Метод для логирования ошибок в файл
        private void LogError(string message)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine($"{DateTime.Now} [ERROR]: {message}");
                }
            }
            catch
            {
                MessageBox.Show("Не удалось записать сообщение об ошибке в лог.");
            }
        }

        // Метод для логирования действий в файл
        private void LogAction(string message)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine($"{DateTime.Now} [ACTION]: {message}");
                }
            }
            catch
            {
                MessageBox.Show("Не удалось записать сообщение о действии в лог.");
            }
        }

        // Метод для выполнения фильтрации заказов
        private DataTable GetFilteredOrders(string cityDistrict, DateTime startDateTime)
        {
            DataTable dataTable = new DataTable();
            DateTime endDateTime = startDateTime.AddMinutes(30); // Определение конца временного интервала

            string query = @"
            SELECT * FROM Orders
            WHERE CityDistrict = @CityDistrict 
              AND DeliveryDateTime BETWEEN @StartDateTime AND @EndDateTime";

            try
            {
                using (var connection = GetConnection())
                using (var command = new SQLiteCommand(query, connection))
                {
                    // Параметры для запроса
                    command.Parameters.AddWithValue("@CityDistrict", cityDistrict);
                    command.Parameters.AddWithValue("@StartDateTime", startDateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    command.Parameters.AddWithValue("@EndDateTime", endDateTime.ToString("yyyy-MM-dd HH:mm:ss"));

                    connection.Open();
                    SQLiteDataAdapter adapter = new SQLiteDataAdapter(command);
                    adapter.Fill(dataTable); // Заполнение таблицы результатами запроса

                    LogAction("Выполнена фильтрация заказов по району: " + cityDistrict + " и времени: " + startDateTime);

                    // Проверка, найдены ли заказы
                    if (dataTable.Rows.Count == 0)
                    {
                        MessageBox.Show("Заказы, соответствующие критериям фильтрации, не найдены.");
                        LogAction("Фильтрация завершена: заказы не найдены.");
                    }
                    else
                    {
                        LogAction($"Фильтрация завершена: найдено {dataTable.Rows.Count} заказов.");
                        SaveResultsToFile(dataTable); // Сохранение результатов в файл
                    }
                }
            }
            catch (SQLiteException ex)
            {
                LogError($"Ошибка при выполнении SQL-запроса: {ex.Message}");
                MessageBox.Show("Произошла ошибка при доступе к базе данных. Пожалуйста, попробуйте еще раз.");
            }
            catch (Exception ex)
            {
                LogError($"Неожиданная ошибка: {ex.Message}");
                MessageBox.Show("Произошла неожиданная ошибка. Подробности записаны в лог-файл.");
            }

            return dataTable;
        }

        // Метод для записи результатов в файл
        private void SaveResultsToFile(DataTable dataTable)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(resultFilePath, false))
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        writer.WriteLine(string.Join(", ", row.ItemArray)); // Запись каждой строки данных в файл
                    }
                }
                LogAction("Результаты фильтрации успешно записаны в файл " + resultFilePath);
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при записи результатов в файл: {ex.Message}");
                MessageBox.Show("Произошла ошибка при записи результатов в файл.");
            }
        }

        // Обработчик события нажатия кнопки фильтрации
        private void filterButton_Click(object sender, EventArgs e)
        {
            LogAction("Нажата кнопка фильтрации заказов.");

            string cityDistrict = cityDistrictTextBox.Text;
            DateTime startDateTime;

            // Проверка введенных пользователем данных
            if (string.IsNullOrWhiteSpace(cityDistrict))
            {
                MessageBox.Show("Пожалуйста, введите название района.");
                LogAction("Фильтрация не выполнена: пустое поле названия района.");
                return;
            }

            if (!DateTime.TryParse(startDateTimeTextBox.Text, out startDateTime))
            {
                MessageBox.Show("Введите корректную дату начала в формате yyyy-MM-dd HH:mm:ss.");
                LogAction("Фильтрация не выполнена: некорректный формат даты.");
                return;
            }

            // Загрузка и отображение отфильтрованных данных
            ordersDataGridView.DataSource = GetFilteredOrders(cityDistrict, startDateTime);
            LogAction("Загрузка и отображение отфильтрованных данных завершены.");
        }
    }
}
