using System.Collections.Generic;
using System.IO;
using SaveClass;
using UnityEngine;

public class ChooseSave : MonoBehaviour
{
    public static ChooseSave Instance;

    public GameObject SaveOption;
    public GameObject Content;
    public AddUser AddUser;
    public EditUser EditUser;
    public SaveOption SelectedSaveOption;
    public GameObject ShowCanvas;

    private readonly List<SaveOption> saveOptions =
        new List<SaveOption>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        HideUI();
    }

    private void HideUI()
    {
        if (ShowCanvas != null)
            ShowCanvas.SetActive(false);

        if (AddUser != null)
            AddUser.gameObject.SetActive(false);

        if (EditUser != null)
            EditUser.gameObject.SetActive(false);
    }

    public bool CheckNameRepeat(string Name)
    {
        for (int i = 0; i < saveOptions.Count; i++)
        {
            SaveOption option = saveOptions[i];

            if (option != null &&
                option.SaveNameText != null &&
                option.SaveNameText.text == Name)
            {
                return true;
            }
        }

        return false;
    }

    public void ShowEditUser()
    {
        if (SelectedSaveOption == null ||
            EditUser == null)
        {
            return;
        }

        if (ShowCanvas != null)
            ShowCanvas.SetActive(true);

        if (AddUser != null)
            AddUser.gameObject.SetActive(false);

        EditUser.gameObject.SetActive(true);

        if (EditUser.inputField != null &&
            SelectedSaveOption.userSave != null)
        {
            EditUser.inputField.text =
                SelectedSaveOption.userSave.playerName;
        }
    }

    public void ShowAddUser()
    {
        if (AddUser == null)
            return;

        if (ShowCanvas != null)
            ShowCanvas.SetActive(true);

        if (EditUser != null)
            EditUser.gameObject.SetActive(false);

        AddUser.Display(true);
    }

    public void ShowFirstAddUser()
    {
        ClearOptions();

        if (ShowCanvas != null)
            ShowCanvas.SetActive(true);

        if (EditUser != null)
            EditUser.gameObject.SetActive(false);

        if (AddUser != null)
            AddUser.Display(false);
    }

    public void LoadSave(UserSave saveUser, string path)
    {
        if (GameManager.Instance == null ||
            saveUser == null ||
            string.IsNullOrEmpty(path))
        {
            return;
        }

        GameManager.Instance.LoadSave(
            saveUser,
            path
        );

        Cancel();
    }

    public void Confirm()
    {
        if (SelectedSaveOption == null ||
            SelectedSaveOption.userSave == null ||
            SelectedSaveOption.Path == null)
        {
            return;
        }

        LoadSave(
            SelectedSaveOption.userSave,
            SelectedSaveOption.Path.FullName
        );
    }

    public void Cancel()
    {
        ClearOptions();
        HideUI();
    }

    private void ClearOptions()
    {
        for (int i = 0; i < saveOptions.Count; i++)
        {
            if (saveOptions[i] != null)
                Destroy(saveOptions[i].gameObject);
        }

        saveOptions.Clear();
        SelectedSaveOption = null;

        if (Content != null)
        {
            RectTransform rect =
                Content.GetComponent<RectTransform>();

            if (rect != null)
                rect.sizeDelta =
                    new Vector2(0f, 20f);
        }
    }

    public void LoadSavegroup()
    {
        if (ShowCanvas != null)
            ShowCanvas.SetActive(true);

        if (AddUser != null)
            AddUser.gameObject.SetActive(false);

        if (EditUser != null)
            EditUser.gameObject.SetActive(false);

        ClearOptions();

        if (GameManager.Instance == null)
        {
            ShowFirstAddUser();
            return;
        }

        string savePath =
            GameManager.Instance.SavePath;

        if (string.IsNullOrEmpty(savePath))
        {
            ShowFirstAddUser();
            return;
        }

        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
            ShowFirstAddUser();
            return;
        }

        if (SaveOption == null ||
            Content == null)
        {
            return;
        }

        DirectoryInfo[] directories =
            new DirectoryInfo(savePath).GetDirectories();

        for (int i = 0; i < directories.Length; i++)
        {
            DirectoryInfo directory =
                directories[i];

            string file =
                Path.Combine(
                    directory.FullName,
                    FixedInfo.PlayerInfoName
                );

            if (!File.Exists(file))
                continue;

            try
            {
                string json =
                    GameManager.Decrypt(
                        File.ReadAllText(file)
                    );

                UserSave saveUser =
                    JsonUtility.FromJson<UserSave>(json);

                if (saveUser == null)
                    continue;

                SaveOption component =
                    Instantiate(SaveOption)
                        .GetComponent<SaveOption>();

                if (component == null)
                    continue;

                component.GetSaveinfo(
                    saveUser,
                    directory
                );

                component.transform.SetParent(
                    Content.transform,
                    false
                );

                component.gameObject.SetActive(true);

                saveOptions.Add(component);
            }
            catch
            {
                continue;
            }
        }

        if (Content != null)
        {
            RectTransform rect =
                Content.GetComponent<RectTransform>();

            if (rect != null)
            {
                rect.sizeDelta =
                    new Vector2(
                        0f,
                        45f * saveOptions.Count
                    );
            }
        }

        if (saveOptions.Count == 0)
        {
            ShowFirstAddUser();
            return;
        }

        if (GameManager.Instance.LocalPlayerSave != null)
        {
            for (int i = 0; i < saveOptions.Count; i++)
            {
                SaveOption option =
                    saveOptions[i];

                if (option == null ||
                    option.userSave == null)
                {
                    continue;
                }

                if (option.userSave.playerName ==
                    GameManager.Instance.LocalPlayerSave.playerName)
                {
                    SelectedSaveOption = option;

                    option.OnPointerClick(null);
                    option.transform.SetSiblingIndex(0);

                    return;
                }
            }
        }

        SelectedSaveOption = saveOptions[0];

        SelectedSaveOption.OnPointerClick(null);
        SelectedSaveOption.transform.SetSiblingIndex(0);
    }

    public void RenewSelect(SaveOption saveOption)
    {
        if (saveOption == null)
            return;

        SelectedSaveOption = saveOption;

        for (int i = 0; i < saveOptions.Count; i++)
        {
            if (saveOptions[i] != null)
                saveOptions[i].RenewSelect();
        }
    }
}