using Godot;

public struct ChunkPosition
{
    public readonly int x, y, z;

    public ChunkPosition(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public Vector3 WorldPosition()
    {
        return new Vector3(
            x * Cell.ROW_SIZE, 
            y * Cell.ROW_SIZE,
            z * Cell.ROW_SIZE
        );
    }

    public ChunkPosition Step(Direction direction)
    {
        switch (direction)
        {
            case Direction.WEST:
                return new ChunkPosition(x - 1, y, z);
            case Direction.EAST:
                return new ChunkPosition(x + 1, y, z);
            case Direction.BELOW:
                return new ChunkPosition(x, y - 1, z);
            case Direction.ABOVE:
                return new ChunkPosition(x, y + 1, z);
            case Direction.NORTH:
                return new ChunkPosition(x, y, z - 1);
            case Direction.SOUTH:
                return new ChunkPosition(x, y, z + 1);
            default: throw new System.ArgumentException(
                "Unexpected direction " + direction);
        }
    }

    public override int GetHashCode()
    {
        int hash = 7;
        hash = hash * 31 + x;
        hash = hash * 31 + y;
        hash = hash * 31 + z;
        return hash;
    }

    public override bool Equals(object obj)
    {
        if (obj is ChunkPosition pos)
            return x == pos.x
                && y == pos.y
                && z == pos.z;
        return false;
    }

    public override string ToString()
    {
        return string.Format("({0}, {1}, {2})", x, y, z);
    }
}