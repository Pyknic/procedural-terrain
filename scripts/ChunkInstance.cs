using System;
using Godot;

public class ChunkInstance : MeshInstance
{
    private const float GEOMETRY_UPDATE_FREQUENCY = 0.05f;

    private Chunk data;
    private GeometryBuffer geometryBuffer;
    private DensityGenerator densityGenerator;
    private int surfaceId = -1;

    public ChunkInstance() { } // Only used internally by Godot.

    public ChunkInstance(Chunk chunk, GeometryBuffer geometry, DensityGenerator density)
    {
        data             = chunk;
        geometryBuffer   = geometry;
        densityGenerator = density;
        Translation      = data.WorldPosition();
        Mesh             = new ArrayMesh();
    }

    public override void _Ready()
    {
        base._Ready();
        data.InitDensity(densityGenerator.GetDensity);
        data.InitLights(pos => pos.y > 0 ? 1.0f : 0.0f);
        data.UpdateLights();
        UpdateGeometry();
    }

    float lastCheck = 0.0f;
    public override void _Process(float delta)
    {
        base._Process(delta);
        lastCheck += delta;
        if (lastCheck > GEOMETRY_UPDATE_FREQUENCY)
        {
            if (data.PollDensityDirty() || data.PollLightDirty())
            {
                UpdateGeometry();
            }

            lastCheck = 0.0f;
        }
    }

    public Chunk GetChunk()
    {
        return data;
    }

    public void UpdateGeometry()
    {
        geometryBuffer.Clear();
        data.CreateGeometry(geometryBuffer);

        if (surfaceId >= 0) ((ArrayMesh)Mesh).SurfaceRemove(surfaceId);
        surfaceId = geometryBuffer.Build((ArrayMesh)Mesh);

        for (int i = 0; i < GetChildCount(); i++)
        {
            var child = GetChild(i);
            if (child is StaticBody)
                child.QueueFree();
        }

        if (surfaceId >= 0) CreateTrimeshCollision();
    }
}