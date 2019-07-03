public struct BlockCoord
{
    public int x, y, z;
    
    public BlockCoord(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
    
    public static BlockCoord operator+ (BlockCoord b, BlockCoord c) {
        return new BlockCoord
        {
            x = b.x + c.x, 
            y = b.y + c.y, 
            z = b.z + c.z
        };
    }
}