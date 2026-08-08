@param heddle.benchmarks.jvm.model.Models.ComposedModel m
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/#content (content blocks: `bodySlot = @`...`` passes the page
      body as a gg.jte.Content parameter — the native jte layout mechanism)
  https://jte.gg/syntax/ (template calls: @template.idiomatic.layout)
  https://jte.gg/pre-compiling/ (precompiled engine setup)

  Native full-page layout composition (ledger E20, workloads.md workload 1): the page
  calls the layout template with the slider body as a live content slot. Both tracks
  carry the SAME slider body — the verifier's removed-segment calibration pin is the
  slider, so an empty body fails.
--%>
@template.idiomatic.layout(m, bodySlot = @`
    <div class="slider-wrapper theme-default">
        <div id="slider" class="nivoSlider">
        </div>
        <div class="home-content">
            <div>
                <a href="/products/gluten-free">
                    <img src="/files/homepage/homebtmbanners/gluten-hp.jpg" width="984" border="0" />
                </a>
            </div>
        </div>
    </div>
`)
