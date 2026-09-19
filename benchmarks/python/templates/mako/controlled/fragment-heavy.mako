<div class="panel">\
% for item in items:
% if item["is_tile"]:
<%include file="shared/tile.mako" args="item=item"/>\
% elif item["is_card"]:
<%include file="shared/card.mako" args="item=item"/>\
% elif item["is_media"]:
<%include file="shared/media-row.mako" args="item=item"/>\
% else:
<%include file="shared/stat.mako" args="item=item"/>\
% endif
% endfor
</div>
