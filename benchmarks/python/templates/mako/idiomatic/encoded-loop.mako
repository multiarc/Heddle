<%doc>
Idiomatic Mako encoded-loop: escaping is declared in the template via
<%page expression_filter="h"/>; the attribute value position is escaped by
the same h filter (Phase 5 D4/D5).
Doc: https://docs.makotemplates.org/en/latest/filtering.html
</%doc>
<%page expression_filter="h"/>
<table>
% for item in items:
<tr><td data-tag="${item["tag"]}">${item["name"]}</td><td>${item["comment"]}</td></tr>
% endfor
</table>
