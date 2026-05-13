# TactiRogue 技术细节文档

## 1. 当前版本定位

当前工程已经形成一套可独立运行的战斗沙盒，核心范围包括：

- 棋盘战斗结算
- 双阶段单位行动
- 敌方意图与重校验 AI
- 卡牌使用、抽牌堆、弃牌堆、场上绑定单位卡
- 预览系统
- 占位 HUD 与调试面板
- Excel 作为唯一长期策划数据源

棋子表现层级、Frame/Portrait 绑定和 Motion 播放规则见：

- [TactiRogue 棋子表现与 Motion 使用说明](TactiRoguePresentationMotionGuide.md)

运行时正式加载的资源仍然是：

- `Assets/Resources/TactiRogue/Content/TactiRogueContentDatabase.asset`
- `Assets/Resources/TactiRogue/Content/Generated/*.asset`
- `Assets/Resources/TactiRogue/Scenarios/*.json`

策划长期编辑入口是：

- `Assets/TactiRogue/DataAuthoring/TactiRogueData.xlsx`

也就是说，Excel 是源数据，运行时资源是导入产物。

## 2. 核心架构

战斗内核采用统一解析链：

- `BattleState`
- `ActionRequest / UnitTurnRequest / PlayCardRequest / PreviewRequest`
- `TactiRogueEngine`
- `BattleEvent / ActionResult / PreviewResult / BattleSnapshot`

预览和正式执行共用同一套规则解析逻辑。预览不会写回真实状态，而是在克隆态上执行，再把结果回传给 HUD。

主要代码位置：

- 运行时模型：
  - `Assets/TactiRogue/Runtime/Model/TactiRogueModel.cs`
- 请求、结果、事件：
  - `Assets/TactiRogue/Runtime/Simulation/TactiRogueRequests.cs`
- 战斗主引擎：
  - `Assets/TactiRogue/Runtime/Simulation/TactiRogueEngine.cs`
- 内部辅助逻辑：
  - `Assets/TactiRogue/Runtime/Simulation/TactiRogueEngine.Internal.cs`
- 战斗、碰撞与死亡：
  - `Assets/TactiRogue/Runtime/Simulation/TactiRogueEngine.Combat.cs`
- 敌方意图与 AI：
  - `Assets/TactiRogue/Runtime/Simulation/TactiRogueEngine.Intents.cs`
- 单位双阶段行动：
  - `Assets/TactiRogue/Runtime/Simulation/TactiRogueEngine.UnitTurns.cs`
- 占位 HUD：
  - `Assets/TactiRogue/Runtime/UI/TactiRogueBattleSandbox.cs`
  - `Assets/TactiRogue/Runtime/UI/TactiRogueBattleSandboxViews.cs`

## 3. 战斗状态结构

### 3.1 BattleState

`BattleState` 现在包含以下关键运行时数据：

- `Grid`
- `Entities`
- `EnemyIntents`
- `CardInstances`
- `DrawPile`
- `Hand`
- `DiscardPile`
- `InBattleUnitCards`
- `CurrentMana / MaxMana`
- `TurnNumber`
- `Phase`

### 3.2 BattlePhase

当前阶段枚举为：

- `PlayerDrawPhase`
- `PlayerAction`
- `EnemyAction`
- `Victory`
- `Defeat`

`PlayerDrawPhase` 已经是正式运行时阶段，但仍按同步解析实现，不会额外停下来等待输入。

## 4. 单位行动模型

### 4.1 双阶段行动

标准我方单位与指挥官采用：

1. 通用移动阶段
2. 行为阶段

特殊单位或特殊行为可通过 `SkipMovePhase` 跳过通用移动阶段，直接执行行为。

相关类型：

- `MoveProfile`
- `UnitTurnRequest`
- `UnitTurnStage`

当前输入状态机：

- `Idle`
- `MoveTargeting`
- `BehaviorTargeting`
- `CardSelected`
- `CardTargeting`

### 4.2 敌方行动

敌人也走统一模型：

- 先依据意图求本回合行为目标
- 若该行为不跳过通用移动，则先找本回合移动终点
- 然后执行行为

红色危险格表示“敌方下一次行为的危险区域”，不表示它的完整移动路径。

## 5. AI 与意图系统

### 5.1 配置入口

敌方 AI 继续使用 `IntentDefinition` 配置，不额外新增第二套行为树或 GOAP 系统。

关键字段：

- `IntentKind`
- `ActionId`
- `AcquireRange`
- `PreferCommander`
- `TargetingMode`
- `RevalidationPolicy`
- `FallbackMode`

### 5.2 运行时意图状态

`IntentState` 会保存锁定结果，而不仅是摘要：

- `TargetEntityId`
- `TargetCell`
- `Direction`
- `DangerCells`
- `IsCancelled`
- `DebugReason`

### 5.3 实时刷新

玩家成功执行以下行为后，敌方意图会立即刷新：

- 单位行为
- 卡牌使用
- 位移
- 召唤
- 击杀

所以右侧意图面板和棋盘危险格会实时反映最新局面。

## 6. 卡牌循环系统

### 6.1 设计目标

当前卡牌循环遵循“接近杀戮尖塔”的基础抽弃牌规则，并加入本项目的单位卡特例。

卡区固定为：

- `DrawPile`
- `Hand`
- `DiscardPile`
- `InBattleUnit`
- `None`

### 6.2 数据结构

`CardInstance` 现在是正式运行时实例状态，关键字段：

- `CardInstanceId`
- `TemplateId`
- `CurrentZone`
- `BoundEntityId`

`BattleState` 中相关结构：

- `CardInstances: Dictionary<int, CardInstance>`
- `DrawPile: List<int>`
- `Hand: List<int>`
- `DiscardPile: List<int>`
- `InBattleUnitCards: Dictionary<int, int>`

含义如下：

- `CardInstances` 是卡实例注册表
- 三个主要卡区只保存卡实例 id 的顺序
- `InBattleUnitCards` 记录“场上某个实体对应哪一张单位卡”

### 6.3 抽牌流程

玩家新回合开始时：

1. 进入 `PlayerDrawPhase`
2. 从 `DrawPile` 抽到 `CardsPerTurn`
3. 若抽牌堆为空且弃牌堆非空，则整堆洗回后继续抽
4. 若抽牌堆和弃牌堆都空，则本次抽牌结束，不产生疲劳
5. 抽牌结束后进入 `PlayerAction`

### 6.4 弃牌流程

玩家回合结束时：

- 手牌中剩余的法术卡进入弃牌堆
- 手牌中剩余的未打出单位卡也进入弃牌堆
- 已经打出并绑定在场上实体的单位卡不参与这一步

### 6.5 单位卡特例

单位卡成功使用后：

- 不进入弃牌堆
- 会与召唤出的实体绑定
- 该卡离开 `Hand`，进入 `InBattleUnit`
- 对应实体死亡时，这张卡才回到 `DiscardPile`

注意：

- 只有由单位卡实际召唤出的实体才会回收卡牌
- 场景初始单位、敌人、建筑、非卡牌来源召唤物死亡时，不会触发卡牌回收

### 6.6 主要卡区辅助方法

实现集中在 `TactiRogueEngine.Internal.cs`：

- `MoveCardToZone`
- `DrawCards`
- `DrawOneCard`
- `ShuffleDiscardIntoDrawPile`
- `DiscardHand`
- `BindUnitCardToEntity`
- `ReturnBoundUnitCardToDiscard`

这些方法是当前卡区状态变更的唯一正式入口，避免了直接改列表导致的数据不同步。

## 7. 出牌规则

### 7.1 法术卡

法术卡成功结算后：

- 扣费
- 解析行为
- `Hand -> DiscardPile`

相关事件：

- `CardPlayed`
- `PlaySpellToDiscard`

### 7.2 单位卡

单位卡成功结算后：

- 扣费
- 召唤实体
- `Hand -> InBattleUnit`
- 记录实体与卡牌绑定

相关事件：

- `CardPlayed`
- `PlayUnitToBattle`
- `EntitySummoned`

### 7.3 出牌失败

若目标非法或费用不足：

- 不扣费
- 不换区
- 不改变绑定关系

## 8. 预览系统

### 8.1 单位预览

单位预览分两类：

- `MoveTargeting`
  - 预览可达格和移动路径
- `BehaviorTargeting`
  - 先在克隆态里把单位临时移动到已选落点
  - 再预览行为结果与模拟后的敌方危险区

### 8.2 卡牌预览

卡牌预览也在克隆态上运行：

- 法术卡预览会模拟费用消耗和行为结果
- 单位卡预览会模拟召唤和单位卡绑定
- 真实状态中的手牌、抽牌堆、弃牌堆、绑定关系不会被污染

## 9. HUD 与调试显示

### 9.1 棋盘颜色叠加顺序

当前棋盘颜色逻辑顺序为：

1. 基础格
2. 危险格
3. 可移动或可选目标
4. 移动路径
5. 冲击
6. 碰撞
7. 选中单位

这样红色危险格会常驻显示，但不会压过当前操作的主要预览。

### 9.2 牌堆查看

占位 HUD 新增两个只读按钮：

- `DrawPile (N)`
- `DiscardPile (N)`

点击后打开同一个查看面板：

- 只显示当前该牌堆中有哪些牌
- 不承诺展示真实未来抽牌顺序
- 当前实现按 `DisplayName -> CardInstanceId` 排序

### 9.3 单位详情

若某单位由单位卡召唤：

- 单位详情面板会显示来源卡牌名

若不是：

- 显示 `None`

## 10. 事件与日志

### 10.1 新增事件

卡牌循环补充后，关键事件包括：

- `DrawPhaseStarted`
- `CardDrawn`
- `ShuffleDiscardIntoDrawPile`
- `PlaySpellToDiscard`
- `PlayUnitToBattle`
- `UnitCardReturnedToDiscard`
- `DrawPileClicked`
- `DiscardPileClicked`

### 10.2 Snapshot

`BattleSnapshot` 当前会输出：

- `Hand`
- `DrawPile`
- `DiscardPile`
- `InBattleUnitCards`
- `Intents`
- `IntentDetails`
- `Entities`

其中 `InBattleUnitCards` 会记录：

- `EntityId`
- `EntityTemplateId`
- `CardInstanceId`
- `CardTemplateId`

## 11. Excel 数据源

### 11.1 工作流

当前策划工作流固定为：

1. 导出现有数据为首版 Excel
2. 策划修改 `TactiRogueData.xlsx`
3. 校验工作簿
4. 导入重建运行时资源

编辑器菜单：

- `Tools/TactiRogue/Excel/Export Current Data To Excel`
- `Tools/TactiRogue/Excel/Validate Excel Workbook`
- `Tools/TactiRogue/Excel/Import Excel To Game Data`

### 11.2 表结构

主表：

- `Status`
- `Action`
- `MoveProfile`
- `Entity`
- `Intent`
- `Card`
- `Scenario`

子表：

- `EntityStatus`
- `ScenarioSpawn`
- `ScenarioDeck`
- `ScenarioVoidCell`

### 11.3 本次卡牌循环与 Excel 的关系

本次补充的抽牌堆、弃牌堆、场上单位卡绑定不新增 Excel authoring 表。

原因是：

- `Scenario.StartingDeck` 依然足够描述初始卡池
- 抽牌、弃牌、绑定属于运行时状态
- 这些状态不应该回写到策划静态配置表中

## 12. 测试覆盖

### 12.1 EditMode

当前测试覆盖重点包括：

- 碰撞与暴击规则
- 双阶段单位行动
- AI 意图生成、重校验与取消
- 预览一致性
- Excel 导入导出校验
- 抽牌堆、弃牌堆、单位卡绑定与死亡回收

### 12.2 PlayMode

当前 PlayMode 测试覆盖重点包括：

- 沙盒场景加载
- 切换战斗预设
- 常驻危险格显示
- 双阶段输入链路
- 抽牌堆与弃牌堆查看入口

## 13. 已知边界

当前版本仍然没有引入以下复杂卡区规则：

- 疲劳
- 放逐
- 消耗
- 烧牌
- 临时生成卡
- 保留手牌
- 额外独立的牌库顺序可视化

这些都可以在现有 `CardZone + CardInstance + BattleState.CardInstances` 结构上继续扩展。
