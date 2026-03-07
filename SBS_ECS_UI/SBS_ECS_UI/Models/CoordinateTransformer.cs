using System;

namespace SBS_ECS_UI.Models
{
    public class CoordinateTransformer
    {
        // ECS가 사전에 알고 있는 발사대의 물리적 위치 (탐색기 원점 기준)
        // 예시: 발사대가 탐색기 우측으로 250mm 떨어진 곳에 배치되어 있다고 가정
        public float LauncherPosX_mm { get; set; } = 250.0f;
        public float LauncherPosY_mm { get; set; } = 0.0f;

        /// <summary>
        /// 탐색기가 획득한 표적 좌표를 받아 발사대 기준의 회전 방위각을 계산합니다.
        /// (오리지널 좌표 변환 방식 적용)
        /// </summary>
        public float CalAngle(float enemyPosX_mm, float enemyPosY_mm)
        {
            // 1. 발사대 기준의 상대 표적 좌표(Delta) 계산
            float deltaX = enemyPosX_mm - LauncherPosX_mm;
            float deltaY = enemyPosY_mm - LauncherPosY_mm;

            // 2. 아크탄젠트(atan2) 함수로 라디안 값 산출 후 Degree 단위로 변환
            float angleDegree = (float)(Math.Atan2(deltaY, deltaX) * (180.0 / Math.PI));

            // 3. 서보모터 구동 범위에 맞춘 예외 처리 (음수 각도 방지)
            if (angleDegree < 0)
            {
                angleDegree += 360.0f;
            }

            return angleDegree;
        }
    }
}