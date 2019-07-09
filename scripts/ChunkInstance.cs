using System.Collections.Generic;
using Godot;

public class ChunkInstance : MeshInstance
{
    private readonly Chunk data;
    private int surfaceId = -1;

    public ChunkInstance() { } // Only used internally by Godot.

    public ChunkInstance(Chunk chunk)
    {
        data             = chunk;
        Translation      = chunk.Position().WorldPosition();
        Mesh             = new ArrayMesh();
        chunk.SetListener(UpdateGeometry);
    }

    public Chunk GetChunk()
    {
        return data;
    }

    public void UpdateGeometry(IList<Triangle> triangles)
    {
        if (triangles.Count == 0) return;

        var indices = new int[triangles.Count * 3];
        var vertices = new Vector3[triangles.Count * 3];
        var normals = new Vector3[triangles.Count * 3];
        var colors = new Color[triangles.Count * 3];

        int i = 0;
        foreach (var triangle in triangles)
        {
            vertices[i * 3]     = triangle.a;
            vertices[i * 3 + 1] = triangle.b;
            vertices[i * 3 + 2] = triangle.c;

            colors[i * 3]     = triangle.colorA;
            colors[i * 3 + 1] = triangle.colorB;
            colors[i * 3 + 2] = triangle.colorC;

            for (int j = 0; j < 3; j++)
            {
                int k = i * 3 + j;
                indices[k] = k;
                normals[k] = triangle.normal;
            }

            i++;
        }

        var mesh = (ArrayMesh)Mesh;
        if (surfaceId >= 0) mesh.SurfaceRemove(surfaceId);
        surfaceId = mesh.GetSurfaceCount();

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)ArrayMesh.ArrayType.Max);
        arrays[(int)ArrayMesh.ArrayType.Index] = indices;
        arrays[(int)ArrayMesh.ArrayType.Vertex] = vertices;
        arrays[(int)ArrayMesh.ArrayType.Normal] = normals;
        arrays[(int)ArrayMesh.ArrayType.Color] = colors;

        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        while (GetChildCount() > 0)
        {
            GetChild(0).Free();
        }

        CreateTrimeshCollision();
    }
}