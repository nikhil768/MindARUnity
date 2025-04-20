using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using AOT;

using UnityEngine;
using UnityEngine.UI;

using Unity.Mathematics;

public class WebCamController : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void SetMindARCallBack(Action<int, float[]> cb);
    [DllImport("__Internal")]
    static extern void SetBarCodeCallBack(Action<string> cb);
#endif

    [RuntimeInitializeOnLoadMethod]
    static void Init()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SetMindARCallBack(MindARCallBack);
        SetBarCodeCallBack(BarCodeCallBack);
#else
        Debug.Log("WebGL interop is not available in Editor. Using mock data.");
        SimulateAR();
#endif
    }

    static List<(int index, float[] matrix)> UpdateQue = new List<(int index, float[] matrix)>();
    static List<string> BarcodesQue = new List<string>();

    [MonoPInvokeCallback(typeof(Action<int, float[]>))]
    static void MindARCallBack(int index, [MarshalAs(UnmanagedType.LPArray, SizeConst = 16)] float[] matrix) => UpdateQue.Add((index, matrix));

    [MonoPInvokeCallback(typeof(Action<string>))]
    static void BarCodeCallBack(string value) => BarcodesQue.Add(value);

    public RawImage image;
    public TMPro.TMP_Text qrCodes;
    public TMPro.TMP_Text[] names;

    void Start()
    {
        WebCamDevice? chosenDevice = null;

        // Try to find rear camera first
        foreach (var device in WebCamTexture.devices)
        {
            if (!device.isFrontFacing)
            {
                chosenDevice = device;
                break;
            }
        }

        // Fallback to front camera if rear is not available
        if (chosenDevice == null && WebCamTexture.devices.Length > 0)
        {
            chosenDevice = WebCamTexture.devices[0];
            Debug.LogWarning("Rear camera not found. Using front-facing camera instead.");
        }

        if (chosenDevice != null)
        {
            var texture = new WebCamTexture(chosenDevice.Value.name);
            image.texture = texture;
            texture.Play();
        }
        else
        {
            Debug.LogError("No camera available on this device.");
        }
    }

    void Update()
    {
        foreach (var group in UpdateQue.GroupBy((pair) => pair.index, (pair) => pair.matrix))
        {
            var floats = group.LastOrDefault((m) => m != null);
            var nameText = names[group.Key];
            nameText.gameObject.SetActive(floats != null);
            if (floats == null)
                continue;

            var matrix = new Matrix4x4();
            foreach (var i in Enumerable.Range(0, floats.Length))
                matrix[i] = floats[i];

            nameText.transform.localRotation = matrix.rotation;
            nameText.transform.localPosition = matrix.GetPosition() / 10;

            nameText.text = nameText.name + "\n" + matrix.GetPosition();
        }

        if (BarcodesQue.Count > 0)
            qrCodes.text = string.Join("\n", BarcodesQue.Select((code) => code));

        BarcodesQue.Clear();
        UpdateQue.Clear();
    }

#if UNITY_EDITOR
    static void SimulateAR()
    {
        // Simulate a marker and barcode after 2 seconds
        UnityEditor.EditorApplication.delayCall += () =>
        {
            UpdateQue.Add((0, new float[]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1
            }));

            BarcodesQue.Add("SIMULATED_BARCODE_001");
        };
    }
#endif
}
