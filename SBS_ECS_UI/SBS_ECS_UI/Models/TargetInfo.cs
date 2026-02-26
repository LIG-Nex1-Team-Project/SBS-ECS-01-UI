using SBS_ECS_UI.ViewModels;

namespace SBS_ECS_UI.Models
{
    // 탐색기로부터 수신받는 표적 데이터 모델
    public class TargetInfo : ViewModelBase
    {
        private float _posX_mm;
        private float _posY_mm;
        private float _azimuthDegree;

        // 오리지널 좌표 변환 방식이 적용된 X 좌표 (mm)
        public float PosX_mm
        {
            get => _posX_mm;
            set
            {
                _posX_mm = value;
                onPropertyChanged();
            }
        }

        // 오리지널 좌표 변환 방식이 적용된 Y 좌표 (mm)
        public float PosY_mm
        {
            get => _posY_mm;
            set
            {
                _posY_mm = value;
                onPropertyChanged();
            }
        }

        // ECS에서 산출한 최종 발사대 지향 방위각 (도)
        public float AzimuthDegree
        {
            get => _azimuthDegree;
            set
            {
                _azimuthDegree = value;
                onPropertyChanged();
            }
        }
    }
}