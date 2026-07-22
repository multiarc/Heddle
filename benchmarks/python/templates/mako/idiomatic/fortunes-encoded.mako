<%doc>
Idiomatic Mako fortunes-encoded: escaping is declared in the template via
<%page expression_filter="h"/> -- the filtering docs' template-declared
escaping pattern; no per-expression filter appears (Phase 5 D4/D5).
Doc: https://docs.makotemplates.org/en/latest/filtering.html
</%doc>
<%page expression_filter="h"/>
<!DOCTYPE html>
<html>
<head>
<title>Fortunes</title>
</head>
<body>
<table>
<tr><th>id</th><th>message</th></tr>
% for r in rows:
<tr><td>${r["id"]}</td><td>${r["message"]}</td></tr>
% endfor
</table>
</body>
</html>
