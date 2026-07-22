@param java.util.List<heddle.benchmarks.jvm.model.Models.FortuneRow> rows
<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr>@for(var r : rows)<tr><td>${r.getId()}</td><td>${r.getMessage()}</td></tr>@endfor</table></body></html>
