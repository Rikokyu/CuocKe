using UnityEngine;

/// <summary>
/// Gear of the pet chicken (put on ChienKe_Pet). Two shop goods are not carried in the bag, they are WORN by the chicken the moment they are bought:
///   - Non la        : a conical hat on the chicken's head.
///   - Tam kiem Zoro : three swords worn under the wing. Buying them also unlocks the chicken's special move
///                     (Animator state "3122_Low", see PetCombatAI.TryPlaySwordSkill). While that animation plays the swords
///                     are held like Zoro Tsunami (one in the left hand, one in the right hand, one in the mouth), then go back under the wing.
/// ShopUI asks Handles / Owns before selling and calls Equip after paying (the item is not put into the bag).
/// Gear is only visible once the chicken is the muscle "pet" model (StarterChicken.IsPet).
///
/// Placement is done BY HAND in the editor: every gear piece is a normal child object of a bone of ChienKe_Pet (hat and mouth sword under the
/// head bone, swords under the wing, held swords under the hands). They are ACTIVE in the editor so you can move / rotate / scale them in the
/// Scene view. At runtime this script hides them and only shows them (SetActive) once the item is bought / the skill is playing.
/// </summary>
public class PetEquipment : MonoBehaviour
{
    public static PetEquipment Instance { get; private set; }

    /// <summary>True once the Zoro swords were bought: the 3122_Low skill is unlocked.</summary>
    public static bool SwordSkillUnlocked { get { return Instance != null && Instance.hasSwords; } }

    [Header("Shop items")]
    [SerializeField] ItemDefinition hatItem;
    [SerializeField] ItemDefinition swordsItem;

    [Header("Gear objects (children of the bones - adjust position / rotation / scale in the editor)")]
    [Tooltip("The conical hat (child of mixamorig:Head).")]
    [SerializeField] GameObject hatObject;
    [Tooltip("The three swords worn under the wing (shown while the skill is not playing).")]
    [SerializeField] GameObject swordsWingObject;
    [Tooltip("Sword held in the left hand during the 3122_Low skill.")]
    [SerializeField] GameObject heldLeftObject;
    [Tooltip("Sword held in the right hand during the 3122_Low skill.")]
    [SerializeField] GameObject heldRightObject;
    [Tooltip("Sword held in the mouth during the 3122_Low skill.")]
    [SerializeField] GameObject heldMouthObject;
    [SerializeField] string skillStateName = "3122_Low";

    bool hasHat, hasSwords;
    Animator anim;
    StarterChicken starter;

    public bool HasHat { get { return hasHat; } }
    public bool HasSwords { get { return hasSwords; } }

    void Awake()
    {
        Instance = this;
        anim = GetComponent<Animator>();
        starter = GetComponent<StarterChicken>();
        Refresh(); // everything is visible in the editor so it can be placed by hand: hide it until it is earned
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>True if 'item' is gear (worn by the chicken) and not a normal bag item.</summary>
    public bool Handles(ItemDefinition item) { return item != null && (item == hatItem || item == swordsItem); }

    public bool Owns(ItemDefinition item)
    {
        if (item == null) return false;
        if (item == hatItem) return hasHat;
        if (item == swordsItem) return hasSwords;
        return false;
    }

    /// <summary>Puts the gear on the chicken. False if it is not gear or already worn.</summary>
    public bool Equip(ItemDefinition item)
    {
        if (!Handles(item) || Owns(item)) return false;
        if (item == hatItem) hasHat = true;
        else hasSwords = true;
        Refresh();
        return true;
    }

    // ---------------------------------------------------------------- visibility

    bool InSkill()
    {
        if (anim == null || !anim.isActiveAndEnabled) return false;
        if (anim.GetCurrentAnimatorStateInfo(0).IsName(skillStateName)) return true;
        return anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsName(skillStateName);
    }

    void LateUpdate()
    {
        Refresh();
    }

    static void Show(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }

    void Refresh()
    {
        bool petForm = starter == null || starter.IsPet;
        bool skill = hasSwords && petForm && InSkill();
        Show(hatObject, hasHat && petForm);
        Show(swordsWingObject, hasSwords && petForm && !skill);
        Show(heldLeftObject, skill);
        Show(heldRightObject, skill);
        Show(heldMouthObject, skill);
    }
}
