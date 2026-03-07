using SBS_ECS_UI.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SBS_ECS_UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // 💡 ViewModel의 메시지 요청 이벤트를 실제 창으로 연결 (Strict MVVM)
            if (this.DataContext is MainViewModel vm)
            {
                vm.MessageRequest += (msg) => MessageBox.Show(this, msg, "시스템 알림");
            }
        }

    }
}