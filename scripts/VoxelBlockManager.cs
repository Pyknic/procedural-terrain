using System;
using System.Collections.Generic;
using Godot;

public class VoxelBlockManager : Spatial
{
    private IDictionary<BlockCoord, VoxelBlock> blocks;
    private GeometryBuffer geometry;
    private Spatial observerNode;

    [Export] int _minHorizontalBlocks;
    [Export] int _maxHorizontalBlocks;
    [Export] int _verticalBlocks;
    [Export] NodePath _observer;
    
    [Export] float _noiseScale = 1.0f;
    [Export] float _heightFactor = 1.0f;
    [Export] float _surfaceLevel = 0.0f;

    [Export] float _maxToolDistance = 100.0f;
    [Export] float _toolRadius      = 2.0f;
    [Export] float _toolStrength    = 0.1f;

    [Export] Material _material;

    private Plane? flattenPlane;

    public VoxelBlockManager()
    {
        blocks   = new Dictionary<BlockCoord, VoxelBlock>();
        geometry = new GeometryBuffer();
    }
    
    public override void _Ready()
    {
        if (_observer == null)
        {
            GD.PrintErr("VoxelBlockManager requires an observer node to know where to generate voxels.");
            return;
        }

        var nodeFound = GetNode<Spatial>(_observer);
        if (nodeFound == null)
        {
            GD.PrintErr("VoxelBlockManager was given a node that did not inherit from Spatial.");
            return;
        }

        observerNode = nodeFound;
        LoadBlocksNear(observerNode.Translation);

        SetPhysicsProcess(true);
    }

    public override void _PhysicsProcess(float delta)
    {
        var flatten = Input.IsActionPressed("flatten");

        var strength = _toolStrength;
        if (Input.IsMouseButtonPressed((int)ButtonList.Right))
            strength = -strength;

        if (Input.IsMouseButtonPressed((int)ButtonList.Left)
        ||  Input.IsMouseButtonPressed((int)ButtonList.Right))
        {
            var spaceState = GetWorld().GetDirectSpaceState();

            var camera = ((Camera)observerNode);
            var from = camera.GetGlobalTransform().origin;
            var cameraNormal = camera.ProjectRayNormal(GetViewport().Size / 2.0f);

            var result = spaceState.IntersectRay(from, from + cameraNormal * _maxToolDistance);
            if (result.Count > 0)
            {
                var clickPos = (Vector3)result["position"];
                var addPos = clickPos + cameraNormal.Inverse().Normalized();
                if (flatten)
                {
                    if (flattenPlane == null)
                    {
                        var collisionNormal = (Vector3)result["normal"];
                        flattenPlane = new Plane(collisionNormal, new Plane(collisionNormal, 0.0f).DistanceTo(clickPos));
                    }

                    EditDensity(addPos, _toolRadius, (worldPos, oldDensity) =>
                    {
                        var distance = (worldPos - clickPos).Length();
                        if (distance >= _toolRadius) return oldDensity;
                        var amount = (_toolRadius - distance) / _toolRadius * (strength * delta);
                        if (flattenPlane.Value.IsPointOver(worldPos))
                            amount = -amount;

                        return oldDensity + amount;
                    });
                }
                else EditDensity(addPos, _toolRadius, (worldPos, oldDensity) =>
                {
                    var distance = (worldPos - clickPos).Length();
                    if (distance >= _toolRadius) return oldDensity;
                    return oldDensity + (_toolRadius - distance) / _toolRadius * (strength * delta);
                });
            }
        }
        else
        {
            flattenPlane = null;
        }
    }

    private static readonly float BLOCK_DIAGONAL = Mathf.Sqrt(2.0f * Mathf.Pow(DensityCube.CELLS_ROW / 2.0f, 2.0f));
    public void EditDensity(Vector3 position, float radius, Func<Vector3, float, float> worldPosToDensity)
    {
        if (Mathf.Abs(radius) < Mathf.Epsilon) throw new ArgumentException("Radius is too small.");
        float maxDistance = radius + BLOCK_DIAGONAL;

        foreach (var coord in blocks.Keys)
        {
            var blockCenter = new Vector3(
                coord.x * DensityCube.CELLS_ROW,
                coord.y * DensityCube.CELLS_ROW,
                coord.z * DensityCube.CELLS_ROW
            );

            var distanceToBlockCenter = (blockCenter - position).Length();
            if (distanceToBlockCenter < maxDistance)
            {
                var block = blocks[coord];
                //var positionInBlockSpace = position - blockCenter;
                block.Edit((localPos, oldDensity) => 
                    worldPosToDensity(localPos + blockCenter, oldDensity));
            }
        }
    }

    private void LoadBlocksNear(Vector3 center)
    {
        var xMax = Mathf.RoundToInt(center.x / DensityCube.CELLS_ROW) + _minHorizontalBlocks / 2;
        var xMin = xMax - _minHorizontalBlocks;
        var yMax = Mathf.RoundToInt(center.y / DensityCube.CELLS_ROW) + _verticalBlocks / 2;
        var yMin = yMax - _verticalBlocks;
        var zMax = Mathf.RoundToInt(center.z / DensityCube.CELLS_ROW) + _minHorizontalBlocks / 2;
        var zMin = zMax - _minHorizontalBlocks;
        
        for (int iy = yMin; iy <= yMax; iy++)
        {
            for (int iz = zMin; iz <= zMax; iz++)
            {
                for (int ix = xMin; ix <= xMax; ix++)
                {
                    var coord = new BlockCoord(ix, iy, iz);
                    if (!blocks.ContainsKey(coord))
                    {
                        var block = new VoxelBlock(coord, geometry);
                        block.MaterialOverride = _material;
                        block.SetConfig(_noiseScale, _heightFactor, _surfaceLevel);
                        
                        block.Translation = new Vector3(
                            ix * DensityCube.CELLS_ROW,
                            iy * DensityCube.CELLS_ROW,
                            iz * DensityCube.CELLS_ROW
                        );
                        block.CreateFromAlgorithm();
                        blocks.Add(coord, block);
                        AddChild(block);
                    }
                }
            }
        }
    }

}