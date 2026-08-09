# Shared effectId classification rules for anomaly buff/debuff text.
# Dot-sourced by classify-effects.ps1 (reporting) and write-effect-ids.ps1 (writeback)
# so the two can never drift apart.
#
# Ordered most-specific-first: first match wins, so narrow patterns must precede
# broad ones (turn_skip before action_deny, hit_override before stat_mod).
# ids are snake_case with no prefix, matching existing JSON field style.

$EffectRules = @(
    # Rewriting an already-resolved action. The backdated notebook and the correct
    # wall calendar are built entirely out of this, and it is the one primitive
    # that constrains combat architecture: it needs a replayable action log.
    @{ k = "retcon";         p = "补录|改写|被录|改录|墨迹|原创|创作|退稿|审稿|代签|逆写|挂历上" },
    @{ k = "legacy";         p = "下一次开局|下一代|遗产|结清者" },
    @{ k = "share_effect";   p = "编组|全队共享" },
    @{ k = "move_cost_mod";  p = "移动消耗|移动不消耗" },
    @{ k = "disguise";       p = "复制一名敌人的外观|外观" },
    @{ k = "reflect";        p = "反弹|反射|错觉" },
    @{ k = "initiative_mod"; p = "行动顺序|节奏偏移|先手" },
    @{ k = "rule_rewrite";   p = "胜负条件|重写当前区域|出版|盖章|必须遵守|必须执行|存在编辑|存在定义|价值归零|重新设定其血量|定义.*价值|签批|使用权|万物归租" },
    @{ k = "summon";         p = "召唤|虚影|参战" },
    @{ k = "action_replace"; p = "替换其内容|行动转移给|替言" },
    @{ k = "turn_skip";      p = "跳过|离场|强制撤退|无法主动行动|打断行动" },
    @{ k = "extra_action";   p = "额外行动|行动三次" },
    @{ k = "hit_override";   p = "必定未命中|攻击必中|必定命中|100%闪避|完全规避|无法被格挡|攻击无效|无法被阻止|自动.*最优" },
    @{ k = "immunity";       p = "免疫" },
    @{ k = "buff_manip";     p = "解除敌人一个Buff|剪掉自己一个Buff|截取|扣除敌人一项Buff|失去一项Buff|删除一个敌人的一个技能|忘记自己一个技能|锁定自身一个技能" },
    @{ k = "displace";       p = "传送|位移|移动到非预期|移动一格|走向最近的门|战场边缘|驱逐|移动至最优位置|方向错误|无法移动|多走一个区域|坐过站|刷不上" },
    @{ k = "taunt";          p = "优先瞄准|集火" },
    @{ k = "reveal";         p = "可看见|可看穿|可读取|可感知|可查看|可阅读|暴露|全部可见|弱点标记|行动意图|数值化|审计|真视|全视|透视|破幻|定价|门感|回声定位|地感|路径记忆" },
    @{ k = "terrain";        p = "天气|战场地形|掩体|通道|战场变为|全场潮湿|降雨|风暴|龙卷风|地权|传送门" },
    @{ k = "damage";         p = "灼烧|震击|额外伤害|全体伤害|溅射|点燃|场地费|承受.*伤害|受到伤害|被雷劈|撞到|反噬|少量伤害|造成.*伤害|附魔|火焰" },
    @{ k = "heal";           p = "治疗|恢复至半血|恢复道具|减伤一次" },
    @{ k = "equip_lock";     p = "无法丢弃|无法卸下|装备锁定|无法更换|无法更改" },
    @{ k = "equip_loss";     p = "永久失去|失去此装备|失去真伞|被拆除|拆除|没收|消失，且永久无法|失去戒指|随机失去一件道具|失去一件消耗品|失一物|永久消失" },
    @{ k = "durability";     p = "耐久|耐度" },
    @{ k = "resource";       p = "金钱|精力|饱食度|收取|扣除|折扣|破产|携带上限" },
    @{ k = "growth";         p = "永久\+1|成长率|属性重置|重置为初始值" },
    @{ k = "encounter_mod";  p = "遭遇率" },
    @{ k = "social";         p = "对话选项|社交|NPC|潜行|态度|皱眉|退后一步|不理你|误认|口头禅" },
    @{ k = "exploration";    p = "隐藏路径|隐藏事件|隐藏路线|进入.*区域|已访问|安全区|区域永久|不过期|绕过任何敌人|开锁|解锁|末班车|不存在的站|封锁" },
    @{ k = "action_force";   p = "必须|强制|自动向前|自动移动|自动点燃|必定反击|不受控制" },
    @{ k = "action_deny";    p = "无法|不能|只能|受限|打断|分心|归零" },
    @{ k = "action_grant";   p = "触发前任佩戴者的招式" },
    @{ k = "stat_mod";       p = "[-+＋±]\s*[0-9]+|翻倍|双倍|递增|微升|随金钱|眩晕|混乱|冻结|僵硬|受伤|低落|易伤|属性" }
)

function Get-EffectKind {
    param([string]$Text)
    foreach ($r in $EffectRules) {
        if ($Text -match $r.p) { return $r.k }
    }
    return $null   # narrative: flavour text with no mechanical hook
}
