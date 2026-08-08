## Idiomatic Mako fragment-heavy (ledger E20): per-row four-way dispatch on the
## precomputed booleans with % if / % elif / % else control lines, invoking the
## per-kind <%def>s imported from fragments.mako via <%namespace> (Phase 5 D5, Q1.7).
## Docs: https://docs.makotemplates.org/en/latest/defs.html
##       https://docs.makotemplates.org/en/latest/namespaces.html
##       https://docs.makotemplates.org/en/latest/syntax.html#control-structures
<%namespace file="fragments.mako" import="tile, card, media_row, stat"/>
<div class="panel">
% for item in items:
% if item["is_tile"]:
${tile(item)}
% elif item["is_card"]:
${card(item)}
% elif item["is_media"]:
${media_row(item)}
% else:
${stat(item)}
% endif
% endfor
</div>
