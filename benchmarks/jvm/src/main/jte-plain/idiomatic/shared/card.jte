@param heddle.benchmarks.jvm.model.Models.FragmentRow row
<%--
  Idiomatic jte authoring — official doc citations:
  https://jte.gg/syntax/ (template calls: @template.idiomatic.shared.badge / .price — the one
      nesting level, passing the row's promo down)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
<article class="card">
    <h3>${row.getName()}</h3>
    @template.idiomatic.shared.badge(row.getPromo())
    @template.idiomatic.shared.price(row.getPromo())
    <p class="v">${row.getValue()}</p>
</article>
