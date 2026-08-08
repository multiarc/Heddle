<div class="panel">\
% for item in items:
% if item["is_tile"]:
<%include file="tile.mako" args="item=item"/>\
% elif item["is_card"]:
<%include file="card.mako" args="item=item"/>\
% elif item["is_media"]:
<%include file="media-row.mako" args="item=item"/>\
% else:
<%include file="stat.mako" args="item=item"/>\
% endif
% endfor
</div>
