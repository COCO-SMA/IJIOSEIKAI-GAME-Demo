using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace KunchengRPG.UI
{
    /// <summary>
    /// HUD overlay for the explore scene.
    ///
    /// The status block is one multi-line Text rather than a field per stat: the
    /// whole point is to read every number at once while testing anomalies, and
    /// the deltas in brackets are the only way to see an equipped item actually
    /// doing something.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Status Panel")]
        public Text statusText;

        [Header("Prompt Panel")]
        public GameObject promptPanel;
        public Text promptText;

        [Header("Message")]
        public GameObject messagePanel;
        public Text messageText;

        private float bobTimer;
        private readonly StringBuilder sb = new StringBuilder(768);

        void Update()
        {
            bobTimer += Time.deltaTime;
            if (statusText != null) statusText.text = BuildStatus();
        }

        private static string PartLabel(string partName)
        {
            switch (partName)
            {
                case "brain":      return "大脑";
                case "torso":      return "躯干";
                case "left_hand":  return "左手";
                case "right_hand": return "右手";
                case "left_leg":   return "左腿";
                case "right_leg":  return "右腿";
                default:           return partName;
            }
        }

        public static string StageLabel(string stage)
        {
            switch (stage)
            {
                case "infant":      return "幼年";
                case "childhood":   return "童年";
                case "teen":        return "少年";
                case "young_adult": return "青年";
                case "prime":       return "壮年";
                case "middle_age":  return "中年";
                default:            return "老年";
            }
        }

        /// <summary>Show a base stat with its anomaly delta, e.g. "力量 12(+8)".</summary>
        private static string WithDelta(string label, int baseValue, int effective)
        {
            int d = effective - baseValue;
            if (d == 0) return $"{label} {baseValue}";
            return d > 0 ? $"{label} {baseValue}(+{d})" : $"{label} {baseValue}({d})";
        }

        private string BuildStatus()
        {
            var gm = Core.GameManager.Instance;
            var p = gm?.Player;
            if (p == null) return "";

            var eff = gm.EffectivePlayerStats;
            var s = p.stats;
            sb.Length = 0;

            string district = gm.GetCurrentDistrict()?.name ?? "?";
            string lottery = p.birthLottery == "native" ? "鲲生" : "过江客";

            sb.Append(p.name).Append("　第").Append(p.generation).Append("代 · ")
              .Append(p.age).Append("岁 · ").Append(StageLabel(p.LifeStage)).Append('\n');
            sb.Append(district).Append(" ｜ ").Append(lottery).Append('\n');

            sb.Append("──── 状态 ────\n");
            sb.Append($"生命 {p.hp}/{p.maxHp}　体力 {p.stamina}/{p.maxStamina}\n");
            sb.Append($"行动 {p.actionPoints}/{p.maxActionPoints}　金钱 ¥{p.money}\n");
            sb.Append($"体重 {p.weight}　共鸣碎片 {p.resonanceShards}\n");

            sb.Append("──── 基础属性 ────\n");
            sb.Append(WithDelta("力量", s.strength, eff.Strength)).Append("　")
              .Append(WithDelta("行动力", s.actionPower, eff.ActionPower)).Append('\n');
            sb.Append(WithDelta("感知", s.perception, eff.Perception)).Append("　")
              .Append(WithDelta("机缘", s.fortune, eff.Fortune)).Append('\n');
            sb.Append(WithDelta("韧性", s.resilience, eff.Resilience)).Append("　")
              .Append(WithDelta("体质", s.vitality, eff.Vitality)).Append('\n');

            sb.Append("──── 衍生属性 ────\n");
            sb.Append($"攻击 {eff.Attack}　防御 {eff.Defense}　速度 {eff.Speed}\n");
            sb.Append($"命中 {eff.HitRate:P0}　闪避 {eff.DodgeRate:P0}　暴击 {eff.CritRate:P0}\n");
            sb.Append($"减伤 {eff.DamageReduction:P0}　暴伤 {eff.CritDamage:P0}");
            sb.Append($"　异常触发 {eff.AnomalyTriggerRate:P0}\n");

            sb.Append("──── 六组件（效能/稳定）────\n");
            if (p.bodyComponents != null)
            {
                for (int i = 0; i < p.bodyComponents.Length; i++)
                {
                    var c = p.bodyComponents[i];
                    if (c == null) continue;
                    sb.Append(PartLabel(c.partName)).Append(' ')
                      .Append(c.efficiency).Append('/').Append(c.stability);
                    if (c.injured) sb.Append("[伤]");
                    if (c.HasAnomaly) sb.Append("[异]");
                    sb.Append(i % 2 == 1 ? "\n" : "　");
                }
                if (p.bodyComponents.Length % 2 == 1) sb.Append('\n');
            }

            AppendEquipped(gm);
            return sb.ToString();
        }

        private void AppendEquipped(Core.GameManager gm)
        {
            var anomalies = gm.Anomalies;
            if (anomalies == null) return;

            int shown = 0;
            foreach (var inst in anomalies.Instances)
            {
                if (!inst.IsEquipped) continue;
                if (shown == 0) sb.Append("──── 已装备 ────\n");
                shown++;

                var def = anomalies.Define(inst.itemId);
                string label = def?.name ?? inst.itemId;
                int toNext = anomalies.DepthToNextLevel(inst);
                sb.Append(MenuLabels.SlotLabel(inst.equippedOn)).Append(' ')
                  .Append(label).Append(" L").Append(inst.level)
                  .Append(toNext > 0 ? $"（深度 {inst.depth}，还差 {toNext}）" : "（已满层）")
                  .Append('\n');
            }
            if (shown == 0) sb.Append("──── 已装备 ────\n（空）\n");
        }

        /// <summary>Show interaction prompt.</summary>
        public void ShowPrompt(string text)
        {
            if (promptPanel == null || promptText == null) return;

            if (string.IsNullOrEmpty(text))
            {
                promptPanel.SetActive(false);
                return;
            }

            promptPanel.SetActive(true);
            promptText.text = Mathf.FloorToInt(bobTimer * 2) % 2 == 0 ? text : "";
        }

        /// <summary>Show a temporary message.</summary>
        public void ShowMessage(string text)
        {
            if (messagePanel == null || messageText == null) return;
            messagePanel.SetActive(true);
            messageText.text = text;
        }

        public void HideMessage()
        {
            if (messagePanel != null) messagePanel.SetActive(false);
        }

        /// <summary>Prompt text for whatever the player is standing next to.</summary>
        public string GetInteractionPrompt(Game.MapController mapController)
        {
            if (mapController == null) return "";

            if (mapController.nearbyNpc != null)
                return $"[空格] 跟{mapController.nearbyNpc.name}说话";

            if (mapController.nearbyPoi != null)
            {
                var poi = mapController.nearbyPoi;
                if (poi.type == "enemy") return $"[空格] 打一场：{poi.name}";
                return $"[空格] 看看{poi.name}";
            }

            return "";
        }

        public string GetIdlePrompt()
        {
            var player = Core.GameManager.Instance?.Player;
            if (player == null || !player.IsAdult) return "[Tab] 菜单";
            return "[Tab] 菜单　[I] 摸鱼(-¥50)　[E] 结束这年";
        }
    }
}
