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

    // 👇 Anchor to attach UI to tracked image
    public GameObject imageTargetAnchor;

    void Start()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        WebCamDevice? rearCamera = null;
        WebCamDevice? frontCamera = null;

        foreach (var device in devices)
        {
            if (device.isFrontFacing)
                frontCamera = device;
            else
                rearCamera = device;
        }

        WebCamDevice? chosenDevice = rearCamera ?? frontCamera;

        if (chosenDevice.HasValue)
        {
            var texture = new WebCamTexture(chosenDevice.Value.name);
            image.texture = texture;
            texture.Play();

            Debug.Log(rearCamera.HasValue && chosenDevice.Value.name == rearCamera.Value.name
                ? "Rear camera found and selected."
                : "Rear camera not found. Using front-facing camera instead.");
        }
        else
        {
            Debug.LogError("No camera available on this device.");
        }

        if (imageTargetAnchor != null)
            imageTargetAnchor.SetActive(false);
    }

    void Update()
    {
        bool targetFound = false;

        foreach (var group in UpdateQue.GroupBy(pair => pair.index, pair => pair.matrix))
        {
            var floats = group.LastOrDefault(m => m != null);
            if (group.Key == 0 && floats != null)
            {
                targetFound = true;

                var matrix = new Matrix4x4();
                for (int i = 0; i < floats.Length; i++)
                    matrix[i] = floats[i];

                // Update anchor position/rotation
                if (imageTargetAnchor != null)
                {
                    imageTargetAnchor.SetActive(true);
                    imageTargetAnchor.transform.localPosition = Vector3.Lerp(
                        imageTargetAnchor.transform.localPosition,
                        matrix.GetPosition() / 10f,
                        Time.deltaTime * 10f
                    );
                    imageTargetAnchor.transform.localRotation = Quaternion.Slerp(
                        imageTargetAnchor.transform.localRotation,
                        matrix.rotation,
                        Time.deltaTime * 10f
                    );
                }

                // Optional name text positioning
                if (names.Length > group.Key)
                {
                    var nameText = names[group.Key];
                    nameText.gameObject.SetActive(true);
                    nameText.transform.localPosition = matrix.GetPosition() / 10f;
                    nameText.transform.localRotation = matrix.rotation;
                    nameText.text = nameText.name + "\n" + matrix.GetPosition();
                }
            }
        }

        if (!targetFound && imageTargetAnchor != null)
            imageTargetAnchor.SetActive(false);

        // Barcode display
        if (BarcodesQue.Count > 0)
            qrCodes.text = string.Join("\n", BarcodesQue.Select(code => code));

        BarcodesQue.Clear();
        UpdateQue.Clear();
    }

#if UNITY_EDITOR
    static void SimulateAR()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            // Simulate image tracking after 2 seconds
            UpdateQue.Add((0, new float[]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1
            }));

            // Simulate QR code scan
            BarcodesQue.Add("https://your-site.com");
        };
    }
#endif
}
