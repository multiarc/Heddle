@param java.util.List<heddle.benchmarks.jvm.model.Models.LoopRow> items
@for(var r : items)<tr><td>${r.getName()}</td><td>${r.getValue()}</td></tr>@endfor
