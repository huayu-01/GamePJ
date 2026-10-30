from pathlib import Path

root = Path.cwd()
page = root / 'outputs/visual-047-048-20261028/index.html'
content = page.read_text(encoding='utf-8')
content = content.replace('body{margin:0;background:#f3f0e4;color:#173e35;', 'body{margin:0;background:#eef1f3;color:#203541;')
content = content.replace('href="../../docs/reviews/20261028/visual-047-048.md"', 'href="../../docs/reviews/20261028/visual-047-048-current.md"')
content = content.replace('当前工程实拍、四版指示器、三个UI页面。这里是方案评审页，不是已接入游戏的运行效果。所有警告使用同一底图、同一落点与同一排版尺度。', '当前推进：C指示器，正常场景深度；简化毛玻璃UI与图标，页面按键栏。三选一卡牌中央烟灰半透明，边缘磨砂。以下均为概念稿，未替换生产资源。')
marker = '<h2>新增D · 极其简化，只保留轮廓</h2>'
current = '''<section class="section"><h2>当前卡牌材质 · 中央透、边缘雾</h2>
<p>按最新反馈重做：中部偏灰而通透，让庭院形体透出来；接近边缘才逐渐雾化。保留薄边，不做厚玻璃相框。底部列出数字选择、鼠标操作与Esc设置，没有状态示例。</p>
<img class="concept" src="boards/ui-reward-edge-frost-v3.png" alt="当前奖励三选一：烟灰透明中央、边缘磨砂，简化线性图标和固定按键栏">
<p class="stack">当前稿v3。关键文字使用稳定灰衬与浅字；实际明暗地表对比仍需材质原型测试。Enter／Space的焦点与默认第1项旁路需在落地时统一，不能误标为确认悬停项。</p>
<h2>构筑与图鉴 · 简化磨砂阅读面</h2><p>长文页面保留更稳定的均匀磨砂底，统一薄边、图标和字级；分别显示本页有效按键。搜索框焦点下图鉴的J按文字输入处理，键栏需动态更新。</p>
<div class="ui"><figure><img class="concept" src="boards/ui-build-glass-v2.png" alt="当前构筑：左库存右详情，简化线性图标，底部C、Esc、方向键、鼠标和滚轮提示"><figcaption><b>本局构筑</b> · 保留左半库存、右半详情；效果／条件／适用分区，不显示全项层数。</figcaption></figure>
<figure><img class="concept" src="boards/ui-codex-glass-v2.png" alt="当前图鉴：磨砂面板、简化图标、模型资料与底部按键栏"><figcaption><b>旅途图鉴</b> · 章节、搜索、条目与详情；模型仍读正式档案，图中小图标与数量不是新的内容登记。</figcaption></figure></div>
<h2>指示器 · C方向继续收敛</h2><p>已记录用户倾向C。撤销强制顶层渲染，采用正常场景深度；贴地、墙体和高差遮挡按空间关系处理，不默认穿墙。原板未模拟深度，仅保留为艺术方向参考。</p><a href="boards/047-direction-c.png">查看C四类技能／四阶段</a>
<p><a href="../../docs/reviews/20261028/visual-047-048-current.md">当前材质、按键语义与验收方案</a> · <a href="support/imagegen-prompts-glass-v2.md">本轮修订提示词</a></p></section>
<details class="section"><summary>历史指示器方向比较（不代表当前层级要求）</summary>
'''
assert marker in content
content = content.replace(marker, current + marker, 1)
old_ui = '<section class="section"><h2>048 · 一套语言，三个页面</h2>'
assert old_ui in content
content = content.replace(old_ui, '</details><details class="section"><summary>历史纸面UI与过程稿（已由毛玻璃方案取代）</summary><section class="section"><h2>旧版纸面方向</h2>', 1)
evidence = '<section class="section"><h2>本轮实拍证据</h2>'
assert evidence in content
content = content.replace(evidence, '</details><section class="section"><h2>初次评估实拍证据</h2>',1)
content = content.replace('推荐基础：负空间多，保留人物脚下信息。','当前推进方向：负空间多，保留人物脚下信息。')
page.write_text(content, encoding='utf-8')
print('评审页已更新为当前C与毛玻璃三页；旧稿折叠留档。')
