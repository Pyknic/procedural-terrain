public class GlobalPoint
{
    public readonly Chunk chunk;
    public readonly Point point;

    public GlobalPoint(Chunk chunk, Point point)
    {
        this.chunk = chunk;
        this.point = point;
    }

    public int X()
    {
        return chunk.Position().x * Cell.ROW_SIZE + point.x;
    }

    public int Y()
    {
        return chunk.Position().y * Cell.ROW_SIZE + point.y;
    }

    public int Z()
    {
        return chunk.Position().z * Cell.ROW_SIZE + point.z;
    }

    public byte GetDepth()
    {
        return chunk.GetLuminance().GetDepth(point);
    }

    public bool IsMaxDepth()
    {
        const byte MAX_DEPTH = 0xff;
        return GetDepth() == MAX_DEPTH;
    }

    public float GetDensity()
    {
        return chunk.GetDensity(point);
    }

    public bool IsSolid()
    {
        return GetDensity() > MarchingCubes.DENSITY_THRESHOLD;
    }

    /// <summary>
    ///     Returns <c>true</c> if a neighbouring point has a density value 
    ///     above the threshold, or if the neighbourhood contains points 
    ///     belonging to other chunks.
    /// </summary>
    /// <returns>
    ///     <c>true</c>, if density in neighbourhood, <c>false</c> otherwise.
    /// </returns>
    public bool HasDensityInNeighbourhood()
    {
        return HasNeighbourWithDensity(Direction.WEST)
            || HasNeighbourWithDensity(Direction.NORTH)
            || HasNeighbourWithDensity(Direction.SOUTH)
            || HasNeighbourWithDensity(Direction.EAST)
            || HasNeighbourWithDensity(Direction.BELOW)
            || HasNeighbourWithDensity(Direction.ABOVE);
    }

    private bool HasNeighbourWithDensity(Direction direction)
    {
        var neighbour = Step(direction);
        return neighbour == null 
            || neighbour.GetDensity() >= MarchingCubes.DENSITY_THRESHOLD;
    }

    public GlobalPoint Step(Direction direction)
    {
        switch (direction)
        {
            case Direction.WEST:
                {
                    if (point.x == Point.FIRST)
                    {
                        var neighbour = chunk.GetNeighbour(direction);
                        if (neighbour == null) return null;
                        return new GlobalPoint(neighbour,
                            new Point(Point.LAST - 1, point.y, point.z));
                    }
                    return new GlobalPoint(chunk, 
                        new Point(point.x - 1, point.y, point.z));
                }
            case Direction.EAST:
                {
                    if (point.x == Point.LAST)
                    {
                        var neighbour = chunk.GetNeighbour(direction);
                        if (neighbour == null) return null;
                        return new GlobalPoint(neighbour,
                            new Point(Point.FIRST + 1, point.y, point.z));
                    }
                    return new GlobalPoint(chunk,
                        new Point(point.x + 1, point.y, point.z));
                }
            case Direction.BELOW:
                {
                    if (point.y == Point.FIRST)
                    {
                        var neighbour = chunk.GetNeighbour(direction);
                        if (neighbour == null) return null;
                        return new GlobalPoint(neighbour,
                            new Point(point.x, Point.LAST - 1, point.z));
                    }
                    return new GlobalPoint(chunk,
                        new Point(point.x, point.y - 1, point.z));
                }
            case Direction.ABOVE:
                {
                    if (point.y == Point.LAST)
                    {
                        var neighbour = chunk.GetNeighbour(direction);
                        if (neighbour == null) return null;
                        return new GlobalPoint(neighbour,
                            new Point(point.x, Point.FIRST + 1, point.z));
                    }
                    return new GlobalPoint(chunk,
                        new Point(point.x, point.y + 1, point.z));
                }
            case Direction.NORTH:
                {
                    if (point.z == Point.FIRST)
                    {
                        var neighbour = chunk.GetNeighbour(direction);
                        if (neighbour == null) return null;
                        return new GlobalPoint(neighbour,
                            new Point(point.x, point.y, Point.LAST - 1));
                    }
                    return new GlobalPoint(chunk,
                        new Point(point.x, point.y, point.z - 1));
                }
            case Direction.SOUTH:
                {
                    if (point.z == Point.LAST)
                    {
                        var neighbour = chunk.GetNeighbour(direction);
                        if (neighbour == null) return null;
                        return new GlobalPoint(neighbour,
                            new Point(point.x, point.y, Point.FIRST + 1));
                    }
                    return new GlobalPoint(chunk,
                        new Point(point.x, point.y, point.z + 1));
                }
            default: 
                throw new System.ArgumentException(
                    "Unexpected direction " + direction);
        }
    }

    /// <summary>
    /// Writes the depth to this point in all loaded chunks that contains it. In
    /// most cases, this is only a single chunk, but points located on the 
    /// boundaries may be shared by multiple chunks.
    /// </summary>
    /// <param name="value">The depth value to set</param>
    public void SetDepth(byte value)
    {
        const int SELF_MASK  = 0x1 << (int)Direction.SELF;
        const int WEST_MASK  = 0x1 << (int)Direction.WEST;
        const int EAST_MASK  = 0x1 << (int)Direction.EAST;
        const int NORTH_MASK = 0x1 << (int)Direction.NORTH;
        const int SOUTH_MASK = 0x1 << (int)Direction.SOUTH;
        const int BELOW_MASK = 0x1 << (int)Direction.BELOW;
        const int ABOVE_MASK = 0x1 << (int)Direction.ABOVE;

        const int BELOW_NORTH_MASK = BELOW_MASK | NORTH_MASK;
        const int ABOVE_NORTH_MASK = ABOVE_MASK | NORTH_MASK;
        const int BELOW_SOUTH_MASK = BELOW_MASK | SOUTH_MASK;
        const int ABOVE_SOUTH_MASK = ABOVE_MASK | SOUTH_MASK;

        chunk.GetLuminance().SetDepth(point, value);

        int neighbourMask = point.NeighbourMask();
        if (neighbourMask == SELF_MASK)
            return;

        if ((neighbourMask & WEST_MASK) != 0)
        {
            var neighbour = chunk.GetNeighbour(Direction.WEST)?.GetLuminance();
            if (neighbour != null)
                neighbour.SetDepth(new Point(Point.LAST, point.y, point.z), value);

            if ((neighbourMask & NORTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.WEST, Direction.NORTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.LAST, point.y, Point.LAST), value);
            } 
            else if ((neighbourMask & SOUTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.WEST, Direction.SOUTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.LAST, point.y, Point.FIRST), value);
            }

            if ((neighbourMask & BELOW_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.WEST, Direction.BELOW)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.LAST, Point.LAST, point.z), value);
            }
            else if ((neighbourMask & ABOVE_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.WEST, Direction.ABOVE)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.LAST, Point.FIRST, point.z), value);
            }

            if ((neighbourMask & BELOW_NORTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.WEST, Direction.BELOW, Direction.NORTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.LAST, Point.LAST, Point.LAST), value);
            }
            else if ((neighbourMask & ABOVE_NORTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.WEST, Direction.ABOVE, Direction.NORTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.LAST, Point.FIRST, Point.LAST), value);
            }
            else if ((neighbourMask & BELOW_SOUTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.WEST, Direction.BELOW, Direction.SOUTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.LAST, Point.LAST, Point.FIRST), value);
            }
            else if ((neighbourMask & ABOVE_SOUTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.WEST, Direction.ABOVE, Direction.SOUTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.LAST, Point.FIRST, Point.FIRST), value);
            }
        }

        if ((neighbourMask & EAST_MASK) != 0)
        {
            var neighbour = chunk.GetNeighbour(Direction.EAST)?.GetLuminance();
            if (neighbour != null)
                neighbour.SetDepth(new Point(Point.FIRST, point.y, point.z), value);

            if ((neighbourMask & NORTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.EAST, Direction.NORTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.FIRST, point.y, Point.LAST), value);
            }
            else if ((neighbourMask & SOUTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.EAST, Direction.SOUTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.FIRST, point.y, Point.FIRST), value);
            }

            if ((neighbourMask & BELOW_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.EAST, Direction.BELOW)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.FIRST, Point.LAST, point.z), value);
            }
            else if ((neighbourMask & ABOVE_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.EAST, Direction.ABOVE)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.FIRST, Point.FIRST, point.z), value);
            }

            if ((neighbourMask & BELOW_NORTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.EAST, Direction.BELOW, Direction.NORTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.FIRST, Point.LAST, Point.LAST), value);
            }
            else if ((neighbourMask & ABOVE_NORTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.EAST, Direction.ABOVE, Direction.NORTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.FIRST, Point.FIRST, Point.LAST), value);
            }
            else if ((neighbourMask & BELOW_SOUTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.EAST, Direction.BELOW, Direction.SOUTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.FIRST, Point.LAST, Point.FIRST), value);
            }
            else if ((neighbourMask & ABOVE_SOUTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.EAST, Direction.ABOVE, Direction.SOUTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(Point.FIRST, Point.FIRST, Point.FIRST), value);
            }
        }

        if ((neighbourMask & BELOW_MASK) != 0)
        {
            var neighbour = chunk.GetNeighbour(Direction.BELOW)?.GetLuminance();
            if (neighbour != null)
                neighbour.SetDepth(new Point(point.x, Point.LAST, point.z), value);

            if ((neighbourMask & NORTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.BELOW, Direction.NORTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(point.x, Point.LAST, Point.LAST), value);
            }
            else if ((neighbourMask & SOUTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.BELOW, Direction.SOUTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(point.x, Point.LAST, Point.FIRST), value);
            }
        }

        if ((neighbourMask & ABOVE_MASK) != 0)
        {
            var neighbour = chunk.GetNeighbour(Direction.ABOVE)?.GetLuminance();
            if (neighbour != null)
                neighbour.SetDepth(new Point(point.x, Point.FIRST, point.z), value);

            if ((neighbourMask & NORTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.ABOVE, Direction.NORTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(point.x, Point.FIRST, Point.LAST), value);
            }
            else if ((neighbourMask & SOUTH_MASK) != 0)
            {
                neighbour = chunk.GetNeighbour(Direction.ABOVE, Direction.SOUTH)?.GetLuminance();
                if (neighbour != null)
                    neighbour.SetDepth(new Point(point.x, Point.FIRST, Point.FIRST), value);
            }
        }

        if ((neighbourMask & NORTH_MASK) != 0)
        {
            var neighbour = chunk.GetNeighbour(Direction.NORTH)?.GetLuminance();
            if (neighbour != null)
                neighbour.SetDepth(new Point(point.x, point.y, Point.LAST), value);
        }

        if ((neighbourMask & SOUTH_MASK) != 0)
        {
            var neighbour = chunk.GetNeighbour(Direction.SOUTH)?.GetLuminance();
            if (neighbour != null)
                neighbour.SetDepth(new Point(point.x, point.y, Point.FIRST), value);
        }
    }

    public override bool Equals(object obj)
    {
        if (obj is GlobalPoint gp)
        {
            return X().Equals(gp.X())
                && Y().Equals(gp.Y())
                && Z().Equals(gp.Z());
        }
        return false;
    }

    public override int GetHashCode()
    {
        int hash = 7;
        hash = hash * 31 + X();
        hash = hash * 31 + Y();
        hash = hash * 31 + Z();
        return hash;
    }

    public override string ToString()
    {
        var chunkPos = chunk.Position();
        return string.Format(
            "[Chunk ({0},{1},{2}), Point ({3},{4},{5})]",
            chunkPos.x, chunkPos.y, chunkPos.z,
            point.x, point.y, point.z
        );
    }
}