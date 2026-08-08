## Idiomatic Mako conditional-heavy: % if / % elif / % else control lines inside a
## % for loop, authored naturally; the note text (note `seq`) is composed by the
## template.
## Doc: https://docs.makotemplates.org/en/latest/syntax.html#control-structures
<ul class="matrix">
% for r in rows:
<li>
% if r["is_bronze"]:
<span class="t0">bronze</span>
% elif r["is_silver"]:
<span class="t1">silver</span>
% elif r["is_gold"]:
<span class="t2">gold</span>
% else:
<span class="t3">platinum</span>
% endif
<em>${r["name"]}</em>
% if r["has_note"]:
<small>note ${r["seq"]}</small>
% endif
% if r["is_active"]:
<b>active</b>
% endif
</li>
% endfor
</ul>
