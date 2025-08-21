using System;
using UnityEngine;
using TMPro;

public class SetElevationLevels : MonoBehaviour
{
    public int altitude;

    private TotalSettings settings;
    private Func<float, float> scaleFunction;
    public GameObject[] textObjects;
    [Header("In KM")]
    public string elevationHeightStr;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        foreach (GameObject textObject in textObjects) {
            textObject.GetComponent<TextMeshProUGUI>().text = elevationHeightStr + " KM";    
        }

        settings = FindFirstObjectByType<TotalSettings>();
        scaleFunction = settings.GetScaleFunction();
        gameObject.transform.localPosition = new Vector3(
            0,
            scaleFunction(altitude),
            0);
    }

}

