## Idiomatic Mako large-loop: % for control lines, one row per source line; the
## display name row-`value` is composed by the template.
## Doc: https://docs.makotemplates.org/en/latest/syntax.html#control-structures
% for item in items:
<tr><td>row-${item["value"]}</td><td>${item["value"]}</td></tr>
% endfor
