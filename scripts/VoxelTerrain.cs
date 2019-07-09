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

    private Dictionary<ChunkPosition, Chunk> chunks;

    public VoxelTerrain()
    {
        chunks = new Dictionary<ChunkPosition, Chunk>();
    }

    public override void _Ready()
    {
        GD.Print("Ready to create world!");

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

        for (int y = -5; y < 5; y++)
        {
            for (int z = -5; z <= 5; z++)
            {
                for (int x = -5; x <= 5; x++)
                {
                    var at    = new ChunkPosition(x, y, z);
                    var chunk = new Chunk(at);
                    AddChunk(chunk);

                    var instance = new ChunkInstance(chunk)
                    {
                        MaterialOverride = material
                    };

                    chunk.InitDensity(densityGenerator.GetDensity);
                    AddChild(instance);
                }
            }
        }

        var origoChunk = chunks[new ChunkPosition(0, 0, 0)];
        origoChunk.UpdateLuminance();
        origoChunk.ForEachChunk(chunk => chunk.TriangulateChunk());

        SetPhysicsProcess(true);
    }

    private float lastUpdate;
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
                        if (chunk.HasNeighbour((Direction)i))
                        {
                            if (first) first = false;
                            else debugLabel.Text += ", ";

                            switch ((Direction)i)
                            {
                                case Direction.WEST:
                                    debugLabel.Text += "WEST"; break;
                                case Direction.EAST:
                                    debugLabel.Text += "EAST"; break;
                                case Direction.BELOW:
                                    debugLabel.Text += "BELOW"; break;
                                case Direction.ABOVE:
                                    debugLabel.Text += "ABOVE"; break;
                                case Direction.NORTH:
                                    debugLabel.Text += "NORTH"; break;
                                case Direction.SOUTH:
                                    debugLabel.Text += "SOUTH"; break;
                                case Direction.SELF:
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

        if (Input.IsActionJustPressed("update_lights"))
        {
            if (chunk != null)
            {
                chunk.UpdateLuminance();
                chunk.ForEachChunk(c => c.TriangulateChunk());
            }
        }
    }

    private void WithChunk(ChunkPosition at, Action<Chunk> action)
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

            for (int i = 0; i < 6; i++)
            {
                var direction = (Direction)i;
                var opposite  = (Direction) (i % 2 == 0 ? i + 1 : i - 1);
                WithChunk(at.Step(direction), other =>
                {
                    chunk.AddNeighbour(direction, other);
                    other.AddNeighbour(opposite, chunk);
                });
            }
        }
    }

    private void RemoveChunk(ChunkPosition at)
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

            for (int i = 0; i < 6; i++)
            {
                var direction = (Direction)i;
                var opposite = (Direction)(i % 2 == 0 ? i + 1 : i - 1);
                WithChunk(at.Step(direction), other =>
                {
                    chunk.RemoveNeighbour(direction, other);
                    other.RemoveNeighbour(opposite, chunk);
                });
            }

            chunks.Remove(at);
        }
    }
}