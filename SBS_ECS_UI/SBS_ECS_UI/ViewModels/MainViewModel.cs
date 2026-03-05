using SBS_ECS_UI.Models;
using SBS_ECS_UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
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
        public ObservableCollection<TargetInfo> EnemyList { get; } = new ObservableCollection<TargetInfo>();

        // 💡 시스템 로그 및 자동 팝업 관리
        public ObservableCollection<string> SystemLogs { get; } = new ObservableCollection<string>();
        private LogWindow _logWindow;
        private bool _isFirstDataReceived = false;

        private string _currentStatus = "0";
        private bool _isDoubleSafetyChecked;
        public bool IsDoubleSafetyChecked
        {
            get => _isDoubleSafetyChecked;
            set { _isDoubleSafetyChecked = value; onPropertyChanged(); onPropertyChanged(nameof(CanFire)); }
        }

        public bool CanAlign => _currentStatus == "1" || _currentStatus == "2" ||
                                _currentStatus == "standby" || _currentStatus == "align";

        public bool CanFire => (_currentStatus == "2" || _currentStatus == "align") && IsDoubleSafetyChecked;

        private bool _isDanger;
        public bool IsDanger { get => _isDanger; set { _isDanger = value; onPropertyChanged(); } }

        private string _connectionStatus = "연결 대기 중...";
        public string ConnectionStatus { get => _connectionStatus; set { _connectionStatus = value; onPropertyChanged(); } }

        private double _pxPerCm = 1.0;
        public double PxPerCm { get => _pxPerCm; set { _pxPerCm = value; onPropertyChanged(); UpdatePixelCoordinates(); } }

        // 레이더 좌표용 변수
        public double TargetX_px { get; set; }
        public double TargetY_px { get; set; }
        public double LauncherX_px { get; set; }
        public double LauncherY_px { get; set; }
        public bool ShowAlignLine { get; set; }
        public double AlignLineX2 { get; set; }
        public double AlignLineY2 { get; set; }
        private double _lastAlignedAngle = 0;

        public event Action<string> MessageRequest;

        // 커맨드 객체
        public ICommand AlignCommand { get; }
        public ICommand FireCommand { get; }
        public ICommand StartSeekerCommand { get; }
        public ICommand StopSeekerCommand { get; }
        public ICommand EmergencyStopCommand { get; }

        public string LauncherStatusText { get; set; } = "INITIALIZING";
        public System.Windows.Media.Brush StatusColor { get; set; } = System.Windows.Media.Brushes.Orange;

        public MainViewModel()
        {
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
            onPropertyChanged(nameof(ShowAlignLine));
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

            // 💡 UI 업데이트 및 로그 창 제어
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 1. 원시 로그 기록
                SystemLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] RX: {line}");
                if (SystemLogs.Count > 100) SystemLogs.RemoveAt(100);

                // 2. 💡 데이터가 처음 들어오면 자동으로 로그 창 팝업
                if (!_isFirstDataReceived)
                {
                    _isFirstDataReceived = true;
                    if (_logWindow == null)
                    {
                        _logWindow = new LogWindow(this);
                        _logWindow.Show();
                    }
                    else
                    {
                        _logWindow.Visibility = Visibility.Visible;
                    }
                }

                // 3. 기존 제어 데이터 파싱
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

        // ... 이하 기존 좌표 업데이트 로직 생략 (UpdateTargetLogic, UpdatePixelCoordinates 등)
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
            onPropertyChanged(nameof(TargetX_px));
            onPropertyChanged(nameof(TargetY_px));
            onPropertyChanged(nameof(LauncherX_px));
            onPropertyChanged(nameof(LauncherY_px));
            UpdateAlignLine();
        }

        private void UpdateAlignLine()
        {
            if (!ShowAlignLine) return;
            double rad = _lastAlignedAngle * Math.PI / 180.0;
            double lineLength = 300.0;
            AlignLineX2 = LauncherX_px + lineLength * Math.Cos(rad);
            AlignLineY2 = LauncherY_px - lineLength * Math.Sin(rad);
            onPropertyChanged(nameof(AlignLineX2));
            onPropertyChanged(nameof(AlignLineY2));
        }

        private void UpdateLauncherStatus(string statusString)
        {
            _currentStatus = statusString;
            onPropertyChanged(nameof(CanAlign));
            onPropertyChanged(nameof(CanFire));

            switch (statusString)
            {
                case "0": LauncherStatusText = "INITIALIZING"; StatusColor = System.Windows.Media.Brushes.Orange; break;
                case "1": case "standby": LauncherStatusText = "STANDBY"; StatusColor = System.Windows.Media.Brushes.Gray; break;
                case "2": case "align": LauncherStatusText = "ALIGN"; StatusColor = System.Windows.Media.Brushes.LimeGreen; break;
                case "3": case "launch": LauncherStatusText = "LAUNCH"; StatusColor = System.Windows.Media.Brushes.DeepSkyBlue; break;
                case "4": case "error": LauncherStatusText = "ERROR"; StatusColor = System.Windows.Media.Brushes.Red; break;
                default: LauncherStatusText = "UNKNOWN (" + statusString + ")"; StatusColor = System.Windows.Media.Brushes.Purple; break;
            }
            onPropertyChanged(nameof(LauncherStatusText));
            onPropertyChanged(nameof(StatusColor));
        }
    }
}