using UnityEngine;

public class OpenURLButton : MonoBehaviour
{
    // Set the link in Inspector or hardcode it
    public string url = "https://example.com";

    public void OpenLink()
    {
        Application.OpenURL(url);
    }
}
