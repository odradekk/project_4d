# 四维与三维框架

> 状态：正式开发的约定稿。规则的玩法含义见 [gameplay.md](../gameplay.md)，世界观见 [story.md](../story.md)。文中的数值源自序章原型（已归档，代码位于 `archives/threejs-demo/src/prologue/`），已在原型中验证过的设计会明确注明；尚未敲定的部分集中在「引擎模块」页的「待定问题」。

## 目录

- [四维计算原则](principles.md)：目的、核心原则与分类判断标准。
- [内容分类](content.md)：各类内容的形状、行为、计算方式与资产制作。
- [跨截面规则与尺寸规范](rules.md)：跨截面交互及关卡尺寸约束。
- [引擎模块](engine.md)：三层分工、解耦规则、接口分组、模块与校验流程。
- [家的区域](home-region.md)：序章里家和小满房间的限制，以及留给第 8、9 场的位置。
- [学校区域](school-region.md)：教室的尺寸、两页的差别、座位和视线。

## 公共契约（待批准）

实现引擎、适配层、场景标注或谜题查询前，读[公共契约](contract.md)。实体形状见[数据定义](contract-data.md)，运动和事件见[动作与推进](contract-actions.md)，抓取、携带和受控布置见[抓取与布置](contract-carry.md)，网格和通用查询见[截面与查询](contract-views.md)，关卡制作与报错见[场景标注](scene-authoring.md)，放样装置的搭法见[装置搭法](device-authoring.md)，角色的搭法见[角色搭法](character-authoring.md)，啃食者的搭法见[啃食者搭法](grazer-authoring.md)。契约未经用户批准前不作为实现授权。
