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

        CELLS_ROW   = 8,
        CELLS_PLANE = CELLS_ROW * CELLS_ROW,
        CELLS_CUBE  = CELLS_PLANE * CELLS_ROW,

        DENSITY_ROW   = CELLS_ROW + 1,
        DENSITY_PLANE = DENSITY_ROW * DENSITY_ROW,
        DENSITY_CUBE  = DENSITY_PLANE * DENSITY_ROW;

    private static readonly float
        MIN_DENSITY = -0.1f,
        MAX_DENSITY = 0.1f;

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

    private float[] density, luminance;

    public DensityCube()
    {
        density   = new float[DENSITY_CUBE];
        luminance = new float[CELLS_CUBE];
    }

    public float GetDensity(int cellIdx, Corner corner)
    {
        return density[CellToDensityIdx(cellIdx, corner)];
    }

    public void SetDensity(int cellIdx, Corner corner, float value)
    {
        density[CellToDensityIdx(cellIdx, corner)] = value;
    }

    public bool EditDensity(Func<Vector3, float, float> editor)
    {
        bool changed = false;
        Vector3 pos = new Vector3();
        for (int i = 0; i < DENSITY_CUBE; i++)
        {
            pos.x = OFFSET.x + i % DENSITY_ROW - 0.5f;
            pos.y = OFFSET.x + i / DENSITY_PLANE - 0.5f;
            pos.z = OFFSET.x + i / DENSITY_ROW % DENSITY_ROW - 0.5f;
            float newDensity = editor(pos, density[i]);
            if (newDensity < MIN_DENSITY) newDensity = MIN_DENSITY;
            if (newDensity > MAX_DENSITY) newDensity = MAX_DENSITY;
            if (Mathf.Abs(newDensity - density[i]) > Mathf.Epsilon)
            {
                changed = true;
                density[i] = newDensity;
            }
        }
        return changed;
    }

    private static readonly float LIGHT_DECAY = 1.0f;
    private static readonly float OCCLUSION_FACTOR = 1.0f;
    public float[] UpdateLuminance(float[] luminanceAbove)
    {
        for (int i = 0; i < CELLS_PLANE; i++)
        {
            int cellIdx = (CELLS_ROW - 1) * CELLS_PLANE + i;
            float lightSum = 0.0f;
            int lightCount = 0;

            for (int j = 0; j < 9; j++)
            {
                if (i == 4) continue;
                int ix = (i % CELLS_ROW) + (j % 3 - 1);
                int iy = (i / CELLS_ROW) + (j / 3 - 1);
                if (ix >= 0 && ix < CELLS_ROW 
                &&  iy >= 0 && iy < CELLS_ROW)
                {
                    lightSum += luminanceAbove[iy * CELLS_ROW + ix];
                    lightCount++;
                }
            }

            luminance[cellIdx] = lightSum / lightCount * LIGHT_DECAY;
        }

        for (int y = CELLS_ROW - 2; y >= 0; y--)
        {
            for (int i = 0; i < CELLS_PLANE; i++)
            {
                int cellIdx = y * CELLS_PLANE + i;
                float occlusion = 0.0f;
                float lightSum  = 0.0f;
                int lightCount  = 0;

                for (int j = 0; j < 8; j++)
                {
                    var densityIdx = CellToDensityIdx(cellIdx, (Corner)j);
                    var value = density[densityIdx];
                    occlusion += (value - MIN_DENSITY) / (MAX_DENSITY - MIN_DENSITY) / 8;
                }

                for (int j = 0; j < 9; j++)
                {
                    if (i == 4) continue;
                    int ix = (i % CELLS_ROW) + (j % 3 - 1);
                    int iy = (i / CELLS_ROW) + (j / 3 - 1);
                    if (ix >= 0 && ix < CELLS_ROW
                    &&  iy >= 0 && iy < CELLS_ROW)
                    {
                        lightSum += luminance[iy * CELLS_ROW + ix];
                        lightCount++;
                    }
                }

                luminance[cellIdx] = lightSum / lightCount * 
                    LIGHT_DECAY * (1.0f - occlusion * OCCLUSION_FACTOR);
            }
        }

        return luminance;
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