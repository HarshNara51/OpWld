using UnityEngine;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class RoadTerrainConformer : MonoBehaviour
{
    [Header("References")]
    public Transform roadsParent;
    public Terrain terrain;

    [Header("Road Shape")]
    [Min(0.1f)]
    public float roadWidth = 8f;

    [Min(0f)]
    public float shoulderWidth = 3f;

    [Min(0f)]
    public float falloffWidth = 10f;

    [Header("Road Height")]
    public float heightOffset = -0.15f;

    [Min(0.01f)]
    public float maxVerticalConform = 2.5f;

    [Header("Quality")]
    [Min(0.25f)]
    public float samplesPerMeter = 2f;

    [Range(0f, 1f)]
    public float strength = 1f;

    [Header("Terrain Behavior")]
    public bool onlyRaiseTerrain = true;

    [Range(0f, 1f)]
    public float smoothing = 0.65f;


#if UNITY_EDITOR

    // ============================================================
    // CONFORM ALL ROADS
    // ============================================================

    [ContextMenu("Conform ALL Roads")]
    public void ConformAllRoads()
    {
        if (roadsParent == null)
        {
            Debug.LogError(
                "Road Terrain Conformer: Assign the Roads parent."
            );
            return;
        }

        if (terrain == null)
        {
            Debug.LogError(
                "Road Terrain Conformer: Assign the Terrain."
            );
            return;
        }

        SplineContainer[] roads =
            roadsParent.GetComponentsInChildren<SplineContainer>(
                true
            );

        if (roads.Length == 0)
        {
            Debug.LogError(
                "Road Terrain Conformer: No Spline Containers found under Roads parent."
            );
            return;
        }

        TerrainData terrainData =
            terrain.terrainData;

        int resolution =
            terrainData.heightmapResolution;

        float[,] heights =
            terrainData.GetHeights(
                0,
                0,
                resolution,
                resolution
            );

        Vector3 terrainPosition =
            terrain.transform.position;

        Vector3 terrainSize =
            terrainData.size;

        // One Undo for the entire operation.
        Undo.RegisterCompleteObjectUndo(
            terrainData,
            "Conform All Roads"
        );

        Debug.Log(
            $"Road Terrain Conformer: Found {roads.Length} roads."
        );

        foreach (SplineContainer road in roads)
        {
            ConformSingleRoad(
                road,
                heights,
                resolution,
                terrainPosition,
                terrainSize
            );
        }

        terrainData.SetHeights(
            0,
            0,
            heights
        );

        EditorUtility.SetDirty(
            terrainData
        );

        Debug.Log(
            $"Road Terrain Conformer: Successfully conformed {roads.Length} roads."
        );
    }


    // ============================================================
    // SINGLE ROAD
    // ============================================================

    private void ConformSingleRoad(
        SplineContainer road,
        float[,] heights,
        int resolution,
        Vector3 terrainPosition,
        Vector3 terrainSize
    )
    {
        float roadLength =
            road.CalculateLength();

        int sampleCount =
            Mathf.Max(
                2,
                Mathf.CeilToInt(
                    roadLength *
                    samplesPerMeter
                )
            );

        float roadHalfWidth =
            roadWidth * 0.5f;

        float shoulderEnd =
            roadHalfWidth +
            shoulderWidth;

        float totalRadius =
            shoulderEnd +
            falloffWidth;

        for (int i = 0; i < sampleCount; i++)
        {
            float t =
                i /
                (float)(sampleCount - 1);

            Vector3 roadPoint =
                road.EvaluatePosition(t);

            roadPoint.y += heightOffset;

            ModifyTerrainAroundPoint(
                heights,
                resolution,
                terrainPosition,
                terrainSize,
                roadPoint,
                shoulderEnd,
                totalRadius
            );
        }
    }


    // ============================================================
    // MODIFY TERRAIN
    // ============================================================

    private void ModifyTerrainAroundPoint(
        float[,] heights,
        int resolution,
        Vector3 terrainPosition,
        Vector3 terrainSize,
        Vector3 roadPoint,
        float shoulderEnd,
        float totalRadius
    )
    {
        float normalizedX =
            (roadPoint.x - terrainPosition.x)
            / terrainSize.x;

        float normalizedZ =
            (roadPoint.z - terrainPosition.z)
            / terrainSize.z;

        if (normalizedX < 0f ||
            normalizedX > 1f ||
            normalizedZ < 0f ||
            normalizedZ > 1f)
        {
            return;
        }

        int centerX =
            Mathf.RoundToInt(
                normalizedX *
                (resolution - 1)
            );

        int centerZ =
            Mathf.RoundToInt(
                normalizedZ *
                (resolution - 1)
            );

        int pixelRadiusX =
            Mathf.CeilToInt(
                totalRadius /
                terrainSize.x *
                (resolution - 1)
            );

        int pixelRadiusZ =
            Mathf.CeilToInt(
                totalRadius /
                terrainSize.z *
                (resolution - 1)
            );

        int minX =
            Mathf.Max(
                0,
                centerX - pixelRadiusX
            );

        int maxX =
            Mathf.Min(
                resolution - 1,
                centerX + pixelRadiusX
            );

        int minZ =
            Mathf.Max(
                0,
                centerZ - pixelRadiusZ
            );

        int maxZ =
            Mathf.Min(
                resolution - 1,
                centerZ + pixelRadiusZ
            );

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float worldX =
                    terrainPosition.x +
                    (
                        x /
                        (float)(resolution - 1)
                    ) *
                    terrainSize.x;

                float worldZ =
                    terrainPosition.z +
                    (
                        z /
                        (float)(resolution - 1)
                    ) *
                    terrainSize.z;

                float dx =
                    worldX - roadPoint.x;

                float dz =
                    worldZ - roadPoint.z;

                float distance =
                    Mathf.Sqrt(
                        dx * dx +
                        dz * dz
                    );

                if (distance > totalRadius)
                    continue;

                // ----------------------------------------------
                // INFLUENCE
                // ----------------------------------------------

                float influence;

                if (distance <= shoulderEnd)
                {
                    influence = 1f;
                }
                else
                {
                    influence =
                        1f -
                        Mathf.InverseLerp(
                            shoulderEnd,
                            totalRadius,
                            distance
                        );

                    influence =
                        influence *
                        influence *
                        (3f - 2f * influence);
                }

                influence *= strength;

                influence =
                    Mathf.Lerp(
                        influence,
                        influence * influence,
                        smoothing
                    );

                if (influence <= 0.001f)
                    continue;

                // ----------------------------------------------
                // CURRENT HEIGHT
                // ----------------------------------------------

                float currentHeight01 =
                    heights[z, x];

                float currentWorldHeight =
                    terrainPosition.y +
                    currentHeight01 *
                    terrainSize.y;

                float targetHeight =
                    roadPoint.y;

                // ----------------------------------------------
                // ONLY RAISE
                // ----------------------------------------------

                if (onlyRaiseTerrain)
                {
                    if (currentWorldHeight >= targetHeight)
                        continue;
                }

                // ----------------------------------------------
                // LIMIT HEIGHT CHANGE
                // ----------------------------------------------

                float difference =
                    targetHeight -
                    currentWorldHeight;

                difference =
                    Mathf.Clamp(
                        difference,
                        onlyRaiseTerrain
                            ? 0f
                            : -maxVerticalConform,
                        maxVerticalConform
                    );

                float newWorldHeight =
                    currentWorldHeight +
                    difference *
                    influence;

                // ----------------------------------------------
                // NEVER GO ABOVE ROAD
                // ----------------------------------------------

                if (onlyRaiseTerrain)
                {
                    newWorldHeight =
                        Mathf.Min(
                            newWorldHeight,
                            targetHeight
                        );
                }

                // ----------------------------------------------
                // BACK TO TERRAIN HEIGHT
                // ----------------------------------------------

                float newHeight01 =
                    (
                        newWorldHeight -
                        terrainPosition.y
                    )
                    /
                    terrainSize.y;

                heights[z, x] =
                    Mathf.Clamp01(
                        newHeight01
                    );
            }
        }
    }


    // ============================================================
    // CONFORM SELECTED ROAD
    // ============================================================

    [ContextMenu("Conform Selected Road")]
    public void ConformSelectedRoad()
    {
        if (roadsParent == null || terrain == null)
        {
            Debug.LogError(
                "Assign Roads Parent and Terrain first."
            );
            return;
        }

        SplineContainer road =
            Selection.activeGameObject?
            .GetComponent<SplineContainer>();

        if (road == null)
        {
            Debug.LogError(
                "Select a Road Spline first."
            );
            return;
        }

        if (!road.transform.IsChildOf(roadsParent))
        {
            Debug.LogError(
                "Selected spline is not under the Roads parent."
            );
            return;
        }

        TerrainData terrainData =
            terrain.terrainData;

        int resolution =
            terrainData.heightmapResolution;

        float[,] heights =
            terrainData.GetHeights(
                0,
                0,
                resolution,
                resolution
            );

        Undo.RegisterCompleteObjectUndo(
            terrainData,
            "Conform Selected Road"
        );

        ConformSingleRoad(
            road,
            heights,
            resolution,
            terrain.transform.position,
            terrainData.size
        );

        terrainData.SetHeights(
            0,
            0,
            heights
        );

        EditorUtility.SetDirty(
            terrainData
        );

        Debug.Log(
            $"Conformed selected road: {road.name}"
        );
    }

#endif
}