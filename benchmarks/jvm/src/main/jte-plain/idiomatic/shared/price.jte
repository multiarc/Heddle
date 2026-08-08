@param heddle.benchmarks.jvm.model.Models.FragmentPromo promo
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/ (params, output expressions)
  https://jte.gg/pre-compiling/ (precompiled engine setup)

  E21: the display price is composed by the template — the int price plus the literal
  `.99` — never pre-formatted model-side.
--%>
<p class="price">${promo.getPrice()}.99</p>
