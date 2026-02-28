using System;
using SBS_ECS_UI.ViewModels;

namespace SBS_ECS_UI.Models
{
    public class TargetInfo : ViewModelBase
    {
        private string _name;
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
            set
            {
                _posX_mm = value;
                onPropertyChanged();
                onPropertyChanged(nameof(Distance)); // 💡 X가 바뀌면 거리도 새로 고침
            }
        }

        public float PosY_mm
        {
            get => _posY_mm;
            set
            {
                _posY_mm = value;
                onPropertyChanged();
                onPropertyChanged(nameof(Distance)); // 💡 Y가 바뀌면 거리도 새로 고침
            }
        }

        public float AzimuthDegree
        {
            get => _azimuthDegree;
            set { _azimuthDegree = value; onPropertyChanged(); }
        }

        // 💡 실시간 거리 계산 속성 (피타고라스 정리)
        // $$Distance = \sqrt{PosX^2 + PosY^2}$$
        public float Distance => (float)Math.Sqrt(Math.Pow(PosX_mm, 2) + Math.Pow(PosY_mm, 2));
    }
}