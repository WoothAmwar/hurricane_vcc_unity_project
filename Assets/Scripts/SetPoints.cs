using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class SetPoints : MonoBehaviour
{
    public int[] dta_point_file_match;
    [Header("Ensure Z_Point Text Files are in decreasing order, so start with higher values and go down to lower")]
    public TextAsset[] z_pointTextFiles;
    public TextAsset[] data_pointTextFiles;
    public GameObject singlePoint;
    public ManageUser manageUser;
    public int[] points_per_z_file;

    private float[][] x_points, y_points;
    private float[][][] z_points, data_points;

    private float spread_factor, scale_up_factor, moveInterval;
    private TextAsset x_pointTextFile, y_pointTextFile;

    private GameObject[] currPoints = new GameObject[0];

    // Improved Object Pooling fields
    private Queue<GameObject> pointPool = new Queue<GameObject>();
    private List<GameObject> allPooledObjects = new List<GameObject>(); // Track all objects for cleanup
    private int poolSize = 0;
    private TotalSettings settings;
    private bool isPoolInitialized = false;

    public bool IsPoolInitialized => isPoolInitialized;
    private Func<float, float> scaleFunction;

    void Awake()  // Start
    {
        settings = FindFirstObjectByType<TotalSettings>();
        x_pointTextFile = settings.GetXPoints();
        y_pointTextFile = settings.GetYPoints();
        spread_factor = settings.GetSpreadFactor();
        // scale_up_factor = settings.GetScaleFactor();
        moveInterval = settings.GetMoveInterval();

        scaleFunction = settings.GetScaleFunction();

        x_points = ReadFromFile(x_pointTextFile);
        y_points = ReadFromFile(y_pointTextFile);
        z_points = new float[z_pointTextFiles.Length][][];
        data_points = new float[data_pointTextFiles.Length][][];
        
        for (int i = 0; i < z_pointTextFiles.Length; i++)
        {
            z_points[i] = ReadFromFile(z_pointTextFiles[i]);
        }
        
        for (int i = 0; i < data_pointTextFiles.Length; i++)
        {
            data_points[i] = ReadFromFile(data_pointTextFiles[i]);
        }
        
        points_per_z_file = new int[z_pointTextFiles.Length];
        for (int i = 0; i < z_pointTextFiles.Length; i++)
        {
            points_per_z_file[i] = 0;
        }
        
        dta_point_file_match = new int[data_pointTextFiles.Length];
        for (int i = 0; i < data_pointTextFiles.Length; i++)
        {
            for (int j = 0; j < z_pointTextFiles.Length; j++)
            {
                string curr_geo = z_pointTextFiles[j].name.Substring(1);
                if (data_pointTextFiles[i].name.Contains(curr_geo))
                {
                    dta_point_file_match[i] = j;
                    points_per_z_file[j]++;
                    break;
                }
            }
        }

        // Pool is initialized in Start() via a coroutine to prevent hitches.
    }

    void Start()
    {
        StartCoroutine(InitializePool());
    }

    private IEnumerator InitializePool()
    {
        // Calculate maximum points needed more accurately
        int maxPointsPerTimeStep = 0;
        for (int t = 0; t < x_points.Length; t++)
        {
            int pointsForThisTimeStep = x_points[t].Length * data_points.Length;
            maxPointsPerTimeStep = Math.Max(maxPointsPerTimeStep, pointsForThisTimeStep);
        }

        // Add some buffer to pool size
        poolSize = Mathf.CeilToInt(maxPointsPerTimeStep * 1.2f);

        Debug.Log($"Initializing pool with {poolSize} objects for {gameObject.name}. This may take a few moments...");

        // Pre-allocate list capacity to avoid reallocations, which is more efficient than using AddRange in a loop.
        allPooledObjects.Capacity = poolSize;

        const int instantiationsPerFrame = 100; // You can tune this value
        var batchForInitialization = new List<GameObject>(instantiationsPerFrame);

        // Pre-instantiate all pool objects
        for (int i = 0; i < poolSize; i++)
        {
            // Instantiate the object. It will be active by default, which is what we want
            // so that its Start() method gets called.
            GameObject pooledObj = Instantiate(singlePoint, transform);
            pooledObj.SetActive(true);
            
            // Add to a temporary list for this frame's batch.
            batchForInitialization.Add(pooledObj);
            allPooledObjects.Add(pooledObj);

            // When the batch is full, or we're at the very last object, we'll process it.
            if (batchForInitialization.Count >= instantiationsPerFrame || i == poolSize - 1)
            {
                // By waiting for the next frame, we give Unity the chance to call Awake() and Start()
                // on all the GameObjects we just instantiated in this batch.
                yield return null;

                // Now that their Start() methods have run, we can deactivate them and add them to the pool.
                foreach (var obj in batchForInitialization)
                {
                    obj.SetActive(false);
                    pointPool.Enqueue(obj);
                }
                
                // Clear the batch to start fresh for the next one.
                batchForInitialization.Clear();
            }
        }

        isPoolInitialized = true;
        Debug.Log($"Pool for {gameObject.name} is ready.");
        settings.ToggleVisualization();
    }

    private void DeactivateAllChildren(GameObject obj)
    {
        foreach (Transform child in obj.transform)
        {
            child.gameObject.SetActive(false);
            DeactivateAllChildren(child.gameObject); // Recursive for nested children
        }
    }

    private GameObject GetPooledObject()
    {
        if (pointPool.Count > 0)
        {
            GameObject obj = pointPool.Dequeue();
            // Debug.Log("Setting Active");
            obj.SetActive(true);
            return obj;
        }
        else
        {
            // Pool is exhausted - this shouldn't happen if sized correctly
            Debug.LogWarning("Pool exhausted! Creating new object. Consider increasing pool size.");
            GameObject newObj = Instantiate(singlePoint, transform);
            allPooledObjects.Add(newObj); // Track it for cleanup
            return newObj;
        }
    }

    private void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;
        
        obj.SetActive(false);
        DeactivateAllChildren(obj);
        
        // Reset the visual component for reuse
        if (obj.TryGetComponent<SetPointVisual>(out var visual))
        {
            visual.ResetForPooling(); // New reset method
        }
        
        pointPool.Enqueue(obj);
    }
    void Update()
    {
        spread_factor = settings.GetSpreadFactor();
        // scale_up_factor = settings.GetScaleFactor();
        moveInterval = settings.GetMoveInterval();
    }

    // To update the position after being enabled through the toggle
    void OnEnable()
    {
        StartCoroutine(ShowInitialPointsWhenReady());
    }

    private IEnumerator ShowInitialPointsWhenReady()
    {
        // Wait until the pool is initialized before trying to show points
        yield return new WaitUntil(() => isPoolInitialized);

        DeactivatePoints();
        if (settings.GetTimeSplit() < 1)
        {
            MovePoints(settings.GetTimeSplit() - 1 + 24);
        }
        else
        {
            MovePoints(settings.GetTimeSplit() - 1);
        }
    }
    public void DoAtTimerEnd()
    {
        if (!isPoolInitialized) return;

        DeactivatePoints();
        MovePoints(settings.GetTimeSplit());
    }

    // Return all current points to pool
    private void DeactivatePoints()
    {
        foreach (GameObject point in currPoints)
        {
            ReturnToPool(point);
        }
        // Clear the array reference
        currPoints = new GameObject[0];
    }

    private float GetClampedNorm(int fileIndex, float valueForFilter)
    {
        string object_val_name = data_pointTextFiles[fileIndex].name;
        string[] var_names = new string[] { "r", "t", "u", "v",
            "d", "cc", "o3", "pv", "ciwc", "clwc", "q", "crwc", "cswc", "w", "vo" };

        Tuple<double, double>[] var_filters = new Tuple<double, double>[] {
            Tuple.Create(-3.8280, 138.0896),  // r
            Tuple.Create(187.4324, 305.06531), // t
            Tuple.Create(-45.1580, 41.9673), // u
            Tuple.Create(-43.2251,54.0019), // v
            Tuple.Create(-0.0009, 0.0008),  // d
            Tuple.Create(0.0039, 1.0),
            Tuple.Create(4.7788e-08, 1.60598e-05),  // o3
            Tuple.Create(-3.1504e-05, 0.000271),  // pv
            Tuple.Create(2.9802e-08, 0.00113),
            Tuple.Create(1.1920e-07, 0.0007518),
            Tuple.Create(1.61099e-08, 0.02393),  // q
            Tuple.Create(1.28579e-10, 0.00205),
            Tuple.Create(2.32830e-10, 0.007559),
            Tuple.Create(-10.3504, 3.58344),  // w
            Tuple.Create(-0.000440, 0.0020763)
        };

        for (int i = 0; i < var_names.Length; i++)
        {
            if (object_val_name.Contains(var_names[i]))
            {
                double var_min = var_filters[i].Item1;
                double var_max = var_filters[i].Item2;
                double norm_dt_pt = (valueForFilter - var_min) / (var_max - var_min);
                float clamped_norm = Mathf.Clamp01((float)norm_dt_pt);
                return clamped_norm;
            }
        }

        return fileIndex;
    }

    public Tuple<float, float> ApplyShift(float x_val, float y_val)
    {
        Tuple<float, float> mid_pos = manageUser.GetMiddlePosition();
        Tuple<float, float> return_pos = new(x_val - mid_pos.Item1, y_val - mid_pos.Item2);
        return return_pos;
    }

    public Vector3 ApplySpread(Vector3 orig_pt, float spread_val)
    {
        Tuple<float, float> mid_pos = new(0f, 0f);
        Vector3 direction = orig_pt - new Vector3(mid_pos.Item1, orig_pt.y, mid_pos.Item2);
        direction.y = 0;
        Vector3 scaled = new Vector3(mid_pos.Item1, orig_pt.y, mid_pos.Item2) + direction * spread_val;
        return scaled;
    }

    private void MovePoints(int time_idx)
    {
        if (x_points[time_idx].Length != y_points[time_idx].Length)
        {
            Debug.LogError("Error: X and Y lists are not the same length at time_idx: " + time_idx.ToString());
            return;
        }

        int totalPoints = x_points[time_idx].Length * data_points.Length;
        currPoints = new GameObject[totalPoints];

        string[] var_names = new string[] { "r", "t", "u", "v",
            "d", "cc", "o3", "pv", "ciwc", "clwc", "q", "crwc", "cswc", "w", "vo" };

        int idx = 0;

        for (int i = 0; i < x_points[time_idx].Length; i++)
        {
            for (int j = 0; j < data_points.Length; j++)
            {
                // Get file index for variable type
                int file_idx = 0;
                for (int v_idx = 0; v_idx < var_names.Length; v_idx++)
                {
                    if (data_pointTextFiles[j].name.Contains(var_names[v_idx]))
                    {
                        file_idx = v_idx;
                        break;
                    }
                }

                float pt = data_points[j][time_idx][i];

                // Get object from pool
                GameObject dec_point = GetPooledObject();
                SetPointVisual decVisual = dec_point.GetComponent<SetPointVisual>();

                // Check if this object already has all the data it needs
                if (!decVisual.HasCompleteData())
                {
                    // Preload all timestep data for this spatial position
                    PreloadAllTimestepData(decVisual, i, j, file_idx);
                }

                // Set current timestep
                decVisual.SetCurrentTimestep(time_idx);

                // The UpdatePoint() method will now handle ensuring the correct child type is active
                decVisual.UpdatePoint();

                // Get the currently active child (which is now guaranteed to be the right type)
                GameObject dupePoint = null;
                foreach (Transform child in dec_point.transform)
                {
                    if (child.gameObject.activeInHierarchy)
                    {
                        dupePoint = child.gameObject;
                        break;
                    }
                }

                if (dupePoint == null)
                {
                    Debug.LogError("No active child found after UpdatePoint()");
                    continue;
                }
                // Debug.Log(("Set DupePoint to active",dec_point.name));
                dupePoint.SetActive(true);
                dupePoint.transform.SetParent(dec_point.transform);

                // Set position using your existing logic
                dupePoint.transform.localPosition = ApplySpread(new Vector3(
                    x_points[time_idx][i],
                    scaleFunction(z_points[dta_point_file_match[j]][time_idx][i]),
                    y_points[time_idx][i]
                ), spread_factor);

                currPoints[idx] = dec_point;
                idx++;
            }
        }
    }

    private void PreloadAllTimestepData(SetPointVisual visual, int spatialIndex, int dataFileIndex, int fileIdx)
    {
        // Load data for all 24 timesteps for this spatial position
        for (int t = 0; t < x_points.Length; t++)
        {
            if (spatialIndex < data_points[dataFileIndex][t].Length)
            {
                float rawValue = data_points[dataFileIndex][t][spatialIndex];
                float clampedValue = GetClampedNorm(dataFileIndex, rawValue);

                visual.AddPointData(clampedValue, rawValue, t);
            }
        }
    }

    private float[][] ReadFromFile(TextAsset pointTextFile)
    {
        string[] lines = pointTextFile.text.Split('\n');
        if (lines.Length != 24)
        {
            return null;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].Trim();
        }

        float[][] parsedLines = new float[lines.Length][];

        for (int l_idx = 0; l_idx < lines.Length; l_idx++)
        {
            string[] tokens = lines[l_idx].Split(',');
            float[] numbers = new float[tokens.Length];
            parsedLines[l_idx] = new float[tokens.Length];

            for (int i = 0; i < tokens.Length; i++)
            {
                float.TryParse(tokens[i], out numbers[i]);
                parsedLines[l_idx][i] = numbers[i];
            }
        }
        return parsedLines;
    }

    // Cleanup method for when the object is destroyed
    private void OnDestroy()
    {
        // Clean up all pooled objects
        foreach (GameObject obj in allPooledObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        pointPool.Clear();
        allPooledObjects.Clear();
    }
}