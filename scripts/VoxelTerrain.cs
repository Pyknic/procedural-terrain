using System;
using System.Collections.Generic;
using Godot;

public class VoxelTerrain : Spatial
{
    [Export] NodePath densityGeneratorPath;
    [Export] Material material;
    [Export] NodePath observerPath;
    [Export] NodePath debugLabelPath;

    [Export] float toolStrength    = 1.0f;
    [Export] float toolRadius      = 2.0f;
    [Export] float maxToolDistance = 10.0f;

    private DensityGenerator densityGenerator;
    private Spatial observer;
    private Label debugLabel;

    private Dictionary<Chunk.ChunkPos, Chunk> chunks;
    private GeometryBuffer geometryBuffer;

    public VoxelTerrain()
    {
        chunks = new Dictionary<Chunk.ChunkPos, Chunk>();
        geometryBuffer = new GeometryBuffer();
    }

    public override void _Ready()
    {
        if (densityGeneratorPath == null)
        {
            GD.PushError("VoxelTerrain must have a Density Generator.");
            return;
        }

        densityGenerator = GetNode<DensityGenerator>(densityGeneratorPath);
        if (densityGenerator == null)
        {
            GD.PushError("VoxelTerrain could not locate the supplied Density Generator.");
            return;
        }

        if (observerPath == null)
        {
            GD.PushError("VoxelTerrain must have an observer.");
            return;
        }

        observer = GetNode<Spatial>(observerPath);
        if (observer == null)
        {
            GD.PushError("VoxelTerrain could not locate the supplied observer.");
            return;
        }

        if (!(observer is Camera))
        {
            observer = (Camera) observer.FindNode("Camera");
        }

        if (debugLabelPath != null)
        {
            debugLabel = GetNode<Label>(debugLabelPath);
        }

        for (int y = 0; y < 1; y++)
        {
            for (int z = -3; z <= 3; z++)
            {
                for (int x = -3; x <= 3; x++)
                {
                    var at    = new Chunk.ChunkPos(x, y, z);
                    var chunk = new Chunk(at);
                    AddChunk(chunk);

                    var instance = new ChunkInstance(chunk, geometryBuffer, densityGenerator)
                    {
                        MaterialOverride = material
                    };

                    AddChild(instance);
                }
            }
        }

        SetPhysicsProcess(true);
    }

    private Plane? flattenPlane;
    public override void _PhysicsProcess(float delta)
    {
        base._PhysicsProcess(delta);
        var flatten = Input.IsActionPressed("flatten");

        var strength = toolStrength;
        if (Input.IsMouseButtonPressed((int)ButtonList.Right))
            strength = -strength;

        Chunk chunk = null;
        Vector3? clickPos = null;
        var spaceState = GetWorld().GetDirectSpaceState();

        var camera = (Camera)observer;
        var from = camera.GetGlobalTransform().origin;
        var cameraNormal = camera.ProjectRayNormal(GetViewport().Size / 2.0f);

        var result = spaceState.IntersectRay(from, from + cameraNormal * maxToolDistance);
        if (result.Count > 0)
        {
            clickPos = (Vector3)result["position"];
            //var addPos = clickPos + cameraNormal.Inverse().Normalized();
            var clicked = ((PhysicsBody)result["collider"]).GetParent<Spatial>();
            if (clicked is ChunkInstance)
            {
                chunk = ((ChunkInstance)clicked).GetChunk();

                if (debugLabel != null)
                {
                    debugLabel.Text =
                        "Player: " + camera.Translation.x + ", " + camera.Translation.y + ", " + camera.Translation.z + "\n" +
                        "Clicked: " + chunk.Position().x + ", " + chunk.Position().y + ", " + chunk.Position().z + "\n" +
                        "Neighbours: [";

                    bool first = true;
                    for (int i = 0; i < 6; i++)
                    {
                        if (chunk.HasNeighbour((Chunk.Direction)i))
                        {
                            if (first) first = false;
                            else debugLabel.Text += ", ";

                            switch ((Chunk.Direction)i)
                            {
                                case Chunk.Direction.WEST:
                                    debugLabel.Text += "WEST"; break;
                                case Chunk.Direction.EAST:
                                    debugLabel.Text += "EAST"; break;
                                case Chunk.Direction.BELOW:
                                    debugLabel.Text += "BELOW"; break;
                                case Chunk.Direction.ABOVE:
                                    debugLabel.Text += "ABOVE"; break;
                                case Chunk.Direction.NORTH:
                                    debugLabel.Text += "NORTH"; break;
                                case Chunk.Direction.SOUTH:
                                    debugLabel.Text += "SOUTH"; break;
                                case Chunk.Direction.SELF:
                                    debugLabel.Text += "SELF"; break;
                            }
                        }
                    }

                    debugLabel.Text += "]\n";

                    debugLabel.Text += "Corner: " + chunk.GetInfo(clickPos.Value);
                }
            }
        }


        if (Input.IsMouseButtonPressed((int)ButtonList.Left)
        || Input.IsMouseButtonPressed((int)ButtonList.Right))
        {
            if (chunk != null)
            {
                chunk.EditDensity((worldPos, oldDensity) =>
                {
                    var distance = (worldPos - clickPos.Value).Length();
                    if (distance >= toolRadius) return oldDensity;
                    return oldDensity + (toolRadius - distance) / toolRadius * (strength * delta);
                });
            }
        }
    }

    private Chunk NearestChunk(Vector3 worldPos)
    {
        // Optimistic approach
        var expectedPos = new Chunk.ChunkPos(
            (int) ((worldPos.x + 4f) / -Chunk.CELLS_ROW),
            (int) ((worldPos.y + 4f) / -Chunk.CELLS_ROW),
            (int) ((worldPos.z + 4f) / -Chunk.CELLS_ROW)
        );

        if (chunks.TryGetValue(expectedPos, out Chunk found))
            return found;

        // Search for the nearest position.
        Chunk nearest = null;
        float nearestDistance = 0.0f;
        foreach (var chunk in chunks.Values)
        {
            var distance = (worldPos - chunk.WorldPosition()).LengthSquared();
            if (nearest == null || distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = chunk;
            }
        }

        if (nearest == null)
            GD.PushError("Error! No chunks loaded.");

        return nearest;
    }

    private void WithChunk(Chunk.ChunkPos at, Action<Chunk> action)
    {
        if (chunks.TryGetValue(at, out Chunk found))
            action(found);
    }

    private void AddChunk(Chunk chunk)
    {
        var at = chunk.Position();
        if (chunks.ContainsKey(at))
        {
            GD.PushError("Error! Can't add chunk at (" +
                at.x + ", " + at.y + ", " + at.z + 
                ") since there is already a chunk with that position."
            );
        }
        else
        {
            chunks.Add(at, chunk);
            WithChunk(at + new Chunk.ChunkPos(1, 0, 0), other =>
            {
                chunk.AddNeighbour(Chunk.Direction.EAST, other);
                other.AddNeighbour(Chunk.Direction.WEST, chunk);
            });

            WithChunk(at + new Chunk.ChunkPos(-1, 0, 0), other =>
            {
                chunk.AddNeighbour(Chunk.Direction.WEST, other);
                other.AddNeighbour(Chunk.Direction.EAST, chunk);
            });

            WithChunk(at + new Chunk.ChunkPos(0, 1, 0), other =>
            {
                chunk.AddNeighbour(Chunk.Direction.ABOVE, other);
                other.AddNeighbour(Chunk.Direction.BELOW, chunk);
            });

            WithChunk(at + new Chunk.ChunkPos(0, -1, 0), other =>
            {
                chunk.AddNeighbour(Chunk.Direction.BELOW, other);
                other.AddNeighbour(Chunk.Direction.ABOVE, chunk);
            });

            WithChunk(at + new Chunk.ChunkPos(0, 0, 1), other =>
            {
                chunk.AddNeighbour(Chunk.Direction.SOUTH, other);
                other.AddNeighbour(Chunk.Direction.NORTH, chunk);
            });

            WithChunk(at + new Chunk.ChunkPos(0, 0, -1), other =>
            {
                chunk.AddNeighbour(Chunk.Direction.NORTH, other);
                other.AddNeighbour(Chunk.Direction.SOUTH, chunk);
            });
        }
    }

    private void RemoveChunk(Chunk.ChunkPos at)
    {
        if (!chunks.ContainsKey(at))
        {
            GD.PushError("Error! Expected a chunk at (" +
                at.x + ", " + at.y + ", " + at.z +
                ") but none exist."
            );
        }
        else
        {
            var chunk = chunks[at];

            WithChunk(at + new Chunk.ChunkPos(1, 0, 0), other =>
            {
                chunk.RemoveNeighbour(Chunk.Direction.EAST, chunk);
                other.RemoveNeighbour(Chunk.Direction.WEST, chunk);
            });

            WithChunk(at + new Chunk.ChunkPos(-1, 0, 0), other =>
            {
                chunk.RemoveNeighbour(Chunk.Direction.WEST, chunk);
                other.RemoveNeighbour(Chunk.Direction.EAST, chunk);
            });

            WithChunk(at + new Chunk.ChunkPos(0, 1, 0), other =>
            {
                chunk.RemoveNeighbour(Chunk.Direction.ABOVE, chunk);
                other.RemoveNeighbour(Chunk.Direction.BELOW, chunk);
            });

            WithChunk(at + new Chunk.ChunkPos(0, -1, 0), other =>
            {
                chunk.RemoveNeighbour(Chunk.Direction.BELOW, chunk);
                other.RemoveNeighbour(Chunk.Direction.ABOVE, chunk);
            });

            WithChunk(at + new Chunk.ChunkPos(0, 0, 1), other =>
            {
                chunk.RemoveNeighbour(Chunk.Direction.SOUTH, chunk);
                other.RemoveNeighbour(Chunk.Direction.NORTH, chunk);
            });

            WithChunk(at + new Chunk.ChunkPos(0, 0, -1), other =>
            {
                chunk.RemoveNeighbour(Chunk.Direction.NORTH, chunk);
                other.RemoveNeighbour(Chunk.Direction.SOUTH, chunk);
            });

            chunks.Remove(at);
        }
    }


}