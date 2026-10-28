using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.SceneManagement;


public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; set; }

    // how many save slots the Save Game / Load Game screens show (see SaveSlotList)
    public const int SlotCount = 10;

    // the picture of the game shown on each save slot (taken when saving, without the menus)
    const int ThumbnailWidth = 400;
    const int ThumbnailHeight = 225;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
        SetSavePaths(); // also here, so the save screens can ask about the slots before Start
    }
    //json project save path
    string jsonPathProject;
    //json external/real save path
    string jsonPathPersistant;
    //binary save path
    string binaryPath;

    string fileName = "SaveGame";
    //encryption/decryption key secret
    string keyEncription = "1234567";


    private bool isSavingToJson = true;
    public bool isLoading;
    private void Start()
    {
        SetSavePaths();
    }

    private void SetSavePaths()
    {
        // In the editor saves stay in Assets/. In a build (phone) the game folder is read-only, so use the app's save folder.
        jsonPathProject = (Application.isEditor ? Application.dataPath : Application.persistentDataPath) + Path.AltDirectorySeparatorChar;
        jsonPathPersistant = Application.persistentDataPath + Path.AltDirectorySeparatorChar;
        binaryPath = Application.persistentDataPath + Path.AltDirectorySeparatorChar;
    }
    #region ||  ---- General Section ----- ||
    #region ||  ---- Saving ----- ||
    public void SaveGame(int slotNumber)
    {
        AllGameData data = new AllGameData();

        data.playerData = GetPlayerData();

        data.enviromentData = GetEnviromentData();

        SavingTypeSwitch(data, slotNumber);

        SaveThumbnail(slotNumber);

        // the name and date shown on the slot
        string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        PlayerPrefs.SetString("Slot" + slotNumber + "Description", "Saved Game" + slotNumber + " | " + time);
        PlayerPrefs.Save();
    }

    #region ||  ---- Slot picture (thumbnail) ----- ||
    // The picture is a JPG next to the save file: SaveGame1.json -> SaveGame1.jpg
    public string ThumbnailPath(int slotNumber)
    {
        if (string.IsNullOrEmpty(jsonPathProject)) SetSavePaths();
        return jsonPathProject + fileName + slotNumber + ".jpg";
    }

    // Takes a small picture of what the player sees, without the menus, and stores it with the save.
    // A failure here only means the slot shows no picture: the save itself is already written.
    private void SaveThumbnail(int slotNumber)
    {
        Texture2D picture = null;
        try
        {
            picture = CaptureGameView(ThumbnailWidth, ThumbnailHeight);
            if (picture != null)
            {
                File.WriteAllBytes(ThumbnailPath(slotNumber), picture.EncodeToJPG(85));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Could not save the picture of save slot " + slotNumber + ": " + e.Message);
        }
        finally
        {
            if (picture != null) Destroy(picture);
        }
    }

    // Renders the game camera into a texture. The menus are Screen Space Overlay canvases, which a
    // camera never draws into a texture, so the picture shows only the game world.
    public static Texture2D CaptureGameView(int width, int height)
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        // drawn at twice the size and then halved: smoother edges
        var big = RenderTexture.GetTemporary(width * 2, height * 2, 24, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
        var small = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
        big.filterMode = FilterMode.Bilinear;

        // canvases drawn by this camera itself (Screen Space - Camera) would show up: hide them meanwhile
        var hiddenCanvases = new List<Canvas>();
        foreach (var canvas in FindObjectsOfType<Canvas>())
        {
            if (canvas.enabled && canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == cam)
            {
                canvas.enabled = false;
                hiddenCanvases.Add(canvas);
            }
        }

        RenderTexture oldTarget = cam.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        try
        {
            cam.targetTexture = big;
            cam.aspect = (float)width / height; // the middle of the view, at the slot picture's shape
            cam.Render();
        }
        finally
        {
            cam.targetTexture = oldTarget;
            cam.ResetAspect();
            foreach (var canvas in hiddenCanvases) canvas.enabled = true;
        }

        Graphics.Blit(big, small);
        RenderTexture.active = small;
        var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
        picture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
        picture.Apply(false);
        RenderTexture.active = oldActive;

        RenderTexture.ReleaseTemporary(big);
        RenderTexture.ReleaseTemporary(small);
        return picture;
    }

    // "2026-10-21 22:12": from the slot's description, or else the time of the save file
    public string GetSlotDate(int slotNumber)
    {
        string description = PlayerPrefs.GetString("Slot" + slotNumber + "Description");
        int bar = description.IndexOf('|');
        if (bar >= 0 && bar + 1 < description.Length)
        {
            return description.Substring(bar + 1).Trim();
        }
        try
        {
            string path = jsonPathProject + fileName + slotNumber + (isSavingToJson ? ".json" : ".bin");
            if (File.Exists(path)) return File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm");
        }
        catch (Exception) { }
        return "";
    }
    #endregion

    private EnviromentData GetEnviromentData()
    {
        List<string> itemsPickedup = InventorySystem.Instance.itemsPickedup;

        //get all choppable trees and stumps (the scenery trees, like the firs, are not saved: they never change)
        List<TreeData> treesToSave = new List<TreeData>();
        var trees = new List<Transform>();
        var stumps = new List<Transform>();
        FindSavedTrees(EnviromentManager.Instance.allTrees.transform, true, trees, stumps);
        foreach(Transform tree in trees)
        {
            treesToSave.Add(NewTreeData("Tree_Parent", tree)); // This needs to be same as preafab name
        }
        foreach(Transform stump in stumps)
        {
            treesToSave.Add(NewTreeData("Stump", stump)); // This needs to be same as preafab name
        }
        //get all animals
        List<string> allAnimals = new List<string>();
        foreach(Transform animalType in EnviromentManager.Instance.allAnimals.transform)// rabbits, wolfs
        {
            foreach(Transform animal in animalType.transform)
            {
                allAnimals.Add(animal.gameObject.name);
            }
        }
        // get all information about storage box
        List<StorageData> allStorage = new List<StorageData>();
        foreach (Transform placeable in EnviromentManager.Instance.placeables.transform)
        {
            if (placeable.gameObject.GetComponent<StorageBox>())
            {
                var sd = new StorageData();
                sd.items = placeable.gameObject.GetComponent<StorageBox>().items;
                sd.position = placeable.position;
                sd.rotation = new Vector3(placeable.rotation.x, placeable.rotation.y, placeable.rotation.z);

                allStorage.Add(sd);
            }
        }

        var enviromentData = new EnviromentData(itemsPickedup, treesToSave, allAnimals, allStorage);
        if (Forest.Instance != null) enviromentData.pineForest = Forest.Instance.GetData();
        if (Vegetation.Instance != null) enviromentData.vegetation = Vegetation.Instance.GetData();
        if (RabbitPopulation.Instance != null) enviromentData.rabbits = RabbitPopulation.Instance.GetData();
        if (StoryManager.Instance != null) enviromentData.story = StoryManager.Instance.GetData();
        if (FreshWater.Instance != null) enviromentData.puddles = FreshWater.Instance.GetData();
        enviromentData.dropped = DroppedItems.GetData();
        enviromentData.buildings = Buildings.GetData();
        enviromentData.snares = RabbitSnare.GetData();
        enviromentData.campfires = new List<CampfireData>();
        foreach (Transform placeable in EnviromentManager.Instance.placeables.transform)
        {
            var fire = placeable.GetComponent<Campfire>();
            if (fire == null) continue;
            enviromentData.campfires.Add(new CampfireData
            {
                position = placeable.position,
                rotation = placeable.eulerAngles,
                fuelMinutes = fire.fuelMinutes,
                burnedMinutes = fire.burnedMinutes,
            });
        }
        return enviromentData;
    }

    // The trees a save can rebuild: the choppable trees (Tree_Parent) and the stumps left after chopping.
    // Everything else under Trees is scenery (the firs, the Birch_forest groups): it is never saved or removed.
    // withGroups: also look inside the scenery groups (the Birch_forest trees), not only at the direct children.
    static void FindSavedTrees(Transform parent, bool withGroups, List<Transform> trees, List<Transform> stumps)
    {
        foreach(Transform child in parent)
        {
            if (IsStump(child))
            {
                stumps.Add(child);
            }
            else if (IsChoppableTree(child))
            {
                trees.Add(child);
            }
            else if (withGroups)
            {
                FindSavedTrees(child, true, trees, stumps);
            }
        }
    }

    // a Tree_Parent: its ChoppableTree sits two levels down (tree_base -> birch_tree -> Tree_Parent)
    static bool IsChoppableTree(Transform t)
    {
        var choppable = t.GetComponentInChildren<ChoppableTree>(true);
        return choppable != null && choppable.transform.parent != null && choppable.transform.parent.parent == t;
    }

    // what is left after chopping (ChoppedTree) or a stump loaded from a save
    static bool IsStump(Transform t)
    {
        return t.name.StartsWith("ChoppedTree") || t.name.StartsWith("Stump");
    }

    static TreeData NewTreeData(string prefabName, Transform tree)
    {
        var td = new TreeData();
        td.name = prefabName;
        td.position = tree.position;
        td.rotation = new Vector3(tree.rotation.x, tree.rotation.y, tree.rotation.z);
        return td;
    }

    // Old saves also stored the scenery that sat directly under Trees, at its own position.
    static bool IsSceneryPosition(Transform allTrees, Vector3 position)
    {
        foreach(Transform child in allTrees)
        {
            if (IsStump(child) || IsChoppableTree(child)) continue;
            if (Vector3.Distance(child.position, position) < 0.05f) return true;
        }
        return false;
    }

    private PlayerData GetPlayerData()
    {
        float[] playerStats = new float[3];
        playerStats[0] = PlayerState.Instance.currentHealth;
        playerStats[1] = PlayerState.Instance.currentCalories;
        playerStats[2] = PlayerState.Instance.currentHydrationPercent;

        float[] playerPosAndRot = new float[6];
        playerPosAndRot[0] = PlayerState.Instance.playerBody.transform.position.x;
        playerPosAndRot[1] = PlayerState.Instance.playerBody.transform.position.y;
        playerPosAndRot[2] = PlayerState.Instance.playerBody.transform.position.z;

        playerPosAndRot[3] = PlayerState.Instance.playerBody.transform.rotation.x;
        playerPosAndRot[4] = PlayerState.Instance.playerBody.transform.rotation.y;
        playerPosAndRot[5] = PlayerState.Instance.playerBody.transform.rotation.z;

        string[] inventory = InventorySystem.Instance.itemList.ToArray();
        string[] quickSlots = GetQuickSlotsContent();

        return new PlayerData(playerStats, playerPosAndRot, inventory, quickSlots);

    }
    private string[] GetQuickSlotsContent()
    {
        List<string> temp = new List<string>();
        foreach(GameObject slot in EquipSystem.Instance.quickSlotsList)
        {
            if(slot.transform.childCount != 0)
            {
                string name = slot.transform.GetChild(0).name;
                string strClone = "(Clone)";
                string cleanName = name.Replace(strClone, "");
                temp.Add(cleanName);
            }
        }
        return temp.ToArray();


    }



    public void SavingTypeSwitch(AllGameData gameData, int slotNumber)
    {
        if (isSavingToJson)
        {
            SaveGameDataToJsonFile(gameData,slotNumber);
        }
        else
        {
            SaveGameDataToBinaryFile(gameData, slotNumber);
        }
    }
    #endregion
    #region ||  ---- Loading (get data from files or others DB) ----- ||
    public AllGameData GetDataSwitch(int slotNumber)
    {
        if (isSavingToJson)
        {
            AllGameData gameData = GetGameDataToJsonFile(slotNumber);
            return gameData;
        }
        else
        {
            AllGameData gameData = GetGameDataToBinaryFile(slotNumber);
            Debug.Log(gameData);
            return gameData;
        }
    }
    public void LoadGame(int slotNumber)
    {
        print("load game");
        //Player data
        SetPlayerData(GetDataSwitch(slotNumber).playerData);

        //Enviroment data
        SetEnviromentData(GetDataSwitch(slotNumber).enviromentData);

        isLoading = false;
    }

    private void SetEnviromentData(EnviromentData enviromentData)
    {
        // pick up items
        foreach(Transform itemType in EnviromentManager.Instance.allItems.transform)
        {
            foreach(Transform item in itemType.transform)
            {
                if (enviromentData.pickedupItems.Contains(item.name))
                {
                    Destroy(item.gameObject);
                }
            }
        }

        InventorySystem.Instance.itemsPickedup = enviromentData.pickedupItems;

        //tress
        // remove the default choppable trees and stumps (the scenery, like the firs, stays).
        // An old save never stored the trees inside the Birch_forest groups, so for it they stay as well.
        Transform allTrees = EnviromentManager.Instance.allTrees.transform;
        bool oldSave = enviromentData.treeSaveVersion < 1;
        var trees = new List<Transform>();
        var stumps = new List<Transform>();
        FindSavedTrees(allTrees, !oldSave, trees, stumps);
        foreach(Transform tree in trees)
        {
            Destroy(tree.gameObject);
        }
        foreach(Transform stump in stumps)
        {
            Destroy(stump.gameObject);
        }
        //add trees and stumps
        foreach(TreeData tree in enviromentData.treeData)
        {
            // an old save turned every fir into a "Tree_Parent" and every group into a "Stump": skip those
            if (oldSave && IsSceneryPosition(allTrees, tree.position)) continue;

            var treePrefab = Instantiate(Resources.Load<GameObject>(tree.name),
                new Vector3(tree.position.x, tree.position.y, tree.position.z),
                Quaternion.Euler(tree.rotation.x, tree.rotation.y, tree.rotation.z));
            treePrefab.transform.SetParent(EnviromentManager.Instance.allTrees.transform);
        }
        // the pine forest as it was saved (a save from before the pine forest keeps the new one)
        if (Forest.Instance != null) Forest.Instance.SetData(enviromentData.pineForest);
        if (Vegetation.Instance != null) Vegetation.Instance.SetData(enviromentData.vegetation);
        if (StoryManager.Instance != null) StoryManager.Instance.SetData(enviromentData.story);
        if (FreshWater.Instance != null) FreshWater.Instance.SetData(enviromentData.puddles);
        DroppedItems.SetData(enviromentData.dropped);

        // the campfires (before, a loaded game had none: they were never saved)
        if (enviromentData.campfires != null)
        {
            foreach (var saved in enviromentData.campfires)
            {
                var prefab = Resources.Load<GameObject>("CampfireModel");
                if (prefab == null) break;
                var fireObject = Instantiate(prefab, saved.position, Quaternion.Euler(saved.rotation));
                fireObject.transform.SetParent(EnviromentManager.Instance.placeables.transform, true);
                var placeable = fireObject.GetComponent<PlacebleItem>();
                if (placeable != null) { placeable.SetDefaultColor(); placeable.enabled = false; }
                var fire = fireObject.GetComponent<Campfire>();
                if (fire != null) { fire.fuelMinutes = saved.fuelMinutes; fire.burnedMinutes = saved.burnedMinutes; }
            }
        }

        // the rabbits: a newer save has all of them (RabbitPopulation); an old one only their names
        bool rabbitsSaved = enviromentData.rabbits != null && enviromentData.rabbits.version >= 1;
        // the buildings first (the rabbits of a pen stand on its floor), then the rabbits, then the snares (they take
        // their caught rabbits back)
        Buildings.SetData(enviromentData.buildings);
        if (RabbitPopulation.Instance != null) RabbitPopulation.Instance.SetData(enviromentData.rabbits);
        RabbitSnare.SetData(enviromentData.snares);

        // destroy animals that should not exist 
        List<string> allAnimals = new List<string>();
        if (!rabbitsSaved && RabbitPopulation.Instance == null)
        foreach (Transform animalType in EnviromentManager.Instance.allAnimals.transform)// rabbits, wolfs
        {
            foreach (Transform animal in animalType.transform)
            {
                if(enviromentData.animals.Contains(animal.gameObject.name) == false)
                {
                    Destroy(animal.gameObject);
                }
            }
        }

        //add storage box
        foreach(StorageData storage in enviromentData.storage)
        {
            var storageBoxPrefab = Instantiate(Resources.Load<GameObject>("StorageBoxModel"),
                new Vector3(storage.position.x, storage.position.y, storage.position.z),
                Quaternion.Euler(storage.rotation.x, storage.rotation.y, storage.rotation.z));
            storageBoxPrefab.GetComponent<StorageBox>().items = storage.items;

            storageBoxPrefab.transform.SetParent(EnviromentManager.Instance.placeables.transform);
        }

    }

    private void SetPlayerData(PlayerData playerData)
    {
        //settings player stats
        /*
        PlayerState.Instance.currentHealth = playerData.playerStats[0];
        PlayerState.Instance.currentCalories = playerData.playerStats[1];
        PlayerState.Instance.currentHydrationPercent = playerData.playerStats[2];
        */
        PlayerState.Instance.setHealth(playerData.playerStats[0]);
        PlayerState.Instance.setCalories(playerData.playerStats[1]);
        PlayerState.Instance.setHydration(playerData.playerStats[2]);


        //settings player position
        Vector3 getPosition;
        getPosition.x = playerData.playerPositionAndRotation[0];
        getPosition.y = playerData.playerPositionAndRotation[1];
        getPosition.z = playerData.playerPositionAndRotation[2];
        PlayerState.Instance.playerBody.transform.position = getPosition;

        //settings player rotation
        Vector3 getRotation;
        getRotation.x = playerData.playerPositionAndRotation[3];
        getRotation.y = playerData.playerPositionAndRotation[4];
        getRotation.z = playerData.playerPositionAndRotation[5];

        PlayerState.Instance.playerBody.transform.rotation = Quaternion.Euler(getRotation);
        //print("set data player");
        
        //Setting the inventory content
        foreach(string item in playerData.inventoryContent)
        {
            InventorySystem.Instance.AddToIventory(item, false, false);
        }
        foreach(string item in playerData.quickSlotsContent)
        {
            //Find nest free quick slot
            GameObject availableSlot = EquipSystem.Instance.FindNextEmptySlot();

            var itemToAdd = Instantiate(Resources.Load<GameObject>(item));

            itemToAdd.transform.SetParent(availableSlot.transform, false);
            // so a tap on it (phone) takes it in the hand, like before saving
            var quickItem = itemToAdd.GetComponent<InventoryItem>();
            if (quickItem != null) quickItem.isInsideQuickSlot = true;
        }

        
    }

    public void StartLoadedGame(int slotNumber)
    {
        isLoading = true;
        PlayerPrefs.SetInt("GameSceneLoad", slotNumber); // Salvează valoarea ca un int (1 pentru true)
        PlayerPrefs.Save(); // Salvează PlayerPrefs

        Debug.Log("Valoarea 'GameSceneLoad' a fost salvată în PlayerPrefs.");
        SceneManager.LoadScene("GameScene");
        //StartCoroutine(DelayedLoading());
        //LoadGame();
    }

    /*
    public IEnumerator DelayedLoading()
    {
        print("DelayedLoading1");
        yield return new WaitForSeconds(1f);
        print("DelayedLoading2");
        LoadGame();

    }
    */
   

    #endregion
    #endregion

    #region ||  ---- To Binary Section ----- ||
    public void SaveGameDataToBinaryFile(AllGameData gameData, int slotNumber)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        FileStream stream = new FileStream(binaryPath + fileName + slotNumber + ".bin", FileMode.Create);

        formatter.Serialize(stream, gameData);
        stream.Close();

        print("Data saved to" + binaryPath);
    }
    public AllGameData GetGameDataToBinaryFile(int slotNumber)
    {
        if (File.Exists(binaryPath + fileName + slotNumber + ".bin"))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream stream = new FileStream(binaryPath + fileName + slotNumber + ".bin", FileMode.Open);
            AllGameData data = formatter.Deserialize(stream) as AllGameData;
            stream.Close();
            print("Data loaded from" + binaryPath + fileName + slotNumber + ".bin");
            return data;
        }

        return null;
    }
    #endregion

    #region ||  ---- To Json Section ----- ||
    public void SaveGameDataToJsonFile(AllGameData gameData, int slotNumber)
    {
        string json = JsonUtility.ToJson(gameData);
        string encrypted = EncryptionDecryption(json);
        using (StreamWriter writer= new StreamWriter(jsonPathProject + fileName + slotNumber + ".json"))
        {
            writer.Write(encrypted);
            print("saved game to json file at:" + jsonPathProject + fileName + slotNumber + ".json");
        }
    }
    public AllGameData GetGameDataToJsonFile(int slotNumber)
    {
        using (StreamReader reader = new StreamReader(jsonPathProject + fileName + slotNumber + ".json"))
        {
            string json = reader.ReadToEnd();
            string decrypted = EncryptionDecryption(json);
            AllGameData data = JsonUtility.FromJson<AllGameData>(decrypted);
            return data;
        }

    }
    #endregion

    #region ||  ---- Settings Section ----- ||
    #region ||  ---- Volume Section ----- ||
    [System.Serializable]
    public class VolumeSettings
    {
        public float music;
        public float effects;
        public float master;
    }
    public void SaveVolumeSettings(float _music, float _effects, float _master)
    {
        VolumeSettings volumeSettings = new VolumeSettings()
        {
            music = _music,
            effects = _effects,
            master = _master,
        };
        PlayerPrefs.SetString("Volume", JsonUtility.ToJson(volumeSettings));
        PlayerPrefs.Save();
    }
    public VolumeSettings GetVolumeSettings()
    {
        return JsonUtility.FromJson<VolumeSettings>(PlayerPrefs.GetString("Volume"));
    }
    #endregion


    #endregion

    #region ||  ---- Encrypt/Decrypt Section ----- ||
    public string EncryptionDecryption(string jsonString)
    {
        // (one char array instead of adding to a string: the forest makes saves big, and adding a
        // character at a time to a string got very slow)
        var result = new char[jsonString.Length];
        for (int i = 0; i < jsonString.Length; i++)
        {
            result[i] = (char)(jsonString[i] ^ keyEncription[i % keyEncription.Length]);
        }
        return new string(result);
        //XOR = "is there a difference"

        // -- Encrypt -- //
        // David -       01000100 01100001 01110110 01101001 01100100 
        // M -          01000100
        //Key -         00000001
        //Encrypted     01000101

        // -- Decrypt -- //
        //Encrypted     01000101
        //Key -         00000001
        //Decrypted     01000100
    }
    #endregion
    #region ||  ---- Utility ----- ||
    public bool DoesFileExist(int slotNumber)
    {
        if (isSavingToJson)
        {
            if (System.IO.File.Exists(jsonPathProject + fileName + slotNumber + ".json"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            if (System.IO.File.Exists(jsonPathProject + fileName + slotNumber + ".bin"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public bool IsSlotEmpty(int slotNumber)
    {
        if (DoesFileExist(slotNumber))
        {
            return false;
        }
        return true;
    }

    public void DeselectButton()
    {
        GameObject myEventSystem = GameObject.Find("EventSystem");
        myEventSystem.GetComponent<UnityEngine.EventSystems.EventSystem>().SetSelectedGameObject(null);
    }
    #endregion
}
