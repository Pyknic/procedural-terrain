using Godot;

public class Triangle
{
    public readonly Vector3 a, b, c;
    public readonly Color colorA, colorB, colorC;
    public readonly Vector3 normal;

    public Triangle(Vector3 a, Vector3 b, Vector3 c, 
                    Color colorA, Color colorB, Color colorC)
    {
        this.a = a;
        this.b = b;
        this.c = c;
        this.colorA = colorA;
        this.colorB = colorB;
        this.colorC = colorC;
        normal = (c - b).Cross(c - a).Normalized();
    }

    public bool IntersectsRay(Vector3 origin, Vector3 direction, bool backfaceCulling=false)
    {
        if (backfaceCulling)
        {
            // If ray and normal are pointing in the same direction, then the
            // triangle should be culled.
            if (normal.Dot(direction) <= 0.0f) return false;
        }

        return ComputeDistance(origin, direction) >= 0.0f;
    }

    public float ComputeDistance(Vector3 origin, Vector3 direction)
    {
        Vector3 edge1, edge2, h, s, q;
        float dot, f, u, v, t;
        edge1 = b - a;
        edge2 = c - a;
        h = direction.Cross(edge2);
        dot = edge1.Dot(h);

        if (dot > -float.Epsilon && dot < float.Epsilon)
            return -1f; // This ray is parallel to this triangle.

        f = 1.0f / dot;
        s = origin - a;
        u = f * s.Dot(h);
        if (u < 0.0f || u > 1.0f)
            return -1f;

        q = s.Cross(edge1);
        v = f * direction.Dot(q);
        if (v < 0.0 || u + v > 1.0)
            return -1f;

        t = f * edge2.Dot(q);
        return t > float.Epsilon ? t : -1f;
    }
}