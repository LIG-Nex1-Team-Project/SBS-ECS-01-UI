using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SBS_ECS_UI.Services
{
    /// <summary>
    /// 실제 보드 없이 WPF UI를 테스트하기 위한 가상 STM32 시뮬레이터
    /// </summary>
    internal class MockUartManager
    {
        public event Action<byte[]> PacketReceivedEvent;

        private DispatcherTimer _timer;

        // 가상 STM32 내부 상태 변수
        private float _currentX = 800.0f;
        private float _currentY = 800.0f;

        // 💡 상태 변수를 int(0, 1, 2)에서 문자열로 변경
        private string _status = "stanby";
        private bool _isSeekerRunning = false;

        public MockUartManager(string portName)
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(200); // 0.2초마다 데이터 송신
            _timer.Tick += (s, e) => SendMockData();
        }

        public bool OpenPort()
        {
            MessageBox.Show("[테스트 모드] 가상 보드(Simulator)에 연결되었습니다.\n데이터가 자동으로 생성됩니다.", "시뮬레이션 모드");
            return true;
        }

        public void ClosePort()
        {
            _timer.Stop();
        }

        public void SendData(byte[] data) { }

        public void SendControlCommand(UartManager.SystemCommand cmd)
        {
            switch (cmd)
            {
                case UartManager.SystemCommand.SeekerStart:
                    _isSeekerRunning = true;
                    _currentX = 800.0f;
                    _currentY = 800.0f;
                    _status = "stanby"; // 💡 초기 상태
                    _timer.Start();
                    break;

                case UartManager.SystemCommand.SeekerStop:
                    _isSeekerRunning = false;
                    _timer.Stop();
                    break;

                case UartManager.SystemCommand.LauncherAlign:
                    SimulateAlign();
                    break;

                case UartManager.SystemCommand.LauncherFire:
                    SimulateFire();
                    break;

                case UartManager.SystemCommand.EmergencyStop:
                    _isSeekerRunning = false;
                    _status = "error"; // 💡 에러 상태
                    SendMockData();
                    break;
            }
        }

        private async void SimulateAlign()
        {
            await Task.Delay(1500);
            _status = "align"; // 💡 정렬 완료 상태
        }

        private async void SimulateFire()
        {
            await Task.Delay(500);
            _status = "launch"; // 💡 발사 완료 상태

            // 발사 완료(launch) 상태를 2초간 보여준 뒤, 자동으로 stanby 상태로 복귀
            await Task.Delay(2000);
            _status = "stanby";

            // 사격 후 새로운 표적 위치를 멀리 초기화
            _currentX = 800.0f;
            _currentY = 800.0f;
        }

        private void SendMockData()
        {
            if (_isSeekerRunning)
            {
                _currentX -= 15.0f;
                _currentY -= 15.0f;

                if (_currentX < 0 && _currentY < 0)
                {
                    _currentX = 800.0f;
                    _currentY = 800.0f;
                }
            }

            float deltaX = _currentX - 500.0f;
            float deltaY = _currentY - 0.0f;
            float angle = (float)(Math.Atan2(deltaY, deltaX) * (180.0 / Math.PI));
            if (angle < 0) angle += 360.0f;

            // 💡 문자열이 포함된 패킷 전송 (예: "650.0,650.0,45.0,align\n")
            string dataStr = $"{_currentX:F1},{_currentY:F1},{angle:F1},{_status}\n";
            byte[] packet = Encoding.ASCII.GetBytes(dataStr);

            PacketReceivedEvent?.Invoke(packet);
        }
    }
}