# J26：灵魂收割与死铠逐杀记录（2026-10-18）

继续原文审查，修正两种不生成攻击的击杀收益：灵魂收割每个本人击杀独立掷掉落概率；死铠每个本人击杀计入加甲间隔。此前两者继承通用攻击触发器，同链第五杀起和合法深度末层的Kill事实会被攻击连锁门挡住。通用攻击连锁的次数／深度／冷却限制保持。

`SoulHarvestRule`和`RuneArmorOnKillRule`改为自行成对订阅／退订Kill，保留拥有者和击杀来源过滤。仍继承TriggerRule供构筑登记和清理；不调用基类的攻击事件注册，不产生新攻击。卸除节点立即退订，即使旧节点尚未释放，也不再得到收益。灵魂的概率、掉落位置、拾取、寿命、死铠禁疗和加甲间隔本身保持；删除灵魂表原本为0的Cooldown及其处理器赋值，避免遗留不会消费的参数。

后续直接改表：

- `Config/Tables/Upgrades/mut_soul_harvest.tres`：Chance／Heal／PickupRadius／LifetimeSeconds／PickupDelaySeconds；生产仍0.3／8／1.2m／15秒／0秒。
- `Config/Tables/Runes/rune_death_armor.tres`：Armor／ArmorPerKills／ArmorPerTrigger；生产仍20／10／1，HealMultiplier仍为0。
- 两条DescriptionTemplate改为引用同一HandlerParams，改概率、回血、初始甲、间隔或每次加甲量后，编译说明自动同步。不新增表行，奖励仍70=17强化／23进化／30异化，符文11，ConfigIndex不改。

## 反例与夹具

[首轮29/11](character-design-j26-failure-20261018_214357.log)真实复现同链五杀只计4、十杀仍只计4且不加甲、合法末层不计，以及说明写死数值。另有两条回血断言属于夹具问题：前面获得的护甲真实累计，固定50点伤害只打掉护甲，没有生命缺口。

[第二轮29/11](character-design-j26-failure-20261018_214506.log)仅调用FillVitals仍未修好夹具，因为它按“开局甲＋累计奖励”回满，不清除奖励。随后按实读护甲／护盾及CombinedDamageReduction计算足以造成25生命损失的真实命中；[生产未改时31/9](character-design-j26-failure-20261018_214553.log)，拾取实际215→222.5通过，九条机制／说明反例保留。未改生产护甲和减伤语义，也未删断言。

新`KillFactProbe`完整40项：真实按键→动画ATK→五个物理靶同帧死亡；同一AttackInstance天然为每目标创建Context，不误称同链。另用共享根Context的真实ReceiveHit验证同链十杀，不直接广播Kill；验证合法深度末层、来源和非致死负例、移除即退订、概率0仍逐杀掷点且不掉落。副本每3杀加2.5甲，七杀实际加两次；副本100%／回血7.5／半径0.5／寿命2秒／延迟0.15实际落到掉落物，真实接触回血和无人接触的Expired路径分别验证。生产概率仍30%，未用概率随机命中作为断言。

## 验收

`& .\_tools\verify_projectile_capsule.ps1 -KillFacts`：双构建0警告／0错误；专项40/0，五回归弓39／蓄力80／原文115／龙之箭42／符文83全部0失败，完整7进程退出0、stderr均0字节。Main退出0、69.06秒，八条硬指标与J11逐字一致；原五条历史红标保持，不称Main全绿。[完整输出](character-design-j26-evidence.log)。无新表现资源，不新增截图，不重拍旧图。

四存档跑前=跑后（meta 6C29…7EA7、ability_cards 594B…99CA、settings 9FD4…02B4、loadout CD92…5462），rune.cfg仍不存在，selftest.flag已清。CodeGraph强制索引237文件、6939节点、15683边。外部12个跟踪文件从回归中途捕获到结束保持同一状态，另登记11个已知未跟踪运行资源的结束哈希，见[工作区记录](character-design-j26-workspace.log)。

当前工作区含外部美术／场景／角色显示修改及旧模型删除，本批保留并精准提交自己的文件，验证不称为干净HEAD独立验证。统一玩家暴击、其余原文、23项特化、符文界面与动作／美术仍待继续，平衡后置。
