@param java.util.List<heddle.benchmarks.jvm.model.Models.FragmentRow> items
<%--
  Idiomatic jte authoring — official doc citations:
  https://jte.gg/syntax/ (conditions: @if/@elseif/@else — jte's dispatch construct;
      template calls: @template.idiomatic.shared.tile etc.; loops: @for)
  https://jte.gg/pre-compiling/ (precompiled engine setup)

  Four dispatched fragment kinds selected by a
  boolean @if/@elseif chain per row; the card fragment nests badge + price (one level).
--%>
<div class="panel">
    @for(var item : items)
        @if(item.isTile())
            @template.idiomatic.shared.tile(item)
        @elseif(item.isCard())
            @template.idiomatic.shared.card(item)
        @elseif(item.isMedia())
            @template.idiomatic.shared.mediarow(item)
        @else
            @template.idiomatic.shared.stat(item)
        @endif
    @endfor
</div>
