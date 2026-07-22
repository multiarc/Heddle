@param java.util.List<heddle.benchmarks.jvm.model.Models.FragmentRow> items
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/ (template calls: @template.idiomatic.tile, loops: @for)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
<div class="panel">
    @for(var item : items)
        @template.idiomatic.tile(item)
    @endfor
</div>
