@param heddle.benchmarks.jvm.model.Models.NavSection section
<div class="nav-section">@if(section.isTitleLinked())<span class="nav-title"><a href="${section.getHref()}">${section.getTitle()}</a></span>@else<span class="nav-title">${section.getTitle()}</span>@endif<ul>@for(var link : section.getLinks())@template.controlled.navlink(link)@endfor</ul></div>
