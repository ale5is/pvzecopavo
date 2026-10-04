using System.Collections.Generic;
using UnityEngine;

public class OutChatTextGroup : MonoBehaviour
{
    public static OutChatTextGroup Instance;

    public GameObject ChatTextPrefab;

    private readonly List<ChatText> chatTexts = new List<ChatText>();

    private const int MaxMessages = 5;
    private const float MessageLifetime = 5f;

    private void Awake()
    {
        Instance = this;
    }

    public void InputContent(string content)
    {
        CreateChatText(content, Color.white);
    }

    public void InputContent(string content, Color32 color)
    {
        CreateChatText(content, color);
    }

    private void CreateChatText(string content, Color color)
    {
        if (ChatTextPrefab == null)
            return;

        GameObject instance =
            Instantiate(ChatTextPrefab, transform);

        ChatText chatText =
            instance.GetComponent<ChatText>();

        if (chatText == null)
        {
            Destroy(instance);
            return;
        }

        chatText.GetContent(content, color);

        chatTexts.Add(chatText);

        RemoveDestroyedMessages();

        while (chatTexts.Count > MaxMessages)
        {
            ChatText oldest = chatTexts[0];
            chatTexts.RemoveAt(0);

            if (oldest != null)
                Destroy(oldest.gameObject);
        }

        Destroy(chatText.gameObject, MessageLifetime);
    }

    private void RemoveDestroyedMessages()
    {
        for (int i = chatTexts.Count - 1; i >= 0; i--)
        {
            if (chatTexts[i] == null)
                chatTexts.RemoveAt(i);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < chatTexts.Count; i++)
        {
            if (chatTexts[i] != null)
                Destroy(chatTexts[i].gameObject);
        }

        chatTexts.Clear();
    }
}