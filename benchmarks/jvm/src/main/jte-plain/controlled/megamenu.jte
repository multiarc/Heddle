@param heddle.benchmarks.jvm.model.Models.MegaMenu menu
<div class="top-menu-wrapper"><ul class="top-menu">@for(var tab : menu.getTabs())<li class="${tab.getCss()}"><a href="${tab.getHref()}" class="drop">${tab.getLabel()}</a>@if(tab.isHasDropdown())<div class="${tab.getDropdownCss()}">@for(var col : tab.getColumns())@template.controlled.navcolumn(col)@endfor</div>@endif</li>@endfor</ul></div>
