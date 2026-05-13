# TactiRogue 策划操作与使用说明

这份文档是给策划直接使用的。当前项目里的大多数战斗数据、单位数据、卡牌数据、AI 数据和战斗预设，已经统一改成 Excel 编辑。

棋子模型、`IdleTiltAngle`、Frame/Portrait 和 Motion 编辑器的使用说明见：

- [TactiRogue 棋子表现与 Motion 使用说明](TactiRoguePresentationMotionGuide.md)

## 1. 现在主要改哪里

唯一长期源数据：

- `Assets/TactiRogue/DataAuthoring/TactiRogueData.xlsx`

游戏实际读取的生成结果：

- `Assets/Resources/TactiRogue/Content/TactiRogueContentDatabase.asset`
- `Assets/Resources/TactiRogue/Content/Generated/*.asset`
- `Assets/Resources/TactiRogue/Scenarios/*.json`

平时调数值时，优先改 Excel，不需要再手工逐个打开 `ScriptableObject`。

## 2. 标准工作流

日常改表流程固定如下：

1. 打开 `Assets/TactiRogue/DataAuthoring/TactiRogueData.xlsx`
2. 修改你要调的表和字段
3. 回到 Unity，执行 `Tools/TactiRogue/Excel/Validate Excel Workbook`
4. 校验通过后，执行 `Tools/TactiRogue/Excel/Import Excel To Game Data`
5. 打开 `Assets/Scenes/SampleScene.unity`
6. 点 `Play` 验证

如果某天 Excel 丢了，可以用：

- `Tools/TactiRogue/Excel/Export Current Data To Excel`

把当前工程里真实生效的数据重新导出成一份新工作簿。

## 3. 三个菜单分别做什么

### 3.1 Export Current Data To Excel

用途：

- 从当前项目资源反推一份最新 Excel

适合：

- 第一次迁移后回收数据
- Excel 丢失后恢复
- 想确认当前运行时真实值时导出对照

### 3.2 Validate Excel Workbook

用途：

- 只检查，不写回项目资源

会拦截的问题：

- 缺表
- 缺列
- 空 `Id`
- 重复 `Id`
- 错误的枚举名
- 错误的布尔值
- 错误的颜色值
- 坏外键

### 3.3 Import Excel To Game Data

用途：

- 用 Excel 全量重建项目当前数据

会覆盖：

- `Generated/*.asset`
- `TactiRogueContentDatabase.asset`
- 所有 `Scenario` JSON

所以导入前默认要把 Excel 当成你想保留的最终版本。

## 4. 每张表是干什么的

### 4.1 Status

改状态和关键词。

常改字段：

- `DisplayName`
- `Description`
- `GrantedKeyword`
- `AttackModifier`
- `PushModifier`
- `DefaultDuration`

### 4.2 Action

改单位主动行为或法术行为。

常改字段：

- `DisplayName`
- `Description`
- `ActionKind`
- `TargetMode`
- `TargetFilter`
- `MinRange`
- `MaxRange`
- `DamageAmount`
- `PushForce`
- `Radius`
- `ApplyStatusId`
- `ExtraActionsGranted`
- `SkipMovePhase`

理解重点：

- `SkipMovePhase = false`
  - 这个行为会走“先通用移动，再执行行为”
- `SkipMovePhase = true`
  - 这个行为跳过通用移动，直接执行

### 4.3 MoveProfile

改单位置于行为之前的通用移动规则，不是改技能自带位移。

常改字段：

- `UseSeparateMovePhase`
- `MoveRange`
- `MoveType`
- `AllowStayInPlace`
- `AllowDiagonalMove`

理解重点：

- 这张表控制的是“单位在行为前能不能先走、能走多远”
- 不是控制 `charger_rush` 这种技能自带位移

### 4.4 Entity

改单元模板本身。

常改字段：

- `DisplayName`
- `ShortLabel`
- `Description`
- `MaxHp`
- `Attack`
- `PushBonus`
- `ActionId`
- `IntentDefinitionId`
- `MoveProfileId`

理解重点：

- `ActionId` 是这个单位自己的主动行为
- `IntentDefinitionId` 是敌人用的 AI 配置
- `MoveProfileId` 指向 `MoveProfile` 表中的一行

### 4.5 EntityStatus

给单位配置初始状态。

例如：

- 让 `guardian` 自带嘲讽
- 让 `anchor_warden` 自带定锚

### 4.6 Intent

改敌方 AI。

常改字段：

- `ActionId`
- `AcquireRange`
- `PreferCommander`
- `TargetingMode`
- `RevalidationPolicy`
- `FallbackMode`

理解重点：

- `TargetingMode`
  - 决定锁人、锁格子还是锁方向
- `RevalidationPolicy`
  - 决定玩家改变局面后，这个意图怎么重新校验
- `FallbackMode`
  - 决定意图失效后是跳过还是重新找目标

### 4.7 Card

改卡牌。

常改字段：

- `DisplayName`
- `Description`
- `Cost`
- `ActionId`
- `SummonEntityId`
- `SummonMinRange`
- `SummonMaxRange`

### 4.8 Scenario

改战斗预设的基础参数。

常改字段：

- `DisplayName`
- `Description`
- `DisplayOrder`
- `Width`
- `Height`
- `StartingMana`
- `MaxMana`
- `CardsPerTurn`

### 4.9 ScenarioSpawn

改每个预设开局刷什么单位、刷在哪。

字段：

- `ScenarioId`
- `Order`
- `TemplateId`
- `Team`
- `X`
- `Y`

### 4.10 ScenarioDeck

改每个预设的起始牌组。

字段：

- `ScenarioId`
- `Order`
- `CardId`

### 4.11 ScenarioVoidCell

改不能站立的空洞格。

字段：

- `ScenarioId`
- `Order`
- `X`
- `Y`

## 5. 卡牌系统现在怎么工作

### 5.1 四个主要卡区

战斗中现在会使用四个卡区：

- 抽牌堆 `DrawPile`
- 手牌 `Hand`
- 弃牌堆 `DiscardPile`
- 场上绑定单位卡 `InBattleUnit`

你在 Excel 里只需要配置起始牌组，不需要手工配置这四个运行时卡区。

### 5.2 回合开始抽牌

每个玩家新回合开始时，系统会：

1. 进入抽牌阶段
2. 从 `DrawPile` 抽到 `CardsPerTurn`
3. 若抽牌堆空了，但弃牌堆还有牌，就把弃牌堆洗回抽牌堆继续抽
4. 若两边都空，就结束本次抽牌，不会有疲劳伤害

这部分规则和《杀戮尖塔》的基础抽弃牌循环接近。

### 5.3 回合结束弃牌

玩家回合结束时：

- 手牌中没有打出的法术卡会进入弃牌堆
- 手牌中没有打出的单位卡也会进入弃牌堆

### 5.4 单位卡的例外

单位卡和普通法术卡最大的区别是：

- 单位卡成功打出后，不会立刻进弃牌堆
- 它会与召唤出的场上单位绑定
- 只有这个单位死亡后，这张卡才会回到弃牌堆

所以你在战斗里看到：

- 打出单位卡后，手里少一张牌
- 弃牌堆数量不一定立刻增加
- 当那个召唤单位死亡时，弃牌堆数量才会增加

### 5.5 什么不会回收卡牌

以下单位死亡时，不会回收任何卡牌：

- 场景开局直接生成的单位
- 敌人
- 建筑
- 非单位卡来源的召唤物

## 6. 在沙盒里怎么验证卡牌循环

进入 `SampleScene` 后，你会看到：

- 顶部或右上方有 `DrawPile (N)` 和 `DiscardPile (N)` 按钮
- 底部是当前手牌

### 6.1 查看牌堆

点击：

- `DrawPile (N)`
- `DiscardPile (N)`

会打开一个只读查看面板，显示这个牌堆目前有哪些牌。

注意：

- 这是调试视图
- 它会显示牌堆里的所有卡
- 当前列表按 `DisplayName -> CardInstanceId` 排序
- 它不是“下一抽顺序”的可视化

### 6.2 看单位卡绑定

若某个场上友军是由单位卡召唤出来的：

- 选中该单位后，右侧详情面板会显示来源卡牌名

若不是卡牌来源：

- 会显示 `None`

### 6.3 看日志

右侧战斗日志现在会出现这些卡牌相关记录：

- 抽牌阶段开始
- 抽牌
- 弃牌堆洗回抽牌堆
- 法术结算后进弃牌堆
- 单位卡进战场
- 单位死亡后单位卡回弃牌堆
- 点击抽牌堆或弃牌堆按钮

## 7. 调数值时最常改的地方

### 7.1 想改一张牌的费用

改：

- `Card.Cost`

### 7.2 想改一张单位卡召唤谁

改：

- `Card.SummonEntityId`

### 7.3 想改一张法术卡效果

改：

- `Card.ActionId`
- 或对应 `Action` 表中的那条行为数据

### 7.4 想改单位走几格

改：

- `Entity.MoveProfileId`
- 对应 `MoveProfile.MoveRange`

### 7.5 想改敌人锁谁

改：

- `Intent.TargetingMode`
- `Intent.RevalidationPolicy`
- `Intent.FallbackMode`

### 7.6 想改某个预设的起始牌组

改：

- `ScenarioDeck`

### 7.7 想改每回合抽几张

改：

- `Scenario.CardsPerTurn`

## 8. 可以放心改什么，不建议乱改什么

### 8.1 可以放心改的

- 中文 `DisplayName`
- 中文 `Description`
- `ShortLabel`
- 数值
- 颜色
- 场景布局
- 卡牌费用
- 单位血量攻击
- AI 的目标与兜底策略

### 8.2 不建议随便改的

- `Id`
- `ActionId`
- `IntentDefinitionId`
- `SummonEntityId`
- `MoveProfileId`
- `StatusId`
- `TemplateId`
- `CardId`

这些都是逻辑外键。显示文本可以改，但逻辑 id 不建议轻易动。

## 9. 填表规则

- 枚举写代码里的枚举名
- 布尔写 `true` 或 `false`
- 颜色写 `#RRGGBB` 或 `#RRGGBBAA`
- `Order` 从 `0` 开始

不支持：

- Excel 公式
- 合并单元格
- 在导入表里自由排版说明区

如果你想写策划备注，建议额外开一张说明表，不要混在正式导入表中。

## 10. 每次改完表后的最低验证清单

建议至少做这几步：

1. `Validate Excel Workbook`
2. `Import Excel To Game Data`
3. 进入 `SampleScene`
4. 检查手牌是否正常显示
5. 检查 `DrawPile / DiscardPile` 数量是否正常
6. 打一张法术卡，看它是否进入弃牌堆
7. 打一张单位卡，看它是否先留在场上绑定
8. 杀掉该单位，看对应卡牌是否回到弃牌堆
9. 看右侧日志和快照是否符合预期

## 11. 如果看到异常，优先怎么判断

### 11.1 导入时报错

优先检查：

- 是否缺表
- 是否缺列
- 是否写错枚举
- 是否写坏了外键 id

### 11.2 牌堆数量不对

优先检查：

- `ScenarioDeck` 是否填错
- `CardsPerTurn` 是否过大
- 是否把单位卡打出后误以为应该立刻进入弃牌堆

### 11.3 单位卡没有回到弃牌堆

优先检查：

- 这个单位是不是确实由单位卡召唤出的
- 它是不是还没真正死亡
- 你看的是否是抽牌堆而不是弃牌堆

### 11.4 红色危险格或意图不对

优先检查：

- `Intent` 表中的目标策略
- 这个单位有没有被位移、击杀或嘲讽重定向
- 当前看到的是不是预览态而不是实时态
