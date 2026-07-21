@param java.util.List<heddle.benchmarks.jvm.model.Models.LoopRow> items
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/ (loops: @for/@endfor, output expressions)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
@for(var r : items)
    <tr>
        <td>${r.getName()}</td>
        <td>${r.getValue()}</td>
    </tr>
@endfor
