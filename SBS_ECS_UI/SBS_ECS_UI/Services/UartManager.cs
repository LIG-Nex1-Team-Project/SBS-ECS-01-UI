using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Windows;

namespace SBS_ECS_UI.Services
{
    /// <summary>
    /// STM32 보드와 UART 통신을 관리하는 클래스 (SDD 4.1.1.3.2)
    /// </summary>
    internal class UartManager
    {
        // SDD 4.1.1.3.2 설계 변수
        private SerialPort _serialPort; // 시리얼 통신 핵심 객체
        private string _portName;       // 연결된 COM 포트 번호
        private int _baudRate = 115200; // STM32와 동일한 통신 속도
        private List<byte> _rcvBuffer = new List<byte>(); // 수신 데이터 임시 저장 버퍼

        // 제어 명령 상수 (STX/ETX)
        private const byte STX = 0x02;
        private const byte ETX = 0x03;

        /// <summary>
        /// SDD에 정의된 시스템 제어 명령 코드 열거형
        /// </summary>
        public enum SystemCommand : byte
        {
            LauncherAlign = 0x00,     // 발사대 정렬 명령 (LTL_CMD_ALIGN)
            LauncherFire = 0x01,      // 발사대 사격 명령 (LTL_CMD_FIRE)
            SeekerStart = 0x02,       // 탐색기 운용 시작 (DET_CMD_STANDBY)
            SeekerStop = 0x03,        // 탐색기 운용 중지 (DET_CMD_RESET)
            EmergencyStop = 0x04      // 시스템 긴급 정지
        }

        // MVVM 패턴에서 ViewModel과 소통하기 위한 이벤트
        public event Action<byte[]> PacketReceivedEvent;

        public UartManager(string portName)
        {
            _portName = portName;
        }

        /// <summary>
        /// UART 포트 초기화 및 연결 (SDD 4.1.1.3.2.1)
        /// </summary>
        public bool OpenPort()
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen) return true;

                // 객체 생성 및 파라미터 설정 (8-N-1 설정)
                _serialPort = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One);

                // 데이터 수신 이벤트 핸들러 등록
                _serialPort.DataReceived += OnDataReceived;

                _serialPort.Open();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"UART 연결 실패 ({_portName}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// UART 연결 종료 (SDD 4.1.1.3.2)
        /// </summary>
        public void ClosePort()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }

        /// <summary>
        /// 데이터를 하드웨어(STM32)로 전송 (SDD 4.1.1.3.2.2)
        /// </summary>
        public void SendData(byte[] data)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Write(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"전송 에러: {ex.Message}");
            }
        }

        /// <summary>
        /// 발사대 및 탐색기 제어 명령 전송 메서드
        /// [STX(0x02)] [Command] [ETX(0x03)] 규격 적용
        /// </summary>
        public void SendControlCommand(SystemCommand cmd)
        {
            //byte[] packet = new byte[] { STX, (byte)cmd, ETX };
            byte[] packet = new byte[] { (byte)cmd };

            SendData(packet);
        }

        /// <summary>
        /// UART 수신 이벤트 핸들러 (SDD 4.1.1.3.2.3)
        /// </summary>
        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            try
            {
                int bytesToRead = _serialPort.BytesToRead;
                byte[] buffer = new byte[bytesToRead];

                _serialPort.Read(buffer, 0, bytesToRead);
                _rcvBuffer.AddRange(buffer);

                // 패킷 분석 호출
                ParseUart();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"수신 에러: {ex.Message}");
            }
        }

        /// <summary>
        /// 수신 데이터 파싱 (SDD 4.1.1.3.2.4)
        /// </summary>
        private void ParseUart()
        {
            while (_rcvBuffer.Contains(0x0A))
            {
                int lfIndex = _rcvBuffer.IndexOf(0x0A);
                byte[] packet = _rcvBuffer.Take(lfIndex + 1).ToArray();
                _rcvBuffer.RemoveRange(0, lfIndex + 1);

                if (packet.Length > 0)
                {
                    PacketReceivedEvent?.Invoke(packet);
                }
            }
        }
    }
}