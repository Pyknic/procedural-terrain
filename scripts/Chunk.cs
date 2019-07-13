using System;
using System.Collections.Generic;
using Godot;

public class Chunk
{
    private readonly ChunkPosition position;
    private Chunk[] neighbours;
    private float[] densities;
    private Luminance luminance;
    private IList<Triangle> triangles;
    private Action<IList<Triangle>> listener;

    public Chunk(ChunkPosition pos)
    {
        position   = pos;
        neighbours = new Chunk[6];
        densities  = new float[Point.CUBE_SIZE];
        luminance  = new Luminance(this);
        triangles  = new List<Triangle>();
    }

    public ChunkPosition Position()
    {
        return position;
    }

    public void SetListener(Action<IList<Triangle>> triangulationListener)
    {
        listener = triangulationListener;
    }

    public void AddNeighbour(Direction direction, Chunk chunk)
    {
        neighbours[(int)direction] = chunk;
    }

    public void RemoveNeighbour(Direction direction, Chunk chunk)
    {
        neighbours[(int)direction] = null;
    }

    public Chunk GetNeighbour(Direction direction)
    {
        return neighbours[(int)direction];
    }

    public Chunk GetNeighbour(Direction dir1, Direction dir2)
    {
        var a = neighbours[(int)dir1];
        if (a == null)
        {
            var b = neighbours[(int)dir2];
            if (b == null) return null;
            return b.GetNeighbour(dir1);
        }
        return a.GetNeighbour(dir2);
    }

    public Chunk GetNeighbour(Direction dir1, Direction dir2, Direction dir3)
    {
        var a = neighbours[(int)dir1];
        if (a != null)
        {
            var abc = a.GetNeighbour(dir2, dir3);
            if (abc != null) return abc;
        }

        var b = neighbours[(int)dir2];
        if (b != null)
        {
            var bac = b.GetNeighbour(dir1, dir3);
            if (bac != null) return bac;
        }

        var c = neighbours[(int)dir3];
        if (c != null)
        {
            var cab = c.GetNeighbour(dir1, dir2);
            if (cab != null) return cab;
        }

        return null;
    }

    public bool HasNeighbour(Direction direction)
    {
        return GetNeighbour(direction) != null;
    }

    public void InitDensity(Func<Vector3, float> initializer)
    {
        var chunkPos = position.WorldPosition();
        for (int i = 0; i < Point.CUBE_SIZE; i++) {
            var point    = new Point(i);
            var pos      = point.WorldPosition(chunkPos);
            var density  = LimitDensity(initializer(pos));
            densities[i] = density;
        }
    }

    public float GetDensity(Point point)
    {
        return point.SampleFrom(ref densities);
    }

    public void EditDensity(Func<Vector3, float, float> editor)
    {
        // Do a breadth-first search through the graph for chunks that changed
        // as a result of applying the editor.
        var queue   = new Queue<Chunk>();
        var visited = new Dictionary<ChunkPosition, Chunk>();

        // Keep track of points where the solidity flipped.
        var flippedPoints = new HashSet<GlobalPoint>();

        queue.Enqueue(this);
        visited.Add(position, this);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var neighbourMask = current.EditDensityLocally(editor, ref flippedPoints);

            for (int n = 0; n < 6; n++)
            {
                var bit = 0x1 << n;
                if ((neighbourMask & bit) != 0)
                {
                    var neighbour = current.neighbours[n];
                    if (neighbour != null)
                    {
                        if (!visited.ContainsKey(neighbour.position))
                        {
                            queue.Enqueue(neighbour);
                            visited.Add(neighbour.position, neighbour);
                        }
                    }
                }
            }
        }

        // Update the luminance in affected points
        if (flippedPoints.Count >= 1)
            luminance.UpdateAt(ref flippedPoints);
    }

    private int EditDensityLocally(Func<Vector3, float, float> editor, ref HashSet<GlobalPoint> flippedPoints)
    {
        const float EDIT_THRESHOLD = 0.001f;
        var chunkPos = position.WorldPosition();

        int neighbourMask = 0;
        for (int i = 0; i < Point.CUBE_SIZE; i++)
        {
            var point = new Point(i);
            var pos   = point.WorldPosition(chunkPos);

            var oldDensity = densities[i];
            var newDensity = LimitDensity(editor(pos, oldDensity));

            if (Mathf.Abs(oldDensity - newDensity) > EDIT_THRESHOLD)
            {
                densities[i] = newDensity;
                neighbourMask |= point.NeighbourMask();
            }

            if ((oldDensity >= MarchingCubes.DENSITY_THRESHOLD
            &&   newDensity < MarchingCubes.DENSITY_THRESHOLD)
            ||  (oldDensity < MarchingCubes.DENSITY_THRESHOLD
            &&   newDensity >= MarchingCubes.DENSITY_THRESHOLD))
                flippedPoints.Add(new GlobalPoint(this, point));
        }

        if (neighbourMask != 0)
            TriangulateChunk();

        return neighbourMask;
    }

    public void TriangulateChunk()
    {
        triangles.Clear();
        MarchingCubes.Triangulate(
            ref densities,
            ref luminance.GetDepths(), 
            ref triangles);
        listener?.Invoke(triangles);
    }

    public Luminance GetLuminance()
    {
        return luminance;
    }

    //public byte GetDepth(Point point)
    //{
    //    return depths[point.index];
    //}

    //public void SetDepth(Point point, byte depth)
    //{
    //    depths[point.index] = depth;
    //}

    //public void ResetDepth(byte depth)
    //{
    //    for (int i = 0; i < Point.CUBE_SIZE; i++)
    //        depths[i] = depth;
    //}

    //public void UpdateLuminance()
    //{
    //    UpdateLuminance(this);
    //}

    //private struct GlobalPointUpdate
    //{
    //    public readonly GlobalPoint point;
    //    public readonly int newValue;

    //    public GlobalPointUpdate(GlobalPoint point, int newValue)
    //    {
    //        this.point    = point;
    //        this.newValue = newValue;
    //    }
    //}

    //private void UpdateLuminanceAt(Point at)
    //{
    //    var queue = new Queue<GlobalPoint>();
    //    queue.Enqueue(new GlobalPoint(this, at));

    //    while (queue.Count > 0)
    //    {

    //    }
    //}

    //private void UpdateLuminanceAt(Point at)
    //{
    //    var point = new GlobalPoint(this, at);

    //    // Determine the minimum depth of any neighbour.
    //    int minDepth = 0xff;
    //    for (int n = 0; n < 6; n++)
    //    {
    //        var dir = (Direction)n;
    //        var neighbour = point.Step(dir);
    //        if (neighbour == null)
    //        {
    //            if (dir == Direction.ABOVE)
    //            {
    //                minDepth = 0;
    //                break;
    //            }
    //        }
    //        else
    //        {
    //            var neighbourDepth = 0xff & neighbour.GetDepth();
    //            if (neighbourDepth < minDepth)
    //            {
    //                minDepth = neighbourDepth;
    //                if (minDepth == 0) break;
    //            }
    //        }
    //    }

    //    //if (minDepth == 0xff)
    //    //{
    //    //    point.SetDepth(0xff);
    //    //    return;
    //    //}

    //    var queue = new Queue<GlobalPointUpdate>();
    //    queue.Enqueue(new GlobalPointUpdate(
    //        new GlobalPoint(this, at), 
    //        minDepth == 0xff ? 0xff : minDepth + 1));

    //    while (queue.Count > 0)
    //    {
    //        var current = queue.Dequeue();
    //        var oldValue = current.point.GetDepth();
    //        current.point.SetDepth((byte)current.newValue);

    //        if (!current.point.IsSolid())
    //        {

    //        }
    //    }

    //    point.SetDepth((byte)(minDepth + 1));
    //    if (point.IsSolid()) return;

    //    for (int n = 0; n < 6; n++)
    //    {
    //        var dir = (Direction)n;
    //        var neighbour = point.Step(dir);
    //        if (neighbour != null) {
    //            var neighbourDepth = 0xff & neighbour.GetDepth();
    //            if (neighbourDepth == minDepth + 1)
    //            {
    //                minDepth = neighbourDepth;
    //                if (minDepth == 0) break;
    //            }
    //        }
    //    }
    //}

    //private static void UpdateLuminance(Chunk firstChunk)
    //{
    //    // Reset the depth value in each chunk in the graph to 0xff.
    //    // Also enqueue all chunks without any chunk above them and use these as
    //    // the base for the daylight check.
    //    var daylightQueue = new Queue<Chunk>();

    //    const byte MAX_DEPTH = 0xff;
    //    const byte NO_DEPTH  = 0x00;

    //    firstChunk.ForEachChunk(chunk => {
    //        for (int i = 0; i < Point.CUBE_SIZE; i++)
    //            chunk.depths[i] = MAX_DEPTH;

    //        if (!chunk.HasNeighbour(Direction.ABOVE))
    //            daylightQueue.Enqueue(chunk);
    //    });

    //    var globalPoints = new Queue<GlobalPoint>();

    //    // Process the queue of chunks that have been hit by daylight by setting 
    //    // the depth of all points without anything above them to 0. Continue
    //    // by queueing up all chunks below until solid density is encountered.
    //    while (daylightQueue.Count > 0)
    //    {
    //        var current = daylightQueue.Dequeue();
    //        var above = current.GetNeighbour(Direction.ABOVE);
    //        long daylightMaskAbove = above == null ? -1L : above.bottomDaylightMask;

    //        current.bottomDaylightMask = 0L;

    //        for (int i = 0; i < Point.PLANE_SIZE; i++)
    //        {
    //            var bit = 0x1L << i;
    //            if ((daylightMaskAbove & bit) != 0)
    //            {
    //                for (int j = Point.ROW_SIZE - 1; j >= 0; j--)
    //                {
    //                    var point = new Point(i % Point.ROW_SIZE, j, i / Point.ROW_SIZE);
    //                    var density = point.SampleFrom(ref current.densities);

    //                    var globalPoint = new GlobalPoint(current, point);


    //                    if (density <= MarchingCubes.DENSITY_THRESHOLD)
    //                    {
    //                        globalPoint.SetDepth(NO_DEPTH);
    //                        globalPoints.Enqueue(globalPoint);
    //                    }
    //                    else
    //                    {
    //                        globalPoint.SetDepth(0x1);
    //                        break;
    //                    }
    //                }
    //            }

    //            // If we hit the bottom of this chunk with daylight, mark it in
    //            // the bitset
    //            if (current.depths[i] == NO_DEPTH)
    //                current.bottomDaylightMask |= bit;
    //        }

    //        // If at least one point of daylight reached the bottom layer, 
    //        // enqueue the chunk below this one
    //        if (current.bottomDaylightMask != 0L)
    //        {
    //            var below = current.GetNeighbour(Direction.BELOW);
    //            if (below != null) daylightQueue.Enqueue(below);
    //        }
    //    }

    //    // We now have a queue of all the points in the world that are located 
    //    // directly under the sun. Process them in breadth-first order, setting 
    //    // the depth of every additional point visited to the depth of the 
    //    // previously visited point + 1.
    //    while (globalPoints.Count > 0)
    //    {
    //        var point = globalPoints.Dequeue();
    //        var d = point.GetDepth();

    //        if ((d & 0xff) < 0xff)
    //        {
    //            for (int n = 0; n < 6; n++)
    //            {
    //                var direction = (Direction)n;
    //                var nextPoint = point.Step(direction);
    //                if (nextPoint != null)
    //                {
    //                    if (nextPoint.IsMaxDepth())
    //                    {
    //                        nextPoint.SetDepth((byte) ((0xff & d) + 1));
    //                        if (nextPoint.GetDensity() <= MarchingCubes.DENSITY_THRESHOLD)
    //                        {
    //                            globalPoints.Enqueue(nextPoint);
    //                        }
    //                    }
    //                }
    //            }
    //        }
    //        else break;
    //    }

    //    // Since the depth values have changed, perform a new triangulation of 
    //    // the entire world. TODO We should keep track of which chunks was 
    //    // actually affected by the change and only triangulate those.
    //    //firstChunk.ForEachChunk(chunk => chunk.TriangulateChunk());
    //}

    public void ForEachChunk(Action<Chunk> action)
    {
        var queue = new Queue<Chunk>();
        var visited = new HashSet<ChunkPosition>();
        queue.Enqueue(this);
        visited.Add(position);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            action(current);
            for (int n = 0; n < 6; n++)
            {
                var dir = (Direction)n;
                var neighbour = current.GetNeighbour(dir);
                if (neighbour != null && visited.Add(neighbour.position))
                    queue.Enqueue(neighbour);
            }
        }
    }

    public string GetInfo(Vector3 corner)
    {
        int x = (int)(corner.x - (position.x - 0.5f) * Cell.ROW_SIZE);
        int y = (int)(corner.y - (position.y - 0.5f) * Cell.ROW_SIZE);
        int z = (int)(corner.z - (position.z - 0.5f) * Cell.ROW_SIZE);

        var globalPoint = new GlobalPoint(this, new Point(x, y, z));
        return globalPoint + "\nDensity: " + globalPoint.GetDensity() + 
            ", Depth: " + (0xff & globalPoint.GetDepth());
    }

    private static float LimitDensity(float density)
    {
        return density < 0.0f ? 0.0f : density > 1.0f ? 1.0f : density;
    }
}