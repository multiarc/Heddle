@param heddle.benchmarks.jvm.model.Models.FragmentRow row
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/ (template calls: @template, params, output expressions)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
<section class="tile">
    <h3>${row.getName()}</h3>
    <p class="v">${row.getValue()}</p>
    <span class="badge">${row.getBadge()}</span>
</section>
