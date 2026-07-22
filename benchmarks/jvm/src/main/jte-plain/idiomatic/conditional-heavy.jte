@param java.util.List<heddle.benchmarks.jvm.model.Models.ConditionalRow> rows
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/ (conditions: @if/@elseif/@else/@endif, loops: @for)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
<ul class="matrix">
    @for(var r : rows)
        <li>
            @if(r.isBronze())
                <span class="t0">bronze</span>
            @elseif(r.isSilver())
                <span class="t1">silver</span>
            @elseif(r.isGold())
                <span class="t2">gold</span>
            @else
                <span class="t3">platinum</span>
            @endif
            <em>${r.getName()}</em>
            @if(r.isHasNote())
                <small>${r.getNote()}</small>
            @endif
            @if(r.isActive())
                <b>active</b>
            @endif
        </li>
    @endfor
</ul>
