using System;
using UnityEngine;


public class ManageUser : MonoBehaviour
{
    public int curr_time_split;
    public TextAsset[] min_pointTextFiles;
    private TotalSettings settings;
    public float mp_x, mp_y;
    private float spread_factor;

    void Start()
    {
        settings = FindFirstObjectByType<TotalSettings>();
        spread_factor = settings.GetSpreadFactor();
    }
    // Logic has been moved to TotalSettings to ensure all movements happen simultaneously
    // void Update()
    // {
    //     SetPosition();
    //     // Doing the -1, or adding a delay essentially, works if you want to set the User Object to the middle
    //     //  of the current squares
    //     // Comment them out if you want to shift the current squares to be "centered" around the Origin Object.
    //     //  Not sure why it behaves this way
    //     curr_time_split = settings.GetTimeSplit() - 1;
    //     if (curr_time_split < 0)
    //     {
    //         curr_time_split = 23;
    //     }
    // }

    public Vector3 SetPosition(int orig_time_split)
    {
        int tmp_time_split = orig_time_split - 1;
        if (tmp_time_split < 0)
        {
            tmp_time_split = 23;
        }
        Tuple<float, float> pos_tup = GetMiddlePosition(tmp_time_split);
        // Now num1 and num2 hold the extracted numbers
        transform.position = new Vector3(pos_tup.Item1, transform.position.y, pos_tup.Item2);
        return transform.position;
    }

    // public void ChangeYPosition(float posDeltaHeight)
    // {
    //     transform.position = new Vector3(transform.position.x, transform.position.y + posDeltaHeight, transform.position.z);    
    // }

    // private void SetPosition()
    // {
    //     Tuple<float, float> pos_tup = GetMiddlePosition(curr_time_split);
    //     // Now num1 and num2 hold the extracted numbers
    //     transform.position = new Vector3(pos_tup.Item1, 5, pos_tup.Item2);
    // }

    public Tuple<float, float> GetMiddlePosition()
    {
        return GetMiddlePosition(curr_time_split);
    }
    public Tuple<float, float> GetMiddlePosition(int pos_time_split)
    {
        if (pos_time_split < 0)
        {
            pos_time_split = 0;
        }
        pos_time_split %= 24;
        // Get the text and remove the brackets
        string text = min_pointTextFiles[pos_time_split].text.Trim();
        text = text.Trim('[', ']');
        // Split by comma and parse the numbers
        string[] parts = text.Split(',');
        // Debug.Log(parts[0]);
        float x_coord = float.Parse(parts[0]);
        float y_coord = float.Parse(parts[1]);
        mp_x = x_coord;
        mp_y = y_coord;
        return new Tuple<float, float>(x_coord * spread_factor, y_coord * spread_factor);
    }
}
