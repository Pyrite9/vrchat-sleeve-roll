using System.Collections.Generic;
using UnityEngine;

namespace Pyrite9.Sleeve
{
    /// <summary>메뉴·인스펙터 언어 — 순서 고정(인스펙터 T(en, jp, ko)와 같음)</summary>
    public enum SleeveMenuLanguage { English = 0, Japanese = 1, Korean = 2 }

    /// <summary>
    /// 소매 걷기 설정 — 옷을 여럿 고를 수 있다(겹쳐 입은 옷은 빌드가 안쪽부터 걷어 바깥 옷을 그 위에 얹는다). 빌드 때 NDMF가 옷 메시 사본마다 SleeveUp_L/R을 계산해 넣고
    /// 손목 대리 본(SleeveRoll_Wrist_L/R)으로 걷힌 천의 손 웨이트를 옮긴다(sleeve-roll.md 2절·D6). 빌드 사본에선 지워진다
    /// </summary>
    [AddComponentMenu("Pyrite9/Sleeve Roll")]
    [DisallowMultipleComponent]
    public class SleeveRoll : MonoBehaviour, VRC.SDKBase.IEditorOnly
    {
        [Tooltip("The menu toggle's default name and the inspector's language")]
        public SleeveMenuLanguage menuLanguage = SleeveMenuLanguage.English;
        [Tooltip("The sleeved garments' Skinned Mesh Renderers — several layers (a shirt under a jacket) roll together, the outer one over the inner one")]
        public List<SkinnedMeshRenderer> targets = new List<SkinnedMeshRenderer>();
        /// <summary>0.1.0 개발판까지의 옷 하나 — 남아 있으면 targets와 함께 쓴다(인스펙터가 고칠 때 targets로 옮긴다)</summary>
        [HideInInspector] public SkinnedMeshRenderer target;
        public bool left = true, right = true;

        [Tooltip("Only matters if the outfit shrinks the arm under the sleeve. On: release the shrink while rolled and roll to Roll To — no thin arm shows inside the rolled cuff, shrunk again when the sleeve comes down. " +
                 "Off: keep the shrink and stop before the shrunk arm shows")]
        public bool unhideWhenRolled = true;
        [Tooltip("Where the rolled cuff sits — along the forearm, 0 elbow ~ 1 wrist")]
        [Range(0f, 1f)] public float rollTo = 0.70f;
        [Tooltip("Gap between the rolled cloth and the skin (m)")]
        [Range(0f, 0.02f)] public float margin = 0.004f;
        [Tooltip("Slack kept after rolling — 0 hugs the arm")]
        [Range(0f, 1f)] public float looseness = 0.35f;
        [Tooltip("Fold depth (m) — 0 = smooth")]
        [Range(0f, 0.015f)] public float foldAmplitude = 0.004f;
        [Tooltip("Fold spacing (m, on the rolled sleeve)")]
        [Range(0.01f, 0.1f)] public float foldLength = 0.045f;

        [Tooltip("Add a toggle to the Expression Menu — on rolls the sleeves. Off: no menu, only the synced Bool 'Pyrite9/SleeveRoll/Hold' (drive it yourself)")]
        public bool installMenu = true;
        [Tooltip("The toggle's name — empty: Sleeve Roll / 袖まくり / 소매 걷기 by the menu language")]
        public string menuName = "";
        [Tooltip("The menu the toggle goes in — empty: the Expression Menu root. It must be a menu in this avatar's Expression Menu (or one a Modular Avatar installer adds), or the toggle is left out")]
        public VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu installTo;
        [Tooltip("Keep the toggle's state when you rejoin or change avatars — the synced Bool 'Pyrite9/SleeveRoll/Hold' is saved. Off: rolled down each time")]
        public bool saved = false;

        [Tooltip("Meshes the skin is measured on. Empty: every other skinned mesh with forearm weights")]
        public SkinnedMeshRenderer[] bodyMeshes = new SkinnedMeshRenderer[0];

        [Tooltip("Above this point (0 = elbow) the sleeve does not move")]
        [Range(0f, 1f)] public float startAt = 0.25f;
        [Tooltip("Move the rolled cloth to a wrist proxy bone — off: the rolled cuff bends with the wrist")]
        public bool wristProxy = true;

        /// <summary>실제로 걷을 옷 — targets(빈 칸·중복 빼고) + 옛 target</summary>
        public List<SkinnedMeshRenderer> Garments
        {
            get
            {
                var list = new List<SkinnedMeshRenderer>();
                if (targets != null) foreach (var t in targets) if (t != null && !list.Contains(t)) list.Add(t);
                if (target != null && !list.Contains(target)) list.Insert(0, target);
                return list;
            }
        }

        /// <summary>메뉴 토글에 실제로 쓰는 이름</summary>
        public string MenuLabel => !string.IsNullOrWhiteSpace(menuName) ? menuName.Trim()
            : Pick(menuLanguage, "Sleeve Roll", "袖まくり", "소매 걷기");

        public static string Pick(SleeveMenuLanguage l, string en, string jp, string ko)
            => l == SleeveMenuLanguage.Japanese ? jp : l == SleeveMenuLanguage.Korean ? ko : en;

        void OnValidate()
        {
            rollTo = Mathf.Clamp01(rollTo);
            startAt = Mathf.Clamp01(startAt);   // kept below rollTo where it's used, so moving rollTo doesn't overwrite it
            margin = Mathf.Clamp(margin, 0f, 0.02f);
            looseness = Mathf.Clamp01(looseness);
            foldAmplitude = Mathf.Clamp(foldAmplitude, 0f, 0.015f);
            foldLength = Mathf.Clamp(foldLength, 0.01f, 0.1f);
        }
    }
}
