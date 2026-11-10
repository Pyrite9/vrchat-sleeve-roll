// Pyrite9 Sleeve Roll — 계산: 걷히는 정점의 손 웨이트를 손목 대리 본으로
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pyrite9.Sleeve
{
    public static partial class SleeveRollShape
    {
        /// <summary>
        /// 걷히는 정점(roll &gt; 0)의 손·손가락 웨이트를 손목 대리 본으로 옮긴다(D6). 대리 본의 바인드 포즈는 손과 같게 —
        /// 대리 본이 손 회전을 따르면(제약 가중치 1) 원래와 똑같이 휘고, 아래팔에 머물면(0) 걷힌 천이 손목을 안 따른다.
        /// 옮긴 정점 수를 돌려준다. 옷이 손 본에 묶여 있지 않거나 옮길 정점이 없으면 0(아무것도 안 바꿈).
        /// keep: 유지하는 PhysBone 소매 체인(10-33) — 손 자손이어도 안 옮긴다. 체인은 통째로 대리 본 밑으로 간다(SleeveRollPb)
        /// </summary>
        internal static int Rebind(SkinnedMeshRenderer cloth, Mesh mesh, Transform hand, Transform proxy, float[] roll, Cache.Entry ce, ICollection<Transform> keep = null)
        {
            var bones = cloth.bones;
            // 손 자리 — 계산용 본으로 찾는다(옷의 손이 MA가 남긴 의상 본·ScaleProxy여도). 대리 본 바인드 포즈도 그 아바타 손 기준(가상 재타깃)이라야
            // 대리 본(아바타 손 회전을 따름)에 옮긴 정점이 원래 자리에 온다
            int hi = Array.IndexOf(ce.bones, hand);
            if (hi < 0 || hi >= bones.Length || roll == null) return 0;
            var isHand = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++) isHand[i] = bones[i] != null && (bones[i] == hand || bones[i].IsChildOf(hand)) && (keep == null || !keep.Contains(bones[i]));
            int pi = Array.IndexOf(bones, proxy);
            var bp = new List<Matrix4x4>(mesh.bindposes);
            var newBones = new List<Transform>(bones);
            if (pi < 0)
            {
                pi = newBones.Count;
                newBones.Add(proxy);
                bp.Add(ce.bindposes[hi]);
            }

            var per = mesh.GetBonesPerVertex();
            var bw = mesh.GetAllBoneWeights();
            var outPer = new byte[per.Length];
            var outW = new List<BoneWeight1>(bw.Length);
            var tmp = new List<BoneWeight1>(8);
            int c = 0, moved = 0;
            for (int v = 0; v < per.Length; v++)
            {
                tmp.Clear();
                float toProxy = 0f;
                for (int j = 0; j < per[v]; j++, c++)
                {
                    var b = bw[c];
                    if (roll[v] > 0f && b.boneIndex < isHand.Length && isHand[b.boneIndex]) toProxy += b.weight;
                    else tmp.Add(b);
                }
                if (toProxy > 0f)
                {
                    tmp.Add(new BoneWeight1 { boneIndex = pi, weight = toProxy });
                    tmp.Sort((x, y) => y.weight.CompareTo(x.weight)); // Unity: 정점마다 무거운 순 — 안 옮긴 정점은 이미 그 순서
                    moved++;
                }
                outPer[v] = (byte)tmp.Count;
                outW.AddRange(tmp);
            }
            if (moved == 0) return 0; // 걷히는 천에 손 웨이트가 없다(Lapwing 볼레로) — 본·웨이트는 그대로
            using (var nPer = new Unity.Collections.NativeArray<byte>(outPer, Unity.Collections.Allocator.Temp))
            using (var nW = new Unity.Collections.NativeArray<BoneWeight1>(outW.ToArray(), Unity.Collections.Allocator.Temp))
                mesh.SetBoneWeights(nPer, nW);
            mesh.bindposes = bp.ToArray();
            cloth.bones = newBones.ToArray();
            return moved;
        }

        /// <summary>걷히는 정점(roll &gt; 0)에 웨이트가 있는 본 — PhysBone 소매 찾기</summary>
        internal static HashSet<Transform> BonesOnRolled(Cache cache, SkinnedMeshRenderer cloth, float[] roll)
        {
            var ce = cache.Get(cloth);
            var set = new HashSet<Transform>();
            var per = ce.mesh.GetBonesPerVertex();
            var bw = ce.mesh.GetAllBoneWeights();
            var bones = ce.rawBones;   // 실제 Transform — PhysBone 체인 본과 비교한다
            int c = 0;
            for (int v = 0; v < per.Length; v++)
                for (int j = 0; j < per[v]; j++, c++)
                    if (v < roll.Length && roll[v] > 0f && bw[c].weight >= 0.01f && bw[c].boneIndex < bones.Length && bones[bw[c].boneIndex] != null)
                        set.Add(bones[bw[c].boneIndex]);
            return set;
        }

        /// <summary>이 본들이 주로 잡는 정점이 팔뚝 tMin보다 손목 쪽까지 가나 — 위팔 밑 소매. t = 그 정점 중 가장 손목 쪽</summary>
        /// <summary>메시에 웨이트가 실린 본</summary>
        internal static HashSet<Transform> WeightedBones(SkinnedMeshRenderer g)
        {
            var set = new HashSet<Transform>();
            var bones = g.bones;
            foreach (var w in g.sharedMesh.GetAllBoneWeights())
                if (w.weight > 0f && w.boneIndex < bones.Length && bones[w.boneIndex] != null) set.Add(bones[w.boneIndex]);
            return set;
        }

        internal static bool Reaches(Cache cache, SkinnedMeshRenderer cloth, Animator animator, bool left, ICollection<Transform> pick, float tMin, out float t)
        {
            t = float.MinValue;
            var ce = cache.Get(cloth);
            if (GetForearm(animator, left, ce.bones, ce.bindposes, out var f, ce.boneMap) != null) return false;
            var w = Weights(ce.rawBones, ce.mesh, new HashSet<Transform>(pick).Contains);   // pick은 실제 체인 본
            var verts = ce.Vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                if (w[i] < 0.5f) continue;
                f.Cyl(verts[i], out var vt, out _, out _);
                if (vt > t) t = vt;
            }
            return t >= tMin;
        }

        /// <summary>손 본의 바인드 포즈 회전을 아래팔 기준으로 — 대리 본의 쉬는 자세(제약 가중치 0일 때)</summary>
        internal static bool BindLocalRotation(Cache.Entry ce, Transform lower, Transform hand, out Quaternion rot)
        {
            rot = Quaternion.identity;
            int hi = Array.IndexOf(ce.bones, hand);
            // 아래팔 본이 메시에 없고 자손(트위스트 등)에만 묶인 옷도 아래팔 바인드 포즈를 유도해 쓴다(10-75)
            if (hi < 0 || hi >= ce.bindposes.Length || !LowerBind(ce.bones, ce.bindposes, lower, hand, out var bpLower, ce.boneMap)) return false;
            rot = (bpLower * ce.bindposes[hi].inverse).rotation;
            return true;
        }
    }
}
