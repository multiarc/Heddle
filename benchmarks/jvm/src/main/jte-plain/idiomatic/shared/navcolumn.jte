@param heddle.benchmarks.jvm.model.Models.NavColumn col
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/ (loops: @for, template calls: @template.idiomatic.shared.navsection)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
<div class="nav-column">
    @for(var section : col.getSections())
        @template.idiomatic.shared.navsection(section)
    @endfor
</div>
