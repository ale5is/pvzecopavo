using System.Collections.Generic;
using UnityEngine;

public class ChatTextGroup : MonoBehaviour
{
    public static ChatTextGroup Instance;

    public RectTransform rectTransform;
    public GameObject ChatTextPrefab;

    private readonly List<ChatText> chatTexts =
        new List<ChatText>();

    private const int MaxMessages = 20;
    private const float BaseHeight = 400f;
    private const float LineHeight = 50f;

    private int scrollNum;
    private int scroll;

    private void Awake()
    {
        Instance = this;

        if (rectTransform == null)
            rectTransform = transform as RectTransform;
    }

    private void Update()
    {
        if (scroll <= 0)
            return;

        float wheel =
            Input.GetAxis("Mouse ScrollWheel");

        if (wheel > 0f)
            SetScroll(scrollNum + 1);
        else if (wheel < 0f)
            SetScroll(scrollNum - 1);
    }

    public void InputContent(string content)
    {
        CreateChatText(content, Color.white);
    }

    public void InputContent(
        string content,
        Color32 color)
    {
        CreateChatText(content, color);
    }

    private void CreateChatText(
        string content,
        Color color)
    {
        if (ChatTextPrefab == null)
            return;

        ChatText component =
            Instantiate(ChatTextPrefab)
                .GetComponent<ChatText>();

        if (component == null)
        {
            Destroy(component != null
                ? component.gameObject
                : null);

            return;
        }

        component.transform.SetParent(
            transform,
            false
        );

        component.GetContent(content, color);

        chatTexts.Add(component);

        TrimMessages();
        UpdateLayout();
    }

    private void TrimMessages()
    {
        while (chatTexts.Count > MaxMessages)
        {
            ChatText oldest = chatTexts[0];

            chatTexts.RemoveAt(0);

            if (oldest != null)
                Destroy(oldest.gameObject);
        }
    }

    private void UpdateLayout()
    {
        if (rectTransform == null)
            return;

        Canvas.ForceUpdateCanvases();

        float totalHeight = 0f;

        for (int i = 0; i < chatTexts.Count; i++)
        {
            ChatText chat = chatTexts[i];

            if (chat == null ||
                chat.rectTransform == null)
            {
                continue;
            }

            totalHeight +=
                Mathf.Max(
                    LineHeight,
                    chat.rectTransform.rect.height
                );
        }

        rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            Mathf.Max(
                BaseHeight,
                totalHeight
            )
        );

        scroll = Mathf.Max(
            0,
            Mathf.CeilToInt(
                (totalHeight - BaseHeight) /
                LineHeight
            )
        );

        if (scrollNum > scroll)
            scrollNum = scroll;

        ApplyScrollPosition();
    }

    private void SetScroll(int value)
    {
        scrollNum =
            Mathf.Clamp(
                value,
                0,
                scroll
            );

        ApplyScrollPosition();
    }

    private void ApplyScrollPosition()
    {
        if (rectTransform == null)
            return;

        Vector3 position =
            rectTransform.localPosition;

        position.y =
            -225f -
            LineHeight * scrollNum;

        rectTransform.localPosition = position;
    }

    public void Clear()
    {
        for (int i = 0; i < chatTexts.Count; i++)
        {
            if (chatTexts[i] != null)
                Destroy(chatTexts[i].gameObject);
        }

        chatTexts.Clear();

        scroll = 0;
        scrollNum = 0;

        if (rectTransform != null)
        {
            rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                BaseHeight
            );

            Vector3 position =
                rectTransform.localPosition;

            position.y = -225f;

            rectTransform.localPosition =
                position;
        }
    }
}