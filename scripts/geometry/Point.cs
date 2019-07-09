using Godot;

public struct Point
{
    public const int
        ROW_SIZE = Cell.ROW_SIZE + 1,
        PLANE_SIZE = ROW_SIZE * ROW_SIZE,
        CUBE_SIZE = PLANE_SIZE * ROW_SIZE,
        FIRST = 0, LAST = ROW_SIZE - 1;

    public readonly int x, y, z, index;

    public Point(int idx)
    {
        x = idx % ROW_SIZE;
        y = idx / PLANE_SIZE;
        z = idx / ROW_SIZE % ROW_SIZE;
        index = idx;
    }

    public Point(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        index = x + y * PLANE_SIZE + z * ROW_SIZE;
    }

    public bool IsInside()
    {
        return x >= 0 && y >= 0 && z >= 0 && index < CUBE_SIZE;
    }

    public float SampleFrom(ref float[] cube)
    {
        return cube[index];
    }

    public byte SampleFrom(ref byte[] cube)
    {
        return cube[index];
    }

    public Vector3 LocalPosition()
    {
        const float OFFSET = ROW_SIZE / 2.0f;
        return new Vector3(
            x - OFFSET,
            y - OFFSET,
            z - OFFSET
        );
    }

    public Vector3 WorldPosition(Vector3 chunkPosition)
    {
        return chunkPosition + LocalPosition();
    }

    public int NeighbourMask()
    {
        const int SELF_MASK = 0x1 << (int)Direction.SELF;
        int mask = SELF_MASK;
        if (x == FIRST)     mask |= 0x1 << (int)Direction.WEST;
        else if (x == LAST) mask |= 0x1 << (int)Direction.EAST;
        if (y == FIRST)     mask |= 0x1 << (int)Direction.BELOW;
        else if (y == LAST) mask |= 0x1 << (int)Direction.ABOVE;
        if (z == FIRST)     mask |= 0x1 << (int)Direction.NORTH;
        else if (z == LAST) mask |= 0x1 << (int)Direction.SOUTH;
        return mask;
    }

    public override int GetHashCode()
    {
        return index;
    }

    public override bool Equals(object obj)
    {
        if (obj is Point point)
            return x == point.x
                && y == point.y
                && z == point.z;
        return false;
    }

    public override string ToString()
    {
        return string.Format("({0}, {1}, {2})", x, y, z);
    }
}
