using System;
using System.Collections.Generic;
using Godot;

public class Chunk
{
    public const int
        CELLS_ROW   = 8,
        CELLS_PLANE = CELLS_ROW * CELLS_ROW,
        CELLS_CHUNK = CELLS_PLANE * CELLS_ROW,

        CORNER_ROW   = CELLS_ROW + 1,
        CORNER_PLANE = CORNER_ROW * CORNER_ROW,
        CORNER_CHUNK = CORNER_PLANE * CORNER_ROW,

        PROXY_ROW   = CORNER_ROW + 2,
        PROXY_PLANE = PROXY_ROW * PROXY_ROW,
        PROXY_CHUNK = PROXY_PLANE * PROXY_ROW,

        FIRST       = 0,               /// <summary>The first density/light belonging to this chunk</summary>
        LAST        = CORNER_ROW - 1,  /// <summary>The last density/light belonging to this chunk</summary>
        PROXY_FIRST = -1,              /// <summary>The last density/light in the previous chunk</summary>
        PROXY_LAST  = CORNER_ROW;      /// <summary>The first density/light in the next chunk</summary>

    public enum Direction
    {
        WEST,
        EAST,
        BELOW,
        ABOVE,
        NORTH,
        SOUTH,
        SELF
    }

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

    public struct ChunkPos
    {
        public readonly int x, y, z;

        public ChunkPos(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static ChunkPos operator+(ChunkPos left, ChunkPos right) {
            return new ChunkPos(
                left.x + right.x,
                left.y + right.y,
                left.z + right.z
            );
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
            if (!(obj is ChunkPos)) return false;
            var chunkPos = (ChunkPos) obj;
            return x == chunkPos.x 
                && y == chunkPos.y 
                && z == chunkPos.z;
        }

        public override string ToString()
        {
            return string.Format("[" + x + ", " + y + ", " + z + "]");
        }
    }

    public class LightKernel
    {
        public float[] lights;

        public LightKernel()
        {
            lights = new float[PROXY_CHUNK];
        }
    }

    private readonly ChunkPos position;
    private Chunk[] neighbours;
    private float[] densities;
    private float[] lights;

    private bool densityDirty;
    private bool lightDirty;

    public Chunk(ChunkPos pos)
    {
        position   = pos;
        neighbours = new Chunk[6];
        densities  = new float[PROXY_CHUNK]; // Store one extra value in each direction
        lights     = new float[PROXY_CHUNK];
    }

    public ChunkPos Position()
    {
        return position;
    }

    public void AddNeighbour(Direction direction, Chunk chunk)
    {
        neighbours[(int) direction] = chunk;
    }

    public void RemoveNeighbour(Direction direction, Chunk chunk)
    {
        neighbours[(int)direction] = null;
    }

    public bool HasNeighbour(Direction direction)
    {
        return neighbours[(int)direction] != null;
    }

    public bool PollDensityDirty()
    {
        if (densityDirty)
        {
            densityDirty = false;
            lightDirty   = false;
            return true;
        }

        return false;
    }

    public bool PollLightDirty()
    {
        if (lightDirty)
        {
            lightDirty = false;
            return true;
        }

        return false;
    }

    public void InitDensity(Func<Vector3, float> initializer)
    {
        for (int y = FIRST; y <= LAST; y++)
        {
            for (int z = FIRST; z <= LAST; z++)
            {
                for (int x = FIRST; x <= LAST; x++)
                {
                    var cornerIdx = CornerIdx(x, y, z);
                    var cornerPos = WorldPositionOfCorner(x, y, z);
                    var newDensity = LimitDensity(initializer(cornerPos));
                    densities[cornerIdx] = newDensity;
                }
            }
        }

        densityDirty = true;
        lightDirty = true;
    }

    public void InitLights()
    {
        const int POINT_NEIGHBOURHOOD = 3 * 3 * 3;
        const float SQRT3 = 1.7320508075688772f;

        for (int y = FIRST; y <= LAST; y++)
        {
            for (int z = FIRST; z <= LAST; z++)
            {
                for (int x = FIRST; x <= LAST; x++)
                {
                    float part  = 0.0f;
                    float total = 0.0f;

                    for (int i = 0; i < POINT_NEIGHBOURHOOD; i++)
                    {
                        var ix = x + (i % 3 - 1);
                        var iy = y + (i / 9 - 1);
                        var iz = z + (i / 3 % 3 - 1);

                        if (ix < FIRST || ix > LAST
                        || iy < FIRST || iy > LAST
                        || iz < FIRST || iz > LAST) continue;

                        switch (i)
                        {
                            // Skip the center
                            case 13: continue;

                            // These are at a distance 1 from the center
                            case 4:
                            case 10:
                            case 12:
                            case 14:
                            case 16:
                            case 22:
                                part  += (1.0f - GetDensity(ix, iy, iz));
                                total += 1.0f;
                                break;

                            // These are at a distance sqrt(3) from the center
                            default:
                                part += (1.0f - GetDensity(ix, iy, iz)) / SQRT3;
                                total += 1.0f / SQRT3;
                                break;
                        }
                    }

                    SetLight(x, y, z, part / total);
                }
            }
        }
    }

    public void InitLights(Func<Vector3, float> initializer)
    {
        for (int y = FIRST; y <= LAST; y++)
        {
            for (int z = FIRST; z <= LAST; z++)
            {
                for (int x = FIRST; x <= LAST; x++)
                {
                    var cornerIdx = CornerIdx(x, y, z);
                    var cornerPos = WorldPositionOfCorner(x, y, z);
                    var newLight  = initializer(cornerPos);
                    lights[cornerIdx] = newLight;
                }
            }
        }

        lightDirty = true;
    }

    public Vector3 WorldPosition()
    {
        return new Vector3(
            position.x * CELLS_ROW,
            position.y * CELLS_ROW,
            position.z * CELLS_ROW
        );
    }

    private Vector3 WorldPositionOfCorner(int cornerX, int cornerY, int cornerZ)
    {
        return new Vector3(
            (position.x - 0.5f) * CELLS_ROW + cornerX,
            (position.y - 0.5f) * CELLS_ROW + cornerY,
            (position.z - 0.5f) * CELLS_ROW + cornerZ
        );
    }

    public float GetBounds(Direction direction)
    {
        switch (direction)
        {
            case Direction.WEST:  return (position.x - 0.5f) * CELLS_ROW;
            case Direction.EAST:  return (position.x + 0.5f) * CELLS_ROW;
            case Direction.BELOW: return (position.y - 0.5f) * CELLS_ROW;
            case Direction.ABOVE: return (position.y + 0.5f) * CELLS_ROW;
            case Direction.NORTH: return (position.z - 0.5f) * CELLS_ROW;
            case Direction.SOUTH: return (position.z + 0.5f) * CELLS_ROW;
            default: throw new ArgumentException("Not a valid direction: " + direction);
        }
    }

    public string GetInfo(Vector3 corner)
    {
        int x = (int)(corner.x - (position.x - 0.5f) * CELLS_ROW);
        int y = (int)(corner.y - (position.y - 0.5f) * CELLS_ROW);
        int z = (int)(corner.z - (position.z - 0.5f) * CELLS_ROW);
        if (x < FIRST || x > LAST
        ||  y < FIRST || y > LAST
        ||  z < FIRST || z > LAST) return "(No info available)";
        int idx = CornerIdx(x, y, z);
        float density = densities[idx];
        float light   = lights[idx];
        return "Density: " + density + ", Light: " + light;
    }

    public float GetLight(int cellIdx, Corner corner)
    {
        return lights[CellToCornerIdx(cellIdx, corner)];
    }

    private float GetLight(int cornerX, int cornerY, int cornerZ)
    {
        return lights[CornerIdx(cornerX, cornerY, cornerZ)];
    }

    public float SetLight(int cellIdx, Corner corner)
    {
        return lights[CellToCornerIdx(cellIdx, corner)];
    }

    private bool SetLight(int cornerX, int cornerY, int cornerZ, float light)
    {
        const float LIGHT_UPDATE_THRESHOLD = 0.01f;

        var idx = CornerIdx(cornerX, cornerY, cornerZ);
        if (Mathf.Abs(lights[idx] - light) > LIGHT_UPDATE_THRESHOLD)
        {
            lights[idx] = light;
            return true;
        }
        return false;
    }

    public float GetDensity(int cellIdx, Corner corner)
    {
        return densities[CellToCornerIdx(cellIdx, corner)];
    }

    private float GetDensity(int cornerX, int cornerY, int cornerZ)
    {
        return densities[CornerIdx(cornerX, cornerY, cornerZ)];
    }

    private bool SetDensity(int cornerX, int cornerY, int cornerZ, float density)
    {
        const float DENSITY_UPDATE_THRESHOLD = 0.01f;

        var idx = CornerIdx(cornerX, cornerY, cornerZ);
        if (Mathf.Abs(densities[idx] - density) > DENSITY_UPDATE_THRESHOLD)
        {
            densities[idx] = density;
            return true;
        }
        return false;
    }

    public void SetDensity(int cellIdx, Corner corner, float value)
    {
        densities[CellToCornerIdx(cellIdx, corner)] = value;
    }

    public void EditDensity(Func<Vector3, float, float> editor)
    {
        // Do a breadth-first search through the graph for chunks that changed
        // as a result of applying the editor.
        var queue   = new Queue<Chunk>();
        var visited = new Dictionary<ChunkPos, Chunk>();

        queue.Enqueue(this);
        visited.Add(position, this);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var neighbourMask = current.EditDensityLocally(editor);

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
                        else
                        {
                            //GD.Print("Affected neighbour " + n + " at " + neighbour.position + " has already been visited.");
                        }
                    }
                    else
                    {
                        //GD.Print("Affected neighbour " + n + " was null.");
                    }
                }
            }
        }

        //GD.Print("Updated density in " + visited.Count + " chunks.");

        foreach (var v in visited.Values)
            queue.Enqueue(v);

        UpdateLights(ref queue, ref visited);
    }

    private int EditDensityLocally(Func<Vector3, float, float> editor)
    {
        const float EDIT_THRESHOLD = 0.01f;

        int neighbourMask = 0;
        for (int y = FIRST; y <= LAST; y++)
        {
            for (int z = FIRST; z <= LAST; z++)
            {
                for (int x = FIRST; x <= LAST; x++)
                {
                    var cornerIdx = CornerIdx(x, y, z);
                    var cornerPos = WorldPositionOfCorner(x, y, z);
                    var oldDensity = densities[cornerIdx];
                    var newDensity = LimitDensity(editor(cornerPos, oldDensity));

                    if (Mathf.Abs(oldDensity - newDensity) > EDIT_THRESHOLD)
                    {
                        densities[cornerIdx] = newDensity;
                        densityDirty = true;
                        lightDirty   = true;

                        if (x == FIRST)     neighbourMask |= 0x1 << (int)Direction.WEST;
                        else if (x == LAST) neighbourMask |= 0x1 << (int)Direction.EAST;
                        if (y == FIRST)     neighbourMask |= 0x1 << (int)Direction.BELOW;
                        else if (y == LAST) neighbourMask |= 0x1 << (int)Direction.ABOVE;
                        if (z == FIRST)     neighbourMask |= 0x1 << (int)Direction.NORTH;
                        else if (z == LAST) neighbourMask |= 0x1 << (int)Direction.SOUTH;
                    }
                }
            }
        }

        //string bitset = "[";
        //if (((0x1 << (int)Direction.WEST) & neighbourMask) != 0)
        //{
        //    if (bitset.Length > 1) bitset += ", ";
        //    bitset += "WEST";
        //}

        //if (((0x1 << (int)Direction.EAST) & neighbourMask) != 0)
        //{
        //    if (bitset.Length > 1) bitset += ", ";
        //    bitset += "EAST";
        //}

        //if (((0x1 << (int)Direction.BELOW) & neighbourMask) != 0)
        //{
        //    if (bitset.Length > 1) bitset += ", ";
        //    bitset += "BELOW";
        //}

        //if (((0x1 << (int)Direction.ABOVE) & neighbourMask) != 0)
        //{
        //    if (bitset.Length > 1) bitset += ", ";
        //    bitset += "ABOVE";
        //}

        //if (((0x1 << (int)Direction.NORTH) & neighbourMask) != 0)
        //{
        //    if (bitset.Length > 1) bitset += ", ";
        //    bitset += "NORTH";
        //}

        //if (((0x1 << (int)Direction.SOUTH) & neighbourMask) != 0)
        //{
        //    if (bitset.Length > 1) bitset += ", ";
        //    bitset += "SOUTH";
        //}

        //bitset += "]";

        //GD.Print("Updating locally: " + position.x + ", " + position.y + ", " + position.z + ": " + bitset);
        return neighbourMask;
    }

    /// <summary>
    /// Updates the lights of this chunk and all connected chunks by searching
    /// through the graph, traversing it in the direction that reports changes
    /// to the lighting in breadth-first order.
    /// </summary>
    public void UpdateLights()
    {
        var queue   = new Queue<Chunk>();
        var visited = new Dictionary<ChunkPos, Chunk>();

        queue.Enqueue(this);
        visited.Add(position, this);

        UpdateLights(ref queue, ref visited);
    }

    private static void UpdateLights(ref Queue<Chunk> queue, ref Dictionary<ChunkPos, Chunk> visited)
    {
        var kernel = new LightKernel();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            var mask = current.UpdateLightsLocally(ref kernel);
            for (int n = 0; n < 6; n++) { 
                if ((mask & (0x1 << n)) != 0)
                {
                    var neighbour = current.neighbours[n];
                    if (neighbour != null)
                    {
                        if (!visited.ContainsKey(neighbour.position))
                        {
                            visited.Add(neighbour.position, neighbour);
                            queue.Enqueue(neighbour);
                        }
                    }
                }
            }

            if ((mask & (0x1 << (int) Direction.SELF)) != 0)
            {
                visited.Remove(current.position);
                queue.Enqueue(current);
            }
        }
    }

    private int UpdateLightsLocally(ref LightKernel kernel)
    {
        // Update the proxy light values from our neighbours
        var west = neighbours[(int)Direction.WEST];
        if (west != null)
            for (int y = FIRST; y <= LAST; y++)
                for (int z = FIRST; z <= LAST; z++)
                    UpdateProxy(
                        PROXY_FIRST, y, z,
                        LAST, y, z,
                        ref west.densities,
                        ref west.lights);

        var east = neighbours[(int)Direction.EAST];
        if (east != null)
            for (int y = FIRST; y <= LAST; y++)
                for (int z = FIRST; z <= LAST; z++)
                    UpdateProxy(
                        PROXY_LAST, y, z,
                        FIRST, y, z,
                        ref east.densities,
                        ref east.lights);

        var north = neighbours[(int)Direction.NORTH];
        if (north != null)
            for (int y = FIRST; y <= LAST; y++)
                for (int x = FIRST; x <= LAST; x++)
                    UpdateProxy(
                        x, y, PROXY_FIRST,
                        x, y, LAST,
                        ref north.densities,
                        ref north.lights);

        var south = neighbours[(int)Direction.SOUTH];
        if (south != null)
            for (int y = FIRST; y <= LAST; y++)
                for (int x = FIRST; x <= LAST; x++)
                    UpdateProxy(
                        x, y, PROXY_LAST,
                        x, y, FIRST,
                        ref south.densities,
                        ref south.lights);

        var below = neighbours[(int)Direction.BELOW];
        if (below != null)
            for (int z = FIRST; z <= LAST; z++)
                for (int x = FIRST; x <= LAST; x++)
                    UpdateProxy(
                        x, PROXY_FIRST, z,
                        x, LAST, z,
                        ref below.densities,
                        ref below.lights);

        var above = neighbours[(int)Direction.ABOVE];
        if (above != null)
            for (int z = FIRST; z <= LAST; z++)
                for (int x = FIRST; x <= LAST; x++)
                    UpdateProxy(
                        x, PROXY_LAST, z,
                        x, FIRST, z,
                        ref above.densities,
                        ref above.lights);


        // Compute the mean (luminance * (1 - density)) / distance for each 
        // point and store the values in the kernel.
        const int POINT_NEIGHBOURHOOD = 3 * 3 * 3;
        const float SQRT3 = 1.7320508075688772f;

        for (int y = FIRST; y <= LAST; y++)
        {
            for (int z = FIRST; z <= LAST; z++)
            {
                for (int x = FIRST; x <= LAST; x++)
                {
                    float occluded = 0.0f;
                    float total = 0.0f;
                    for (int i = 0; i < POINT_NEIGHBOURHOOD; i++)
                    {
                        var ix = x + (i % 3 - 1);
                        var iy = y + (i / 9 - 1);
                        var iz = z + (i / 3 % 3 - 1);

                        if (ix < FIRST || ix > LAST
                        || iy < FIRST || iy > LAST
                        || iz < FIRST || iz > LAST) continue;

                        switch (i)
                        {
                            // Skip the center
                            case 13: continue;

                            // These are at a distance 1 from the center
                            case 4:
                            case 10:
                            case 12:
                            case 14:
                            case 16:
                            case 22:
                                if (GetDensity(ix, iy, iz) <= 0.5f)
                                    occluded += GetLight(ix, iy, iz);
                                else
                                    occluded += GetLight(ix, iy, iz) * Mathf.Clamp((1.0f - GetDensity(ix, iy, iz)) * 2.0f, 0.0f, 1.0f);
                                total += 1.0f;
                                break;

                            // These are at a distance sqrt(3) from the center
                            default:
                                if (GetDensity(ix, iy, iz) <= 0.5f)
                                    occluded += GetLight(ix, iy, iz) / SQRT3;
                                else
                                    occluded += GetLight(ix, iy, iz) * Mathf.Clamp((1.0f - GetDensity(ix, iy, iz)) * 2.0f, 0.0f, 1.0f) / SQRT3;
                                total += 1.0f / SQRT3;
                                break;
                        }
                    }

                    kernel.lights[CornerIdx(x, y, z)] = occluded / total;
                }
            }
        }

        // Go through each point and lookup the next light value in the kernel.
        // If the value changed close to a neighbour, mark it in a bitset.
        int neighbourMask = 0x0;

        const float LIGHT_THRESHOLD = 0.01f;
        const float LIGHT_SKY = 1.0f;

        for (int y = FIRST; y <= LAST; y++)
        {
            for (int z = FIRST; z <= LAST; z++)
            {
                for (int x = FIRST; x <= LAST; x++)
                {
                    var cornerIdx = CornerIdx(x, y, z);
                    var oldLight = lights[cornerIdx];

                    float newLight = kernel.lights[cornerIdx];

                    if (Mathf.Abs(oldLight - newLight) > LIGHT_THRESHOLD)
                    {
                        lights[cornerIdx] = newLight;
                        if (x == FIRST)     neighbourMask |= 0x1 << (int)Direction.WEST;
                        else if (x == LAST) neighbourMask |= 0x1 << (int)Direction.EAST;
                        if (y == FIRST)     neighbourMask |= 0x1 << (int)Direction.BELOW;
                        else if (y == LAST) neighbourMask |= 0x1 << (int)Direction.ABOVE;
                        if (z == FIRST)     neighbourMask |= 0x1 << (int)Direction.NORTH;
                        else if (z == LAST) neighbourMask |= 0x1 << (int)Direction.SOUTH;
                        neighbourMask |= 0x1 << (int) Direction.SELF;
                        lightDirty = true;
                    }
                }
            }
        }

        return neighbourMask;
    }

    private void UpdateProxy(int x, int y, int z, int srcX, int srcY, int srcZ,
                             ref float[] srcDensities, ref float[] srcLights)
    {
        var srcIdx  = CornerIdx(srcX, srcY, srcZ);
        var destIdx = CornerIdx(x, y, z);
        densities[destIdx] = srcDensities[srcIdx];
        lights[destIdx]    = srcLights[srcIdx];
    }


    //public void UpdateLights()
    //{
    //    var visited = new Dictionary<ChunkPos, Chunk>();
    //    visited.Add(position, this);

    //    var nextQueue = new Queue<Chunk>();
    //    nextQueue.Enqueue(this);

    //    for (int y = FIRST;; y++)
    //    {
    //        Queue<Chunk> queue;
    //        if (y % CORNER_ROW == 0)
    //        {
    //            if (nextQueue.Count == 0) break;
    //            queue = nextQueue;
    //            nextQueue = new Queue<Chunk>();
    //        }
    //        else
    //        {
    //            queue = new Queue<Chunk>(visited.Values);
    //        }

    //        visited.Clear();

    //        while (queue.Count > 0)
    //        {
    //            var next = queue.Dequeue();
    //            visited.Add(next.position, next);

    //            if (y / CORNER_ROW == position.y - next.position.y)
    //            {
    //                var changes = next.UpdateLightsInLayer(y % CORNER_ROW);
    //                for (int n = 0; n < 6; n++)
    //                {
    //                    if ((changes & (0x1 << n)) != 0)
    //                    {
    //                        switch ((Direction) n)
    //                        {
    //                            case Direction.ABOVE: continue;
    //                            case Direction.BELOW:
    //                            {
    //                                if ((y + 1) % CORNER_ROW == 0)
    //                                {
    //                                    var neighbour = neighbours[n];
    //                                    if (neighbour != null)
    //                                    {
    //                                        var neighbourPos = neighbour.position;
    //                                        nextQueue.Enqueue(neighbour);
    //                                    }
    //                                }
    //                                break;
    //                            }
    //                            default:
    //                            {
    //                                var neighbour = neighbours[n];
    //                                if (neighbour != null)
    //                                {
    //                                    var neighbourPos = neighbour.position;
    //                                    if (!visited.ContainsKey(neighbourPos))
    //                                    {
    //                                        visited.Add(neighbourPos, neighbour);
    //                                        queue.Enqueue(neighbour);
    //                                    }
    //                                }
    //                                break;
    //                            }
    //                        }
    //                    }
    //                }
    //            }
    //        }
    //    }
    //}

    ///// <summary>
    ///// Updates the lights on the specified layer. This will query the 
    ///// neighbouring chunks for the current light and density values on the 
    ///// layer above this, so those are assumed to have already been updated.
    ///// </summary>
    ///// <param name="layerY">
    /////     The layer to update (starting with 0 for the top-most layer and 
    /////     going down to and including <c>LIGHT_ROW - 1</c>.
    ///// </param>
    ///// <returns>
    /////     Bitmask of the neighbours that are affected by the update where the 
    /////     least significant bit indicates the 1:st neighbour (WEST) and the
    /////     6:th bit indicates the last neighbour (SOUTH).
    ///// </returns>
    //private int UpdateLightsInLayer(int layerY)
    //{
    //    var layerAbove = layerY - 1;

    //    // Update the frame around the layer above to make sure all those values
    //    // have been updated.

    //    var west = neighbours[(int)Direction.WEST];
    //    if (west != null)
    //    {
    //        for (int z = FIRST; z <= LAST; z++)
    //        {
    //            SetDensity(PROXY_FIRST, layerAbove, z,
    //                west.GetDensity(LAST, layerAbove, z));

    //            SetLight(PROXY_FIRST, layerAbove, z,
    //                west.GetLight(LAST, layerAbove, z));
    //        }
    //    }

    //    var east = neighbours[(int)Direction.EAST];
    //    if (east != null)
    //    {
    //        for (int z = FIRST; z <= LAST; z++)
    //        {
    //            SetDensity(PROXY_LAST, layerAbove, z,
    //                east.GetDensity(FIRST, layerAbove, z));

    //            SetLight(PROXY_LAST, layerAbove, z,
    //                east.GetLight(FIRST, layerAbove, z));
    //        }
    //    }

    //    var north = neighbours[(int)Direction.NORTH];
    //    if (north != null)
    //    {
    //        for (int x = FIRST; x <= LAST; x++)
    //        {
    //            SetDensity(x, layerAbove, PROXY_FIRST,
    //                north.GetDensity(x, layerAbove, LAST));

    //            SetLight(x, layerAbove, PROXY_FIRST,
    //                north.GetLight(x, layerAbove, LAST));
    //        }
    //    }

    //    var south = neighbours[(int)Direction.SOUTH];
    //    if (south != null)
    //    {
    //        for (int x = FIRST; x <= LAST; x++)
    //        {
    //            SetDensity(x, layerAbove, PROXY_LAST,
    //                north.GetDensity(x, layerAbove, FIRST));

    //            SetLight(x, layerAbove, PROXY_LAST,
    //                north.GetLight(x, layerAbove, FIRST));
    //        }
    //    }

    //    // If this is the top layer, also update the entire proxy plane above
    //    if (layerY == 0)
    //    {
    //        var above = neighbours[(int)Direction.ABOVE];
    //        if (above == null)
    //        {
    //            for (int z = FIRST; z <= LAST; z++)
    //            {
    //                for (int x = FIRST; x <= LAST; x++)
    //                {
    //                    SetLight(x, PROXY_FIRST, z, 1.0f);
    //                }
    //            }
    //        }
    //        else
    //        {
    //            for (int z = FIRST; z <= LAST; z++)
    //            {
    //                for (int x = FIRST; x <= LAST; x++)
    //                {
    //                    SetLight(x, PROXY_FIRST, z,
    //                        above.GetLight(x, LAST, z));
    //                }
    //            }
    //        }
    //    }

    //    // Compute the mean <c>light*(1-density)</c> of each neighbouring cell 
    //    // on the layer above. This is now the lightness of this cell.
    //    int changes = 0;
    //    for (int z = FIRST; z <= LAST; z++)
    //    {
    //        for (int x = FIRST; x <= LAST; x++)
    //        {
    //            if (SetLight(x, layerY, z, 0.2f * (
    //                GetLightDensity(x - 1, layerAbove, z) +
    //                GetLightDensity(x + 1, layerAbove, z) +
    //                GetLightDensity(x, layerAbove, z - 1) +
    //                GetLightDensity(x, layerAbove, z + 1) +
    //                GetLightDensity(x, layerAbove, z))))
    //            {
    //                if (x == FIRST)     changes |= 0x1 << (int)Direction.WEST;
    //                if (z == FIRST)     changes |= 0x1 << (int)Direction.NORTH;
    //                if (x == LAST)      changes |= 0x1 << (int)Direction.EAST;
    //                if (z == LAST)      changes |= 0x1 << (int)Direction.SOUTH;
    //                if (layerY == LAST) changes |= 0x1 << (int)Direction.BELOW;
    //            }
    //        }
    //    }

    //    return changes;
    //}

    private float GetLightDensity(int cornerX, int cornerY, int cornerZ)
    {
        return (1.0f - GetDensity(cornerX, cornerY, cornerZ)) *
            GetLight(cornerX, cornerY, cornerZ);
    }

    private static float LimitDensity(float density)
    {
        if (density < 0.0f) return 0.0f;
        if (density > 1.0f) return 1.0f;
        return density;
    }

    /// <summary>
    /// Returns the cell center position in local coordinates (relative to the
    /// center of the chunk). The cell index should start at 0 and go to but 
    /// excluding <c>CELLS_ROW</c>.
    /// </summary>
    /// <param name="cellIdx">Cell index</param>
    /// <returns>The cell center position</returns>
    public static Vector3 LocalCellPosition(int cellIdx)
    {
        const float OFFSET = CELLS_ROW * -0.5f + 0.5f;
        return new Vector3(
            OFFSET + cellIdx % CELLS_ROW,
            OFFSET + cellIdx / CELLS_PLANE,                
            OFFSET + cellIdx / CELLS_ROW % CELLS_ROW
        );
    }

    /// <summary>
    /// Returns the position of the specified corner in the specified cell 
    /// relative to the center of the chunk.
    /// </summary>
    /// <param name="cellIdx">Cell index</param>
    /// <param name="corner">Which corner</param>
    /// <returns>The corner position relative to chunk center.</returns>
    public static Vector3 LocalCornerPosition(int cellIdx, Corner corner)
    {
        return LocalCellPosition(cellIdx) + LocalCornerPosition(corner);
    }

    /// <summary>
    /// Returns the position of the specified corner relative to the center of 
    /// <i>that cell</i>.
    /// </summary>
    /// <param name="corner">Which corner</param>
    /// <returns>The corner position relative to cell center.</returns>
    public static Vector3 LocalCornerPosition(Corner corner)
    {
        switch (corner)
        {
            case Corner.LEFT_BOTTOM_NEAR: return new Vector3(-.5f, -.5f, -.5f);
            case Corner.RIGHT_BOTTOM_NEAR: return new Vector3(.5f, -.5f, -.5f);
            case Corner.RIGHT_BOTTOM_FAR: return new Vector3(.5f, -.5f, .5f);
            case Corner.LEFT_BOTTOM_FAR: return new Vector3(-.5f, -.5f, .5f);
            case Corner.LEFT_TOP_NEAR: return new Vector3(-.5f, .5f, -.5f);
            case Corner.RIGHT_TOP_NEAR: return new Vector3(.5f, .5f, -.5f);
            case Corner.RIGHT_TOP_FAR: return new Vector3(.5f, .5f, .5f);
            case Corner.LEFT_TOP_FAR: return new Vector3(-.5f, .5f, .5f);
            default: throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Returns the index of the density at the given density coordinates. 
    /// Coordinates can be as low as -1 and as high as <c>DENSITY_ROW</c> since 
    /// one additional row of densities are stored in each direction.
    /// </summary>
    /// <param name="cornerX">
    ///     The x coordinate (<c>WEST</c> to <c>EAST</c>).
    /// </param>
    /// <param name="cornerY">
    ///     Layer number (-1 to <c>DENSITY_ROW</c>) where -1 is the 
    ///     highest layer and <c>DENSITY_ROW</c> is the bottom layer
    /// </param>
    /// <param name="cornerZ">
    ///     The z coordinate (<c>NORTH</c> to <c>SOUTH</c>).
    /// </param>
    /// <returns>The density index</returns>
    private static int CornerIdx(int cornerX, int cornerY, int cornerZ)
    {
        return (cornerY + 1) * PROXY_PLANE
             + (cornerZ + 1) * PROXY_ROW
             +  cornerX + 1;
    }

    /// <summary>
    /// Converts a cell index to a density/light index by specifying which 
    /// corner of the cell should be accessed.
    /// </summary>
    /// <param name="cellIdx">Cell index.</param>
    /// <param name="corner">Which corner to access.</param>
    /// <returns>The to density/light index.</returns>
    private static int CellToCornerIdx(int cellIdx, Corner corner)
    {
        int x = cellIdx % CELLS_ROW;
        int y = cellIdx / CELLS_PLANE;
        int z = cellIdx / CELLS_ROW % CELLS_ROW;

        if (IsCornerEast(corner))  x += 1;
        if (IsCornerAbove(corner)) y += 1;
        if (IsCornerNorth(corner)) z += 1;

        return CornerIdx(x, y, z);
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

    public void CreateGeometry(GeometryBuffer geometry)
    {
        for (int i = 0; i < CELLS_CHUNK; i++)
            TriangulateVoxel(geometry, i);
    }

    private void TriangulateVoxel(GeometryBuffer geometry, int cellIdx)
    {
        const float SURFACE_LEVEL = 0.5f;

        var cellPos = LocalCellPosition(cellIdx);

        // Build a bitset of the status of the eight corners in the cube
        var voxelType = 0;
        for (int i = 0; i < 8; i++)
        {
            var cornerDensity = GetDensity(cellIdx, (Corner) i);
            if (cornerDensity > SURFACE_LEVEL)
                voxelType |= 0x1 << i;
        }

        var fromTriangleTable = TRIANGLE_TABLE[voxelType];
        if (fromTriangleTable.Length > 0)
        {
            var triangle = new Vector3[3];
            var colors   = new Color[3];

            // Iterate over the triangles in the voxel
            for (var i = 0; i < fromTriangleTable.Length / 3; i++)
            {
                // Compute the vertices of the triangle
                for (var j = 0; j < 3; j++)
                {
                    var edgeIdx = fromTriangleTable[i * 3 + j];
                    var cornerA = (Corner) CORNER_INDEX_A_FROM_EDGE[edgeIdx];
                    var cornerB = (Corner) CORNER_INDEX_B_FROM_EDGE[edgeIdx];

                    var densityA = GetDensity(cellIdx, cornerA);
                    var densityB = GetDensity(cellIdx, cornerB);

                    var lightA = GetLight(cellIdx, cornerA);
                    var lightB = GetLight(cellIdx, cornerB);

                    var cornerPosA = cellPos + LocalCornerPosition(cornerA);
                    var cornerPosB = cellPos + LocalCornerPosition(cornerB);

                    var interpFactor = (SURFACE_LEVEL - densityA) / (densityB - densityA);
                    float light = (lightA + (lightB - lightA) * interpFactor);

                    triangle[2 - j] = cornerPosA + (cornerPosB - cornerPosA) * interpFactor;
                    colors[2 - j] = new Color(light, light, light);
                }

                // Compute the normals of the triangle
                var verticeA = triangle[0];
                var verticeB = triangle[1];
                var verticeC = triangle[2];
                var normal = (verticeC - verticeB).Cross(verticeC - verticeA).Normalized();

                for (var j = 0; j < 3; j++)
                    geometry.Append(triangle[j], normal, colors[j]);
            }
        }
    }

    private static readonly int[] CORNER_INDEX_A_FROM_EDGE = { 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3 };
    private static readonly int[] CORNER_INDEX_B_FROM_EDGE = { 1, 2, 3, 0, 5, 6, 7, 4, 4, 5, 6, 7 };

    //private static readonly int[] CORNER_INDEX_A_FROM_EDGE = { 3, 2, 1, 0, 7, 6, 5, 4, 3, 2, 1, 0 };
    //private static readonly int[] CORNER_INDEX_B_FROM_EDGE = { 7, 6, 5, 4, 4, 7, 6, 5, 0, 3, 2, 1 };

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