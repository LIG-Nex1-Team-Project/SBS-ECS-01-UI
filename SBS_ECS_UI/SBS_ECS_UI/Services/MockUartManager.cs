using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SBS_ECS_UI.Services
{
    internal class MockUartManager
    {
        public event Action<byte[]> PacketReceivedEvent;

        private DispatcherTimer _timer;

        // 가상 STM32 내부 상태 변수
        private float _currentX = 800.0f;
        private float _currentY = 800.0f;

        // 💡 상태 값 매핑: "1"=Standby, "2"=Align, "3"=Launch, "4"=Error
        private string _status = "1";
        private bool _isSeekerRunning = false;
        private int _debugMsgCounter = 0;

        public MockUartManager(string portName)
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(200);
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
                    _status = "1"; // 💡 "stanby" -> "1"
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
                    _status = "4"; // 💡 "error" -> "4"
                    SendMockData();
                    break;
            }
        }

        private async void SimulateAlign()
        {
            // 원래 로직: 1.5초 대기 후 상태 변경
            await Task.Delay(1000);
            _status = "2"; // 💡 "align" -> "2"
        }

        private async void SimulateFire()
        {
            // 원래 로직: 0.5초 대기 후 발사 상태, 2초 후 대기 상태 복귀
            await Task.Delay(500);
            _status = "3"; // 💡 "launch" -> "3"

            await Task.Delay(2000);
            _status = "1"; // 💡 "stanby" -> "1"

            _currentX = 800.0f;
            _currentY = 800.0f;
        }

        private void SendMockData()
        {
            if (_isSeekerRunning)
            {
                _currentX -= 15.0f;
                _currentY -= 15.0f;
                if (_currentX < 0) { _currentX = 800.0f; _currentY = 800.0f; }
            }

            float deltaX = _currentX - 500.0f;
            float deltaY = _currentY - 0.0f;
            float angle = (float)(Math.Atan2(deltaY, deltaX) * (180.0 / Math.PI));
            if (angle < 0) angle += 360.0f;

            // 1. 제어 패킷 전송
            string dataStr = $"{_currentX:F1},{_currentY:F1},{angle:F1},{_status}\n";
            byte[] packet = Encoding.ASCII.GetBytes(dataStr);
            PacketReceivedEvent?.Invoke(packet);

            // 2. 로그 창 테스트용 디버그 메시지 (원래 로직 유지용)
            _debugMsgCounter++;
            if (_debugMsgCounter >= 5)
            {
                _debugMsgCounter = 0;
                string debugStr = $"[STM32 DEBUG] CPU Temp: 42.5C, Seeker: {(_isSeekerRunning ? "ON" : "OFF")}\n";
                byte[] debugPacket = Encoding.ASCII.GetBytes(debugStr);
                PacketReceivedEvent?.Invoke(debugPacket);
            }
        }
    }
}