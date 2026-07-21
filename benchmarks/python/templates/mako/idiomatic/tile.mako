<%doc>
Idiomatic Mako tile: the repeated unit as a <%def>, imported by
fragment-heavy.mako via <%namespace> (Phase 5 D5).
Doc: https://docs.makotemplates.org/en/latest/defs.html
</%doc>
<%def name="tile(item)">
<section class="tile">
  <h3>${item["name"]}</h3>
  <p class="v">${item["value"]}</p>
  <span class="badge">${item["badge"]}</span>
</section>
</%def>
