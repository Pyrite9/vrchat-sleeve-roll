using UnityEngine;

namespace Pyrite9.Sleeve
{
    static class SleeveRollConst
    {
        public const string Version = "0.1.3";
        public const string LogPath = "Logs/Pyrite9_SleeveRoll_build.txt";
        public const long LogLimit = 512 * 1024;   // bytes; past this the build log starts over, the previous one kept as .old
        public const string MenuIconGuid = "2c1a6432f6b84d1f850a76fe1f2f09b8";   // Icons/SleeveRoll_Menu.png (Source/tools/make_menu_icon.py)

        /// <summary>
        /// 빌드 때 0인 몸 셰이프를 FX가 켤 때 그것을 팔뚝 숨김으로 볼 문턱(0.1.3 B) — 팔뚝 구간(0 ≤ t ≤ 1)에서 움직인 정점의 이동량 가중 "축 거리가 줄어드는" 비율.
        /// 근거(플랜 1단계 실측, 2026-11-15): 팔 숨김 셰이프는 전부 1.000, 손톱·손가락 셰이프는 팔뚝 이동 0. 못 집으면 지금과 같은 동작(걷힌 소매 안에 가는 팔이 그대로)이고
        /// 잘못 집으면 새 결함(팔 굵기·근육 셰이프를 걷는 동안 되돌림)이라 높게 잡는다
        /// </summary>
        public const float ZeroHideInward = 0.9f;
    }

    /// <summary>왼쪽(true)·오른쪽(false)마다 달라지는 이름과 본 — 양쪽을 도는 코드가 같이 쓴다</summary>
    static class SrSide
    {
        public static readonly bool[] Both = { true, false };

        public static string Letter(bool left) => left ? "L" : "R";
        public static HumanBodyBones Lower(bool left) => left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
        public static HumanBodyBones Hand(bool left) => left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
        public static bool On(SleeveRoll c, bool left) => left ? c.left : c.right;
        public static string ShapeName(bool left) => left ? SleeveRollShape.ShapeLeft : SleeveRollShape.ShapeRight;
        public static string UnhideName(bool left) => left ? SleeveRollShape.UnhideLeft : SleeveRollShape.UnhideRight;
        public static string ProxyName(bool left) => left ? SleeveRollPlugin.ProxyLeft : SleeveRollPlugin.ProxyRight;
    }
}
