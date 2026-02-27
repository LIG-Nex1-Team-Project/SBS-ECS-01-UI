using System.Windows;
using SBS_ECS_UI.ViewModels; // ViewModel 참조

namespace SBS_ECS_UI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 로그인 창과 뷰모델 생성
            var loginWindow = new LoginWindow();
            var loginVM = new LoginViewModel();
            loginWindow.DataContext = loginVM;

            // 2. 로그인 성공 이벤트 구독
            loginVM.OnLoginSuccess += () =>
            {
                // 3. 메인 화면 생성 및 표시
                var mainWindow = new MainWindow();
                mainWindow.Show();

                // 4. 로그인 창 닫기
                loginWindow.Close();
            };

            // 로그인 창 표시
            loginWindow.Show();
        }
    }
}