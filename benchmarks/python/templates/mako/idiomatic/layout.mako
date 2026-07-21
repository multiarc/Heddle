<%doc>
Idiomatic Mako layout for composed-page: renders the section/component
fragments and the ordered area menus, then calls the inheriting page's
(empty) body (Phase 5 D5).
Docs: https://docs.makotemplates.org/en/latest/inheritance.html
      https://docs.makotemplates.org/en/latest/syntax.html#control-structures
</%doc>
${section["meta"]}
${section["social"]}
${comp["assets_styles"]}
${comp["custom_styles"]}
${comp["head_scripts"]}
${comp["body_scripts"]}
% for name in area_names:
${areas[name]}
% endfor
${comp["assets_scripts"]}
${section["page_scripts"]}
${section["endpage_scripts"]}
${comp["body_end_scripts"]}
${self.body()}
