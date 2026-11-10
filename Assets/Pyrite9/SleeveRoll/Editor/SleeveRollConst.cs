using UnityEngine;

namespace Pyrite9.Sleeve
{
    static class SleeveRollConst
    {
        public const string Version = "0.1.3-beta1";
        public const string LogPath = "Logs/Pyrite9_SleeveRoll_build.txt";
        public const long LogLimit = 512 * 1024;   // bytes; past this the build log starts over, the previous one kept as .old
        public const string MenuIconGuid = "2c1a6432f6b84d1f850a76fe1f2f09b8";   // Icons/SleeveRoll_Menu.png (Source/tools/make_menu_icon.py)
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
