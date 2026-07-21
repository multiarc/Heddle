<%doc>
Idiomatic Mako large-loop: % for control lines, one row per source line
(Phase 5 D5).
Doc: https://docs.makotemplates.org/en/latest/syntax.html#control-structures
</%doc>
% for item in items:
<tr><td>${item["name"]}</td><td>${item["value"]}</td></tr>
% endfor
