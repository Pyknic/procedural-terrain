using System;
using Godot;

public class DensityCube
{
    /**
     * There are 16 cells in each x-row of a block. That means that there are
     * 17 density values since each corner has a density shared between the
     * neighbouring cells. The 256 cells in one X-Z-plane has 289 corners.
     */
    public static readonly int

        CELLS_ROW   = 16,
        CELLS_PLANE = CELLS_ROW * CELLS_ROW,
        CELLS_CUBE  = CELLS_PLANE * CELLS_ROW,

        DENSITY_ROW   = CELLS_ROW + 1,
        DENSITY_PLANE = DENSITY_ROW * DENSITY_ROW,
        DENSITY_CUBE  = DENSITY_PLANE * DENSITY_ROW;

    private static readonly Vector3 OFFSET = new Vector3(
        -CELLS_ROW / 2.0f,
        -CELLS_ROW / 2.0f,
        -CELLS_ROW / 2.0f
    );

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

    private float[] density;

    public DensityCube()
    {
        density = new float[DENSITY_CUBE];
    }

    public float GetDensity(int cellIdx, Corner corner)
    {
        return density[CellToDensityIdx(cellIdx, corner)];
    }

    public void SetDensity(int cellIdx, Corner corner, float value)
    {
        density[CellToDensityIdx(cellIdx, corner)] = value;
    }

    public bool AddDensity(Func<Vector3, float> localPosToDensity)
    {
        bool changed = false;
        Vector3 pos = new Vector3();
        for (int i = 0; i < DENSITY_CUBE; i++)
        {
            pos.x = OFFSET.x + i % DENSITY_ROW - 0.5f;
            pos.y = OFFSET.x + i / DENSITY_PLANE - 0.5f;
            pos.z = OFFSET.x + i / DENSITY_ROW % DENSITY_ROW - 0.5f;
            float amount = localPosToDensity(pos);
            if (Mathf.Abs(amount) > Mathf.Epsilon)
            {
                changed = true;
                density[i] += amount;
                if (density[i] < -1.0f) density[i] = -1.0f;
                if (density[i] >  1.0f) density[i] =  1.0f;
            }
        }
        return changed;
    }

    public static Vector3 CellCenter(int cellIdx)
    {
        return new Vector3(
            cellIdx % CELLS_ROW,
            cellIdx / CELLS_PLANE,
            cellIdx / CELLS_ROW % CELLS_ROW
        ) + OFFSET;
    }

    public static Vector3 CornerPosition(int cellIdx, Corner corner)
    {
        return CellCenter(cellIdx) + CornerPosition(corner);
    }

    public static Vector3 CornerPosition(Corner corner)
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

    public static int CellToDensityIdx(int cellIdx, Corner corner)
    {
        int cellX = cellIdx % CELLS_ROW;
        int cellY = cellIdx / CELLS_PLANE;
        int cellZ = cellIdx / CELLS_ROW % CELLS_ROW;

        if (IsCornerRight(corner)) cellX += 1;
        if (IsCornerTop(corner))   cellY += 1;
        if (IsCornerFar(corner))   cellZ += 1;

        return cellX + cellY * DENSITY_PLANE + cellZ * DENSITY_ROW;
    }

    private static bool IsCornerLeft(Corner corner)
    {
        return (((int)corner + 1) % 4) < 2;
    }

    private static bool IsCornerRight(Corner corner)
    {
        return (((int)corner + 1) % 4) >= 2;
    }

    private static bool IsCornerBottom(Corner corner)
    {
        return ((int)corner) < 4;
    }

    private static bool IsCornerTop(Corner corner)
    {
        return ((int)corner) >= 4;
    }

    private static bool IsCornerNear(Corner corner)
    {
        return (((int)corner) % 4) < 2;
    }

    private static bool IsCornerFar(Corner corner)
    {
        return (((int)corner) % 4) >= 2;
    }

    public static Corner MirrorX(Corner corner)
    {
        int cornerId = (int)corner;
        return (Corner) (cornerId / 4 * 4 + (cornerId + 1) % 2);
    }

    public static Corner MirrorY(Corner corner)
    {
        int cornerId = (int)corner;
        return (Corner)((cornerId + 4) % 8);
    }

    public static Corner MirrorZ(Corner corner)
    {
        int cornerId = (int)corner;
        return (Corner)(3 - (cornerId % 4) + cornerId / 4 * 4);
    }
}