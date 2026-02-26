using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SBS_ECS_UI.ViewModels
{
    // 데이터 변경 시 화면(View)에 알림을 주는 기본 클래스
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void onPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}