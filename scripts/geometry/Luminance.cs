using System;
using System.Collections.Generic;
using Godot;

public class Luminance
{
    public const byte 
        MAX_DEPTH = 0xF,
        MIN_DEPTH = 0x0;

    private readonly Chunk owner;
    private byte[] depths;
    private long bottomDaylightMask = -1L;

    public Luminance(Chunk chunk)
    {
        owner = chunk;
        depths = new byte[Point.CUBE_SIZE];
    }

    public byte GetDepth(Point point)
    {
        return depths[point.index];
    }

    public void SetDepth(Point point, byte depth)
    {
        depths[point.index] = depth;
    }

    public ref byte[] GetDepths()
    {
        return ref depths;
    }

    public void Init()
    {
        // TODO As new chunks are loaded, we need some way to initialize those
        // without changing more points in the existing chunks than absolutly 
        // neccessary.

        // Reset the depth value in each chunk in the graph to 0xff.
        // Also enqueue all chunks without any chunk above them and use these as
        // the base for the daylight check.
        var daylightQueue = new Queue<Chunk>();

        owner.ForEachChunk(chunk => {
            chunk.GetLuminance().FillDepth(MAX_DEPTH);

            if (!chunk.HasNeighbour(Direction.ABOVE))
                daylightQueue.Enqueue(chunk);
        });

        var globalPoints = new Queue<GlobalPoint>();

        // Process the queue of chunks that have been hit by daylight by setting 
        // the depth of all points without anything above them to 0. Continue
        // by queueing up all chunks below until solid density is encountered.
        while (daylightQueue.Count > 0)
        {
            var current = daylightQueue.Dequeue();
            var currentLuminance = current.GetLuminance();

            var above = current.GetNeighbour(Direction.ABOVE);
            long daylightMaskAbove = above == null ? -1L 
                : above.GetLuminance().bottomDaylightMask;

            currentLuminance.bottomDaylightMask = 0L;

            for (int i = 0; i < Point.PLANE_SIZE; i++)
            {
                var bit = 0x1L << i;
                if ((daylightMaskAbove & bit) != 0)
                {
                    for (int j = Point.ROW_SIZE - 1; j >= 0; j--)
                    {
                        var point = new Point(i % Point.ROW_SIZE, j, i / Point.ROW_SIZE);
                        var density = current.GetDensity(point);

                        var globalPoint = new GlobalPoint(current, point);

                        if (density <= MarchingCubes.DENSITY_THRESHOLD)
                        {
                            globalPoint.SetDepth(MIN_DEPTH);
                            globalPoints.Enqueue(globalPoint);
                        }
                        else
                        {
                            globalPoint.SetDepth(0x1);
                            break;
                        }
                    }
                }

                // If we hit the bottom of this chunk with daylight, mark it in
                // the bitset
                if (currentLuminance.depths[i] == MIN_DEPTH)
                    currentLuminance.bottomDaylightMask |= bit;
            }

            // If at least one point of daylight reached the bottom layer, 
            // enqueue the chunk below this one
            if (currentLuminance.bottomDaylightMask != 0L)
            {
                var below = current.GetNeighbour(Direction.BELOW);
                if (below != null) daylightQueue.Enqueue(below);
            }
        }

        // We now have a queue of all the points in the world that are located 
        // directly under the sun. Process them in breadth-first order, setting 
        // the depth of every additional point visited to the depth of the 
        // previously visited point + 1.
        while (globalPoints.Count > 0)
        {
            var point = globalPoints.Dequeue();
            var d = point.GetDepth();

            if ((d & 0xff) < 0xff)
            {
                for (int n = 0; n < 6; n++)
                {
                    var direction = (Direction)n;
                    var nextPoint = point.Step(direction);
                    if (nextPoint != null)
                    {
                        if (nextPoint.IsMaxDepth())
                        {
                            nextPoint.SetDepth((byte)((0xff & d) + 1));
                            if (nextPoint.GetDensity() <= MarchingCubes.DENSITY_THRESHOLD)
                            {
                                globalPoints.Enqueue(nextPoint);
                            }
                        }
                    }
                }
            }
            else break;
        }
    }

    public void UpdateAt(ref HashSet<GlobalPoint> origin)
    {
        GD.Print("Updating luminance starting at " + origin.Count + " points");

        MakeSolidAt(ref origin);
        MakeNonSolidAt(ref origin);
    }

    private void MakeSolidAt(ref HashSet<GlobalPoint> origin)
    {
        var seekQueue = new Queue<GlobalPoint>();
        var fillQueue = new Queue<GlobalPoint>();
        var visited   = new HashSet<GlobalPoint>();

        foreach (var o in origin)
        {
            if (o.IsSolid() && visited.Add(o))
                seekQueue.Enqueue(o);
        }

        // Seek through space starting at the origin point, isolating the 
        // subspace where the luminance has changed due to this update.
        while (seekQueue.Count > 0)
        {
            // A boundary point is the farthest point that was modified due to
            // this update. These are the points where the filling will begin.
            bool boundary = false;

            var seek = seekQueue.Dequeue();
            var seekDepth = seek.GetDepth();

            // If this was previously a daylight cell, all cells below must be 
            // updated.
            if (seekDepth == MIN_DEPTH 
            && (!seek.IsSolid() || origin.Contains(seek)))
            {
                var below = seek.Step(Direction.BELOW);
                if (below != null && visited.Add(below))
                    seekQueue.Enqueue(below);
            }

            // If neighbour.depth == depth + 1, that cell should be updated.
            if (!seek.IsSolid() || origin.Contains(seek))
            {
                for (int n = 0; n < 6; n++)
                {
                    var dir = (Direction)n;
                    var neighbour = seek.Step(dir);
                    if (neighbour == null) continue;

                    var neighbourDepth = neighbour.GetDepth();
                    if (neighbourDepth == seekDepth + 1)
                    {
                        if (visited.Add(neighbour))
                        {
                            seekQueue.Enqueue(neighbour);
                            continue;
                        }
                    }

                    boundary |= !neighbour.IsSolid()
                        && neighbourDepth < MAX_DEPTH
                        && neighbourDepth <= seekDepth
                        && !visited.Contains(neighbour);
                }
            }

            seek.SetDepth(MAX_DEPTH);

            if (boundary || seek.IsSolid())
                fillQueue.Enqueue(seek);
        }

        // Do a second breadth-first search, this time starting at the boundary 
        // points.
        visited.Clear();

        while (fillQueue.Count > 0)
        {
            var fill = fillQueue.Dequeue();

            // If there is daylight above the point, it should also be in 
            // daylight.
            var above = fill.Step(Direction.ABOVE);
            if (!above.IsSolid() && above.GetDepth() == MIN_DEPTH)
            {
                fill.SetDepth(MIN_DEPTH);
            }
            else
            {
                byte minDepth = MAX_DEPTH;
                for (int n = 0; n < 6; n++)
                {
                    var dir = (Direction)n;
                    var neighbour = fill.Step(dir);
                    if (neighbour == null) continue;

                    var neighbourDepth = neighbour.GetDepth();
                    if (!neighbour.IsSolid() && neighbourDepth < minDepth)
                        minDepth = (byte) (neighbourDepth + 1);
                }

                fill.SetDepth(minDepth);
            }

            if (!fill.IsSolid())
            {
                var fillDepth = fill.GetDepth();
                for (int n = 0; n < 6; n++)
                {
                    var dir = (Direction)n;
                    var neighbour = fill.Step(dir);
                    if (neighbour == null) continue;

                    var neighbourDepth = neighbour.GetDepth();
                    if (neighbourDepth > fillDepth + 1)
                    {
                        if (visited.Add(neighbour)) 
                            fillQueue.Enqueue(neighbour);
                    }
                }
            }
        }
    }

    private void MakeNonSolidAt(ref HashSet<GlobalPoint> origin)
    {
        var queue   = new Queue<GlobalPoint>();
        var visited = new HashSet<GlobalPoint>();

        foreach (var o in origin)
        {
            if (o.IsSolid()) continue;

            if (visited.Add(o)) 
                queue.Enqueue(o);

            for (int n = 0; n < 6; n++)
            {
                var dir = (Direction)n;
                var neighbour = o.Step(dir);
                if (neighbour == null) continue;

                if (visited.Add(neighbour))
                    queue.Enqueue(neighbour);
            }
        }

        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            var atDepth = at.GetDepth();

            byte minDepth;
            if (IsDaylight(at))
            {
                minDepth = 0xff; // -1
            }
            else
            {
                minDepth = MAX_DEPTH - 1;

                for (int n = 0; n < 6; n++)
                {
                    var dir = (Direction)n;
                    var neighbour = at.Step(dir);
                    if (neighbour == null) continue;

                    var neighbourDepth = neighbour.GetDepth();
                    if (!neighbour.IsSolid() && neighbourDepth < minDepth)
                    {
                        minDepth = neighbourDepth;
                        if (minDepth == 0) break;
                    }
                }

                if (atDepth <= minDepth)
                {
                    minDepth = MAX_DEPTH - 1;
                }
            }

            if (atDepth != minDepth + 1)
            {
                at.SetDepth((byte) (minDepth + 1));

                for (int n = 0; n < 6; n++)
                {
                    var dir = (Direction)n;
                    var neighbour = at.Step(dir);
                    if (neighbour == null) continue;

                    if (visited.Add(neighbour))
                        queue.Enqueue(neighbour);
                }
            }
        }
    }

    private bool IsDaylight(GlobalPoint point)
    {
        var above = point.Step(Direction.ABOVE);
        if (above == null) return true;
        return !above.IsSolid() 
            && above.GetDepth() == MIN_DEPTH;
    }

    private void FillDepth(byte newDepth)
    {
        for (int i = 0; i < Point.CUBE_SIZE; i++)
            depths[i] = newDepth;
    }
}