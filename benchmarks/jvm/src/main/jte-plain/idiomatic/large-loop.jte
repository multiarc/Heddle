@param java.util.List<heddle.benchmarks.jvm.model.Models.LoopRow> items
<%--
  Idiomatic jte authoring — official doc citations:
  https://jte.gg/syntax/ (loops: @for/@endfor, output expressions)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
@for(var r : items)
    <tr>
        <td>row-${r.getValue()}</td>
        <td>${r.getValue()}</td>
    </tr>
@endfor
