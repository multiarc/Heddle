@param heddle.benchmarks.jvm.model.Models.FragmentRow row
<%--
  Idiomatic jte authoring — official doc citations:
  https://jte.gg/syntax/ (params, output expressions)
  https://jte.gg/pre-compiling/ (precompiled engine setup)

  Image src (`/img/` + name + `.jpg`) and caption (`Caption for ` + name) are
  composed by the template as literal-plus-substitution.
--%>
<div class="media-row">
    <img src="/img/${row.getName()}.jpg" alt="${row.getName()}" />
    <div class="media-body">
        <h4>${row.getName()}</h4>
        <p>Caption for ${row.getName()}</p>
    </div>
</div>
