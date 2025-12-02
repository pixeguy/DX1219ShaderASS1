using UnityEngine;

[ExecuteAlways]
public class DrawAllColliders : MonoBehaviour
{
    public Color colliderColor = Color.green;
    public bool draw3D = true;
    public bool draw2D = true;

    private void OnDrawGizmos()
    {
        // Prevent Gizmos from drawing inside GameView
#if UNITY_EDITOR
        if (!UnityEditor.SceneView.currentDrawingSceneView)
            return;
#endif

        Gizmos.color = colliderColor;

        if (draw3D) Draw3DColliders();
        if (draw2D) Draw2DColliders();
    }

    private void Draw3DColliders()
    {
        foreach (var col in FindObjectsOfType<Collider>())
        {
            if (!col.enabled) continue;
            Gizmos.matrix = col.transform.localToWorldMatrix;

            switch (col)
            {
                case BoxCollider b:
                    Gizmos.DrawWireCube(b.center, b.size);
                    break;

                case SphereCollider s:
                    Gizmos.DrawWireSphere(s.center, s.radius);
                    break;

                case CapsuleCollider c:
                    DrawCapsuleGizmo(c);
                    break;

                case MeshCollider m:
                    if (m.sharedMesh)
                        Gizmos.DrawWireMesh(m.sharedMesh);
                    break;
            }
        }
    }

    private void Draw2DColliders()
    {
        foreach (var col in FindObjectsOfType<Collider2D>())
        {
            if (!col.enabled) continue;
            Gizmos.matrix = col.transform.localToWorldMatrix;

            switch (col)
            {
                case BoxCollider2D b:
                    Gizmos.DrawWireCube(b.offset, b.size);
                    break;

                case CircleCollider2D c:
                    Gizmos.DrawWireSphere(c.offset, c.radius);
                    break;

                case PolygonCollider2D poly:
                    DrawPoly(poly);
                    break;

                case EdgeCollider2D edge:
                    DrawEdge(edge);
                    break;
            }
        }
    }

    private void DrawPoly(PolygonCollider2D poly)
    {
        for (int p = 0; p < poly.pathCount; p++)
        {
            var pts = poly.GetPath(p);
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 a = pts[i];
                Vector3 b = pts[(i + 1) % pts.Length];
                Gizmos.DrawLine(a, b);
            }
        }
    }

    private void DrawEdge(EdgeCollider2D edge)
    {
        var pts = edge.points;
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Gizmos.DrawLine(pts[i], pts[i + 1]);
        }
    }

    // Capsule drawing helper
    private void DrawCapsuleGizmo(CapsuleCollider cap)
    {
        Matrix4x4 m = cap.transform.localToWorldMatrix;
        Gizmos.matrix = m;

        float height = cap.height;
        float radius = cap.radius;
        Vector3 center = cap.center;

        int dir = cap.direction;
        Vector3 axis = dir == 0 ? Vector3.right :
                       dir == 1 ? Vector3.up :
                                  Vector3.forward;

        float cylinderHeight = Mathf.Max(0, height - radius * 2);

        Gizmos.DrawWireCube(center, new Vector3(
            dir == 0 ? height : radius * 2,
            dir == 1 ? height : radius * 2,
            dir == 2 ? height : radius * 2
        ));

        Gizmos.DrawWireSphere(center + axis * (height / 2 - radius), radius);
        Gizmos.DrawWireSphere(center - axis * (height / 2 - radius), radius);
    }
}