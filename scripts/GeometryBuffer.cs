using System;
using Godot;

public class GeometryBuffer
{
    private int[] indices;
    private Vector3[] vertices;
    private Vector3[] normals;
    private Color[] colors;
    private int pos;

    public GeometryBuffer()
    {
        indices  = new int[16];
        vertices = new Vector3[16];
        normals  = new Vector3[16];
        colors   = new Color[16];
        pos      = 0;
    }

    public void Clear()
    {
        pos = 0;
    }

    public void Append(Vector3 vertex, Vector3 normal, Color color)
    {
        if (pos >= indices.Length)
        {
            var newLength = (int) (indices.Length * 1.5);

            {
                var newIndices = new int[newLength];
                Array.Copy(indices, newIndices, indices.Length);
                indices = newIndices;
            }
            
            {
                var newVertices = new Vector3[newLength];
                Array.Copy(vertices, newVertices, vertices.Length);
                vertices = newVertices;
            }
            
            {
                var newNormals = new Vector3[newLength];
                Array.Copy(normals, newNormals, normals.Length);
                normals = newNormals;
            }
            
            {
                var newColors = new Color[newLength];
                Array.Copy(colors, newColors, colors.Length);
                colors = newColors;
            }
        }

        indices[pos]  = pos;
        vertices[pos] = vertex;
        normals[pos]  = normal;
        colors[pos]   = color;
        pos++;
    }

    public int Build(ArrayMesh destination)
    {
        if (pos == 0) return -1;
        
        var id = destination.GetSurfaceCount();

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int) ArrayMesh.ArrayType.Max);

        var newIndices = new int[pos];
        Array.Copy(indices, newIndices, pos);
        arrays[(int) ArrayMesh.ArrayType.Index] = newIndices;
        
        var newVertices = new Vector3[pos];
        Array.Copy(vertices, newVertices, pos);
        arrays[(int) ArrayMesh.ArrayType.Vertex] = newVertices;
        
        var newNormals = new Vector3[pos];
        Array.Copy(normals, newNormals, pos);
        arrays[(int) ArrayMesh.ArrayType.Normal] = newNormals;

        var newColors = new Color[pos];
        Array.Copy(colors, newColors, pos);
        arrays[(int) ArrayMesh.ArrayType.Color] = newColors;
        
        destination.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        return id;
    }
}