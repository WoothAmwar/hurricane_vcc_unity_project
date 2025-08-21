using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ManageData : MonoBehaviour
{
    // d, o3, pv, q, w + r, t, u, v
    public GameObject entirePanel;
    public Dictionary<string, GameObject> all_hurricane_dta = new();
    public TotalSettings settingsObj;
    readonly string[] valid_names = new string[] { "d", "o3", "pv", "q", "w", "r", "t", "u", "v" };

    void Awake()
    {
        foreach (Transform childObject in gameObject.transform)
        {
            string dtaType = childObject.name.Split("_")[0].ToLower();
            if (!valid_names.Contains(dtaType))
            {
                UnityEngine.Debug.LogError($"GameObject with name {childObject.name} invalid");
            }
            all_hurricane_dta.Add(dtaType, childObject.gameObject);
        }
    }

    void Start()
    {
        // Use a coroutine to deactivate objects after all Start() methods have been called.
        StartCoroutine(InitializeActiveObject());
    }

    private IEnumerator InitializeActiveObject()
{
    var allSetPoints = all_hurricane_dta.Values
        .Select(go => go.GetComponent<SetPoints>())
        .Where(sp => sp != null)
        .ToList();

    var allVisuals = all_hurricane_dta.Values
        .SelectMany(go => go.GetComponentsInChildren<SetPointVisual>(true))
        .ToList();

    if (allSetPoints.Count == 0)
    {
        UnityEngine.Debug.LogWarning("No SetPoints components found to wait for.");
        yield break;
    }

    // Wait for all SetPoints pools and all SetPointVisuals to be initialized
    while (allSetPoints.Any(sp => !sp.IsPoolInitialized) ||
           allVisuals.Any(v => !v.isPointsInitialized))
    {
        yield return null;
    }
    MakeInactive();
}

    public void MakeInactive()
    {
        settingsObj.ToggleLoadingWindow();
        UnityEngine.Debug.Log("Being made inactive");
        foreach (string name in valid_names)
        {
            if (all_hurricane_dta.TryGetValue(name, out GameObject obj))
                obj.SetActive(false);
        }
        if (all_hurricane_dta.TryGetValue("r", out GameObject r_obj))
            r_obj.SetActive(true);
    }

    public void SetToggleManagerStatus()
    {
        entirePanel.SetActive(!entirePanel.activeInHierarchy);
    }

    public bool GetObjectStatus(string data_type)
    {
        // Debug.Log(("Getting Status", data_type, all_hurricane_dta.ContainsKey(data_type)));
        return all_hurricane_dta[data_type].activeInHierarchy;
    }

    public void SetObjectStatus(string data_type)
    {
        // Debug.Log(("SEtting Status, opp of", GetObjectStatus(data_type)));
        all_hurricane_dta[data_type].SetActive(!GetObjectStatus(data_type));
    }
}
