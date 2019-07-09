using Godot;

public struct Cell
{
    public enum Corner
    {
        LEFT_BOTTOM_NEAR,
        RIGHT_BOTTOM_NEAR,
        RIGHT_BOTTOM_FAR,
        LEFT_BOTTOM_FAR,

        LEFT_TOP_NEAR,
        RIGHT_TOP_NEAR,
        RIGHT_TOP_FAR,
        LEFT_TOP_FAR
    }

    public const int
        ROW_SIZE   = 7,
        PLANE_SIZE = ROW_SIZE * ROW_SIZE,
        CUBE_SIZE  = PLANE_SIZE * ROW_SIZE;

    public readonly int x, y, z;

    public Cell(int index)
    {
        x = index % ROW_SIZE;
        y = index / PLANE_SIZE;
        z = index / ROW_SIZE % ROW_SIZE;
    }

    public Cell(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public int Index()
    {
        return x + y * PLANE_SIZE + z * ROW_SIZE;
    }

    public Point GetPoint(Corner corner)
    {
        int x = this.x;
        int y = this.y;
        int z = this.z;

        if (IsCornerEast(corner))  x += 1;
        if (IsCornerAbove(corner)) y += 1;
        if (IsCornerNorth(corner)) z += 1;

        return new Point(x, y, z);
    }

    public Vector3 LocalPosition()
    {
        const float OFFSET = ROW_SIZE / 2f - 0.5f;
        return new Vector3(
            x - OFFSET,
            y - OFFSET,
            z - OFFSET
        );
    }

    public Vector3 WorldPosition(Vector3 blockPosition)
    {
        return blockPosition + LocalPosition();
    }

    public override int GetHashCode()
    {
        return Index();
    }

    public override bool Equals(object obj)
    {
        if (obj is Cell cell)
            return x == cell.x
                && y == cell.y
                && z == cell.z;
        return false;
    }

    public override string ToString()
    {
        return string.Format("({0}, {1}, {2})", x, y, z);
    }

    private static bool IsCornerWest(Corner corner)
    {
        return (((int)corner + 1) % 4) < 2;
    }

    private static bool IsCornerEast(Corner corner)
    {
        return (((int)corner + 1) % 4) >= 2;
    }

    private static bool IsCornerBelow(Corner corner)
    {
        return ((int)corner) < 4;
    }

    private static bool IsCornerAbove(Corner corner)
    {
        return ((int)corner) >= 4;
    }

    private static bool IsCornerSouth(Corner corner)
    {
        return (((int)corner) % 4) < 2;
    }

    private static bool IsCornerNorth(Corner corner)
    {
        return (((int)corner) % 4) >= 2;
    }
}