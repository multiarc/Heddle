@param java.util.List<heddle.benchmarks.jvm.model.Models.FortuneRow> rows
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/ (loops: @for, output expressions)
  https://jte.gg/html-rendering/ (ContentType.Html, stock OWASP-compliant output escaping)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
<!DOCTYPE html>
<html>
<head>
<title>Fortunes</title>
</head>
<body>
<table>
    <tr>
        <th>id</th>
        <th>message</th>
    </tr>
    @for(var r : rows)
        <tr>
            <td>${r.getId()}</td>
            <td>${r.getMessage()}</td>
        </tr>
    @endfor
</table>
</body>
</html>
