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

        // 💡 탐색기 제어 커맨드 추가
        public ICommand StartSeekerCommand { get; }
        public ICommand StopSeekerCommand { get; }
        // 💡 긴급 정지 커맨드 추가
        public ICommand EmergencyStopCommand { get; }

        private string _launcherStatusText = "대기 중";
        public string LauncherStatusText { get => _launcherStatusText; set { _launcherStatusText = value; onPropertyChanged(); } }

        private System.Windows.Media.Brush _statusColor = System.Windows.Media.Brushes.Gray;
        public System.Windows.Media.Brush StatusColor { get => _statusColor; set { _statusColor = value; onPropertyChanged(); } }

        public MainViewModel()
        {
            CurrentTarget = new TargetInfo { PosX_mm = 0, PosY_mm = 0, AzimuthDegree = 0 };
            FireCommand = new RelayCommand(ExecuteFireCommand);

            // 💡 커맨드 연결
            StartSeekerCommand = new RelayCommand(ExecuteStartSeeker);
            StopSeekerCommand = new RelayCommand(ExecuteStopSeeker);
            EmergencyStopCommand = new RelayCommand(ExecuteEmergencyStop);

            _uart = new UartManager("COM3");
            _uart.PacketReceivedEvent += OnPacketReceived;

            if (_uart.OpenPort()) ConnectionStatus = "연결 성공 (COM3)";
            else ConnectionStatus = "연결 실패 (포트 확인 필요)";
        }

        // 💡 탐색기 시작 패킷 전송 로직
        private void ExecuteStartSeeker(object parameter)
        {
            // STX(0x02) | 운용명령(0x02) | ETX(0x03)
            _uart.SendData(new byte[] { 0x02, 0x02, 0x03 });
        }

        // 💡 탐색기 정지 패킷 전송 로직
        private void ExecuteStopSeeker(object parameter)
        {
            // STX(0x02) | 정지명령(0x03) | ETX(0x03)
            _uart.SendData(new byte[] { 0x02, 0x03, 0x03 });
        }

        // 💡 긴급 정지 패킷 전송 로직 (명령 코드: 0x04)
        private void ExecuteEmergencyStop(object parameter)
        {
            // 💡 1. 버튼이 눌렸는지 팝업으로 즉시 확인
            System.Windows.MessageBox.Show("긴급 정지 버튼 클릭됨!");
            // STX(0x02) | 긴급정지(0x04) | ETX(0x03)
            _uart.SendData(new byte[] { 0x02, 0x04, 0x03 });
        }
        // 시리얼 통신으로 들어오는 조각난 문자열을 임시로 모아둘 버퍼
        private string _uartBuffer = "";

        private void OnPacketReceived(byte[] packet)
        {
            if (packet == null || packet.Length == 0) return;

            // 1. STM32에서 들어온 바이트 배열을 문자열로 변환
            string receivedText = System.Text.Encoding.ASCII.GetString(packet);
            _uartBuffer += receivedText;

            // 2. 개행문자(\n)가 들어왔다면 완전한 한 줄의 데이터가 도착한 것
            if (_uartBuffer.Contains("\n"))
            {
                // 데이터가 여러 줄 쌓였을 수 있으므로 분리
                string[] lines = _uartBuffer.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (string line in lines)
                    {
                        // "X,Y,방위각,상태" 형태를 콤마(,) 기준으로 자르기
                        string[] parts = line.Trim().Split(',');

                        if (parts.Length >= 4)
                        {
                            if (float.TryParse(parts[0], out float x) &&
        float.TryParse(parts[1], out float y) &&
        float.TryParse(parts[2], out float angle) &&
        int.TryParse(parts[3], out int status)) // 💡 4번째 상태 값 파싱
                            {
                                CurrentTarget.PosX_mm = x;
                                CurrentTarget.PosY_mm = y;
                                CurrentTarget.AzimuthDegree = angle;

                                // 💡 발사대 상태 업데이트 로직 추가
                                UpdateLauncherStatus(status);

                                UpdateTargetLogic();
                            }
                        }
                    }
                });

                // 처리가 끝난 문자열은 버퍼에서 비워주기
                _uartBuffer = "";
            }
        }

        private void UpdateTargetLogic()
        {
            // 1. 방위각 계산 (주석 처리)
            // STM32 펌웨어 내부에서 오리지널 좌표 변환 방식으로 이미 정밀하게 계산해서 보내주므로, 
            // PC UI에서는 중복 연산할 필요 없이 보드의 두뇌를 믿고 그대로 화면에 띄웁니다!
            // CurrentTarget.AzimuthDegree = _transformer.CalAngle(CurrentTarget.PosX_mm, CurrentTarget.PosY_mm);

            // 2. 거리 계산 및 위험 상태 업데이트
            double distanceMm = Math.Sqrt(Math.Pow(CurrentTarget.PosX_mm, 2) + Math.Pow(CurrentTarget.PosY_mm, 2));

            // 위험 반경 50cm(500mm) 이내 진입 시 경고
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
                // 처음 추가할 때도 방위각을 넣어줍니다.
                EnemyList.Add(new TargetInfo
                {
                    Name = targetId,
                    PosX_mm = CurrentTarget.PosX_mm,
                    PosY_mm = CurrentTarget.PosY_mm,
                    AzimuthDegree = CurrentTarget.AzimuthDegree // 💡 추가
                });
            }
            else
            {
                // 이미 목록에 있을 때 방위각을 업데이트하는 이 코드가 빠져있었습니다!
                targetInList.PosX_mm = CurrentTarget.PosX_mm;
                targetInList.PosY_mm = CurrentTarget.PosY_mm;
                targetInList.AzimuthDegree = CurrentTarget.AzimuthDegree; // 💡 이 한 줄을 추가하세요!
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
        // 💡 상태 값에 따른 텍스트 및 색상 매핑 함수
        private void UpdateLauncherStatus(int statusCode)
        {
            switch (statusCode)
            {
                case 0:
                    LauncherStatusText = "IDLE";
                    StatusColor = System.Windows.Media.Brushes.Gray;
                    break;
                case 1: // LTL_STATUS_ALIGN_DONE
                    LauncherStatusText = "READY";
                    StatusColor = System.Windows.Media.Brushes.LimeGreen;
                    break;
                case 2: // LTL_STATUS_FIRE_DONE
                    LauncherStatusText = "FIRE";
                    StatusColor = System.Windows.Media.Brushes.DeepSkyBlue;
                    break;
                case 3: // LTL_STATUS_ERROR
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