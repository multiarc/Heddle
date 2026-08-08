@param java.util.List<heddle.benchmarks.jvm.model.Models.FragmentRow> items
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/ (conditions: @if/@elseif/@else — jte's dispatch construct;
      template calls: @template.idiomatic.tile etc.; loops: @for)
  https://jte.gg/pre-compiling/ (precompiled engine setup)

  E20 redesign (workloads.md workload 6): four dispatched fragment kinds selected by a
  boolean @if/@elseif chain per row; the card fragment nests badge + price (one level).
--%>
<div class="panel">
    @for(var item : items)
        @if(item.isTile())
            @template.idiomatic.tile(item)
        @elseif(item.isCard())
            @template.idiomatic.card(item)
        @elseif(item.isMedia())
            @template.idiomatic.mediarow(item)
        @else
            @template.idiomatic.stat(item)
        @endif
    @endfor
</div>
