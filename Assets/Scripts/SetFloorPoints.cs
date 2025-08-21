using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class SetFloorPoints : MonoBehaviour
{
    private float scale_up_factor;
    public TextAsset[] data_pointTextFiles;
    public GameObject singlePoint;

    private float[][] x_points, y_points;
    private float[][][] data_points;

    private GameObject[] currPoints = new GameObject[0];

    // Object Pooling fields
    private Queue<GameObject> pointPool = new Queue<GameObject>();
    private int poolSize = 0;

    private float spread_factor;
    private float moveInterval;
    public TextAsset x_pointTextFile, y_pointTextFile;
    private TotalSettings settings;
    private color_schemes color_Schemes;

    void Start()
    {
        settings = FindFirstObjectByType<TotalSettings>();
        spread_factor = settings.GetSpreadFactor();
        moveInterval = settings.GetMoveInterval();
        color_Schemes = FindFirstObjectByType<color_schemes>();

        scale_up_factor = spread_factor;

        x_points = ReadFromFile(x_pointTextFile);
        y_points = ReadFromFile(y_pointTextFile);
        data_points = new float[data_pointTextFiles.Length][][];

        data_points = new float[data_pointTextFiles.Length][][];
        for (int i = 0; i < data_pointTextFiles.Length; i++)
        {
            data_points[i] = ReadFromFile(data_pointTextFiles[i]);
        }

        // Estimate maximum pool size needed
        int maxPoints = 0;
        for (int t = 0; t < x_points.Length; t++)
        {
            maxPoints = Math.Max(maxPoints, x_points[t].Length * data_points.Length);
        }
        poolSize = maxPoints;

        // Pre-instantiate pool
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(singlePoint, transform);
            obj.SetActive(false);
            pointPool.Enqueue(obj);
        }
    }

    void Awake()
    {
        settings = FindFirstObjectByType<TotalSettings>();
        spread_factor = settings.GetSpreadFactor();
        scale_up_factor = spread_factor;
        moveInterval = settings.GetMoveInterval();
    }

    public void DoAtTimerEnd()
    {
        DeactivatePoints();
        MovePoints(settings.GetTimeSplit());
    }

    // Instead of destroying, just deactivate and return to pool
    private void DeactivatePoints()
    {
        foreach (GameObject point in currPoints)
        {
            if (point != null)
            {
                point.SetActive(false);
                pointPool.Enqueue(point);
            }
        }
    }

    // Higher values are lighter colors
    private Color ApplyColor(string var_type, float clamp_dta)
    {
        int array_pos = (int)(clamp_dta * 255);
        double[,] chosen_color_scheme;
        // switch (var_type)
        // {
        //     case "mslp":
        //         chosen_color_scheme = FindFirstObjectByType<color_schemes>().GetBYR_254();
        //         array_pos = (int)(clamp_dta * 253);
        //         break;
        //     case "t2m":
        //         chosen_color_scheme = FindFirstObjectByType<color_schemes>().GetPlasmaScheme();
        //         break;
        //     case "sp":
        //         chosen_color_scheme = FindFirstObjectByType<color_schemes>().GetCividisScheme();
        //         break;
        //     default:
        //         chosen_color_scheme = FindFirstObjectByType<color_schemes>().GetInfernoScheme();
        //         break;
            
        // }
        chosen_color_scheme = color_Schemes.GetViridisScheme();
        array_pos = (int)(clamp_dta * 253);
        return new Color((float)chosen_color_scheme[array_pos, 0], (float)chosen_color_scheme[array_pos, 1], (float)chosen_color_scheme[array_pos, 2]);
    }

    private Vector3 ApplyScale(float clamp_dta, float pt_scale_factor)
    {
        float min_size = 1f;
        return new Vector3(min_size + clamp_dta * pt_scale_factor, 1, min_size + clamp_dta * pt_scale_factor);
    }

    private GameObject ApplyFilter(int fileIndex, float valueForFilter, GameObject dupePoint)
    {
        // mslp , Min: 96418.9375 , 102169.5625
        Tuple<float, float> mslp_filter = new(96418.9375f, 102169.5625f);
        double var_min = mslp_filter.Item1;
        double var_max = mslp_filter.Item2;
        double norm_dt_pt = (valueForFilter - var_min) / (var_max - var_min);
        float clamped_norm = Mathf.Clamp01((float)norm_dt_pt);
        dupePoint.GetComponent<Renderer>().material.color = ApplyColor("mslp", clamped_norm);

        Vector3 scaleShift = ApplyScale(clamped_norm, scale_up_factor);
        dupePoint.transform.localScale = new Vector3(scaleShift.x, scaleShift.y, scaleShift.z);


        return dupePoint;
    }

    // public Tuple<float, float> ApplyShift(float x_val, float y_val)
    // {
    //     Tuple<float, float> mid_pos = FindFirstObjectByType<ManageUser>().GetMiddlePosition();
    //     Tuple<float, float> return_pos = new(x_val - mid_pos.Item1, y_val - mid_pos.Item2);
    //     return return_pos;
    // }
    
    public Vector3 ApplySpread(Vector3 orig_pt, float spread_val)
    {
        // Tuple<float, float> mid_pos = FindFirstObjectByType<ManageUser>().GetMiddlePosition(settings.GetTimeSplit());
        Tuple<float, float> mid_pos = new(0f, 0f);
        Tuple<float, float> dist_from_mid = new(orig_pt.x - mid_pos.Item1, orig_pt.z - mid_pos.Item2);
        // Scales distance by the spread factor
        dist_from_mid = new Tuple<float, float>(dist_from_mid.Item1 * spread_val, dist_from_mid.Item2 * spread_val);
        return new Vector3(mid_pos.Item1 + dist_from_mid.Item1, orig_pt.y, mid_pos.Item2 + dist_from_mid.Item2);
    }

    private void MovePoints(int time_idx)
    {
        if (x_points[time_idx].Length != y_points[time_idx].Length)
        {
            Debug.LogError("Error: X and Y lists are not the same length at time_idx: " + time_idx.ToString());
        }
        currPoints = new GameObject[x_points[time_idx].Length * data_points.Length];

        int idx = 0;
        for (int i = 0; i < x_points[time_idx].Length; i++)
        {
            for (int j = 0; j < data_points.Length; j++)
            {
                GameObject dupePoint;
                if (pointPool.Count > 0)
                {
                    dupePoint = pointPool.Dequeue();
                }
                else
                {
                    dupePoint = Instantiate(singlePoint, transform);
                }

                dupePoint.SetActive(true);
                dupePoint.transform.SetParent(transform);
                // float x_rand_move = UnityEngine.Random.Range(-0.5f, 0.5f);
                float y_rand_move = UnityEngine.Random.Range(-0.5f, 0.5f);
                
                // dupePoint.transform.localPosition = new Vector3(
                //     ApplyShift(x_points[time_idx][i],y_points[time_idx][i]).Item1 * spread_factor, // + x_rand_move, // * points_per_z_file.Max(), // (data_pointTextFiles.Length - z_pointTextFiles.Length),
                //     1, // + y_rand_move * (points_per_z_file.Max()/2),
                //     ApplyShift(x_points[time_idx][i],y_points[time_idx][i]).Item2 * spread_factor //  + y_rand_move * points_per_z_file.Max() // (data_pointTextFiles.Length - z_pointTextFiles.Length)
                // );

                dupePoint.transform.localPosition = ApplySpread(new Vector3(
                    x_points[time_idx][i], // + x_rand_move, // * points_per_z_file.Max(), // (data_pointTextFiles.Length - z_pointTextFiles.Length),
                    1, // + y_rand_move * (points_per_z_file.Max()/2),
                    y_points[time_idx][i] //  + y_rand_move * points_per_z_file.Max() // (data_pointTextFiles.Length - z_pointTextFiles.Length)
                ), spread_factor);
                float pt = data_points[j][time_idx][i];

                dupePoint = ApplyFilter(j, pt, dupePoint);
                dupePoint.transform.localScale = new Vector3(scale_up_factor, 1, scale_up_factor);

                currPoints[idx] = dupePoint;
                idx++;
            }
        }
    }

    private float[][] ReadFromFile(TextAsset pointTextFile)
    {
        string[] lines = pointTextFile.text.Split('\n');
        if (lines.Length != 24)
        {
            Debug.Log("ERROR: Length not equal to 24");
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
}