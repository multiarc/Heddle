## Idiomatic Mako composed-page navigation: the structured nav model
## rendered through nested <%def> blocks -- link -> section -> column -> mega menu --
## imported by the layout via <%namespace> and called from % for loops.
## Definition-only: importing this file renders nothing.
## Docs: https://docs.makotemplates.org/en/latest/defs.html
##       https://docs.makotemplates.org/en/latest/namespaces.html
<%def name="nav_link(link)">
<li class="nav-link"><a href="${link["href"]}">${link["label"]}</a></li>
</%def>
<%def name="nav_section(section)">
<div class="nav-section">
% if section["title_linked"]:
    <span class="nav-title"><a href="${section["href"]}">${section["title"]}</a></span>
% else:
    <span class="nav-title">${section["title"]}</span>
% endif
    <ul>
% for link in section["links"]:
        ${nav_link(link)}
% endfor
    </ul>
</div>
</%def>
<%def name="nav_column(column)">
<div class="nav-column">
% for section in column["sections"]:
    ${nav_section(section)}
% endfor
</div>
</%def>
<%def name="mega_menu(menu)">
<div class="top-menu-wrapper">
    <ul class="top-menu">
% for tab in menu["tabs"]:
        <li class="${tab["css"]}"><a href="${tab["href"]}" class="drop">${tab["label"]}</a>
% if tab["has_dropdown"]:
            <div class="${tab["dropdown_css"]}">
% for column in tab["columns"]:
                ${nav_column(column)}
% endfor
            </div>
% endif
        </li>
% endfor
    </ul>
</div>
</%def>
