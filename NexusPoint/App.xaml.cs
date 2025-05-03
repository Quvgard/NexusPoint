using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NexusPoint
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Инициализируем базу данных (создаст файл и таблицы, если их нет)
            Data.DatabaseHelper.InitializeDatabaseIfNotExists();

            // Создаем и показываем главное окно выбора режима
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();

            // Опционально: Показать LoginWindow сразу, если это требуется
            // LoginWindow login = new LoginWindow();
            // if(login.ShowDialog() == true) // Показать как диалог
            // {
            //    // Логин успешен, открываем MainWindow или сразу нужный режим
            //    MainWindow mainWindow = new MainWindow();
            //    mainWindow.LoggedInUser = login.AuthenticatedUser; // Передать пользователя
            //    mainWindow.Show();
            // }
            // else
            // {
            //    // Логин не удался или окно закрыли, завершаем приложение
            //    Shutdown();
            // }
        }
    }
}