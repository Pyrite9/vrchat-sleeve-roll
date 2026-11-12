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
        /// keep: 유지하는 PhysBone 소매 체인(10-33) — 손 자손이어도 안 옮긴다. 체인은 통째로 대리 본 밑으로 간다(SleeveRollPb).
        /// slotTo·slotBind(감사 P3, 지금 cloth.bones와 같은 인덱스): slotTo[i]가 있으면 본 i의 걷히는 웨이트는 대리 본 대신 그 본(손가락 그림자)으로,
        /// 그 칸의 바인드 포즈는 slotBind[i]. 칸은 대리 본 뒤에 본 인덱스 순으로 붙는다. 없으면(손가락 웨이트 없는 옷) 전과 같은 결과
        /// </summary>
        internal static int Rebind(SkinnedMeshRenderer cloth, Mesh mesh, Transform hand, Transform proxy, float[] roll, Cache.Entry ce, ICollection<Transform> keep = null,
            Transform[] slotTo = null, Matrix4x4[] slotBind = null)
        {
            var bones = cloth.bones;
            // 손 자리 — 계산용 본으로 찾는다(옷의 손이 MA가 남긴 의상 본·ScaleProxy여도). 대리 본 바인드 포즈도 그 아바타 손 기준(가상 재타깃)이라야
            // 대리 본(아바타 손 회전을 따름)에 옮긴 정점이 원래 자리에 온다
            // 손 본이 메시에 없고 그 자손(소체가 제약으로 따라가게 둔 손 사본 `Hand_L_Const/Hand_L` 등)에만 묶인 옷은 손 바인드 포즈를 유도한다(10-77)
            if (roll == null || !LowerBind(ce.bones, ce.bindposes, hand, null, out var bpHand, ce.boneMap)) return 0;
            var isHand = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++) isHand[i] = bones[i] != null && (bones[i] == hand || bones[i].IsChildOf(hand)) && (keep == null || !keep.Contains(bones[i]));
            int pi = Array.IndexOf(bones, proxy);
            var bp = new List<Matrix4x4>(mesh.bindposes);
            var newBones = new List<Transform>(bones);
            if (pi < 0)
            {
                pi = newBones.Count;
                newBones.Add(proxy);
                bp.Add(bpHand);
            }
            // 본마다 걷히는 웨이트가 갈 칸 — 대리 본, 아니면 손가락 그림자(감사 P3)
            int[] dest = null;
            if (slotTo != null)
                for (int i = 0; i < bones.Length && i < slotTo.Length; i++)
                {
                    if (slotTo[i] == null || !isHand[i]) continue;
                    if (dest == null) { dest = new int[bones.Length]; for (int k = 0; k < dest.Length; k++) dest[k] = pi; }
                    int to = newBones.IndexOf(slotTo[i]);
                    if (to < 0)
                    {
                        to = newBones.Count;
                        newBones.Add(slotTo[i]);
                        bp.Add(slotBind[i]);
                    }
                    dest[i] = to;
                }

            var per = mesh.GetBonesPerVertex();
            var bw = mesh.GetAllBoneWeights();
            var outPer = new byte[per.Length];
            var outW = new List<BoneWeight1>(bw.Length);
            var tmp = new List<BoneWeight1>(8);
            var toShadow = new List<BoneWeight1>(4);
            int c = 0, moved = 0;
            for (int v = 0; v < per.Length; v++)
            {
                tmp.Clear();
                toShadow.Clear();
                float toProxy = 0f;
                for (int j = 0; j < per[v]; j++, c++)
                {
                    var b = bw[c];
                    if (roll[v] > 0f && b.boneIndex < isHand.Length && isHand[b.boneIndex])
                    {
                        int to = dest != null ? dest[b.boneIndex] : pi;
                        if (to == pi) { toProxy += b.weight; continue; }
                        int at = toShadow.FindIndex(x => x.boneIndex == to);
                        if (at < 0) toShadow.Add(new BoneWeight1 { boneIndex = to, weight = b.weight });
                        else toShadow[at] = new BoneWeight1 { boneIndex = to, weight = toShadow[at].weight + b.weight };
                    }
                    else tmp.Add(b);
                }
                if (toProxy > 0f || toShadow.Count > 0)
                {
                    if (toProxy > 0f) tmp.Add(new BoneWeight1 { boneIndex = pi, weight = toProxy });
                    tmp.AddRange(toShadow);
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

        /// <summary>
        /// 걷히는 정점(roll &gt; 0)의 slotTo[i] 본 웨이트만 그 칸(따라가기 그림자, 0.1.3-beta4)으로. 칸은 본 목록 끝에 본 인덱스 순으로 붙고, 바인드 포즈는 본 i의 실제 바인드 포즈.
        /// 안 걷히는 정점은 그대로(위팔 쪽 소매는 전처럼 흔들림). 옮긴 정점 수를 돌려준다 — 0이면 아무것도 안 바꿈. Rebind와 따로(대상 없는 아바타는 출력이 그대로)
        /// </summary>
        internal static int MoveToFollow(SkinnedMeshRenderer cloth, Mesh mesh, float[] roll, Transform[] slotTo)
        {
            if (roll == null || slotTo == null) return 0;
            var bones = cloth.bones;
            var oldBp = mesh.bindposes;
            var newBones = new List<Transform>(bones);
            var bp = new List<Matrix4x4>(oldBp);
            var dest = new int[bones.Length];
            bool any = false;
            for (int i = 0; i < dest.Length; i++)
            {
                dest[i] = -1;
                any |= i < slotTo.Length && slotTo[i] != null && i < oldBp.Length;
            }
            if (!any) return 0;
            // 본 목록과 바인드 포즈 길이가 다른 렌더러 — 새 칸이 같은 인덱스를 가리키게 짧은 쪽을 채운다
            while (bp.Count < newBones.Count) bp.Add(Matrix4x4.identity);
            while (newBones.Count < bp.Count) newBones.Add(null);
            for (int i = 0; i < dest.Length && i < slotTo.Length; i++)
            {
                if (slotTo[i] == null || i >= oldBp.Length) continue;
                dest[i] = newBones.Count;
                newBones.Add(slotTo[i]);
                bp.Add(oldBp[i]);
            }

            var per = mesh.GetBonesPerVertex().ToArray();
            var bw = mesh.GetAllBoneWeights();
            var outW = new List<BoneWeight1>(bw.Length);
            var tmp = new List<BoneWeight1>(8);
            int c = 0, moved = 0;
            for (int v = 0; v < per.Length; v++)
            {
                tmp.Clear();
                bool hit = false;
                for (int j = 0; j < per[v]; j++, c++)
                {
                    var b = bw[c];
                    if (v < roll.Length && roll[v] > 0f && b.weight > 0f && b.boneIndex < dest.Length && dest[b.boneIndex] >= 0)
                    {
                        tmp.Add(new BoneWeight1 { boneIndex = dest[b.boneIndex], weight = b.weight });
                        hit = true;
                    }
                    else tmp.Add(b);
                }
                if (hit)
                {
                    var sorted = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OrderByDescending(tmp, x => x.weight));   // 무거운 순(같은 값은 원래 순서)
                    tmp.Clear(); tmp.AddRange(sorted);
                    moved++;
                }
                outW.AddRange(tmp);
            }
            if (moved == 0) return 0;
            using (var nPer = new Unity.Collections.NativeArray<byte>(per, Unity.Collections.Allocator.Temp))
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

        /// <summary>메시에 웨이트가 실린 본</summary>
        internal static HashSet<Transform> WeightedBones(SkinnedMeshRenderer g)
        {
            var set = new HashSet<Transform>();
            var bones = g.bones;
            foreach (var w in g.sharedMesh.GetAllBoneWeights())
                if (w.weight > 0f && w.boneIndex < bones.Length && bones[w.boneIndex] != null) set.Add(bones[w.boneIndex]);
            return set;
        }

        /// <summary>이 본들이 주로 잡는 정점이 팔뚝 tMin보다 손목 쪽까지 가나 — 위팔 밑 소매. t = 그 정점 중 가장 손목 쪽</summary>
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
            // 아래팔·손 본이 메시에 없고 자손(트위스트, 소체의 제약 사본 등)에만 묶인 옷도 바인드 포즈를 유도해 쓴다(10-75·77)
            if (!LowerBind(ce.bones, ce.bindposes, hand, null, out var bpHand, ce.boneMap)) return false;
            if (!LowerBind(ce.bones, ce.bindposes, lower, hand, out var bpLower, ce.boneMap)) return false;
            rot = (bpLower * bpHand.inverse).rotation;
            return true;
        }

        /// <summary>손 본의 바인드 포즈 스케일을 아래팔 기준으로 — 대리 본의 걷은 상태 스케일(감사 P2). 아바타 본에 바로 묶인 옷은 보통 1,
        /// MA가 합치며 손 본 스케일을 바인드 포즈에 상쇄해 넣은 옷은 그 손 스케일. BindLocalRotation과 같은 바인드 포즈를 읽기만 한다</summary>
        internal static bool BindLocalScale(Cache.Entry ce, Transform lower, Transform hand, out Vector3 scale)
        {
            scale = Vector3.one;
            if (!LowerBind(ce.bones, ce.bindposes, hand, null, out var bpHand, ce.boneMap)) return false;
            if (!LowerBind(ce.bones, ce.bindposes, lower, hand, out var bpLower, ce.boneMap)) return false;
            scale = (bpLower * bpHand.inverse).lossyScale;
            return true;
        }
    }
}
