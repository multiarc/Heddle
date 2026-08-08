<%page args="column"/>\
<div class="nav-column">\
% for section in column["sections"]:
<%include file="nav-section.mako" args="section=section"/>\
% endfor
</div>\
