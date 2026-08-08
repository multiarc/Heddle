@param heddle.benchmarks.jvm.model.Models.FragmentRow row
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/ (params, output expressions)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
<div class="stat">
    <span class="stat-name">${row.getName()}</span>
    <span class="stat-value">${row.getValue()}</span>
    <span class="stat-delta">${row.getDelta()}</span>
</div>
