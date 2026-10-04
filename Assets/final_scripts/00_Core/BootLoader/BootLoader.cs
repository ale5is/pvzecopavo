using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BootLoader : MonoBehaviour
{
    [SerializeField]
    private string firstScene = "level0";

    [SerializeField]
    private Slider progressSlider;

    [SerializeField]
    private TextMeshProUGUI progressText;

    [SerializeField]
    private float progressSpeed = 0.5f;

    [SerializeField]
    private float finalDelay = 0.25f;

    private float displayedProgress;

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);

        displayedProgress = 0f;
        SetProgress(0f);

        AsyncOperation operation =
            SceneManager.LoadSceneAsync(firstScene);

        if (operation == null)
            yield break;

        operation.allowSceneActivation = true;

        while (!operation.isDone)
        {
            float sceneProgress =
                Mathf.Clamp01(operation.progress);

            float targetProgress =
                sceneProgress * 0.8f;

            while (displayedProgress < targetProgress)
            {
                displayedProgress = Mathf.MoveTowards(
                    displayedProgress,
                    targetProgress,
                    progressSpeed * Time.deltaTime
                );

                SetProgress(displayedProgress);

                yield return null;
            }

            yield return null;
        }

        while (displayedProgress < 0.8f)
        {
            displayedProgress = Mathf.MoveTowards(
                displayedProgress,
                0.8f,
                progressSpeed * Time.deltaTime
            );

            SetProgress(displayedProgress);

            yield return null;
        }

        while (GameManager.Instance == null)
        {
            yield return null;
        }

        while (!GameManager.Instance.IsStartupReady)
        {
            float startupProgress =
                Mathf.Clamp01(
                    GameManager.Instance.StartupProgress
                );

            float targetProgress =
                0.8f + startupProgress * 0.2f;

            while (displayedProgress < targetProgress)
            {
                displayedProgress = Mathf.MoveTowards(
                    displayedProgress,
                    targetProgress,
                    progressSpeed * Time.deltaTime
                );

                SetProgress(displayedProgress);

                yield return null;
            }

            yield return null;
        }

        while (displayedProgress < 1f)
        {
            displayedProgress = Mathf.MoveTowards(
                displayedProgress,
                1f,
                progressSpeed * Time.deltaTime
            );

            SetProgress(displayedProgress);

            yield return null;
        }

        SetProgress(1f);

        yield return new WaitForSeconds(finalDelay);

        Destroy(gameObject);
    }

    private void SetProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (progressSlider != null)
            progressSlider.value = progress;

        if (progressText != null)
            progressText.text =
                Mathf.RoundToInt(progress * 100f) + "%";
    }
}