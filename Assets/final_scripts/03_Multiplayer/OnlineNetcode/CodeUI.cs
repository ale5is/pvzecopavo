using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CodeUI : MonoBehaviour
{
    [SerializeField] private TMP_Text codeText;
    [SerializeField] private Button copyButton;
    [SerializeField] private string copiedText = "Copied";
    [SerializeField] private float copiedDuration = 1.5f;

    private string currentCode = "";
    private Coroutine restoreCoroutine;

    private void Awake()
    {
        if (copyButton == null)
            copyButton = GetComponent<Button>();

        if (copyButton != null)
            copyButton.onClick.AddListener(CopyCode);
    }

    private void OnDestroy()
    {
        if (copyButton != null)
            copyButton.onClick.RemoveListener(CopyCode);
    }

    public void Show(
        string code)
    {
        currentCode =
            (code ?? "").Trim();

        if (codeText != null)
            codeText.text =
                currentCode;
    }

    public void CopyCode()
    {
        if (codeText == null)
            return;

        string code =
            currentCode;

        if (string.IsNullOrEmpty(code))
            code =
                codeText.text.Trim();

        if (string.IsNullOrEmpty(code))
            return;

        GUIUtility.systemCopyBuffer =
            code;

        if (restoreCoroutine != null)
        {
            StopCoroutine(
                restoreCoroutine);
        }

        codeText.text =
            copiedText;

        restoreCoroutine =
            StartCoroutine(
                RestoreCode(
                    code));
    }

    private IEnumerator RestoreCode(
        string code)
    {
        yield return new WaitForSeconds(
            copiedDuration);

        if (codeText != null)
            codeText.text =
                code;

        currentCode =
            code;

        restoreCoroutine = null;
    }
}