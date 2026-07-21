@param java.util.List<heddle.benchmarks.jvm.model.Models.EncodedLoopRow> items
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/ (loops: @for, output expressions)
  https://jte.gg/html-rendering/ (ContentType.Html, stock OWASP-compliant output escaping,
  context-aware attribute escaping)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
<table>
    @for(var item : items)
        <tr>
            <td data-tag="${item.getTag()}">${item.getName()}</td>
            <td>${item.getComment()}</td>
        </tr>
    @endfor
</table>
