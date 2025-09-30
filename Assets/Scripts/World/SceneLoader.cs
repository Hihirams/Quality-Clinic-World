using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [Header("Fade")]
    public CanvasGroup fade;   // asigna ScreenFader (CanvasGroup)
    public float fadeDur = 0.35f;

    bool busy;

    public void LoadAdditiveAndSwap(string sceneToLoad, string sceneToUnload = null)
    {
        if (!busy) StartCoroutine(C_Load(sceneToLoad, sceneToUnload));
    }

    IEnumerator C_Load(string addScene, string unloadScene)
    {
        busy = true;

        // Fade in
        yield return StartCoroutine(FadeTo(1f, fadeDur));

        // Cargar aditiva
        var op = SceneManager.LoadSceneAsync(addScene, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;

        // Activar nueva
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(addScene));

        // Descargar la anterior (opcional)
        if (!string.IsNullOrEmpty(unloadScene))
        {
            var prev = SceneManager.GetSceneByName(unloadScene);
            if (prev.IsValid())
            {
                var uop = SceneManager.UnloadSceneAsync(prev);
                while (uop != null && !uop.isDone) yield return null;
            }
        }

        // Fade out
        yield return StartCoroutine(FadeTo(0f, fadeDur));
        busy = false;
    }

    IEnumerator FadeTo(float target, float dur)
    {
        if (!fade) yield break;
        float start = fade.alpha, t = 0f;
        fade.blocksRaycasts = true;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            fade.alpha = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }
        fade.alpha = target;
        fade.blocksRaycasts = target > 0.5f;
    }
}
