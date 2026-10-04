using System.Collections;
using System.IO;
using SaveClass;
using UnityEngine;
using UnityEngine.UI;

public class AddUser : MonoBehaviour
{
    public InputField inputField;

    public Text Errortext;

    private Coroutine ErrortextCoroutine;

    private bool CanCancel;

    private void Start()
    {
        if (Errortext != null)
            Errortext.gameObject.SetActive(false);
    }

    public void Display(bool canCancel)
    {
        CanCancel = canCancel;

        if (ChooseSave.Instance != null)
            ChooseSave.Instance.gameObject.SetActive(true);

        gameObject.SetActive(true);

        if (Errortext != null)
            Errortext.gameObject.SetActive(false);
    }

    public void Confirm()
    {
        if (GameManager.Instance == null ||
            ChooseSave.Instance == null ||
            inputField == null)
        {
            return;
        }

        string playerName =
            inputField.text.Trim();

        if (string.IsNullOrEmpty(playerName))
            return;

        if (ChooseSave.Instance.CheckNameRepeat(
            playerName))
        {
            if (ErrortextCoroutine != null)
                StopCoroutine(ErrortextCoroutine);

            ErrortextCoroutine =
                StartCoroutine(RepeatLog());

            return;
        }

        if (string.IsNullOrEmpty(
            GameManager.Instance.SavePath))
        {
            return;
        }

        if (!Directory.Exists(
            GameManager.Instance.SavePath))
        {
            Directory.CreateDirectory(
                GameManager.Instance.SavePath
            );
        }

        string safeName =
            playerName;

        foreach (char invalidChar in
            Path.GetInvalidFileNameChars())
        {
            safeName =
                safeName.Replace(
                    invalidChar.ToString(),
                    "_"
                );
        }

        string path =
            Path.Combine(
                GameManager.Instance.SavePath,
                safeName
            );

        while (Directory.Exists(path))
        {
            path += "smf";
        }

        Directory.CreateDirectory(path);

        UserSave userSave =
            new UserSave();

        userSave.playerName =
            playerName;

        string data =
            JsonUtility.ToJson(userSave);

        string file =
            Path.Combine(
                path,
                FixedInfo.PlayerInfoName
            );

        File.WriteAllText(
            file,
            GameManager.Encrypt(data)
        );

        inputField.text = "";

        CanCancel = true;

        ChooseSave.Instance.LoadSave(
            userSave,
            path
        );

        gameObject.SetActive(false);
    }

    public void Cancel()
    {
        if (CanCancel)
        {
            if (ErrortextCoroutine != null)
                StopCoroutine(ErrortextCoroutine);

            if (Errortext != null)
                Errortext.gameObject.SetActive(false);

            if (inputField != null)
                inputField.text = "";

            gameObject.SetActive(false);

            if (ChooseSave.Instance != null)
                ChooseSave.Instance.gameObject.SetActive(true);
        }
        else
        {
            if (AudioManager.Instance != null &&
                GameManager.Instance != null &&
                GameManager.Instance.AudioConf != null)
            {
                AudioManager.Instance.PlayEFAudio(
                    GameManager.Instance.AudioConf.Buzzer,
                    transform.position,
                    isAll: true
                );
            }
        }
    }

    private IEnumerator RepeatLog()
    {
        if (AudioManager.Instance != null &&
            GameManager.Instance != null &&
            GameManager.Instance.AudioConf != null)
        {
            AudioManager.Instance.PlayEFAudio(
                GameManager.Instance.AudioConf.Buzzer,
                transform.position,
                isAll: true
            );
        }

        if (Errortext != null)
            Errortext.gameObject.SetActive(true);

        yield return new WaitForSeconds(3f);

        if (Errortext != null)
            Errortext.gameObject.SetActive(false);
    }
}