@param heddle.benchmarks.jvm.model.Models.SubstitutionModel m
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/ (params, output expressions, comments)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
<article>
    <h1>${m.getTitle()}</h1>
    <p class="sku">${m.getSku()}</p>
    <p class="price">${m.getPrice()}</p>
    <p class="brand">${m.getBrand()}</p>
    <p class="cat">${m.getCategory()}</p>
    <p class="avail">${m.getAvailability()}</p>
    <a class="link" href="${m.getUrl()}">
        <img src="${m.getImageUrl()}">
    </a>
    <p class="sum">${m.getSummary()}</p>
    <p class="rating">${m.getRating()}</p>
</article>
