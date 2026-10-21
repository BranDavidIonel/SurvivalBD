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
    }

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

        return new EnviromentData(itemsPickedup, treesToSave, allAnimals, allStorage);
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
        // destroy animals that should not exist 
        List<string> allAnimals = new List<string>();
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
            InventorySystem.Instance.AddToIventory(item,false);
        }
        foreach(string item in playerData.quickSlotsContent)
        {
            //Find nest free quick slot
            GameObject availableSlot = EquipSystem.Instance.FindNextEmptySlot();

            var itemToAdd = Instantiate(Resources.Load<GameObject>(item));

            itemToAdd.transform.SetParent(availableSlot.transform, false);
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
        string result = "";
        for (int i = 0; i < jsonString.Length; i++)
        {
            result += (char)(jsonString[i] ^ keyEncription[i % keyEncription.Length]);

        }
        return result;
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
