using System;
using System.IO;
using System.Diagnostics;

namespace SBS_ECS_UI.Services
{
    public class AuthService
    {
        // SDD 4.1.1.1.2 설계 결정사항 반영
        private const string TargetPassword = "1234"; // 하드코딩된 PIN
        public bool IsDeleteMode { get; private set; } = false;

        /// <summary>
        /// PIN 번호 검증 (VerifyPassword - E11-1.2.2)
        /// </summary>
        public bool VerifyPassword(string input)
        {
            if (IsDeleteMode) return false;
            return input == TargetPassword;
        }

        /// <summary>
        /// 보안 위반 시 로그 소각 (Execute_Delete / Erase_Logs - E11-1.2.6)
        /// </summary>
        public void ExecuteIncineration()
        {
            IsDeleteMode = true;
            Debug.WriteLine("보안 위반 발생: 시스템 소각 모드 진입");

            try
            {
                // 실제 로그 파일 삭제 로직 (예시)
                string logPath = "engagement_log.txt";
                if (File.Exists(logPath))
                {
                    // 0xFF로 덮어쓰기 후 삭제
                    byte[] dummy = new byte[1024];
                    for (int i = 0; i < dummy.Length; i++) dummy[i] = 0xFF;
                    File.WriteAllBytes(logPath, dummy);
                    File.Delete(logPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"소각 실패: {ex.Message}");
            }
        }
    }
}