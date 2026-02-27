using SBS_ECS_UI.Models;
using SBS_ECS_UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace SBS_ECS_UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private UartManager _uart;
        private CoordinateTransformer _transformer = new CoordinateTransformer();

        // 실시간 표적 정보
        private TargetInfo _currentTarget;
        public TargetInfo CurrentTarget { get => _currentTarget; set { _currentTarget = value; onPropertyChanged(); } }

        // 적 목록 (Enemy List) 컬렉션
        public ObservableCollection<TargetInfo> EnemyList { get; } = new ObservableCollection<TargetInfo>();

        private bool _isDoubleSafetyChecked;
        public bool IsDoubleSafetyChecked { get => _isDoubleSafetyChecked; set { _isDoubleSafetyChecked = value; onPropertyChanged(); } }

        // 위험 반경 진입 여부 (화면 전시용)
        private bool _isDanger;
        public bool IsDanger { get => _isDanger; set { _isDanger = value; onPropertyChanged(); } }

        private string _connectionStatus = "연결 대기 중...";
        public string ConnectionStatus { get => _connectionStatus; set { _connectionStatus = value; onPropertyChanged(); } }

        private double _pxPerCm = 1.0;
        public double PxPerCm { get => _pxPerCm; set { _pxPerCm = value; onPropertyChanged(); UpdatePixelCoordinates(); } }

        private double _targetX_px;
        private double _targetY_px;
        public double TargetX_px { get => _targetX_px; set { _targetX_px = value; onPropertyChanged(); } }
        public double TargetY_px { get => _targetY_px; set { _targetY_px = value; onPropertyChanged(); } }

        public event Action<string> MessageRequest; // 사격 안전장치 알림용으로 유지
        public ICommand FireCommand { get; }

        public MainViewModel()
        {
            CurrentTarget = new TargetInfo { PosX_mm = 0, PosY_mm = 0, AzimuthDegree = 0 };
            FireCommand = new RelayCommand(ExecuteFireCommand);
            _uart = new UartManager("COM3");
            _uart.PacketReceivedEvent += OnPacketReceived;

            if (_uart.OpenPort()) ConnectionStatus = "연결 성공 (COM3)";
            else ConnectionStatus = "연결 실패 (포트 확인 필요)";
        }

        private void OnPacketReceived(byte[] packet)
        {
            if (packet == null || packet.Length < 6) return;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentTarget.PosX_mm = (short)((packet[1] << 8) | packet[2]);
                CurrentTarget.PosY_mm = (short)((packet[3] << 8) | packet[4]);
                UpdateTargetLogic();
            });
        }

        private void UpdateTargetLogic()
        {
            // 1. 방위각 계산
            CurrentTarget.AzimuthDegree = _transformer.CalAngle(CurrentTarget.PosX_mm, CurrentTarget.PosY_mm);

            // 2. 거리 계산 및 위험 상태 업데이트
            double distanceMm = Math.Sqrt(Math.Pow(CurrentTarget.PosX_mm, 2) + Math.Pow(CurrentTarget.PosY_mm, 2));

            // 💡 팝업 호출 없이 상태값만 변경 (XAML에서 이 값을 바인딩하여 경고창과 형상을 제어합니다)
            IsDanger = distanceMm < 500.0;

            // 3. 적 목록 및 PPI 좌표 업데이트
            UpdateEnemyList();
            UpdatePixelCoordinates();
        }

        private void UpdateEnemyList()
        {
            string targetId = "Target_01";
            var targetInList = EnemyList.FirstOrDefault(t => t.Name == targetId);

            if (targetInList == null)
            {
                EnemyList.Add(new TargetInfo { Name = targetId, PosX_mm = CurrentTarget.PosX_mm, PosY_mm = CurrentTarget.PosY_mm });
            }
            else
            {
                targetInList.PosX_mm = CurrentTarget.PosX_mm;
                targetInList.PosY_mm = CurrentTarget.PosY_mm;
            }
        }

        private void UpdatePixelCoordinates()
        {
            TargetX_px = 250 + (CurrentTarget.PosX_mm / 10.0) * PxPerCm;
            TargetY_px = 250 - (CurrentTarget.PosY_mm / 10.0) * PxPerCm;
        }

        private void ExecuteFireCommand(object parameter)
        {
            if (!IsDoubleSafetyChecked)
            {
                // 안전장치 미해제 시의 알림은 필수 보안 사항이므로 유지합니다
                MessageRequest?.Invoke("안전 스위치 해제가 필요합니다.");
                return;
            }
            byte[] firePacket = new byte[] { 0x02, 0x01, 0x05, 0x00, 0x00, 0x03 };
            _uart.SendData(firePacket);
        }
    }
}