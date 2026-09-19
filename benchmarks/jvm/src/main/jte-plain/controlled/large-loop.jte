@param java.util.List<heddle.benchmarks.jvm.model.Models.LoopRow> items
@for(var r : items)<tr><td>row-${r.getValue()}</td><td>${r.getValue()}</td></tr>@endfor
