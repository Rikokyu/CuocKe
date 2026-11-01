using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Runtime helper (created automatically, no scene setup): puts an InteractObject on everything the player can use with E -
/// NPCs (grandparents, villagers, shop, reward, trainer, opponents), training stations, pick-up items, scrap, quest objects, bed...
/// so the crosshair dot can target them and show their name. Re-scans twice a second, so items dropped at runtime are covered.
/// The interactor scripts themselves only offer E while InteractObject.IsAimed(this) is true.
/// </summary>
public class InteractObjectAutoRegister : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<InteractObjectAutoRegister>() != null) return;
        new GameObject("InteractObjectAutoRegister").AddComponent<InteractObjectAutoRegister>();
    }

    // GameObject name -> shown name (used when the component has no 'npcName' of its own)
    static readonly Dictionary<string, string> Names = new Dictionary<string, string>
    {
        { "NPC_Grandmother", "Bà" }, { "NPC_Grandfather", "Ông" }, { "NPC_ChiBa", "Chị Ba" }, { "NPC_ChuTu", "Chú Tư" },
        { "NPC_CoLan", "Cô Lan" }, { "NPC_BacHai", "Bác Hai" }, { "NPC_MarketVillager", "Người bán hàng" },
        { "NPC_RewardKeeper", "Người phát thưởng" }, { "NPC_Trainer", "Huấn luyện viên" }, { "NPC_ChildhoodFriend", "Bạn thân" },
        { "Station_PushUp", "Hít đất" }, { "Station_PunchingBag", "Bao cát" }, { "Station_Treadmill", "Máy chạy bộ" },
        { "Station_WeightLift", "Tạ" }, { "QuestDeliveryBox", "Thùng hàng" }, { "QuestProteinBag", "Bao protein" },
        { "QuestWateringCan", "Bình tưới" }, { "FeedingSpot", "Chỗ cho gà ăn" }, { "StoryBed", "Giường" },
        { "ChienKe_Pet", "Gà chiến kê" },
    };

    float nextScan;

    void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 0.5f;
        Scan();
    }

    void Scan()
    {
        RegisterAll<QuestNPC>();
        RegisterAll<ShopNPC>();
        RegisterAll<RewardNPC>();
        RegisterAll<VillagerFlavorNPC>();
        RegisterAll<TournamentOpponentNPC>();
        RegisterAll<TrainerNPC>();
        RegisterAll<TrainingStation>();
        RegisterAll<GrandfatherChickenRescue>();
        RegisterAll<ChildhoodFriendBattleTaunt>();
        RegisterAll<QuestCarryItem>();
        RegisterAll<QuestObjectiveTarget>();
        RegisterAll<FeedingSpot>();
        RegisterAll<StoryBed>();
        RegisterAll<ItemPickup>();
        RegisterAll<ScrapPickup>();

        // the starter chicken can only be used while it is still lying down (its combat AI is off until acquired)
        MainChickenAcquire acquire = MainChickenAcquire.Instance;
        if (acquire != null)
        {
            InteractObject io = Ensure(acquire.gameObject, "Gà chiến kê");
            PetCombatAI ai = acquire.GetComponent<PetCombatAI>();
            if (io != null && ai != null) io.enabled = !ai.enabled;
        }
    }

    void RegisterAll<T>() where T : Component
    {
        T[] list = FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] == null) continue;
            GameObject go = list[i].gameObject;
            InteractObject existing = go.GetComponent<InteractObject>();
            if (existing != null) continue;
            Ensure(go, NameFor(list[i]));
        }
    }

    static InteractObject Ensure(GameObject go, string displayName)
    {
        InteractObject io = go.GetComponent<InteractObject>();
        if (io == null) io = go.AddComponent<InteractObject>();
        io.SetDisplayName(displayName);
        return io;
    }

    static string NameFor(Component c)
    {
        ItemPickup pickup = c as ItemPickup;
        if (pickup != null && !string.IsNullOrEmpty(pickup.ItemDisplayName)) return pickup.ItemDisplayName;

        ScrapPickup scrap = c as ScrapPickup;
        if (scrap != null && scrap.Type != null && !string.IsNullOrEmpty(scrap.Type.displayName)) return scrap.Type.displayName;

        // NPC scripts keep their name in a private 'npcName' field
        FieldInfo f = c.GetType().GetField("npcName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f != null && f.FieldType == typeof(string))
        {
            string n = f.GetValue(c) as string;
            if (!string.IsNullOrEmpty(n) && n != "Dân làng") return n;
        }

        string goName = c.gameObject.name;
        string mapped;
        if (Names.TryGetValue(goName, out mapped)) return mapped;
        if (goName.StartsWith("VegetablePlot")) return "Luống rau";

        if (f != null && f.FieldType == typeof(string))
        {
            string n = f.GetValue(c) as string;
            if (!string.IsNullOrEmpty(n)) return n;
        }
        return goName.Replace("_", " ");
    }
}
