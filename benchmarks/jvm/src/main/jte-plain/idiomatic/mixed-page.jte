@param heddle.benchmarks.jvm.model.Models.MixedModel m
<%--
  Idiomatic jte authoring (Q1.7/D7) — official doc citations:
  https://jte.gg/syntax/ (conditions: @if/@endif, loops: @for, output expressions)
  https://jte.gg/pre-compiling/ (precompiled engine setup)
--%>
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>${m.getPageTitle()}</title>
<style>body{font:16px/1.5 system-ui;margin:0;color:#222}header{background:#1a2b3c;color:#fff;padding:12px 24px}nav a{color:#9cf;margin-right:12px;text-decoration:none}main{max-width:960px;margin:0 auto;padding:24px}.hero{background:#f4f6f8;padding:32px;border-radius:8px}.banner{background:#fff4d6;padding:8px 16px;border-radius:4px}.grid{display:flex;flex-wrap:wrap;gap:16px}.card{border:1px solid #ddd;border-radius:6px;padding:16px;width:280px}.card h3{margin:0 0 8px}.price{font-weight:700}.sale{color:#b00020;font-weight:700}footer{border-top:1px solid #ddd;margin-top:32px;padding:16px 24px;color:#666}</style>
</head>
<body>
<header>
    <h1>${m.getStoreName()}</h1>
    <nav><a href="/">Home</a><a href="/catalog">Catalog</a><a href="/deals">Deals</a><a href="/about">About</a><a href="/support">Support</a><a href="/account">Account</a></nav>
</header>
<main>
    @if(m.isShowBanner())
        <div class="banner">${m.getBannerText()}</div>
    @endif
    <section class="hero">
        <h2>${m.getHeroHeading()}</h2>
        <p>${m.getHeroTagline()}</p>
    </section>
    <section class="grid">
        @for(var p : m.getProducts())
            <article class="card">
                <h3>${p.getName()}</h3>
                <p class="sku">${p.getSku()}</p>
                <p class="price">${p.getPrice()}</p>
                @if(p.isOnSale())
                    <p class="sale">On sale</p>
                @endif
                <p class="blurb">${p.getBlurb()}</p>
            </article>
        @endfor
    </section>
    @if(m.isShowDebugPanel())
        <pre class="debug">debug</pre>
    @endif
</main>
<footer>
    <p>${m.getFooterNote()}</p>
    <p>${m.getStoreName()} ${m.getYear()} ${m.getSupportEmail()}</p>
</footer>
</body>
</html>
