@param heddle.benchmarks.jvm.model.Models.NavLink link
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/ (params, output expressions)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
<li class="nav-link"><a href="${link.getHref()}">${link.getLabel()}</a></li>
