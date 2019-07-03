using System;
using Godot;

public class VoxelBlock : MeshInstance
{

    //public const int VOXELS_ROW    = 16;
    //private const int VOXELS_PLANE = VOXELS_ROW * VOXELS_ROW;
    //private const int VOXELS_CUBE  = VOXELS_PLANE * VOXELS_ROW;

    private readonly BlockCoord coordinate;
    private readonly GeometryBuffer geometry;
    private readonly DensityCube density;

    [Export] int _noiseSeed          = 1;
    [Export] int _noiseOctaves       = 4;
    [Export] float _noisePeriod      = 20.0f;
    [Export] float _noisePersistence = 0.8f;
    [Export] float _noiseScale       = 1.0f;
    [Export] float _heightFactor     = 1.0f;
    [Export] float _surfaceLevel     = 0.0f;
    [Export] float _cubeScale        = 0.5f;

    public VoxelBlock()
    {
        // Warning! This constructor is not intended to be used! It is only 
        // there so that Godot can create a temporary instance.
    }

    public VoxelBlock(BlockCoord coordinate, GeometryBuffer geometryBuffer = null)
    {
        this.coordinate = coordinate;
        geometry = geometryBuffer ?? new GeometryBuffer();
        density = new DensityCube();
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Mesh = new ArrayMesh();
        RecreateGeometry();
    }

    public void SetConfig(float noiseScale, float heightFactor, float surfaceLevel)
    {
        _noiseScale   = noiseScale;
        _heightFactor = heightFactor;
        _surfaceLevel = surfaceLevel;
    }

    //public float GetDensity(BlockCoord cell)
    //{
    //    if (IsInside(cell)) return density[cell.y * WIDTH2 + cell.z * WIDTH + cell.x];
    //    throw new ArgumentOutOfRangeException(
    //        $"Given local coordinates ({cell.x}, {cell.y}, {cell.z}) is out of range.");
    //}

    public void CreateFromAlgorithm()
    {
        var noise = new OpenSimplexNoise
        {
            Octaves     = _noiseOctaves,
            Period      = _noisePeriod,
            Persistence = _noisePersistence,
            Seed        = _noiseSeed
        };

        var blockPos = new Vector3(Translation);
        density.AddDensity(localPos =>
        {
            var pos = blockPos + localPos;
            return noise.GetNoise3dv(pos * _noiseScale) - pos.y * _heightFactor;
        });
    }

    public void AddDensity(Vector3 localPosition, float radius, float strength)
    {
        if (Mathf.Abs(radius) < Mathf.Epsilon) throw new ArgumentException("Radius is too small.");
        if (Mathf.Abs(strength) < Mathf.Epsilon) return;

        if (density.AddDensity(localPos =>
        {
            var distance = (localPos - localPosition).Length();
            if (distance >= radius) return 0.0f;
            return (radius - distance) / radius * strength;
        }))
        {
            RecreateGeometry();
        }
    }

    private int surfaceId = -1;

    private void RecreateGeometry()
    {
        geometry.Clear();

        for (int i = 0; i < DensityCube.CELLS_CUBE; i++)
            TriangulateVoxel(i);

        if (surfaceId >= 0) ((ArrayMesh) Mesh).SurfaceRemove(surfaceId);
        surfaceId = geometry.Build((ArrayMesh) Mesh);

        for (int i = 0; i < GetChildCount(); i++)
        {
            var child = GetChild(i);
            if (child is StaticBody)
                child.QueueFree();
        }

        if (surfaceId >= 0) CreateTrimeshCollision();
    }

    private void TriangulateVoxel(int cellIdx)
    {
        var cellPos = DensityCube.CellCenter(cellIdx);

        // Build a bitset of the status of the eight corners in the cube
        var voxelType = 0;
        for (int i = 0; i < 8; i++)
        {
            var cornerDensity = density.GetDensity(cellIdx, (DensityCube.Corner) i);
            if (cornerDensity < _surfaceLevel)
                voxelType |= 0x1 << i;
        }
        
        var fromTriangleTable = TRIANGLE_TABLE[voxelType];
        var triangle = new Vector3[3];
        
        // Iterate over the triangles in the voxel
        for (var i = 0; i < fromTriangleTable.Length / 3; i++)
        {
            // Compute the vertices of the triangle
            for (var j = 0; j < 3; j++)
            {
                var edgeIdx = fromTriangleTable[i * 3 + j];
                var cornerA = (DensityCube.Corner)CORNER_INDEX_A_FROM_EDGE[edgeIdx];
                var cornerB = (DensityCube.Corner)CORNER_INDEX_B_FROM_EDGE[edgeIdx];

                var densityA = density.GetDensity(cellIdx, cornerA);
                var densityB = density.GetDensity(cellIdx, cornerB);

                var cornerPosA = cellPos + DensityCube.CornerPosition(cornerA);
                var cornerPosB = cellPos + DensityCube.CornerPosition(cornerB);

                var interpFactor = (_surfaceLevel - densityA) / (densityB - densityA);
                triangle[j] = cornerPosA + (cornerPosB - cornerPosA) * interpFactor;
            }

            // Compute the normals of the triangle
            var crossA = triangle[0];
            var crossB = triangle[1];
            var crossC = triangle[2];
            var normal = (crossC - crossB).Cross(crossC - crossA).Normalized();
            
            for (var j = 0; j < 3; j++)
                geometry.Append(triangle[j], normal, new Color(0, 0, 0));
        }
    }

    //private float LookupDensity(BlockCoord neighbourCell)
    //{
    //    float cornerDensity;
    //    if (IsInside(neighbourCell))
    //        cornerDensity = density[neighbourCell.y * VOXELS_PLANE + neighbourCell.z * VOXELS_ROW + neighbourCell.x];
    //    else
    //    {
    //        var neighbourBlock = new BlockCoord(coordinate.x, coordinate.y, coordinate.z);
    //        NeighbourCoordinates(ref neighbourBlock, ref neighbourCell);
    //        cornerDensity = manager.GetDensity(neighbourBlock, neighbourCell);
    //    }

    //    return cornerDensity;
    //}

    //private bool IsInside(BlockCoord cell)
    //{
    //    return cell.x >= 0 && cell.x < VOXELS_ROW
    //                       && cell.y >= 0 && cell.y < VOXELS_ROW
    //                       && cell.z >= 0 && cell.z < VOXELS_ROW;
    //}

    //private Vector3 WorldPositionOf(int cellIdx)
    //{
    //    return Translation + LocalPositionOf(cellIdx);
    //}

    //private static Vector3 LocalPositionOf(int cellIdx)
    //{
    //    return new Vector3(
    //        cellIdx % VOXELS_ROW,
    //        cellIdx / VOXELS_PLANE % VOXELS_ROW,
    //        cellIdx / VOXELS_ROW % VOXELS_ROW
    //    );
    //}

    //private static BlockCoord LocalCoordinateOf(int cellIdx)
    //{
    //    return new BlockCoord(
    //        cellIdx % VOXELS_ROW,
    //        cellIdx / VOXELS_PLANE % VOXELS_ROW,
    //        cellIdx / VOXELS_ROW % VOXELS_ROW
    //    );
    //}

    //private static void NeighbourCoordinates(ref BlockCoord neighbourBlock, ref BlockCoord neighbourCell)
    //{
    //    if (neighbourCell.x < 0)
    //    {
    //        neighbourBlock.x--;
    //        neighbourCell.x = VOXELS_ROW - 1;
    //    }
    //    else if (neighbourCell.x >= VOXELS_ROW)
    //    {
    //        neighbourBlock.x++;
    //        neighbourCell.x = 0;
    //    }

    //    if (neighbourCell.y < 0)
    //    {
    //        neighbourBlock.y--;
    //        neighbourCell.y = VOXELS_ROW - 1;
    //    }
    //    else if (neighbourCell.y >= VOXELS_ROW)
    //    {
    //        neighbourBlock.y++;
    //        neighbourCell.y = 0;
    //    }

    //    if (neighbourCell.z < 0)
    //    {
    //        neighbourBlock.z--;
    //        neighbourCell.z = VOXELS_ROW - 1;
    //    }
    //    else if (neighbourCell.z >= VOXELS_ROW)
    //    {
    //        neighbourBlock.z++;
    //        neighbourCell.z = 0;
    //    }
    //}

    //private static readonly BlockCoord[] CUBE_CORNERS =
    //{
    //    new BlockCoord(-1, -1, -1),
    //    new BlockCoord(1, -1, -1),
    //    new BlockCoord(1, -1, 1),
    //    new BlockCoord(-1, -1, 1),
    //    new BlockCoord(-1, 1, -1),
    //    new BlockCoord(1, 1, -1),
    //    new BlockCoord(1, 1, 1),
    //    new BlockCoord(-1, 1, 1)
    //};
    
    //private static readonly Vector3[] CUBE_CORNER_POSITIONS =
    //{
    //    new Vector3(-.5f, -.5f, -.5f),
    //    new Vector3(.5f, -.5f, -.5f),
    //    new Vector3(.5f, -.5f, .5f),
    //    new Vector3(-.5f, -.5f, .5f),
    //    new Vector3(-.5f, .5f, -.5f),
    //    new Vector3(.5f, .5f, -.5f),
    //    new Vector3(.5f, .5f, .5f),
    //    new Vector3(-.5f, .5f, .5f)
    //};

    private static readonly int[] CORNER_INDEX_A_FROM_EDGE = { 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3 };
    private static readonly int[] CORNER_INDEX_B_FROM_EDGE = { 1, 2, 3, 0, 5, 6, 7, 4, 4, 5, 6, 7 };

    private static readonly byte[][] TRIANGLE_TABLE =
    {
        new byte[] { },
        new byte[] {0, 8, 3},
        new byte[] {0, 1, 9},
        new byte[] {1, 8, 3, 9, 8, 1},
        new byte[] {1, 2, 10},
        new byte[] {0, 8, 3, 1, 2, 10},
        new byte[] {9, 2, 10, 0, 2, 9},
        new byte[] {2, 8, 3, 2, 10, 8, 10, 9, 8},
        new byte[] {3, 11, 2},
        new byte[] {0, 11, 2, 8, 11, 0},
        new byte[] {1, 9, 0, 2, 3, 11},
        new byte[] {1, 11, 2, 1, 9, 11, 9, 8, 11},
        new byte[] {3, 10, 1, 11, 10, 3},
        new byte[] {0, 10, 1, 0, 8, 10, 8, 11, 10},
        new byte[] {3, 9, 0, 3, 11, 9, 11, 10, 9},
        new byte[] {9, 8, 10, 10, 8, 11},
        new byte[] {4, 7, 8},
        new byte[] {4, 3, 0, 7, 3, 4},
        new byte[] {0, 1, 9, 8, 4, 7},
        new byte[] {4, 1, 9, 4, 7, 1, 7, 3, 1},
        new byte[] {1, 2, 10, 8, 4, 7},
        new byte[] {3, 4, 7, 3, 0, 4, 1, 2, 10},
        new byte[] {9, 2, 10, 9, 0, 2, 8, 4, 7},
        new byte[] {2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4},
        new byte[] {8, 4, 7, 3, 11, 2},
        new byte[] {11, 4, 7, 11, 2, 4, 2, 0, 4},
        new byte[] {9, 0, 1, 8, 4, 7, 2, 3, 11},
        new byte[] {4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1},
        new byte[] {3, 10, 1, 3, 11, 10, 7, 8, 4},
        new byte[] {1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4},
        new byte[] {4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3},
        new byte[] {4, 7, 11, 4, 11, 9, 9, 11, 10},
        new byte[] {9, 5, 4},
        new byte[] {9, 5, 4, 0, 8, 3},
        new byte[] {0, 5, 4, 1, 5, 0},
        new byte[] {8, 5, 4, 8, 3, 5, 3, 1, 5},
        new byte[] {1, 2, 10, 9, 5, 4},
        new byte[] {3, 0, 8, 1, 2, 10, 4, 9, 5},
        new byte[] {5, 2, 10, 5, 4, 2, 4, 0, 2},
        new byte[] {2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8},
        new byte[] {9, 5, 4, 2, 3, 11},
        new byte[] {0, 11, 2, 0, 8, 11, 4, 9, 5},
        new byte[] {0, 5, 4, 0, 1, 5, 2, 3, 11},
        new byte[] {2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5},
        new byte[] {10, 3, 11, 10, 1, 3, 9, 5, 4},
        new byte[] {4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10},
        new byte[] {5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3},
        new byte[] {5, 4, 8, 5, 8, 10, 10, 8, 11},
        new byte[] {9, 7, 8, 5, 7, 9},
        new byte[] {9, 3, 0, 9, 5, 3, 5, 7, 3},
        new byte[] {0, 7, 8, 0, 1, 7, 1, 5, 7},
        new byte[] {1, 5, 3, 3, 5, 7},
        new byte[] {9, 7, 8, 9, 5, 7, 10, 1, 2},
        new byte[] {10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3},
        new byte[] {8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2},
        new byte[] {2, 10, 5, 2, 5, 3, 3, 5, 7},
        new byte[] {7, 9, 5, 7, 8, 9, 3, 11, 2},
        new byte[] {9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11},
        new byte[] {2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7},
        new byte[] {11, 2, 1, 11, 1, 7, 7, 1, 5},
        new byte[] {9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11},
        new byte[] {5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0},
        new byte[] {11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0},
        new byte[] {11, 10, 5, 7, 11, 5},
        new byte[] {10, 6, 5},
        new byte[] {0, 8, 3, 5, 10, 6},
        new byte[] {9, 0, 1, 5, 10, 6},
        new byte[] {1, 8, 3, 1, 9, 8, 5, 10, 6},
        new byte[] {1, 6, 5, 2, 6, 1},
        new byte[] {1, 6, 5, 1, 2, 6, 3, 0, 8},
        new byte[] {9, 6, 5, 9, 0, 6, 0, 2, 6},
        new byte[] {5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8},
        new byte[] {2, 3, 11, 10, 6, 5},
        new byte[] {11, 0, 8, 11, 2, 0, 10, 6, 5},
        new byte[] {0, 1, 9, 2, 3, 11, 5, 10, 6},
        new byte[] {5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11},
        new byte[] {6, 3, 11, 6, 5, 3, 5, 1, 3},
        new byte[] {0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6},
        new byte[] {3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9},
        new byte[] {6, 5, 9, 6, 9, 11, 11, 9, 8},
        new byte[] {5, 10, 6, 4, 7, 8},
        new byte[] {4, 3, 0, 4, 7, 3, 6, 5, 10},
        new byte[] {1, 9, 0, 5, 10, 6, 8, 4, 7},
        new byte[] {10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4},
        new byte[] {6, 1, 2, 6, 5, 1, 4, 7, 8},
        new byte[] {1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7},
        new byte[] {8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6},
        new byte[] {7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9},
        new byte[] {3, 11, 2, 7, 8, 4, 10, 6, 5},
        new byte[] {5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11},
        new byte[] {0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6},
        new byte[] {9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6},
        new byte[] {8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6},
        new byte[] {5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11},
        new byte[] {0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7},
        new byte[] {6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9},
        new byte[] {10, 4, 9, 6, 4, 10},
        new byte[] {4, 10, 6, 4, 9, 10, 0, 8, 3},
        new byte[] {10, 0, 1, 10, 6, 0, 6, 4, 0},
        new byte[] {8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10},
        new byte[] {1, 4, 9, 1, 2, 4, 2, 6, 4},
        new byte[] {3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4},
        new byte[] {0, 2, 4, 4, 2, 6},
        new byte[] {8, 3, 2, 8, 2, 4, 4, 2, 6},
        new byte[] {10, 4, 9, 10, 6, 4, 11, 2, 3},
        new byte[] {0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6},
        new byte[] {3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10},
        new byte[] {6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1},
        new byte[] {9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3},
        new byte[] {8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1},
        new byte[] {3, 11, 6, 3, 6, 0, 0, 6, 4},
        new byte[] {6, 4, 8, 11, 6, 8},
        new byte[] {7, 10, 6, 7, 8, 10, 8, 9, 10},
        new byte[] {0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10},
        new byte[] {10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0},
        new byte[] {10, 6, 7, 10, 7, 1, 1, 7, 3},
        new byte[] {1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7},
        new byte[] {2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9},
        new byte[] {7, 8, 0, 7, 0, 6, 6, 0, 2},
        new byte[] {7, 3, 2, 6, 7, 2},
        new byte[] {2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7},
        new byte[] {2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7},
        new byte[] {1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11},
        new byte[] {11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1},
        new byte[] {8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6},
        new byte[] {0, 9, 1, 11, 6, 7},
        new byte[] {7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0},
        new byte[] {7, 11, 6},
        new byte[] {7, 6, 11},
        new byte[] {3, 0, 8, 11, 7, 6},
        new byte[] {0, 1, 9, 11, 7, 6},
        new byte[] {8, 1, 9, 8, 3, 1, 11, 7, 6},
        new byte[] {10, 1, 2, 6, 11, 7},
        new byte[] {1, 2, 10, 3, 0, 8, 6, 11, 7},
        new byte[] {2, 9, 0, 2, 10, 9, 6, 11, 7},
        new byte[] {6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8},
        new byte[] {7, 2, 3, 6, 2, 7},
        new byte[] {7, 0, 8, 7, 6, 0, 6, 2, 0},
        new byte[] {2, 7, 6, 2, 3, 7, 0, 1, 9},
        new byte[] {1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6},
        new byte[] {10, 7, 6, 10, 1, 7, 1, 3, 7},
        new byte[] {10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8},
        new byte[] {0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7},
        new byte[] {7, 6, 10, 7, 10, 8, 8, 10, 9},
        new byte[] {6, 8, 4, 11, 8, 6},
        new byte[] {3, 6, 11, 3, 0, 6, 0, 4, 6},
        new byte[] {8, 6, 11, 8, 4, 6, 9, 0, 1},
        new byte[] {9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6},
        new byte[] {6, 8, 4, 6, 11, 8, 2, 10, 1},
        new byte[] {1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6},
        new byte[] {4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9},
        new byte[] {10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3},
        new byte[] {8, 2, 3, 8, 4, 2, 4, 6, 2},
        new byte[] {0, 4, 2, 4, 6, 2},
        new byte[] {1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8},
        new byte[] {1, 9, 4, 1, 4, 2, 2, 4, 6},
        new byte[] {8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1},
        new byte[] {10, 1, 0, 10, 0, 6, 6, 0, 4},
        new byte[] {4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3},
        new byte[] {10, 9, 4, 6, 10, 4},
        new byte[] {4, 9, 5, 7, 6, 11},
        new byte[] {0, 8, 3, 4, 9, 5, 11, 7, 6},
        new byte[] {5, 0, 1, 5, 4, 0, 7, 6, 11},
        new byte[] {11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5},
        new byte[] {9, 5, 4, 10, 1, 2, 7, 6, 11},
        new byte[] {6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5},
        new byte[] {7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2},
        new byte[] {3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6},
        new byte[] {7, 2, 3, 7, 6, 2, 5, 4, 9},
        new byte[] {9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7},
        new byte[] {3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0},
        new byte[] {6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8},
        new byte[] {9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7},
        new byte[] {1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4},
        new byte[] {4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10},
        new byte[] {7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10},
        new byte[] {6, 9, 5, 6, 11, 9, 11, 8, 9},
        new byte[] {3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5},
        new byte[] {0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11},
        new byte[] {6, 11, 3, 6, 3, 5, 5, 3, 1},
        new byte[] {1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6},
        new byte[] {0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10},
        new byte[] {11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5},
        new byte[] {6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3},
        new byte[] {5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2},
        new byte[] {9, 5, 6, 9, 6, 0, 0, 6, 2},
        new byte[] {1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8},
        new byte[] {1, 5, 6, 2, 1, 6},
        new byte[] {1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6},
        new byte[] {10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0},
        new byte[] {0, 3, 8, 5, 6, 10},
        new byte[] {10, 5, 6},
        new byte[] {11, 5, 10, 7, 5, 11},
        new byte[] {11, 5, 10, 11, 7, 5, 8, 3, 0},
        new byte[] {5, 11, 7, 5, 10, 11, 1, 9, 0},
        new byte[] {10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1},
        new byte[] {11, 1, 2, 11, 7, 1, 7, 5, 1},
        new byte[] {0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11},
        new byte[] {9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7},
        new byte[] {7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2},
        new byte[] {2, 5, 10, 2, 3, 5, 3, 7, 5},
        new byte[] {8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5},
        new byte[] {9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2},
        new byte[] {9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2},
        new byte[] {1, 3, 5, 3, 7, 5},
        new byte[] {0, 8, 7, 0, 7, 1, 1, 7, 5},
        new byte[] {9, 0, 3, 9, 3, 5, 5, 3, 7},
        new byte[] {9, 8, 7, 5, 9, 7},
        new byte[] {5, 8, 4, 5, 10, 8, 10, 11, 8},
        new byte[] {5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0},
        new byte[] {0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5},
        new byte[] {10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4},
        new byte[] {2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8},
        new byte[] {0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11},
        new byte[] {0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5},
        new byte[] {9, 4, 5, 2, 11, 3},
        new byte[] {2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4},
        new byte[] {5, 10, 2, 5, 2, 4, 4, 2, 0},
        new byte[] {3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9},
        new byte[] {5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2},
        new byte[] {8, 4, 5, 8, 5, 3, 3, 5, 1},
        new byte[] {0, 4, 5, 1, 0, 5},
        new byte[] {8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5},
        new byte[] {9, 4, 5},
        new byte[] {4, 11, 7, 4, 9, 11, 9, 10, 11},
        new byte[] {0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11},
        new byte[] {1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11},
        new byte[] {3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4},
        new byte[] {4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2},
        new byte[] {9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3},
        new byte[] {11, 7, 4, 11, 4, 2, 2, 4, 0},
        new byte[] {11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4},
        new byte[] {2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9},
        new byte[] {9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7},
        new byte[] {3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10},
        new byte[] {1, 10, 2, 8, 7, 4},
        new byte[] {4, 9, 1, 4, 1, 7, 7, 1, 3},
        new byte[] {4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1},
        new byte[] {4, 0, 3, 7, 4, 3},
        new byte[] {4, 8, 7},
        new byte[] {9, 10, 8, 10, 11, 8},
        new byte[] {3, 0, 9, 3, 9, 11, 11, 9, 10},
        new byte[] {0, 1, 10, 0, 10, 8, 8, 10, 11},
        new byte[] {3, 1, 10, 11, 3, 10},
        new byte[] {1, 2, 11, 1, 11, 9, 9, 11, 8},
        new byte[] {3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9},
        new byte[] {0, 2, 11, 8, 0, 11},
        new byte[] {3, 2, 11},
        new byte[] {2, 3, 8, 2, 8, 10, 10, 8, 9},
        new byte[] {9, 10, 2, 0, 9, 2},
        new byte[] {2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8},
        new byte[] {1, 10, 2},
        new byte[] {1, 3, 8, 9, 1, 8},
        new byte[] {0, 9, 1},
        new byte[] {0, 3, 8},
        new byte[] { }
    };
}
