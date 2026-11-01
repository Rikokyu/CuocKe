using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventorySystem : MonoBehaviour
{
 
   public static InventorySystem Instance { get; set; }
 
    public GameObject inventoryScreenUI;
    public List<GameObject> slotList = new List<GameObject>();
    public List<string> itemList = new List<string>();
    private GameObject itemToAdd;
    private GameObject whatSlotToEquip;
    public bool isOpen;
    // public bool isFull;
 
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
 
 
    void Start()
    {
        isOpen = false;
        PopulateSlotList();
    }
 
    private void PopulateSlotList()
    {
        foreach (Transform child in inventoryScreenUI.transform)
        {
            if(child.CompareTag("Slot"))
            {
                slotList.Add(child.gameObject);
            }
        }
    }
 
    void Update()
    {
 
        if (Input.GetKeyDown(KeyCode.I) && !isOpen)
        {
 
            Debug.Log("i is pressed");
            inventoryScreenUI.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            isOpen = true;
 
        }
        else if (Input.GetKeyDown(KeyCode.I) && isOpen)
        {
            inventoryScreenUI.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            isOpen = false;
        }
    }
    public void AddToInventory(string itemName)
    {

            whatSlotToEquip = FindNextEmptySlot();
            itemToAdd = (GameObject)Instantiate(Resources.Load<GameObject>(itemName), whatSlotToEquip.transform.position, whatSlotToEquip.transform.rotation);
            itemToAdd.transform.SetParent(whatSlotToEquip.transform);
            itemList.Add(itemName);
    }
    public bool CheckIfFull()
    {
        int counter = 0;
        foreach (GameObject slot in slotList)
        {
            if(slot.transform.childCount > 0)
            {
                counter+=1;
            }
        }
        if(counter == 21)
            {
                return true;
            }
            else
            {
                return false;
            }
    }
    private GameObject FindNextEmptySlot()
    {
        foreach (GameObject slot in slotList)
        {
            if (slot.transform.childCount == 0)
            {
                return slot;
            }
        }
        return new GameObject();
    }

    // ================= Item data layer (hotbar) =================
    // Used by HotbarUI (display) and ItemPickup (adding). The older grid code above is unchanged.

    [Header("Hotbar items")]
    [SerializeField, Min(1)] int slotCapacity = 5;

    ItemStack[] itemSlots;

    /// <summary>Raised after any add/remove so the hotbar can redraw.</summary>
    public event Action Changed;

    public int SlotCapacity => slotCapacity;

    ItemStack[] Slots
    {
        get
        {
            if (itemSlots == null || itemSlots.Length != slotCapacity)
            {
                itemSlots = new ItemStack[slotCapacity];
                for (int i = 0; i < itemSlots.Length; i++) itemSlots[i] = new ItemStack();
            }
            return itemSlots;
        }
    }

    /// <summary>The stack in slot 'index' (never null for a valid index; check IsEmpty). Null if out of range.</summary>
    public ItemStack GetSlot(int index)
    {
        ItemStack[] slots = Slots;
        return index >= 0 && index < slots.Length ? slots[index] : null;
    }

    /// <summary>Total count of an item over all slots.</summary>
    public int GetCount(ItemDefinition item)
    {
        if (item == null) return 0;
        int total = 0;
        foreach (ItemStack s in Slots) if (!s.IsEmpty && s.item == item) total += s.count;
        return total;
    }

    public bool Has(ItemDefinition item, int amount = 1)
    {
        return item != null && amount > 0 && GetCount(item) >= amount;
    }

    /// <summary>True if the whole amount fits (topping up existing stacks, then empty slots).</summary>
    public bool CanAdd(ItemDefinition item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        int room = 0;
        foreach (ItemStack s in Slots)
        {
            if (s.IsEmpty) room += item.maxStack;
            else if (s.item == item) room += Mathf.Max(0, item.maxStack - s.count);
        }
        return room >= amount;
    }

    /// <summary>Adds the whole amount or nothing. Returns false (and changes nothing) if it does not fit.</summary>
    public bool Add(ItemDefinition item, int amount = 1)
    {
        if (!CanAdd(item, amount)) return false;

        int left = amount;
        foreach (ItemStack s in Slots)
        {
            if (left <= 0) break;
            if (s.IsEmpty || s.item != item) continue;
            int put = Mathf.Min(left, item.maxStack - s.count);
            s.count += put;
            left -= put;
        }
        foreach (ItemStack s in Slots)
        {
            if (left <= 0) break;
            if (!s.IsEmpty) continue;
            int put = Mathf.Min(left, item.maxStack);
            s.item = item;
            s.count = put;
            left -= put;
        }

        if (Changed != null) Changed();
        return true;
    }

    /// <summary>Removes the whole amount or nothing (taking from the last slots first). Returns false if not enough.</summary>
    public bool Remove(ItemDefinition item, int amount = 1)
    {
        if (!Has(item, amount)) return false;

        int left = amount;
        ItemStack[] slots = Slots;
        for (int i = slots.Length - 1; i >= 0 && left > 0; i--)
        {
            ItemStack s = slots[i];
            if (s.IsEmpty || s.item != item) continue;
            int take = Mathf.Min(left, s.count);
            s.count -= take;
            left -= take;
            if (s.count <= 0) { s.item = null; s.count = 0; }
        }

        if (Changed != null) Changed();
        return true;
    }
}
