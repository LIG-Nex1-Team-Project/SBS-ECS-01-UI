using SBS_ECS_UI.Models;
using SBS_ECS_UI.Services;
using System;
using System.Diagnostics;
using System.Windows.Input; // ICommand 사용을 위해 추가

namespace SBS_ECS_UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private UartManager _uart;
        private CoordinateTransformer _transformer = new CoordinateTransformer();

        // 속성들 (CurrentTarget, IsDoubleSafetyChecked, IsDanger, ConnectionStatus)
        private TargetInfo _currentTarget;
        public TargetInfo CurrentTarget { get => _currentTarget; set { _currentTarget = value; onPropertyChanged(); } }

        private bool _isDoubleSafetyChecked;
        public bool IsDoubleSafetyChecked { get => _isDoubleSafetyChecked; set { _isDoubleSafetyChecked = value; onPropertyChanged(); } }

        private bool _isDanger;
        public bool IsDanger { get => _isDanger; set { _isDanger = value; onPropertyChanged(); } }

        private string _connectionStatus = "연결 대기 중...";
        public string ConnectionStatus { get => _connectionStatus; set { _connectionStatus = value; onPropertyChanged(); } }

        // 이벤트 및 커맨드
        public event Action<string> MessageRequest;
        public ICommand FireCommand { get; } // 💡 버튼과 바인딩할 커맨드

        public MainViewModel()
        {
            CurrentTarget = new TargetInfo { PosX_mm = 0, PosY_mm = 0, AzimuthDegree = 0 };

            // 커맨드 초기화
            FireCommand = new RelayCommand(ExecuteFireCommand);

            _uart = new UartManager("COM3");
            _uart.PacketReceivedEvent += OnPacketReceived;

            if (_uart.OpenPort())
            {
                ConnectionStatus = "연결 성공 (COM3)";
            }
            else
            {
                ConnectionStatus = "연결 실패";
            }
        }

        private void OnPacketReceived(byte[] packet)
        {
            if (packet == null || packet.Length < 6) return;

            // 💡 WPF는 속성이 변경되면 자동으로 UI 스레드에서 업데이트를 시도하므로 
            // 단순 값 대입은 Dispatcher 없이도 가능한 경우가 많습니다. (환경에 따라 조정)
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentTarget.PosX_mm = (short)((packet[1] << 8) | packet[2]);
                CurrentTarget.PosY_mm = (short)((packet[3] << 8) | packet[4]);
                UpdateTargetLogic();
            });
        }

        private void UpdateTargetLogic()
        {
            CurrentTarget.AzimuthDegree = _transformer.CalAngle(CurrentTarget.PosX_mm, CurrentTarget.PosY_mm);
            double distance = Math.Sqrt(Math.Pow(CurrentTarget.PosX_mm, 2) + Math.Pow(CurrentTarget.PosY_mm, 2));
            IsDanger = distance < 500;
        }

        // 💡 실제 발사 로직 (ICommand에 의해 실행됨)
        private void ExecuteFireCommand(object parameter)
        {
            if (!IsDoubleSafetyChecked)
            {
                MessageRequest?.Invoke("안전 스위치 해제가 필요합니다.");
                return;
            }

            byte[] firePacket = new byte[] { 0x02, 0x01, 0x05, 0x00, 0x00, 0x03 };
            _uart.SendData(firePacket);
            Debug.WriteLine("발사 명령 전송 완료");
        }
    }
}