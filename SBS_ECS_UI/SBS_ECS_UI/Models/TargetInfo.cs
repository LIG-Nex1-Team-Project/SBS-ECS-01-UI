using SBS_ECS_UI.ViewModels;

namespace SBS_ECS_UI.Models
{
    public class TargetInfo : ViewModelBase
    {
        private string _name; // 💡 이름 속성 추가
        private float _posX_mm;
        private float _posY_mm;
        private float _azimuthDegree;

        public string Name
        {
            get => _name;
            set { _name = value; onPropertyChanged(); }
        }

        public float PosX_mm
        {
            get => _posX_mm;
            set { _posX_mm = value; onPropertyChanged(); }
        }

        public float PosY_mm
        {
            get => _posY_mm;
            set { _posY_mm = value; onPropertyChanged(); }
        }

        public float AzimuthDegree
        {
            get => _azimuthDegree;
            set { _azimuthDegree = value; onPropertyChanged(); }
        }
    }
}