@param java.util.List<heddle.benchmarks.jvm.model.Models.EncodedLoopRow> items
<table>@for(var item : items)<tr><td data-tag="${item.getTag()}">${item.getName()}</td><td>${item.getComment()}</td></tr>@endfor</table>
