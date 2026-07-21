<%doc>
Idiomatic Mako fragment-heavy: imports the tile def from its namespace and
calls it per item (Phase 5 D5).
Docs: https://docs.makotemplates.org/en/latest/defs.html
      https://docs.makotemplates.org/en/latest/namespaces.html
</%doc>
<%namespace file="tile.mako" import="tile"/>
<div class="panel">
% for item in items:
${tile(item)}
% endfor
</div>
