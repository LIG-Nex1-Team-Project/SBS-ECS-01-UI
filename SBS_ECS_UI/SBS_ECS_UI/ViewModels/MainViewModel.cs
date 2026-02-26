using SBS_ECS_UI.Models;
using SBS_ECS_UI.Services;
using System;
using System.Diagnostics; // Debug.WriteLine 사용을 위해 추가
using System.Windows;
using System.Windows.Threading;

namespace SBS_ECS_UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        // 하드웨어 제어 및 좌표 변환 모듈
        private UartManager _uart;
        private CoordinateTransformer _transformer = new CoordinateTransformer();

        // 표적 정보 속성
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

        // 이중 안전장치 체크 상태 (R-ECS-SSR-002)
        private bool _isDoubleSafetyChecked;
        public bool IsDoubleSafetyChecked
        {
            get => _isDoubleSafetyChecked;
            set
            {
                _isDoubleSafetyChecked = value;
                onPropertyChanged();
            }
        }

        // UART 연결 상태 표시 속성
        private string _connectionStatus = "연결 대기 중...";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                _connectionStatus = value;
                onPropertyChanged();
            }
        }

        public MainViewModel()
        {
            // 초기 표적 정보 설정
            CurrentTarget = new TargetInfo { PosX_mm = 0, PosY_mm = 0, AzimuthDegree = 0 };

            // 1. UART 매니저 초기화 및 이벤트 구독 (E12-3 CSU)
            _uart = new UartManager("COM3"); // 실제 장치 관리자 포트 번호에 맞게 수정 가능
            _uart.PacketReceivedEvent += OnPacketReceived;

            // 2. 포트 개방 및 UI 상태 업데이트
            if (_uart.OpenPort())
            {
                ConnectionStatus = "연결 성공 (COM3)";
                Debug.WriteLine("STM32와 UART 연결 성공");
            }
            else
            {
                ConnectionStatus = "연결 실패 (포트 확인 필요)";
                Debug.WriteLine("UART 포트 개방 실패");
            }
        }

        /// <summary>
        /// UART 패킷 수신 처리 (E12-3.2.3)
        /// STM32에서 6바이트 패킷이 완성되어 올 때마다 호출됩니다.
        /// </summary>
        private void OnPacketReceived(byte[] packet)
        {
            // 시리얼 수신 스레드에서 UI 요소에 접근하기 위해 Dispatcher 사용
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 출력 창(Debug)에 수신 데이터 로그 표시
                Debug.WriteLine($"패킷 수신: {BitConverter.ToString(packet)}");

                // [패킷 파싱 로직 구현부]
                // SDD 규격에 따라 packet[n] 바이트를 조합하여 PosX, PosY에 할당합니다.
                 
                CurrentTarget.PosX_mm = (packet[1] << 8) | packet[2];
                CurrentTarget.PosY_mm = (packet[3] << 8) | packet[4];
                 

                // 좌표 기반 로직 업데이트 (방위각 계산 등)
                UpdateTargetLogic();
            });
        }

        /// <summary>
        /// 수신된 좌표를 바탕으로 방위각 역산 및 위험 반경 체크
        /// </summary>
        private void UpdateTargetLogic()
        {
            // 1. 발사대 위치 기준 방위각 역산 (CoordinateTransformer 활용)
            CurrentTarget.AzimuthDegree = _transformer.CalAngle(CurrentTarget.PosX_mm, CurrentTarget.PosY_mm);

            // 2. 위험 반경(500mm) 체크 및 경보 (R-ECS-SFR-003)
            double distance = Math.Sqrt(Math.Pow(CurrentTarget.PosX_mm, 2) + Math.Pow(CurrentTarget.PosY_mm, 2));
            if (distance < 500)
            {
                // TODO: 위험 반경 내 진입 시 UI 상의 색상이나 경고 아이콘 처리
            }
        }

        /// <summary>
        /// 교전 통제 명령 전송 (E12-2 CSU)
        /// </summary>
        public void SendFireCommand()
        {
            // 이중 안전장치 확인 (R-ECS-SSR-002)
            if (!IsDoubleSafetyChecked)
            {
                MessageBox.Show("안전 스위치 해제가 필요합니다.");
                return;
            }

            // 발사 명령 패킷 전송 (SDD 4.1.2.1.2.2 규격 준수)
            // STX(0x02), 대상(0x01), 명령(0x05), 데이터(0x00, 0x00), Checksum(0x03) 예시
            byte[] firePacket = new byte[] { 0x02, 0x01, 0x05, 0x00, 0x00, 0x03 };
            _uart.SendData(firePacket);
        }
    }
}