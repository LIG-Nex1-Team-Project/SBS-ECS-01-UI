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
        //private MockUartManager _uart;
        private CoordinateTransformer _transformer = new CoordinateTransformer();

        // 실시간 표적 정보
        private TargetInfo _currentTarget;
        public TargetInfo CurrentTarget { get => _currentTarget; set { _currentTarget = value; onPropertyChanged(); } }

        // 적 목록 (Enemy List) 컬렉션
        public ObservableCollection<TargetInfo> EnemyList { get; } = new ObservableCollection<TargetInfo>();

        private string _currentStatus = "stanby";

        private bool _isDoubleSafetyChecked;
        public bool IsDoubleSafetyChecked
        {
            get => _isDoubleSafetyChecked;
            set
            {
                _isDoubleSafetyChecked = value;
                onPropertyChanged();
                onPropertyChanged(nameof(CanFire));
            }
        }

        public bool CanAlign => _currentStatus == "stanby" || _currentStatus == "align";
        public bool CanFire => _currentStatus == "align" && IsDoubleSafetyChecked;

        // 위험 반경 진입 여부
        private bool _isDanger;
        public bool IsDanger { get => _isDanger; set { _isDanger = value; onPropertyChanged(); } }

        private string _connectionStatus = "연결 대기 중...";
        public string ConnectionStatus { get => _connectionStatus; set { _connectionStatus = value; onPropertyChanged(); } }

        private double _pxPerCm = 1.0;
        public double PxPerCm { get => _pxPerCm; set { _pxPerCm = value; onPropertyChanged(); UpdatePixelCoordinates(); } }

        // 화면 좌표 프로퍼티
        private double _targetX_px;
        private double _targetY_px;
        public double TargetX_px { get => _targetX_px; set { _targetX_px = value; onPropertyChanged(); } }
        public double TargetY_px { get => _targetY_px; set { _targetY_px = value; onPropertyChanged(); } }

        // 발사대 및 정렬 선 좌표 프로퍼티
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

        private double _lastAlignedAngle = 0; // 정렬 명령 하달 시점의 각도 기억용

        public event Action<string> MessageRequest;

        // 커맨드 정의
        public ICommand AlignCommand { get; }
        public ICommand FireCommand { get; }
        public ICommand StartSeekerCommand { get; }
        public ICommand StopSeekerCommand { get; }
        public ICommand EmergencyStopCommand { get; }

        private string _launcherStatusText = "STANDBY";
        public string LauncherStatusText { get => _launcherStatusText; set { _launcherStatusText = value; onPropertyChanged(); } }

        private System.Windows.Media.Brush _statusColor = System.Windows.Media.Brushes.Gray;
        public System.Windows.Media.Brush StatusColor { get => _statusColor; set { _statusColor = value; onPropertyChanged(); } }

        public MainViewModel()
        {
            // 💡 초기화 시 표적 이름(Name) 할당
            CurrentTarget = new TargetInfo { Name = "Target_01", PosX_mm = 0, PosY_mm = 0, AzimuthDegree = 0 };

            AlignCommand = new RelayCommand(ExecuteAlignCommand);
            FireCommand = new RelayCommand(ExecuteFireCommand);
            StartSeekerCommand = new RelayCommand(ExecuteStartSeeker);
            StopSeekerCommand = new RelayCommand(ExecuteStopSeeker);
            EmergencyStopCommand = new RelayCommand(ExecuteEmergencyStop);

            _uart = new UartManager("COM3");
            //_uart = new MockUartManager("COM3");
            _uart.PacketReceivedEvent += OnPacketReceived;

            if (_uart.OpenPort()) ConnectionStatus = "연결 성공 (COM3)";
            else ConnectionStatus = "연결 실패 (포트 확인 필요)";
        }

        private void ExecuteStartSeeker(object parameter)
        {
            _uart.SendControlCommand(UartManager.SystemCommand.SeekerStart);
        }

        private void ExecuteStopSeeker(object parameter)
        {
            _uart.SendControlCommand(UartManager.SystemCommand.SeekerStop);
        }

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
            if (!IsDoubleSafetyChecked)
            {
                MessageRequest?.Invoke("안전 스위치 해제가 필요합니다.");
                return;
            }

            _uart.SendControlCommand(UartManager.SystemCommand.LauncherFire);
        }

        private void OnPacketReceived(byte[] packet)
        {
            if (packet == null || packet.Length == 0) return;

            string line = System.Text.Encoding.ASCII.GetString(packet).Trim();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                string[] parts = line.Split(',');

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

            if (targetInList == null)
            {
                EnemyList.Add(new TargetInfo
                {
                    Name = targetId,
                    PosX_mm = CurrentTarget.PosX_mm,
                    PosY_mm = CurrentTarget.PosY_mm,
                    AzimuthDegree = CurrentTarget.AzimuthDegree
                });
            }
            else
            {
                targetInList.PosX_mm = CurrentTarget.PosX_mm;
                targetInList.PosY_mm = CurrentTarget.PosY_mm;
                targetInList.AzimuthDegree = CurrentTarget.AzimuthDegree;
            }
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

            AlignLineX2 = LauncherX_px + 500.0 * Math.Cos(rad);
            AlignLineY2 = LauncherY_px - 500.0 * Math.Sin(rad);
        }

        private void UpdateLauncherStatus(string statusString)
        {
            _currentStatus = statusString;
            onPropertyChanged(nameof(CanAlign));
            onPropertyChanged(nameof(CanFire));

            switch (statusString)
            {
                case "stanby":
                case "standby":
                    LauncherStatusText = "STANDBY";
                    StatusColor = System.Windows.Media.Brushes.Gray;
                    break;
                case "align":
                    LauncherStatusText = "ALIGN";
                    StatusColor = System.Windows.Media.Brushes.LimeGreen;
                    break;
                case "launch":
                    LauncherStatusText = "LAUNCH";
                    StatusColor = System.Windows.Media.Brushes.DeepSkyBlue;
                    break;
                case "error":
                    LauncherStatusText = "ERROR";
                    StatusColor = System.Windows.Media.Brushes.Red;
                    break;
                default:
                    LauncherStatusText = "UNKNOWN";
                    StatusColor = System.Windows.Media.Brushes.Orange;
                    break;
            }
        }
    }
}