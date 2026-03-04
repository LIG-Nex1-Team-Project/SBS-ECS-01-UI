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

        public ObservableCollection<TargetInfo> EnemyList { get; } = new ObservableCollection<TargetInfo>();

        // 💡 1. 초기 상태 변수를 "0"으로 설정
        private string _currentStatus = "0";

        private bool _isDoubleSafetyChecked;
        public bool IsDoubleSafetyChecked
        {
            get => _isDoubleSafetyChecked;
            set { _isDoubleSafetyChecked = value; onPropertyChanged(); onPropertyChanged(nameof(CanFire)); }
        }

        // 💡 버튼 활성화: 1(Standby) 또는 2(Align)일 때만 정렬 가능
        public bool CanAlign => _currentStatus == "1" || _currentStatus == "2" ||
                                _currentStatus == "standby" || _currentStatus == "align";

        // 💡 사격 승인: 2(Align) 상태이면서 안전장치가 해제되어야 함
        public bool CanFire => (_currentStatus == "2" || _currentStatus == "align") && IsDoubleSafetyChecked;

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

        private double _launcherX_px;
        private double _launcherY_px;
        public double LauncherX_px { get => _launcherX_px; set { _launcherX_px = value; onPropertyChanged(); } }
        public double LauncherY_px { get => _launcherY_px; set { _launcherY_px = value; onPropertyChanged(); } }

        private bool _showAlignLine;
        public bool ShowAlignLine { get => _showAlignLine; set { _showAlignLine = value; onPropertyChanged(); } }

        private double _alignLineX2;
        private double _alignLineY2;
        public double AlignLineX2 { get => _alignLineX2; set { _alignLineX2 = value; onPropertyChanged(); } }
        public double AlignLineY2 { get => _alignLineY2; set { _alignLineY2 = value; onPropertyChanged(); } }

        private double _lastAlignedAngle = 0;

        public event Action<string> MessageRequest;

        public ICommand AlignCommand { get; }
        public ICommand FireCommand { get; }
        public ICommand StartSeekerCommand { get; }
        public ICommand StopSeekerCommand { get; }
        public ICommand EmergencyStopCommand { get; }

        // 💡 2. 초기 텍스트를 INITIALIZING으로 설정
        private string _launcherStatusText = "INITIALIZING";
        public string LauncherStatusText { get => _launcherStatusText; set { _launcherStatusText = value; onPropertyChanged(); } }

        // 💡 3. 초기 색상을 Orange로 설정
        private System.Windows.Media.Brush _statusColor = System.Windows.Media.Brushes.Orange;
        public System.Windows.Media.Brush StatusColor { get => _statusColor; set { _statusColor = value; onPropertyChanged(); } }

        public MainViewModel()
        {
            CurrentTarget = new TargetInfo { Name = "Target_01", PosX_mm = 0, PosY_mm = 0, AzimuthDegree = 0 };

            AlignCommand = new RelayCommand(ExecuteAlignCommand);
            FireCommand = new RelayCommand(ExecuteFireCommand);
            StartSeekerCommand = new RelayCommand(ExecuteStartSeeker);
            StopSeekerCommand = new RelayCommand(ExecuteStopSeeker);
            EmergencyStopCommand = new RelayCommand(ExecuteEmergencyStop);

            _uart = new UartManager("COM3"); // 실제 포트 번호 확인 필요
            _uart.PacketReceivedEvent += OnPacketReceived;

            if (_uart.OpenPort()) ConnectionStatus = "연결 성공 (COM3)";
            else ConnectionStatus = "연결 실패 (포트 확인 필요)";
        }

        private void ExecuteStartSeeker(object parameter) => _uart.SendControlCommand(UartManager.SystemCommand.SeekerStart);
        private void ExecuteStopSeeker(object parameter) => _uart.SendControlCommand(UartManager.SystemCommand.SeekerStop);

        private void ExecuteEmergencyStop(object parameter)
        {
            _uart.SendControlCommand(UartManager.SystemCommand.EmergencyStop);
            MessageRequest?.Invoke("긴급 정지 명령이 하달되었습니다.");
        }

        private void ExecuteAlignCommand(object parameter)
        {
            _uart.SendControlCommand(UartManager.SystemCommand.LauncherAlign);
            _lastAlignedAngle = CurrentTarget.AzimuthDegree;
            ShowAlignLine = true;
            UpdateAlignLine();
        }

        private void ExecuteFireCommand(object parameter)
        {
            if (!IsDoubleSafetyChecked) { MessageRequest?.Invoke("안전 스위치 해제가 필요합니다."); return; }
            _uart.SendControlCommand(UartManager.SystemCommand.LauncherFire);
        }

        private void OnPacketReceived(byte[] packet)
        {
            if (packet == null || packet.Length == 0) return;
            string line = System.Text.Encoding.ASCII.GetString(packet).Trim();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                string[] parts = line.Split(',');
                // 💡 STM32 패킷 규격: X, Y, Angle, Status (총 4개 필드)
                if (parts.Length >= 4)
                {
                    if (float.TryParse(parts[0], out float x) &&
                        float.TryParse(parts[1], out float y) &&
                        float.TryParse(parts[2], out float angle))
                    {
                        CurrentTarget.PosX_mm = x;
                        CurrentTarget.PosY_mm = y;
                        CurrentTarget.AzimuthDegree = angle;

                        string statusString = parts[3].Trim().ToLower();
                        UpdateLauncherStatus(statusString);
                        UpdateTargetLogic();
                    }
                }
            });
        }

        private void UpdateTargetLogic()
        {
            double distanceMm = Math.Sqrt(Math.Pow(CurrentTarget.PosX_mm, 2) + Math.Pow(CurrentTarget.PosY_mm, 2));
            IsDanger = distanceMm < 500.0;
            UpdateEnemyList();
            UpdatePixelCoordinates();
        }

        private void UpdateEnemyList()
        {
            string targetId = "Target_01";
            var targetInList = EnemyList.FirstOrDefault(t => t.Name == targetId);
            if (targetInList == null) EnemyList.Add(new TargetInfo { Name = targetId, PosX_mm = CurrentTarget.PosX_mm, PosY_mm = CurrentTarget.PosY_mm, AzimuthDegree = CurrentTarget.AzimuthDegree });
            else { targetInList.PosX_mm = CurrentTarget.PosX_mm; targetInList.PosY_mm = CurrentTarget.PosY_mm; targetInList.AzimuthDegree = CurrentTarget.AzimuthDegree; }
        }

        private void UpdatePixelCoordinates()
        {
            TargetX_px = 250 + (CurrentTarget.PosX_mm / 10.0) * PxPerCm;
            TargetY_px = 250 - (CurrentTarget.PosY_mm / 10.0) * PxPerCm;
            LauncherX_px = 250 + (500.0 / 10.0) * PxPerCm;
            LauncherY_px = 250;
            UpdateAlignLine();
        }

        private void UpdateAlignLine()
        {
            if (!ShowAlignLine) return;
            double rad = _lastAlignedAngle * Math.PI / 180.0;
            double lineLength = 300.0;
            AlignLineX2 = LauncherX_px + lineLength * Math.Cos(rad);
            AlignLineY2 = LauncherY_px - lineLength * Math.Sin(rad);
        }

        private void UpdateLauncherStatus(string statusString)
        {
            _currentStatus = statusString;
            onPropertyChanged(nameof(CanAlign));
            onPropertyChanged(nameof(CanFire));

            switch (statusString)
            {
                case "0":
                    LauncherStatusText = "INITIALIZING";
                    StatusColor = System.Windows.Media.Brushes.Orange;
                    break;
                case "1":
                case "standby":
                    LauncherStatusText = "STANDBY";
                    StatusColor = System.Windows.Media.Brushes.Gray;
                    break;
                case "2":
                case "align":
                    LauncherStatusText = "ALIGNING";
                    StatusColor = System.Windows.Media.Brushes.LimeGreen;
                    break;
                case "3":
                case "launch":
                    LauncherStatusText = "LAUNCH";
                    StatusColor = System.Windows.Media.Brushes.DeepSkyBlue;
                    break;
                case "4":
                case "error":
                    LauncherStatusText = "ERROR";
                    StatusColor = System.Windows.Media.Brushes.Red;
                    break;
                default:
                    LauncherStatusText = "UNKNOWN (" + statusString + ")";
                    StatusColor = System.Windows.Media.Brushes.Purple;
                    break;
            }
        }
    }
}