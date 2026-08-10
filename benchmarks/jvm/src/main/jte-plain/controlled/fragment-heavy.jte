@param java.util.List<heddle.benchmarks.jvm.model.Models.FragmentRow> items
<div class="panel">@for(var item : items)@if(item.isTile())@template.controlled.shared.tile(item)@elseif(item.isCard())@template.controlled.shared.card(item)@elseif(item.isMedia())@template.controlled.shared.mediarow(item)@else@template.controlled.shared.stat(item)@endif@endfor</div>
