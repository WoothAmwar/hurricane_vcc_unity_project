using System;
using UnityEngine;
using UnityEngine.Rendering.Universal.Internal;
using TMPro;
using System.Linq;
using NUnit.Framework.Constraints;
using System.Collections.Generic;
using Unity.Mathematics;

public class SetPointVisual : MonoBehaviour
{
    public GameObject ellipsoidPoint, cloudMidPoint, numberPoint;
    public int visualIndex;
    public float scaleFactor;
    public TotalSettings settings=null;
    public color_schemes color_Schemes=null;
    
    [Header("Data Caching")]
    public int dataFileIdx;
    public int currPointVisualIdx;
    
    // Store all data for this point across timesteps
    private List<PointData> allPointData;
    private bool isDataInitialized = false;
    public bool isPointsInitialized = false;
    private int expectedTotalPoints = 24; // Based on your file structure
    
    private readonly string[] var_names = new string[] { "r", "t", "u", "v",
            "d", "cc", "o3", "pv", "ciwc", "clwc", "q", "crwc", "cswc", "w", "vo" };

    [System.Serializable]
    public struct PointData
    {
        public float rawValue;
        public float clampedValue;
        public int timestep;
        
        public PointData(float raw, float clamped, int time)
        {
            rawValue = raw;
            clampedValue = clamped;
            timestep = time;
        }
    }

    void Awake()
    {
        InitializeReferences();
        InitializeDataStorage();
    }

    void Start()
    {
        InitializeReferences();
        if (!isDataInitialized)
        {
            InitializeDataStorage();
        }
        GameObject newChild2 = Instantiate(cloudMidPoint, transform);
        newChild2.SetActive(false);
        GameObject newChild3 = Instantiate(numberPoint, transform);
        newChild3.SetActive(false);
        isPointsInitialized = true;

        SetDataIdx();
    }

    private void SetDataIdx()
    {
        string parentName = gameObject.transform.parent.name.ToString().Split("_")[0];
        dataFileIdx = var_names.ToList().IndexOf(parentName.ToLower());
    }

    private void InitializeReferences()
    {
        // if (settings == null)
        //     settings = FindFirstObjectByType<TotalSettings>();
        // if (color_Schemes == null)
        //     color_Schemes = FindFirstObjectByType<color_schemes>();

        if (settings != null)
        {
            visualIndex = settings.GetVisualIndex();
            scaleFactor = settings.GetScaleFactor();
        }
    }

    private void InitializeDataStorage()
    {
        if (allPointData == null)
        {
            allPointData = new List<PointData>();
        }
        currPointVisualIdx = 0;
        dataFileIdx = 0;
        isDataInitialized = true;
    }

    void Update()
    {
        if (settings != null)
        {
            visualIndex = settings.GetVisualIndex();
            scaleFactor = settings.GetScaleFactor();
        }
    }

    // Reset for object pooling
    public void ResetForPooling()
    {
        allPointData?.Clear();
        currPointVisualIdx = 0;
        dataFileIdx = 0;
        isDataInitialized = false;

        // Reset visual state
        if (transform.childCount > 0)
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
            }
        }
        SetDataIdx();
    }

    // Check if this point already has data for all timesteps
    public bool HasCompleteData()
    {
        return allPointData != null && allPointData.Count >= expectedTotalPoints;
    }

    // Add point data for a specific timestep
    public bool AddPointData(float clampedValue, float rawValue, int timestep = -1)
    {
        if (allPointData == null)
        {
            InitializeDataStorage();
        }

        // If timestep not specified, use current count as timestep
        if (timestep == -1)
        {
            timestep = allPointData.Count;
        }

        // Check if we already have data for this timestep
        var existingData = allPointData.FirstOrDefault(pd => pd.timestep == timestep);
        if (existingData.timestep == timestep && existingData.rawValue == rawValue)
        {
            return false; // Already have this data
        }

        // Remove any existing data for this timestep (in case of updates)
        allPointData.RemoveAll(pd => pd.timestep == timestep);
        
        // Add new data
        allPointData.Add(new PointData(rawValue, clampedValue, timestep));
        
        // Sort by timestep to maintain order
        allPointData = allPointData.OrderBy(pd => pd.timestep).ToList();

        
        return true; // New data added
    }

    // Set the current timestep to display
    public void SetCurrentTimestep(int timestep)
    {
        if (allPointData == null || allPointData.Count == 0)
        {
            Debug.LogWarning($"No data available for timestep {timestep}");
            return;
        }

        // Find the data for the requested timestep
        var dataForTimestep = allPointData.FirstOrDefault(pd => pd.timestep == timestep);
        if (dataForTimestep.timestep == timestep && allPointData.IndexOf(dataForTimestep)!=-1)
        {
            // Debug.Log(("Tried it:", timestep, "|", dataForTimestep.timestep, dataForTimestep.clampedValue, allPointData[0].clampedValue,allPointData.IndexOf(dataForTimestep)));
            currPointVisualIdx = allPointData.IndexOf(dataForTimestep);
        }
        else
        {
            // Fallback to closest timestep
            currPointVisualIdx = Mathf.Clamp(timestep, 0, Math.Max(allPointData.Count-1, 0));
            // Debug.LogWarning($"No data for exact timestep {timestep}, using index {currPointVisualIdx}");
        }
    }

    // Get current point data
    private PointData GetCurrentPointData()
    {
        if (allPointData == null || allPointData.Count == 0 || currPointVisualIdx >= allPointData.Count)
        {
            return new PointData(0f, 0f, 0);
        }
        // Debug.Log((allPointData.Count, currPointVisualIdx));
        return allPointData[currPointVisualIdx];
    }

    // Higher values are lighter colors
    private Color ApplyColor(string var_type)
    {
        PointData currentData = GetCurrentPointData();
        float clamp_dta = currentData.clampedValue;
        
        int array_pos = (int)(clamp_dta * 255);
        double[,] chosen_color_scheme;
        
        switch (var_type)
        {
            // Plasma, Inferno, and Magma are essentially the same
            case "o3":  // DONE
                chosen_color_scheme = color_Schemes.GetBG_128();
                // Debug.Log("!!!! o3 has been chosen");
                array_pos /= 2;
                break;
            case "r":   // DONE
                chosen_color_scheme = color_Schemes.GetRainbowScheme();
                // Debug.Log("????? r has been chosen");
                break;
            case "t":   // DONE
                chosen_color_scheme = color_Schemes.GetBYR_254();
                array_pos = (int)(clamp_dta * 253);
                break;
            case "q":   // DONE
                chosen_color_scheme = color_Schemes.GetCividisScheme();
                break;
            case "u":   // DONE
                chosen_color_scheme = color_Schemes.GetInfernoScheme();
                break;
            case "d":   // DONE
                chosen_color_scheme = color_Schemes.GetGnbuScheme_128();
                array_pos /= 2;
                break;
            case "v":   // DONE
                chosen_color_scheme = color_Schemes.GetMagmaScheme();
                break;
            case "pv":  // DONE
                chosen_color_scheme = color_Schemes.GetRYG_128();
                array_pos /= 2;
                break;
            case "w":   // DONE
                chosen_color_scheme = color_Schemes.GetYGScheme();
                break;

            default:
                chosen_color_scheme = color_Schemes.GetInfernoScheme();
                break;
        }
        
        // Ensure array_pos is within bounds
        int maxIndex = chosen_color_scheme.GetLength(0) - 1;
        array_pos = Mathf.Clamp(array_pos, 0, maxIndex);
        
        return new Color(
                (float)chosen_color_scheme[array_pos, 0],
                (float)chosen_color_scheme[array_pos, 1],
                (float)chosen_color_scheme[array_pos, 2]
            );
    }

    private string FormatScientific(float num, int sigFigs)
    {
        if (num == 0)
            return "0";

        int exponent = (int)Mathf.Floor(Mathf.Log10(Mathf.Abs((float)num)));
        double mantissa = num / Mathf.Pow(10, exponent);
        double roundedMantissa = System.Math.Round(mantissa, sigFigs - 1);

        return $"{roundedMantissa}e{(exponent >= 0 ? "+" : "")}{exponent}";
    }

    private void UpdateNumberChild(GameObject childObject)
    {
        var tmp = childObject.GetComponentInChildren<TextMeshPro>();
        if (tmp != null)
        {
            PointData currentData = GetCurrentPointData();
            tmp.color = ApplyColor(var_names[dataFileIdx]);
            tmp.text = FormatScientific(currentData.rawValue, 3);
            
            RectTransform childRect = childObject.GetComponentInChildren<RectTransform>();
            if (childRect != null)
            {
                childRect.localScale = new Vector3(scaleFactor / 5f, scaleFactor / 5f, 1f);
            }
        }
    }

    private void EmitCloud(ParticleSystem particleSystem, Vector3 center, int count)
    {
        // ParticleSystem.EmitParams emitParams = new();
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.EmitParams emitParams = new();
            emitParams.position = center + new Vector3(
                UnityEngine.Random.Range(-1f*scaleFactor/4, scaleFactor/4),
                UnityEngine.Random.Range(-1f*scaleFactor/4, scaleFactor/4),
                UnityEngine.Random.Range(-1f*scaleFactor/4, scaleFactor/4)
            );
            emitParams.velocity = Vector3.zero;
            emitParams.startColor = ApplyColor(var_names[dataFileIdx]);
            float sizeScaler = (count / 30f) / 2;  // clampedValue ~ count / 20
            emitParams.startSize = 1f + sizeScaler;
            particleSystem.Emit(emitParams, 1);
        }
    }


    private void UpdateCloudChild(GameObject childObject)
    {
        var tmp = childObject.GetComponentInChildren<ParticleSystem>();
        if (tmp != null)
        {
            PointData currentData = GetCurrentPointData();
            ParticleSystem particleSystem = childObject.GetComponentInChildren<ParticleSystem>();
            if (particleSystem != null)
            {
                // Consider using sqrt of the clamped value, or something to scale non-linearly
                // 20 was an arbitrary choice, can change, but should make same change in EmitCloud
                EmitCloud(particleSystem, new Vector3(0, 0, 0), Math.Max(1, (int)(currentData.clampedValue * 30)));
            }
        }
    }

    private Vector3 ApplyVerticalScale(float pt_scale_factor)
    {
        PointData currentData = GetCurrentPointData();
        float clamp_dta = currentData.clampedValue;
        float min_size = 1f;
        
        return new Vector3(
            min_size + clamp_dta * pt_scale_factor,
            Mathf.Max((min_size - clamp_dta) * Mathf.Min(Mathf.Max(pt_scale_factor, 1f), 1.5f), 0.15f),
            min_size + clamp_dta * pt_scale_factor
        );
    }

    private void UpdateEllipsoidChild(GameObject childObject)
    {
        Vector3 scaleShift = ApplyVerticalScale(scaleFactor);
        
        var meshRenderer = childObject.GetComponent<UnityEngine.MeshRenderer>();
        if (meshRenderer != null && meshRenderer.materials.Length > 0)
        {
            meshRenderer.materials[0].color = ApplyColor(var_names[dataFileIdx]);
            childObject.transform.localScale = scaleShift;
        }
        else
        {
            Debug.Log("No colour for you");
        }
    }

    // Update visual for current timestep - ensures correct child type is active
    public void UpdatePoint()
    {
        // Get the correct prefab for current visualIndex
        GameObject correctPrefab = GetCorrectPrefabForCurrentVisualIndex();
        
        // Check if we need to switch child types
        GameObject currentChild = GetActiveChild();
        
        if (currentChild == null || !IsCorrectChildType(currentChild, correctPrefab))
        {
            // Need to switch child types
            SwitchToCorrectChildType(correctPrefab);
            currentChild = GetActiveChild();
        }
        
        // Update the correct child
        if (currentChild != null)
        {
            UpdateChildBasedOnType(currentChild);
        }
    }

    private GameObject GetCorrectPrefabForCurrentVisualIndex()
    {
        return visualIndex switch
        {
            0 => ellipsoidPoint,
            1 => cloudMidPoint,
            2 => numberPoint,
            _ => ellipsoidPoint,
        };
    }
    
    private GameObject GetActiveChild()
    {
        foreach (Transform child in transform)
        {
            if (child.gameObject.activeInHierarchy)
            {
                return child.gameObject;
            }
        }
        return null;
    }

    private bool IsCorrectChildType(GameObject child, GameObject correctPrefab)
    {
        // Compare based on prefab name or components
        if (correctPrefab == ellipsoidPoint)
        {
            return child.GetComponent<MeshRenderer>() != null && child.GetComponentInChildren<TextMeshPro>() == null;
        }
        else if (correctPrefab == numberPoint)
        {
            return child.GetComponentInChildren<TextMeshPro>() != null;
        }
        else if (correctPrefab == cloudMidPoint)
        {
            // TODO - Add cloud-specific detection logic here
            return child.name.Contains("cloud") || child.name.Contains("Cloud");
        }
        
        return false;
    }

    private void SwitchToCorrectChildType(GameObject correctPrefab)
    {
        // Deactivate all existing children
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }
        
        // Look for existing child of correct type
        GameObject existingCorrectChild = FindExistingChildOfType(correctPrefab);
        
        if (existingCorrectChild != null)
        {
            // Debug.Log(("Using old child", existingCorrectChild.name));
            // Reactivate existing correct child
            existingCorrectChild.SetActive(true);
        }
        else
        {
            // Create new child of correct type
            GameObject newChild = Instantiate(correctPrefab, transform);
            newChild.SetActive(true);
            // Debug.Log(("Creating New Child", newChild.name));
        }
    }

    private GameObject FindExistingChildOfType(GameObject correctPrefab)
    {
        foreach (Transform child in transform)
        {
            // Debug.Log(("Compare:", child.gameObject.name,
            // correctPrefab.name, correctPrefab == ellipsoidPoint, IsCorrectChildType(child.gameObject, correctPrefab)));
            if (IsCorrectChildType(child.gameObject, correctPrefab))
            {
                return child.gameObject;
            }
        }
        return null;
    }

    private void UpdateChildBasedOnType(GameObject child)
    {
        // Could also use the name of the child object if this gets messy
        // Determine child type and update accordingly
        if (child.GetComponent<MeshRenderer>() != null && child.GetComponentInChildren<TextMeshPro>() == null)
        {
            UpdateEllipsoidChild(child);
        }
        else if (child.GetComponentInChildren<TextMeshPro>() != null)
        {
            UpdateNumberChild(child);
        }
        else if (child.name.Contains("cloud") || child.name.Contains("Cloud"))
        {
            UpdateCloudChild(child);
        }
    }

    // Advance to next timestep
    public void AdvanceTimestep()
    {
        if (allPointData != null && allPointData.Count > 0)
        {
            currPointVisualIdx = (currPointVisualIdx + 1) % allPointData.Count;
            UpdatePoint();
        }
    }

    // Debug method to check data state
    public void LogDataState()
    {
        Debug.Log($"Point has {(allPointData?.Count ?? 0)} data points, current index: {currPointVisualIdx}");
        if (allPointData != null)
        {
            foreach (var data in allPointData)
            {
                Debug.Log($"Timestep {data.timestep}: Raw={data.rawValue}, Clamped={data.clampedValue}");
            }
        }
    }
}