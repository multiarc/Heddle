@param heddle.benchmarks.jvm.model.Models.NavSection section
<%--
  Idiomatic jte authoring — official doc citations:
  https://jte.gg/syntax/ (conditions: @if/@else/@endif, loops: @for, template calls)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
<div class="nav-section">
    @if(section.isTitleLinked())
        <span class="nav-title"><a href="${section.getHref()}">${section.getTitle()}</a></span>
    @else
        <span class="nav-title">${section.getTitle()}</span>
    @endif
    <ul>
        @for(var link : section.getLinks())
            @template.idiomatic.shared.navlink(link)
        @endfor
    </ul>
</div>
