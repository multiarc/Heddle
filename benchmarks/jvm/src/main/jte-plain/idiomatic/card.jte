@param heddle.benchmarks.jvm.model.Models.FragmentRow row
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/ (template calls: @template.idiomatic.badge / .price — the one
      nesting level, passing the row's promo down)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
<article class="card">
    <h3>${row.getName()}</h3>
    @template.idiomatic.badge(row.getPromo())
    @template.idiomatic.price(row.getPromo())
    <p class="v">${row.getValue()}</p>
</article>
