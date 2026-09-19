@param heddle.benchmarks.jvm.model.Models.MegaMenu menu
<%--
  Idiomatic jte authoring — official doc citations:
  https://jte.gg/syntax/ (loops: @for, conditions: @if, template calls, output expressions)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
<div class="top-menu-wrapper">
    <ul class="top-menu">
        @for(var tab : menu.getTabs())
            <li class="${tab.getCss()}">
                <a href="${tab.getHref()}" class="drop">${tab.getLabel()}</a>
                @if(tab.isHasDropdown())
                    <div class="${tab.getDropdownCss()}">
                        @for(var col : tab.getColumns())
                            @template.idiomatic.shared.navcolumn(col)
                        @endfor
                    </div>
                @endif
            </li>
        @endfor
    </ul>
</div>
