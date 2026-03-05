namespace SBS_ECS_UI
{
    public partial class LogWindow : System.Windows.Window
    {
        public LogWindow(object viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel; // MainViewModel의 SystemLogs를 공유
        }

        // 창을 닫을 때 완전히 종료하지 않고 숨기기만 하려면 아래 코드 추가 (선택사항)
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Visibility = System.Windows.Visibility.Collapsed;
        }
    }
}