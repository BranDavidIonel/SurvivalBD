using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Everything the game shows, in Romanian when Romanian is chosen (Settings or the main menu): the names of the things
// (items, plants, trees, rabbits), the item descriptions, the words of the windows and buttons. The story and the
// messages use Lang.T(...) where they are written; this holds the rest:
//  - Translations.Name("Stone") -> "Piatră": names and short words, by their English text
//  - Translations.Item("stone", ...): an item's name, description and use (the inventory's info box)
//  - SceneTexts (made in code in every scene): the words written in the scenes and on the buttons made in code
//    ("Save Game", "Tools", "Craft", "Inventory"...) change language by themselves.
public static class Translations
{
    // ---------------- names and short words ---------------- //

    static readonly Dictionary<string, string> Ro = new Dictionary<string, string>
    {
        // items
        { "Stone", "Piatră" }, { "Stick", "Băț" }, { "Log", "Buștean" }, { "Pine Log", "Buștean de brad" },
        { "Birch Log", "Buștean de mesteacăn" }, { "Oak Log", "Buștean de stejar" }, { "Beech Log", "Buștean de fag" },
        { "Plank", "Scândură" }, { "Planks", "Scânduri" }, { "Planks x2", "Scânduri x2" }, { "Plank X2", "Scânduri x2" },
        { "Hay", "Fân" }, { "Hemp", "Cânepă" }, { "Rope", "Frânghie" }, { "Iron Ore", "Minereu de fier" },
        { "Stone Axe", "Topor de piatră" }, { "Axe", "Topor" }, { "Iron Axe", "Topor de fier" }, { "Torch", "Torță" },
        { "Campfire", "Foc de tabără" }, { "StorageBox", "Ladă" }, { "Storage Box", "Ladă" },
        { "Foundation", "Fundație" }, { "Fundation", "Fundație" }, { "Wall", "Perete" }, { "Door Wall", "Perete cu ușă" },
        { "Window Wall", "Perete cu fereastră" }, { "Roof", "Acoperiș" }, { "Cross", "Cruce" }, { "Snare", "Laț" },
        { "Snare (empty)", "Laț (gol)" },
        { "Porcini", "Hribi" }, { "Red Mushroom", "Ciupercă roșie" }, { "Field Mushroom", "Ciupercă de câmp" },
        { "Berries", "Fructe de pădure" }, { "Chicory", "Cicoare" }, { "Heather", "Iarbă neagră" },
        { "Raw Meat", "Carne crudă" }, { "Cooked Meat", "Carne gătită" }, { "Rabbit Hide", "Blană de iepure" },
        { "Pine Cone", "Con de brad" }, { "Acorn", "Ghindă" }, { "Beechnut", "Jir" }, { "Birch Catkin", "Mâțișor de mesteacăn" },
        { "Old Journal", "Jurnalul vechi" }, { "Prayer Book", "Carte de rugăciuni" },
        { "Rabbit (male)", "Iepure (mascul)" }, { "Rabbit (female)", "Iepure (femelă)" },
        { "Baby rabbit (male)", "Pui de iepure (mascul)" }, { "Baby rabbit (female)", "Pui de iepure (femelă)" },
        // plants, trees and things lying around
        { "Berry Bush", "Tufă cu fructe" }, { "Branch", "Creangă" }, { "Chicory (herb)", "Cicoare (plantă)" },
        { "Heather (herb)", "Iarbă neagră (plantă)" }, { "Dry Grass (hay)", "Iarbă uscată (fân)" },
        { "Grass (hay)", "Iarbă (fân)" }, { "Tall Grass (hay)", "Iarbă înaltă (fân)" },
        { "Plank (from the ship)", "Scândură (de la corabie)" },
        { "Tree", "Copac" }, { "Pine", "Brad" }, { "Birch", "Mesteacăn" }, { "Oak", "Stejar" }, { "Beech", "Fag" },
        { "Stump", "Buturugă" },
        { "Campfire (cooking)", "Foc de tabără (gătește)" }, { "Campfire (fire out)", "Foc de tabără (stins)" },
        { "Campfire (food ready)", "Foc de tabără (mâncarea e gata)" }, { "Campfire (burning)", "Foc de tabără (arde)" },
        // windows and buttons
        { "Inventory", "Inventar" }, { "Crafting", "Meșteșuguri" }, { "Tools", "Unelte" }, { "Survival", "Supraviețuire" },
        { "Basic Survival", "Supraviețuire" }, { "Refine & Process", "Prelucrare" }, { "Construction", "Construcții" },
        { "Craft", "Fă" }, { "Inventory full", "Inventar plin" }, { "<  Back", "<  Înapoi" }, { "Back", "Înapoi" },
        { "Storage", "Ladă" }, { "Storage Box ", "Ladă" }, { "Cook", "Gătește" }, { "Fuel", "Lemne" },
        { "Menu", "Meniu" }, { "Help", "Ajutor" }, { "Map", "Hartă" }, { "Settings", "Setări" },
        { "Save Game", "Salvează" }, { "Load Game", "Încarcă" }, { "New Game", "Joc nou" },
        { "Quit", "Ieșire" }, { "Exit", "Ieșire" }, { "Yes", "Da" }, { "No", "Nu" }, { "yes", "da" }, { "no", "nu" },
        { "Cancel", "Anulează" }, { "Attack", "Lovește" }, { "Take", "Ia" }, { "Open", "Deschide" }, { "Close", "Închide" },
        { "Place", "Așază" }, { "Jump", "Sari" }, { "Up", "Sus" }, { "Down", "Jos" }, { "Left", "Stânga" }, { "Right", "Dreapta" },
        { "Are you sure ?", "Ești sigur?" }, { "Do you wish to override this save?", "Vrei să scrii peste această salvare?" },
        { "Loading..", "Se încarcă..." }, { "Place item", "Așază obiectul" }, { "In Construction Mode", "Construiești" },
        { "Volume", "Volum" }, { "Music", "Ambianță" }, { "Master", "General" }, { "Effects", "Efecte" },
        { "Empty", "Gol" }, { "Item name", "" }, { "Item Description", "" }, { "Item Functionality", "" },
        { "Inventory  [I]", "Inventar  [I]" }, { "Crafting  [C]", "Meșteșuguri  [C]" }, { "Menu  [M]", "Meniu  [M]" },
        { "Help  [H]", "Ajutor  [H]" }, { "Map  [N]", "Hartă  [N]" }, { "Tab: free the mouse to click", "Tab: eliberezi mouse-ul ca să apeși" },
        { "Survival Game by BD", "Joc de supraviețuire de BD" },
        // the inventory's hint
        { "Tap an item to see what it is.\nDouble tap: use it / put it in the quick bar.  Drag: move it.",
          "Atinge un obiect ca să vezi ce e.\nDublu tap: îl folosești / îl pui în bara rapidă.  Trage: îl muți." },
        { "Point at an item to see what it is.\nRight click: use it / put it in the quick bar.  Drag: move it.",
          "Du mouse-ul pe un obiect ca să vezi ce e.\nClic dreapta: îl folosești / îl pui în bara rapidă.  Trage: îl muți." },
        { "Campfire ", "Foc de tabără" },
    };

    // the words are looked up as one line with single spaces (the scene writes some of them on two lines)
    static Translations()
    {
        var copy = new Dictionary<string, string>();
        foreach (var pair in Ro) copy[Key(pair.Key)] = pair.Value;
        Ro.Clear();
        foreach (var pair in copy) Ro[pair.Key] = pair.Value;
    }

    static string Key(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        // "Refine & \nProcess" and such: one line, single spaces
        var parts = text.Replace("\r", " ").Replace("\n", " ").Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }

    // the Romanian name of a thing / word (in English: as it is)
    public static string Name(string english)
    {
        if (!Lang.Ro || string.IsNullOrEmpty(english)) return english;
        return Ro.TryGetValue(Key(english), out var ro) ? ro : english;
    }

    public static bool Has(string english) => Ro.ContainsKey(Key(english));

    // the Romanian words whatever the language now is (SceneTexts switches back and forth)
    public static string ToRo(string english) => Ro.TryGetValue(Key(english), out var ro) ? ro : english;

    // ---------------- the items (name, what it is, what it is for) ---------------- //

    static readonly Dictionary<string, string[]> Items = new Dictionary<string, string[]>
    {
        { "Axe", new[] { "Topor de piatră", "O piatră ascuțită legată de un băț.", "Taie copaci (bușteni și crengi) și vânează iepuri." } },
        { "Campfire", new[] { "Foc de tabără", "Un cerc de pietre pentru foc și gătit.", "Folosește-l (dublu tap / clic dreapta) ca să-l așezi." } },
        { "Cooked Meat", new[] { "Carne gătită", "O bucată de carne friptă la foc.", "Se mănâncă. Bună și ca hrană pentru drum." } },
        { "Rabbit Hide", new[] { "Blană de iepure", "Blana unui iepure vânat.", "Ține de cald pe mare: pluta are nevoie de 3." } },
        { "Raw Meat", new[] { "Carne crudă", "Carne de iepure, crudă.", "Frige-o la focul de tabără înainte s-o mănânci." } },
        { "StorageBox", new[] { "Ladă", "O ladă mică de scânduri pentru lucruri.", "Folosește-o ca s-o așezi, apoi deschide-o și pune lucruri în ea." } },
        { "acorn", new[] { "Ghindă", "Sămânța stejarului. Ghindele căzute cresc stejari noi.", "Folosește-o (dublu tap / clic dreapta) ca să plantezi un stejar în fața ta." } },
        { "beechnut", new[] { "Jir", "Sămânța fagului. Jirul căzut crește fagi noi.", "Folosește-l (dublu tap / clic dreapta) ca să plantezi un fag în fața ta." } },
        { "berries", new[] { "Fructe de pădure", "Fructe sălbatice dulci.", "Mănâncă: +3 viață, +120 hrană, +6 apă." } },
        { "birchseed", new[] { "Mâțișor de mesteacăn", "Plin de semințe mici. Mâțișorii căzuți cresc mesteceni noi.", "Folosește-l (dublu tap / clic dreapta) ca să plantezi un mesteacăn în fața ta." } },
        { "chicory", new[] { "Cicoare", "O plantă cu flori albastre. Bună împotriva slăbiciunii.", "Mănâncă: +8 viață, +4 apă." } },
        { "cross", new[] { "Cruce", "O cruce din cinci scânduri, ca aceea înălțată de fratele Ioan.", "Canonul fratelui Ioan: înalț-o la sfârșit, pe vârful celuilalt munte (Meniu > Hartă)." } },
        { "doorwall", new[] { "Perete cu ușă", "Un perete cu o ușă de scânduri, prinsă în balamale de frânghie.", "Folosește-l, apoi privește locul unui perete pe o fundație. Privește ușa și apasă ca s-o deschizi sau s-o închizi." } },
        { "fieldmushroom", new[] { "Ciupercă de câmp", "O ciupercă mică și cenușie, de pe solul pădurii.", "Mănâncă: +10 viață, +150 hrană." } },
        { "foundation", new[] { "Fundație", "Baza unei case sau a unui țarc: pe ea pui pereții și acoperișul.", "Folosește-o ca s-o așezi pe pământ (sau lângă altă fundație)." } },
        { "hay", new[] { "Fân", "Iarbă uscată. Ia foc foarte ușor.", "Trebuie ca să aprinzi focul, împreună cu lemne." } },
        { "heather", new[] { "Iarbă neagră", "O plantă mică, mov, de pe dealuri.", "Mănâncă: +6 viață, +20 hrană." } },
        { "hemp", new[] { "Cânepă", "O plantă înaltă, cu fibre tari. Crește cel mai deasă în jurul lacului.", "3 cânepă fac o frânghie (Meșteșuguri > Prelucrare); 5 cânepă și un băț fac un laț (Meșteșuguri > Supraviețuire)." } },
        { "ironaxe", new[] { "Topor de fier", "Un topor cu lama de fier, din minereul peșterii.", "Orice copac cade din 3 lovituri. Vânează și mai bine." } },
        { "ironore", new[] { "Minereu de fier", "O piatră grea și ruginie din peșteră.", "10 minereuri și un buștean fac un topor de fier (Meșteșuguri > Unelte)." } },
        { "journal", new[] { "Jurnalul vechi", "Jurnalul fratelui Ioan, un călugăr care a trăit demult pe insula asta.", "Folosește-l (dublu tap / clic dreapta) ca să-l citești." } },
        { "log", new[] { "Buștean", "Un buștean dintr-un copac tăiat.", "Arde în focul de tabără și face scânduri (Meșteșuguri > Prelucrare)." } },
        { "pinecone", new[] { "Con de brad", "Un con plin de semințe. Conurile căzute cresc brazi noi.", "Folosește-l (dublu tap / clic dreapta) ca să plantezi un brad în fața ta. Arde și puțin în foc." } },
        { "pinelog", new[] { "Buștean de brad", "Lemn închis la culoare, de brad. Brazii mari dau mai mulți bușteni.", "Arde în focul de tabără și face scânduri, ca orice buștean." } },
        { "plank", new[] { "Scândură", "Scânduri de lemn, tăiate din bușteni.", "Pentru construcții, ladă, cruce, plută și multe altele." } },
        { "porcini", new[] { "Hribi", "O ciupercă mare și gustoasă de pădure.", "Mănâncă: +15 viață, +250 hrană." } },
        { "prayerbook", new[] { "Carte de rugăciuni", "Cartea veche de rugăciuni a fratelui Ioan.", "Folosește-o ca să citești rugăciunile. Noaptea rugăciunea alungă umbra o vreme." } },
        { "rabbitfemale", new[] { "Iepure (femelă)", "Un iepure viu, prins în laț.", "Dă-i drumul în țarc (drop sau folosește-l), apoi închide ușa. În afara țarcului fuge!" } },
        { "rabbitmale", new[] { "Iepure (mascul)", "Un iepure viu, prins în laț.", "Dă-i drumul în țarc (drop sau folosește-l), apoi închide ușa. În afara țarcului fuge!" } },
        { "redmushroom", new[] { "Ciupercă roșie", "O ciupercă roșie de pădure. Amară, dar bună.", "Mănâncă: +8 viață, +120 hrană." } },
        { "roof", new[] { "Acoperiș", "Scânduri cu un strat gros de fân deasupra.", "Folosește-l, apoi privește partea de sus a pereților unei fundații." } },
        { "rope", new[] { "Frânghie", "O frânghie tare, împletită din fibre de cânepă.", "Leagă buștenii plutei; balamalele ușii țarcului." } },
        { "snare", new[] { "Laț", "O buclă de sfoară de cânepă, legată de un băț înfipt în pământ.", "Folosește-l (dublu tap / clic dreapta) ca să-l pui lângă iepuri. Un iepure mare care intră în el rămâne prins viu." } },
        { "stick", new[] { "Băț", "Un băț uscat.", "Pentru meșteșuguri și pentru foc." } },
        { "stone", new[] { "Piatră", "O piatră simplă.", "Pentru meșteșuguri (topor, foc de tabără)." } },
        { "torch", new[] { "Torță", "Un băț cu iarbă uscată legată în jurul lui. Arde cam 6 ore în mână.", "Pune-o în bara rapidă și ia-o în mână: luminează întunericul (peștera!)." } },
        { "wall", new[] { "Perete", "Un perete de scânduri, pentru casă sau țarc.", "Folosește-l, apoi privește locul unui perete pe o fundație." } },
        { "windowwall", new[] { "Perete cu fereastră", "Un perete cu o fereastră, cu două bețe de-a curmezișul.", "Folosește-l, apoi privește locul unui perete pe o fundație." } },
    };

    // the info box of the inventory: in Romanian if there is a translation (item: "stone", "Axe"...)
    public static void Item(string item, ref string name, ref string description, ref string use)
    {
        if (!Lang.Ro || item == null || !Items.TryGetValue(item, out var t)) return;
        name = t[0];
        description = t[1];
        use = t[2];
    }

    // ---------------- the words written in the scenes ---------------- //

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

    static void Attach()
    {
        if (Object.FindObjectOfType<SceneTexts>() != null) return;
        new GameObject("Translations (made in code)").AddComponent<SceneTexts>();
    }
}

// Changes the scene's fixed words (and the buttons made in code) to the language of the Settings, and back.
public class SceneTexts : MonoBehaviour
{
    readonly Dictionary<Component, string> english = new Dictionary<Component, string>();
    float timer = 0.3f;
    bool lastRo;

    void OnEnable() => Lang.Changed += Apply;
    void OnDisable() => Lang.Changed -= Apply;

    void Update()
    {
        // now and then: windows and buttons are also made later (crafting cards, the phone's buttons...)
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f && lastRo == Lang.Ro) return;
        timer = 2f;
        Apply();
    }

    void Apply()
    {
        lastRo = Lang.Ro;
        if (!Lang.Ro && english.Count == 0) return; // English and nothing changed: nothing to do
        foreach (var t in FindObjectsOfType<Text>(true)) Translate(t, t.text, s => t.text = s);
        foreach (var t in FindObjectsOfType<TMP_Text>(true)) Translate(t, t.text, s => t.text = s);
    }

    void Translate(Component c, string now, System.Action<string> set)
    {
        if (c == null) return;
        english.TryGetValue(c, out var original);
        if (Lang.Ro)
        {
            if (original != null && now == Translations.ToRo(original)) return; // already in Romanian
            if (!Translations.Has(now)) { if (original != null) english.Remove(c); return; }
            english[c] = now;
            string ro = Translations.ToRo(now);
            if (ro != now) { set(ro); FitOnOneLine(c as TMP_Text, now, ro); }
        }
        else if (original != null)
        {
            if (now == Translations.ToRo(original)) set(original); // back to English
            english.Remove(c);
        }
    }

    // The big yellow words of the menus (a title, a button) were made for the English words: a longer Romanian one
    // wrapped onto a second line and ran into the word under it. It now gets smaller instead, on one line.
    static void FitOnOneLine(TMP_Text t, string en, string ro)
    {
        if (t == null || en.Contains("\n") || ro.Contains("\n") || ro.Length <= en.Length || ro.Length > 40) return;
        if (t.enableAutoSizing) return;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = Mathf.Max(8f, t.fontSize * 0.55f);
        t.enableWordWrapping = false;
        t.enableAutoSizing = true;
    }
}
