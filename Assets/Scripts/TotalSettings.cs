using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class TotalSettings : MonoBehaviour
{
    public bool start_visualization;
    [Header("Spread should be equal to at least Scale+1")]
    public float spread_factor;
    public float scale_up_factor;

    [Header("Should both be of the same format and type/dimension")]
    public TextAsset x_pointTextFile;
    public TextAsset y_pointTextFile;
    [Header("In Seconds")]
    public float moveInterval;
    public int hurricane_time_split;

    private float timer = 0f;

    private SetPoints[] allSetPointsObjs;
    private SetFloorPoints setFloorPointsObj;
    private ManageUser userObj;
    [Header("Visual Stuff")]
    public int visualIndex;
    public Transform playerHead; // E.g., center eye anchor or camera
    public Material cutoutMaterial;

    public GameObject playerCamera;
    public GameObject compassArrow;

    public TextMeshProUGUI sliderMoveIntervalTxt;
    public GameObject loadingWindow;

    public TextMeshProUGUI timestampTxt;

    private readonly string[] timestamps = {"2024-9-23 12am", "2024-9-23 6am",
        "2024-9-23 12pm", "2024-9-23 6pm", "2024-9-24 12am", "2024-9-24 6am", "2024-9-24 12pm", "2024-9-24 6pm", "2024-9-25 12am", "2024-9-25 6am",
        "2024-9-25 12pm", "2024-9-25 6pm", "2024-9-26 12am", "2024-9-26 6am", "2024-9-26 12pm", "2024-9-26 6pm", "2024-9-27 12am", "2024-9-27 6am",
        "2024-9-27 12pm", "2024-9-27 6pm", "2024-9-28 12am", "2024-9-28 6am", "2024-9-28 12pm", "2024-9-28 6pm"};

    private bool toggleTopView;
    private float topViewHeight = 480f;
    private float preTopHeight = 0f;
    private Quaternion preTopRot = new(0, 0, 0, 0);

    public GameObject ElevationMarkersObjs;


    void Awake()
    {
        allSetPointsObjs = FindObjectsByType<SetPoints>(FindObjectsSortMode.None);
        Debug.Log(("ALLPOINTS:", allSetPointsObjs.Count()));
        start_visualization = false;
        toggleTopView = false;
    }
    void Start()
    {
        // allSetPointsObjs = FindObjectsByType<SetPoints>(FindObjectsSortMode.None);
        loadingWindow.SetActive(true);
        setFloorPointsObj = FindFirstObjectByType<SetFloorPoints>();
        userObj = FindFirstObjectByType<ManageUser>();
        moveInterval = 0.1f;
        SetStrMoveInterval();
    }
    void Update()
    {
        UpdateCompass();
        if (!start_visualization)
        {
            return;
        }

        timer += Time.deltaTime;
        if (timer >= moveInterval)
        {
            timestampTxt.text = GetCurrTimestampStr();
            Vector3 userPos = userObj.SetPosition(hurricane_time_split + 1);
            playerCamera.transform.position = new Vector3(userPos.x, playerCamera.transform.position.y, userPos.z);

            foreach (SetPoints singleSetObj in allSetPointsObjs)
            {
                if (singleSetObj != null && singleSetObj.isActiveAndEnabled)
                {
                    // Debug.Log(("Not NULL:", singleSetObj.name));
                    singleSetObj.DoAtTimerEnd();
                }
                // else
                // {
                //     Debug.Log(("NULL:", singleSetObj.name));
                // }
            }
            if (setFloorPointsObj != null)
            {
                setFloorPointsObj.DoAtTimerEnd();
            }
            UpdateElevationLabels();
            timer = 0f;
            hurricane_time_split = (hurricane_time_split + 1) % 24;
        }
    }

    public void ToggleLoadingWindow()
    {
        loadingWindow.SetActive(!loadingWindow.activeInHierarchy);
    }

    public void ToggleTopView()
    {
        toggleTopView = !toggleTopView;
        if (toggleTopView)
        {

            preTopRot = playerCamera.transform.rotation;
            preTopHeight = playerCamera.transform.position.y;
            ElevationMarkersObjs.SetActive(false);
            playerCamera.transform.SetPositionAndRotation(new Vector3(
                playerCamera.transform.position.x,
                topViewHeight,
                playerCamera.transform.position.z), Quaternion.Euler(
                90,
                0,
                0));
        }
        else
        {
            Vector3 origPos = userObj.SetPosition(hurricane_time_split);
            playerCamera.transform.SetPositionAndRotation(new Vector3(
                origPos.x,
                preTopHeight,
                origPos.z), preTopRot);
            ElevationMarkersObjs.SetActive(true);
        }
    }

    private void UpdateElevationLabels()
    {
        if (cutoutMaterial != null && playerHead != null)
        {
            Vector3 center = playerHead.position;
            cutoutMaterial.SetVector("_CutoutCenter", new Vector4(center.x, center.y, center.z, 0));
        }
    }

    private void UpdateCompass()
    {
        compassArrow.transform.localRotation = Quaternion.Euler(
            compassArrow.transform.localRotation.eulerAngles.x,
            compassArrow.transform.localRotation.eulerAngles.y,
            -1 * playerCamera.transform.localRotation.eulerAngles.y
        );
    }

    private float ScaleHeight(float height)
    {
        const float scale_num = 20;
        // const float div_num = 10;
        // return height / 10;
        if (height == 0)
        {
            return 20;
        }
        float negScale = 0;
        if (height < 0)
        {
            while (negScale <= height)
            {
                negScale += 1;
            }
            if (negScale < 10)
            {
                return Mathf.Log10(Mathf.Max(negScale, 0.01f)) * scale_num / 10f;
            }
            return Mathf.Log10(Mathf.Max(negScale, 10)) * scale_num * -1;
            // return Mathf.Max(Mathf.Log10(Mathf.Max(negScale, 10f)) * (scale_num - negScale / 4f), 1f) / div_num;
        }

        if (height < 10)
        {
            return Mathf.Log10(Mathf.Max(height, 0.01f)) * -1 * scale_num / 10f;
        }
        // return (20 - negScale / 4f) - 50;
        // return Mathf.Log10(height+10) * scale_num / div_num;
        return Mathf.Log10(height) * scale_num;
    }

    public TextAsset GetXPoints()
    {
        return x_pointTextFile;
    }

    public TextAsset GetYPoints()
    {
        return y_pointTextFile;
    }

    public Func<float, float> GetScaleFunction()
    {
        Func<float, float> scaleFunc = ScaleHeight;
        return scaleFunc;
    }

    public float GetSpreadFactor()
    {
        return spread_factor;
    }

    public float GetScaleFactor()
    {
        return scale_up_factor;
    }
    public float GetMoveInterval()
    {
        return moveInterval;
    }

    public int GetTimeSplit()
    {
        return hurricane_time_split;
    }

    public int GetVisualIndex()
    {
        return visualIndex;
    }

    public void SetMoveInterval(Slider moveSlider)
    {
        moveInterval = Mathf.Max(0.1f, moveSlider.value * 10f);
        SetStrMoveInterval();
    }

    public void SetStrMoveInterval()
    {
        sliderMoveIntervalTxt.text = Math.Round(moveInterval, 2).ToString() + "s";
    }

    public void IncrementVisualIndex()
    {
        SetVisualIndex((visualIndex + 1) % 3);
    }

    public void SetVisualIndex(int newVisIndex)
    {
        visualIndex = newVisIndex;
    }

    public void ToggleVisualization()
    {
        start_visualization = !start_visualization;
    }

    public void ChangeCameraYPosition(float posDeltaHeight)
    {
        playerCamera.transform.position = new Vector3(
            playerCamera.transform.position.x,
            playerCamera.transform.position.y + posDeltaHeight,
            playerCamera.transform.position.z);
    }

    public string GetCurrTimestampStr()
    {
        return timestamps[hurricane_time_split % 24];
    }
}
