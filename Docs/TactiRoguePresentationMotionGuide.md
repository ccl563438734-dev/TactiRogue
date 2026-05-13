# TactiRogue 棋子表现与 Motion 使用说明

这份文档说明战场棋子的模型绑定、默认姿态、贴图规则和 Motion 编辑器的使用方式。它面向策划和技术共同查看。

## 1. 棋子表现结构

运行时每个战场棋子都会生成标准层级：

- `UnitRoot`
- `MotionRoot`
- `RotationRoot`
- `ScaleRoot`
- `VisualRoot`
- `Frame`
- `Portrait`
- `ShadowSocket / VFXSocket / SelectionSocket`

`UnitRoot` 只负责放到格子中心。移动、攻击、受击、死亡等表现动画都应该作用在 `MotionRoot` 之后的表现层，不反向影响战斗逻辑坐标。

## 2. FrameModelKey 与模型命名

`CardPieceVisual.FrameModelKey` 是战场棋子模型的主要绑定字段。

当前默认资源：

- 普通单位：`Assert/Model/F_Unit`
- 建筑、障碍：`Assert/Model/F_Structure`
- 兜底模型：`Assert/Model/F_Base`

FBX 内部需要包含命名好的子节点：

- `Frame`：卡牌框模型
- `Portrait`：卡牌片/肖像承载面

运行时会从同一个 FBX 里拆出 `Frame` 和 `Portrait`，分别挂到标准层级的 `Frame` 与 `Portrait` 根节点下。这样卡牌框和卡牌片会使用同一套导入姿态，并被同一个 `RotationRoot / ScaleRoot / VisualRoot` 统一旋转和缩放。

如果模型里没有 `Portrait` 节点，运行时才会生成临时 `PortraitSurface` 兜底。正式资源不建议依赖这个兜底。

## 3. IdleTiltAngle 的含义

`IdleTiltAngle` 保留为策划调参字段，默认值仍为 `45`。

它不是单独旋转 Frame 或 Portrait，而是统一作用到 `RotationRoot`。因此：

- Frame 和 Portrait 必须同步倾斜。
- 不要在 Frame 或 Portrait 子节点上单独写死 45 度旋转。
- 如果棋子看起来翻转方向不一致，优先检查模型是否正确包含 `Frame` / `Portrait`，以及 `FrameModelKey` 是否仍为空或误用 `sample`。

## 4. Portrait 贴图规则

`CardPieceVisual.CardArtKey` 继续指定单位图片。

运行时只会通过 `MaterialPropertyBlock` 把图片设置到 `Portrait` 的 Renderer 上：

- 不修改 Frame 材质。
- 不修改共享材质上的 `_MainTex`。
- 不把单位图贴到整个模型或 Frame 上。

如果需要换框材质，使用 `FrameMaterialKey`；如果只是换单位图，改 `CardArtKey`。

## 5. Motion 编辑器使用

入口：

- `Tools/TactiRogue/Motion Definition Editor`

常用流程：

1. 选择或创建一个 `MotionDefinition`。
2. 在左侧选择动作类别：Idle、Move、Attack、Hit、Death、Spawn。
3. 在中间添加或调整 Segment。
4. 在右侧设置目标层、位移、旋转、缩放、时长和 Ease。
5. 点击 `Preview` 预览，点击 `Stop` 停止，点击 `Reset` 恢复默认表现状态。

建议：

- 位移优先用 `MotionRoot`。
- 转身或倾斜优先用 `RotationRoot`。
- 缩放优先用 `ScaleRoot`。
- 只在确实需要局部效果时才直接动 `Frame` 或 `Portrait`。

## 6. 常见问题排查

### Frame 和 Portrait 旋转方向不一致

优先检查：

- `FrameModelKey` 是否是 `Assert/Model/F_Unit`、`Assert/Model/F_Structure` 或 `Assert/Model/F_Base`。
- FBX 内部是否存在同级或可查找到的 `Frame`、`Portrait` 节点。
- 是否误用了旧的 `Assert/Model/sample`。
- 是否有 Motion Segment 单独作用到了 `Frame` 或 `Portrait`，但没有在结束后重置。

### 看不到单位图片

优先检查：

- `CardArtKey` 是否填对。
- 模型里是否有 `Portrait` 节点和 Renderer。
- 是否被运行时兜底 `PortraitSurface` 替代。

### 动画结束后姿态不对

优先检查：

- Motion 是否设置为播放后重置。
- Segment 是否动了正确层级。
- `ResetVisualState()` 是否能把 `MotionRoot / RotationRoot / ScaleRoot / VisualRoot / Frame / Portrait` 恢复到默认状态。

## 7. Excel 字段速查

`CardPieceVisual` 中与棋子表现相关的字段：

- `ModelKey`：旧模型字段，只作为兼容和最后兜底。
- `CardArtKey`：单位图片，贴到 Portrait。
- `BackArtKey`：保留给兼容和未来卡背。
- `IdleTiltAngle`：统一默认倾斜角，默认 45。
- `DefaultScale`：统一默认缩放。
- `YOffset`：棋子离格子中心的垂直偏移。
- `FrameModelKey`：战场棋子模型，优先使用。
- `FrameMaterialKey`：可选 Frame 材质。
- `IdleMotionKey / MoveMotionKey / AttackMotionKey / HitMotionKey / DeathMotionKey / SpawnMotionKey`：各动作 Motion 资源。
