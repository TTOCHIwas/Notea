// App.xaml.cs
using System.Windows;

namespace Notea
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 데이터베이스 초기화
            Database.DatabaseInitializer.InitializeDatabase();
            Database.DatabaseInitializer.UpdateSchemaForDisplayOrder();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            
            base.OnExit(e);
        }
    }
}