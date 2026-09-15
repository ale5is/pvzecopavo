using UnityEngine;
using UnityEngine.UI;

public class ChatText : MonoBehaviour
{
    public RectTransform rectTransform;
    public Text text;

    public void GetContent(string content)
    {
        if (text == null || rectTransform == null)
            return;

        text.text = content;

        Canvas.ForceUpdateCanvases();

        float height = Mathf.Max(
            50f,
            text.preferredHeight + 6f
        );

        rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            height
        );

        gameObject.SetActive(true);
    }

    public void GetContent(string content, Color32 color)
    {
        if (text == null || rectTransform == null)
            return;

        text.text = content;
        text.color = color;

        Canvas.ForceUpdateCanvases();

        float height = Mathf.Max(
            50f,
            text.preferredHeight + 6f
        );

        rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            height
        );

        gameObject.SetActive(true);
    }
}