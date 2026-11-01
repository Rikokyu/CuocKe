using UnityEngine;

/// <summary>
/// Data for one NPC tournament opponent (M9). The order of the opponents is the order of the array in
/// TournamentManager, not a field here. Stats are applied to that opponent's rooster only when its fight starts.
/// </summary>
[CreateAssetMenu(fileName = "Opponent_New", menuName = "CuocKe/Tournament Opponent")]
public class OpponentDefinition : ScriptableObject
{
    [Header("Identity")]
    public string opponentId = "opponent";
    public string displayName = "Đối thủ";

    [Header("Rooster stats (applied when the fight starts)")]
    [Min(1f)] public float maxHealth = 130f;
    [Min(1f)] public float damage = 14f;
    [Min(0.2f)] public float attackCooldown = 1.5f;

    [Header("Reward (paid once, on the first win only)")]
    [Min(0)] public int rewardMoney = 200;
    [Min(0)] public int medals = 1;

    [Header("Dialogue")]
    [TextArea(2, 4)] public string[] introLines = { "Nghe nói gà mày mạnh lắm? Vào đấu một trận xem!" };
    [TextArea(2, 4)] public string[] retryLines = { "Chưa phục à? Luyện thêm rồi đấu lại cũng được." };
    [TextArea(2, 4)] public string[] defeatedLines = { "Tao thua rồi, gà mày khá thật." };
    [TextArea(2, 4)] public string[] lockedLines = { "Chưa đến lượt mày đâu, đi thắng người trước đã." };
    public string challengeLabel = "Thách đấu!";
}
