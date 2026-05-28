using System.Collections.Generic;
using UnityEngine;

namespace TechnicalTask
{
    [RequireComponent(typeof(MeshFilter))]
    public class MorphMeshGenerator : MonoBehaviour
    {
        public const int RingSegments = 24;

        [Range(0.005f, 0.2f)] [SerializeField] private float strokeHalfWidth = 0.04f;
        [Range(0.1f, 2.0f)]   [SerializeField] private float shapeRadius     = 0.7f;
        [Range(0f, 0.4f)]     [SerializeField] private float triangleCornerRounding = 0.18f;
        [Range(0f, 0.49f)]    [SerializeField] private float hexagonCornerRounding  = 0.18f;
        [Range(0f, 0.49f)]    [SerializeField] private float squareCornerRounding   = 0.15f;
        [Range(0f, 20f)]      [SerializeField] private float maxMiterRatio   = 4f;
        [SerializeField] private bool liveRebuildOnInspectorChange = true;

        private bool meshDirty;

        private void Awake()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            if (liveRebuildOnInspectorChange) meshDirty = true;
        }

        private void Update()
        {
            if (meshDirty)
            {
                meshDirty = false;
                if (isActiveAndEnabled) Rebuild();
            }
        }

        [ContextMenu("Rebuild Mesh")]
        public void Rebuild()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null) return;
            var mesh = BuildMesh();
            if (Application.isPlaying) mf.mesh = mesh;
            else mf.sharedMesh = mesh;
        }

        private Mesh BuildMesh()
        {
            int N = RingSegments;
            int vertexCount = N * 2;

            var triOuter = new Vector2[N];
            var hexOuter = new Vector2[N];
            var cirOuter = new Vector2[N];
            var sqrOuter = new Vector2[N];
            for (int i = 0; i < N; i++)
            {
                triOuter[i] = GetTriangleTarget(i, triangleCornerRounding);
                hexOuter[i] = GetHexagonTarget(i, hexagonCornerRounding);
                cirOuter[i] = GetCircleTarget(i);
                sqrOuter[i] = GetSquareTarget(i, squareCornerRounding);
            }

            float inset = strokeHalfWidth * 2f / shapeRadius;
            Vector2[] triInner = ComputePerpendicularInner(triOuter, inset, maxMiterRatio);
            Vector2[] hexInner = ComputePerpendicularInner(hexOuter, inset, maxMiterRatio);
            Vector2[] cirInner = ComputePerpendicularInner(cirOuter, inset, maxMiterRatio);
            Vector2[] sqrInner = ComputePerpendicularInner(sqrOuter, inset, maxMiterRatio);

            var positions    = new Vector3[vertexCount];
            var triTargets   = new Vector2[vertexCount];
            var hexTargets   = new Vector2[vertexCount];
            var cirTargets   = new Vector2[vertexCount];
            var sqrTargets   = new Vector2[vertexCount];
            var strokeSides  = new Vector2[vertexCount];

            for (int i = 0; i < N; i++)
            {
                positions[i]     = new Vector3(triOuter[i].x * shapeRadius, triOuter[i].y * shapeRadius, 0f);
                positions[N + i] = new Vector3(triInner[i].x * shapeRadius, triInner[i].y * shapeRadius, 0f);

                triTargets[i]     = triOuter[i] * shapeRadius;
                hexTargets[i]     = hexOuter[i] * shapeRadius;
                cirTargets[i]     = cirOuter[i] * shapeRadius;
                sqrTargets[i]     = sqrOuter[i] * shapeRadius;

                triTargets[N + i] = triInner[i] * shapeRadius;
                hexTargets[N + i] = hexInner[i] * shapeRadius;
                cirTargets[N + i] = cirInner[i] * shapeRadius;
                sqrTargets[N + i] = sqrInner[i] * shapeRadius;

                strokeSides[i]     = new Vector2(1f, 0f);
                strokeSides[N + i] = new Vector2(0f, 0f);
            }

            var triangles = new int[N * 6];
            for (int i = 0; i < N; i++)
            {
                int o0 = i;
                int o1 = (i + 1) % N;
                int i0 = N + i;
                int i1 = N + ((i + 1) % N);

                int t = i * 6;
                triangles[t + 0] = o0;
                triangles[t + 1] = o1;
                triangles[t + 2] = i1;
                triangles[t + 3] = o0;
                triangles[t + 4] = i1;
                triangles[t + 5] = i0;
            }

            var mesh = new Mesh { name = "MorphRing" };
            mesh.SetVertices(positions);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(1, new List<Vector2>(triTargets));
            mesh.SetUVs(2, new List<Vector2>(hexTargets));
            mesh.SetUVs(3, new List<Vector2>(cirTargets));
            mesh.SetUVs(4, new List<Vector2>(sqrTargets));
            mesh.SetUVs(5, new List<Vector2>(strokeSides));
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (shapeRadius * 3f));
            return mesh;
        }

        // Per-vertex perpendicular inset with miter at corners. For our clockwise polygons,
        // the inward normal of an edge with unit direction (e.x, e.y) is (e.y, -e.x).
        // At a corner, the miter offset is along the bisector of the two inward normals
        // and scaled so that the perpendicular distance from each adjacent edge is `inset`.
        private static Vector2[] ComputePerpendicularInner(Vector2[] outer, float inset, float maxMiterRatio)
        {
            int N = outer.Length;
            var inner = new Vector2[N];

            for (int i = 0; i < N; i++)
            {
                Vector2 curr = outer[i];

                Vector2 edgeIn  = FindNonZeroEdge(outer, i, -1);
                Vector2 edgeOut = FindNonZeroEdge(outer, i, +1);
                if (edgeIn.sqrMagnitude  < 1e-12f) edgeIn  = edgeOut;
                if (edgeOut.sqrMagnitude < 1e-12f) edgeOut = edgeIn;

                edgeIn.Normalize();
                edgeOut.Normalize();

                Vector2 nIn  = new Vector2(edgeIn.y,  -edgeIn.x);
                Vector2 nOut = new Vector2(edgeOut.y, -edgeOut.x);
                Vector2 sumN = nIn + nOut;
                float sumLen2 = sumN.sqrMagnitude;

                Vector2 offset;
                if (sumLen2 < 1e-6f)
                {
                    offset = nIn * inset;
                }
                else
                {
                    offset = sumN * (2f * inset / sumLen2);
                    float maxLen = inset * maxMiterRatio;
                    if (offset.sqrMagnitude > maxLen * maxLen)
                    {
                        offset = offset.normalized * maxLen;
                    }
                }
                inner[i] = curr + offset;
            }
            return inner;
        }

        private static Vector2 FindNonZeroEdge(Vector2[] positions, int i, int direction)
        {
            int N = positions.Length;
            int safety = N;
            int j = i;
            Vector2 e;
            do
            {
                int k = ((j + direction) % N + N) % N;
                e = direction > 0 ? positions[k] - positions[i] : positions[i] - positions[k];
                if (e.sqrMagnitude > 1e-12f) return e;
                j = k;
            } while (--safety > 0 && j != i);
            return Vector2.zero;
        }

        private static Vector2 GetCircleTarget(int i)
        {
            float angle = i * (Mathf.PI * 2f / RingSegments);
            return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        }

        // Vertex-up triangle, vertices at angles 0°/120°/240°. The 5 ring vertices that
        // would otherwise collapse at each triangle corner are distributed along a
        // quadratic Bezier curve from one edge tangent point through the corner to the
        // other edge tangent point — giving a smoothly rounded corner. Vertices 22 and 2
        // (and the equivalents at the other corners) still sit at the corner tangent
        // points and will fan outward into hex corners during the tri→hex morph.
        private static Vector2 GetTriangleTarget(int i, float r)
        {
            Vector2 vTop = new Vector2(0f, 1f);
            Vector2 vBR  = new Vector2(Mathf.Sin(120f * Mathf.Deg2Rad), Mathf.Cos(120f * Mathf.Deg2Rad));
            Vector2 vBL  = new Vector2(Mathf.Sin(240f * Mathf.Deg2Rad), Mathf.Cos(240f * Mathf.Deg2Rad));

            r = Mathf.Clamp(r, 0f, 0.49f);

            Vector2 topL = Vector2.Lerp(vTop, vBL, r);
            Vector2 topR = Vector2.Lerp(vTop, vBR, r);
            Vector2 brT  = Vector2.Lerp(vBR, vTop, r);
            Vector2 brB  = Vector2.Lerp(vBR, vBL, r);
            Vector2 blR  = Vector2.Lerp(vBL, vBR, r);
            Vector2 blT  = Vector2.Lerp(vBL, vTop, r);

            switch (i)
            {
                case 22: return Bezier2(topL, vTop, topR, 0.00f);
                case 23: return Bezier2(topL, vTop, topR, 0.25f);
                case 0:  return Bezier2(topL, vTop, topR, 0.50f);
                case 1:  return Bezier2(topL, vTop, topR, 0.75f);
                case 2:  return Bezier2(topL, vTop, topR, 1.00f);

                case 3:  return Vector2.Lerp(topR, brT, 0.25f);
                case 4:  return Vector2.Lerp(topR, brT, 0.50f);
                case 5:  return Vector2.Lerp(topR, brT, 0.75f);

                case 6:  return Bezier2(brT, vBR, brB, 0.00f);
                case 7:  return Bezier2(brT, vBR, brB, 0.25f);
                case 8:  return Bezier2(brT, vBR, brB, 0.50f);
                case 9:  return Bezier2(brT, vBR, brB, 0.75f);
                case 10: return Bezier2(brT, vBR, brB, 1.00f);

                case 11: return Vector2.Lerp(brB, blR, 0.25f);
                case 12: return Vector2.Lerp(brB, blR, 0.50f);
                case 13: return Vector2.Lerp(brB, blR, 0.75f);

                case 14: return Bezier2(blR, vBL, blT, 0.00f);
                case 15: return Bezier2(blR, vBL, blT, 0.25f);
                case 16: return Bezier2(blR, vBL, blT, 0.50f);
                case 17: return Bezier2(blR, vBL, blT, 0.75f);
                case 18: return Bezier2(blR, vBL, blT, 1.00f);

                case 19: return Vector2.Lerp(blT, topL, 0.25f);
                case 20: return Vector2.Lerp(blT, topL, 0.50f);
                case 21: return Vector2.Lerp(blT, topL, 0.75f);

                default: return Vector2.zero;
            }
        }

        // Flat-top hexagon. Apothem = 1. Each corner gets a 3-vertex Bezier arc; the
        // single edge-interior vertex between consecutive arcs sits on the straight
        // side. Corner indices: 2/6/10/14/18/22 → c30/c90/c150/c210/c270/c330.
        private static Vector2 GetHexagonTarget(int i, float r)
        {
            float R = 1f / Mathf.Cos(30f * Mathf.Deg2Rad);
            Vector2 Corner(float angleDeg)
            {
                float a = angleDeg * Mathf.Deg2Rad;
                return new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * R;
            }

            r = Mathf.Clamp(r, 0f, 0.49f);

            Vector2 c30  = Corner(30f);
            Vector2 c90  = Corner(90f);
            Vector2 c150 = Corner(150f);
            Vector2 c210 = Corner(210f);
            Vector2 c270 = Corner(270f);
            Vector2 c330 = Corner(330f);

            // For each corner, tangent points toward its previous and next neighbors (ring order).
            Vector2 t30p  = Vector2.Lerp(c30,  c330, r);  Vector2 t30n  = Vector2.Lerp(c30,  c90,  r);
            Vector2 t90p  = Vector2.Lerp(c90,  c30,  r);  Vector2 t90n  = Vector2.Lerp(c90,  c150, r);
            Vector2 t150p = Vector2.Lerp(c150, c90,  r);  Vector2 t150n = Vector2.Lerp(c150, c210, r);
            Vector2 t210p = Vector2.Lerp(c210, c150, r);  Vector2 t210n = Vector2.Lerp(c210, c270, r);
            Vector2 t270p = Vector2.Lerp(c270, c210, r);  Vector2 t270n = Vector2.Lerp(c270, c330, r);
            Vector2 t330p = Vector2.Lerp(c330, c270, r);  Vector2 t330n = Vector2.Lerp(c330, c30,  r);

            switch (i)
            {
                case 1:  return Bezier2(t30p,  c30,  t30n,  0.0f);
                case 2:  return Bezier2(t30p,  c30,  t30n,  0.5f);
                case 3:  return Bezier2(t30p,  c30,  t30n,  1.0f);
                case 4:  return Vector2.Lerp(t30n, t90p, 0.5f);
                case 5:  return Bezier2(t90p,  c90,  t90n,  0.0f);
                case 6:  return Bezier2(t90p,  c90,  t90n,  0.5f);
                case 7:  return Bezier2(t90p,  c90,  t90n,  1.0f);
                case 8:  return Vector2.Lerp(t90n, t150p, 0.5f);
                case 9:  return Bezier2(t150p, c150, t150n, 0.0f);
                case 10: return Bezier2(t150p, c150, t150n, 0.5f);
                case 11: return Bezier2(t150p, c150, t150n, 1.0f);
                case 12: return Vector2.Lerp(t150n, t210p, 0.5f);
                case 13: return Bezier2(t210p, c210, t210n, 0.0f);
                case 14: return Bezier2(t210p, c210, t210n, 0.5f);
                case 15: return Bezier2(t210p, c210, t210n, 1.0f);
                case 16: return Vector2.Lerp(t210n, t270p, 0.5f);
                case 17: return Bezier2(t270p, c270, t270n, 0.0f);
                case 18: return Bezier2(t270p, c270, t270n, 0.5f);
                case 19: return Bezier2(t270p, c270, t270n, 1.0f);
                case 20: return Vector2.Lerp(t270n, t330p, 0.5f);
                case 21: return Bezier2(t330p, c330, t330n, 0.0f);
                case 22: return Bezier2(t330p, c330, t330n, 0.5f);
                case 23: return Bezier2(t330p, c330, t330n, 1.0f);
                case 0:  return Vector2.Lerp(t330n, t30p, 0.5f);
                default: return Vector2.zero;
            }
        }

        // Flat-top square. Apothem = 1, side length = 2. Each corner gets a 3-vertex
        // Bezier arc; 3 edge-interior vertices live on each straight side. Corner
        // indices: 3/9/15/21 → cTR/cBR/cBL/cTL.
        private static Vector2 GetSquareTarget(int i, float r)
        {
            Vector2 cTR = new Vector2( 1f,  1f);
            Vector2 cBR = new Vector2( 1f, -1f);
            Vector2 cBL = new Vector2(-1f, -1f);
            Vector2 cTL = new Vector2(-1f,  1f);

            r = Mathf.Clamp(r, 0f, 0.49f);

            Vector2 tTRp = Vector2.Lerp(cTR, cTL, r);  Vector2 tTRn = Vector2.Lerp(cTR, cBR, r);
            Vector2 tBRp = Vector2.Lerp(cBR, cTR, r);  Vector2 tBRn = Vector2.Lerp(cBR, cBL, r);
            Vector2 tBLp = Vector2.Lerp(cBL, cBR, r);  Vector2 tBLn = Vector2.Lerp(cBL, cTL, r);
            Vector2 tTLp = Vector2.Lerp(cTL, cBL, r);  Vector2 tTLn = Vector2.Lerp(cTL, cTR, r);

            switch (i)
            {
                case 2:  return Bezier2(tTRp, cTR, tTRn, 0.0f);
                case 3:  return Bezier2(tTRp, cTR, tTRn, 0.5f);
                case 4:  return Bezier2(tTRp, cTR, tTRn, 1.0f);
                case 5:  return Vector2.Lerp(tTRn, tBRp, 0.25f);
                case 6:  return Vector2.Lerp(tTRn, tBRp, 0.50f);
                case 7:  return Vector2.Lerp(tTRn, tBRp, 0.75f);
                case 8:  return Bezier2(tBRp, cBR, tBRn, 0.0f);
                case 9:  return Bezier2(tBRp, cBR, tBRn, 0.5f);
                case 10: return Bezier2(tBRp, cBR, tBRn, 1.0f);
                case 11: return Vector2.Lerp(tBRn, tBLp, 0.25f);
                case 12: return Vector2.Lerp(tBRn, tBLp, 0.50f);
                case 13: return Vector2.Lerp(tBRn, tBLp, 0.75f);
                case 14: return Bezier2(tBLp, cBL, tBLn, 0.0f);
                case 15: return Bezier2(tBLp, cBL, tBLn, 0.5f);
                case 16: return Bezier2(tBLp, cBL, tBLn, 1.0f);
                case 17: return Vector2.Lerp(tBLn, tTLp, 0.25f);
                case 18: return Vector2.Lerp(tBLn, tTLp, 0.50f);
                case 19: return Vector2.Lerp(tBLn, tTLp, 0.75f);
                case 20: return Bezier2(tTLp, cTL, tTLn, 0.0f);
                case 21: return Bezier2(tTLp, cTL, tTLn, 0.5f);
                case 22: return Bezier2(tTLp, cTL, tTLn, 1.0f);
                case 23: return Vector2.Lerp(tTLn, tTRp, 0.25f);
                case 0:  return Vector2.Lerp(tTLn, tTRp, 0.50f);
                case 1:  return Vector2.Lerp(tTLn, tTRp, 0.75f);
                default: return Vector2.zero;
            }
        }

        private static Vector2 Bezier2(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }
    }
}
