using Microsoft.Data.Sqlite;
using Notea.Database;
using System.Data.SQLite;
using System.IO;
using System.Windows;

namespace Notea
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            using var conn = new SqliteConnection("Data Source=notea.db");
            conn.Open();
        }
    }
}
