@param java.util.List<heddle.benchmarks.jvm.model.Models.FragmentRow> items
<div class="panel">@for(var item : items)@if(item.isTile())@template.controlled.tile(item)@elseif(item.isCard())@template.controlled.card(item)@elseif(item.isMedia())@template.controlled.mediarow(item)@else@template.controlled.stat(item)@endif@endfor</div>
