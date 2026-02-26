using SBS_ECS_UI.Models;
using System;
using System.Windows.Threading;

namespace SBS_ECS_UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private TargetInfo _currentTarget;
        public TargetInfo CurrentTarget
        {
            get => _currentTarget;
            set
            {
                _currentTarget = value;
                onPropertyChanged();
            }
        }

        private DispatcherTimer _simulationTimer;

        // 💡 좌표 변환기 인스턴스 생성
        private CoordinateTransformer _transformer = new CoordinateTransformer();

        public MainViewModel()
        {
            CurrentTarget = new TargetInfo { PosX_mm = 0, PosY_mm = 0, AzimuthDegree = 0 };

            _simulationTimer = new DispatcherTimer();
            _simulationTimer.Interval = TimeSpan.FromMilliseconds(100);
            _simulationTimer.Tick += SimulateIncomingData;
            _simulationTimer.Start();
        }

        private void SimulateIncomingData(object sender, EventArgs e)
        {
            // 가상의 탐색기 데이터 업데이트
            CurrentTarget.PosX_mm += 15;
            CurrentTarget.PosY_mm += 10;

            // 💡 변환기를 통해 실제 발사대 방위각 역산 및 UI 업데이트
            CurrentTarget.AzimuthDegree = _transformer.CalAngle(CurrentTarget.PosX_mm, CurrentTarget.PosY_mm);
        }
    }
}