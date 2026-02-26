using System;
using System.Collections.Generic;
using System.IO.Ports; // 시리얼 통신을 위한 네임스페이스
using System.Windows;

namespace SBS_ECS_UI.Services
{
    internal class UartManager
    {
        // SDD 4.1.1.3.2 설계 변수
        private SerialPort _serialPort; // 시리얼 통신 핵심 객체
        private string _portName;       // 연결된 COM 포트 번호
        private int _baudRate = 115200; // STM32와 동일한 통신 속도
        private List<byte> _rcvBuffer = new List<byte>(); // 수신 데이터 임시 저장 버퍼

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

                // 객체 생성 및 파라미터 설정
                _serialPort = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One);

                // 데이터 수신 이벤트 핸들러 등록
                _serialPort.DataReceived += OnDataReceived;

                _serialPort.Open();
                return true;
            }
            catch (Exception ex)
            {
                // 실무에서는 로그 매니저를 통해 기록하거나 ViewModel에 에러 전달
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
                    _serialPort.Write(data, 0, data.Length); // 하드웨어 버퍼에 기록
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"전송 에러: {ex.Message}");
            }
        }

        /// <summary>
        /// UART 수신 이벤트 핸들러 (SDD 4.1.1.3.2.3)
        /// </summary>
        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            int bytesToRead = _serialPort.BytesToRead;
            byte[] buffer = new byte[bytesToRead];

            // 수신 버퍼에서 데이터를 읽어 임시 배열에 저장
            _serialPort.Read(buffer, 0, bytesToRead);

            // 읽어온 데이터를 리스트 버퍼에 추가
            _rcvBuffer.AddRange(buffer);

            // 패킷 분석 호출 (SDD 4.1.1.3.2.4)
            ParseUart();
        }

        /// <summary>
        /// 수신 데이터 파싱 (SDD 4.1.1.3.2.4)
        /// STM32에서 정의한 6바이트 패킷 규격에 맞춰 분리
        /// </summary>
        private void ParseUart()
        {
            // 예시: STM32에서 6바이트 단위로 데이터를 보낼 경우
            while (_rcvBuffer.Count >= 6)
            {
                // 6바이트 패킷 추출
                byte[] packet = _rcvBuffer.GetRange(0, 6).ToArray();
                _rcvBuffer.RemoveRange(0, 6);

                // 유효한 패킷일 경우 ViewModel로 전달
                PacketReceivedEvent?.Invoke(packet);
            }
        }
    }
}