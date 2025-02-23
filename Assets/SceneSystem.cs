using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneSystem : MonoBehaviour
{
    public Image loadingScreen;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GameScene") // Verifică dacă scena încărcată este "GameScene"
        {
            // Apelul metodei pe care dorești să o rulezi când se încarcă "GameScene"
            int slotNumber = CheckAndGetLoadedGame();
            if (slotNumber != -1)
            {
                //SaveManager.Instance.LoadGame();
                ActivateLoadingScreen();
                StartCoroutine(DelayedLoading(slotNumber));
            }
        }
    }
    private IEnumerator DelayedLoading(int slotNumber)
    {
        print("DelayedLoading1");
        yield return new WaitForSeconds(2.0f);
        print("DelayedLoading2");
        SaveManager.Instance.LoadGame(slotNumber);
        DisableLoadingScreen();

    }


    private int CheckAndGetLoadedGame()//get slot number , -1 is empty
    {
        // Verifică dacă există cheia "GameSceneLoad" în PlayerPrefs
        if (PlayerPrefs.HasKey("GameSceneLoad"))
        {
            int loadedGame = PlayerPrefs.GetInt("GameSceneLoad");

            if (loadedGame > 0)
            {
                // Șterge cheia "GameSceneLoad" din PlayerPrefs
                PlayerPrefs.SetInt("GameSceneLoad",-1);
                //PlayerPrefs.DeleteKey("GameSceneLoad");
                PlayerPrefs.Save(); 
                Debug.Log("Valoarea 'GameSceneLoad' a fost ștearsă din PlayerPrefs." + loadedGame);
                return loadedGame;
            }
        }
        return -1;
    }
    #region || ------ Loading Section ------||
    private void ActivateLoadingScreen()
    {
        loadingScreen.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        //music for loading screen

        //animation
    }
    private void DisableLoadingScreen()
    {
        loadingScreen.gameObject.SetActive(false);
    }

    #endregion

}
