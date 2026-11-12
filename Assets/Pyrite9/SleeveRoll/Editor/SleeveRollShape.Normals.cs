// Pyrite9 Sleeve Roll — 계산: 셰이프의 노멀 변화
using UnityEngine;

namespace Pyrite9.Sleeve
{
    public static partial class SleeveRollShape
    {
        /// <summary>노멀 변화용 원래 모양 — 같은 위치 묶음·삼각형·원래 면 노멀 합. 좌우 셰이프가 같이 쓴다</summary>
        sealed class NormalBase
        {
            public Vector3[] v0, n0;
            public int[] key;
            public int count;
            public int[] tris;           // 삼각형 서브메시만, 이어 붙인 것
            public Vector3[] a0;
            public bool HasNormals => n0 != null && n0.Length == v0.Length;
        }

        static NormalBase PrepareNormals(Cache.Entry ce)
        {
            var nb = new NormalBase { v0 = ce.Vertices, n0 = ce.Normals };   // 입은 모양 기준 — 셰이프 노멀 델타는 그 위에 더해진다(0.1.3-beta6)
            if (!nb.HasNormals) return nb;   // 노멀이 없으면 노멀 델타는 0
            nb.key = WeldKeys(nb.v0, null, out nb.count);
            nb.tris = ce.Triangles;
            nb.a0 = Accumulate(nb.tris, nb.v0, nb.key, nb.count);
            return nb;
        }

        // 노멀 변화: 같은 위치 정점(UV 이음새)을 묶어 다시 계산한 노멀의 차이를 원래 노멀에 더한다
        static Vector3[] NormalDeltas(NormalBase nb, Vector3[] delta)
        {
            var v0 = nb.v0; var n0 = nb.n0; var key = nb.key; var a0 = nb.a0;
            var dn = new Vector3[v0.Length];
            if (!nb.HasNormals) return dn;
            var v1 = new Vector3[v0.Length];
            for (int i = 0; i < v0.Length; i++) v1[i] = v0[i] + delta[i];
            var a1 = Accumulate(nb.tris, v1, key, nb.count);
            for (int i = 0; i < v0.Length; i++)
            {
                if (delta[i] == Vector3.zero) continue;
                var nn = (n0[i] + a1[key[i]].normalized - a0[key[i]].normalized).normalized;
                dn[i] = nn - n0[i];
            }
            return dn;
        }

        static Vector3[] Accumulate(int[] tri, Vector3[] p, int[] key, int count)
        {
            var acc = new Vector3[count];
            for (int i = 0; i + 2 < tri.Length; i += 3)
            {
                var fn = Vector3.Cross(p[tri[i + 1]] - p[tri[i]], p[tri[i + 2]] - p[tri[i]]);
                acc[key[tri[i]]] += fn; acc[key[tri[i + 1]]] += fn; acc[key[tri[i + 2]]] += fn;
            }
            return acc;
        }
    }
}
