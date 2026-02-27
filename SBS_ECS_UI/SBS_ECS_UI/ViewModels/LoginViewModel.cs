using SBS_ECS_UI.Services; // AuthService 참조를 위해 추가
using System;
using System.Windows;
using System.Windows.Input;

namespace SBS_ECS_UI.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        // 💡 비즈니스 로직 처리를 위한 서비스 인스턴스 (SDD 2.1.3)
        private readonly AuthService _authService = new AuthService();
        private string _password;
        private int _failedCount;

        // 비밀번호 입력 값 (LoginWindow.xaml의 PasswordBox/TextBox와 바인딩)
        public string Password
        {
            get => _password;
            set { _password = value; onPropertyChanged(); } //
        }

        // 틀린 횟수 표시 (E11-1.2.4)
        public int FailedCount
        {
            get => _failedCount;
            set { _failedCount = value; onPropertyChanged(); } //
        }

        // 로그인 성공 시 App.xaml.cs에서 처리할 성공 이벤트
        public event Action OnLoginSuccess;

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(ExecuteLogin);
        }

        /// <summary>
        /// 로그인 실행 로직 (E11-1.2.1)
        /// </summary>
        private void ExecuteLogin(object parameter)
        {
            // 1. AuthService를 통한 비밀번호 검증 (E11-1.2.2)
            if (_authService.VerifyPassword(Password))
            {
                OnLoginSuccess?.Invoke();
            }
            else
            {
                // 2. 인증 실패 처리 및 횟수 증가 (HandleLoginFail)
                FailedCount++;
                Password = ""; // 입력창 초기화

                // 3. 보안 정책 확인: 5회 실패 시 소각 모드 (R-ECS-SSR-004)
                if (FailedCount >= 5)
                {
                    // 데이터 파괴 로직 실행 (Execute_Delete)
                    _authService.ExecuteIncineration();
                    MessageBox.Show("보안 위반으로 시스템이 소각 및 종료됩니다.");
                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show($"비밀번호가 틀렸습니다. (현재 {FailedCount}회 실패)");
                }
            }
        }
    }

    /// <summary>
    /// 버튼 명령 처리를 위한 RelayCommand 구현체
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        public RelayCommand(Action<object> execute) => _execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged;
    }
}