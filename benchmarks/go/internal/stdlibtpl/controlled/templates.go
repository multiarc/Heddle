// Package controlled hosts the stdlib-engine controlled ports: text/template for the six
// raw workloads and html/template for the two encoded workloads (README D1 / Q6.1 — both
// surfaces of the one credibility-pick stdlib engine; benchmark ids stdlib-text /
// stdlib-html). Template sources are Go raw string literals transcribed from the pinned
// Heddle/twin shapes with {{…}} actions in place of @(…) substitutions; whitespace-only
// layout differences are erased by the contract's N3b comparison strip. Every template is
// parsed once in a package var block (the cached-template render path); renders execute
// into a reused pre-grown buffer (render.go).
//
// Normative texts: docs/spec/cross-stack-benchmarks/phase-6-go/port-mapping.md
// §Controlled track — stdlib surfaces (workload shapes from Phase 1 workloads.md).
package controlled

import (
	htmltemplate "html/template"
	texttemplate "text/template"
)

// ---- workload 1 — composed-page (text/template) ----------------------------------------------
//
// The E20/E22 full-page layout workload, composed with the stdlib's native mechanism
// (workloads.md §Native-layout mandate, Go stdlib row): the layout template holds the FULL
// literal chrome with a live {{block "body" .}} slot; the page's parse in the same
// associated set adds a non-empty {{define "body"}} that overrides the block's empty
// default. The inert chrome fragments are per-fragment {{define}}s mirroring
// chrome-fragments.heddle (E22 — all literal page text lives in the template tier; the
// model carries ONLY the structured nav). text/template = no escaping, preserving the raw
// discipline.

// composedChromeSrc is the definition-only chrome-fragment library (E22): the alert
// banner, the two secondary menus, the pinned-empty alert-below slot, and the six fixed
// asset/script snippets, transcribed from chrome-fragments.heddle. Parsing it renders
// nothing.
const composedChromeSrc = `{{define "alert_top"}}<div class="top-banner" style=""><a href="/content/shipping-information#Holidays"><img src="/files/images/sitewide-alerts/xmas-shipping-alert-1.jpg" alt="holiday shipping"/></a></div>{{end}}
{{define "secondary_wholesale_menu"}}				<ul class="hide">
                <li>
                <a href="#">New</a>
                </li>
                <li>
                <a href="#">Signature Products</a>
                </li>
                <li>
                <a href="/products/specials-and-top-sellers">Special Offers</a>
                </li>
                <li>
                <a href="/content/in-the-kitchen">Recipes</a>
                <div class="dropdown_1column">
                <div class="col_1">
                <ul>
                <li>
                <a href="/content/in-the-kitchen">All Recipes</a>
                </li>
                <li>
                <a href="/recipe/Vital-Choice-Seafood-Cooking-Videos">Recipe Videos</a>
                </li>
                <li>
                <a href="/content/Seafood-Storage-Cooking-Tips">Seafood Cooking Tips</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                <li>
                <a href="/content/About-Vital-Choice">Why Vital Choice?</a>
                <div class="dropdown_1column">
                <div class="col_1">
                <ul>
                <li>
                <a href="/content/About-Vital-Choice">About Us</a>
                </li>
                <li>
                <a href="/content/our-mission">Our Mission</a>
                </li>
                <li>
                <a href="/content/what-are-people-saying-about-vital-choice">Testimonials</a>
                </li>
                <li>
                <a href="/content/Giving-Back-to-the-Community">Giving Back</a>
                </li>
                <li>
                <a href="/content/News-Room">News Room</a>
                </li>
                <li>
                <a href="/content/Vital-Green-Environmental-Stewardship-Program">Vital Green™</a>
                </li>
                <li>
                <a href="/content/Sustainability">Sustainability</a>
                </li>
                <li>
                <a href="/content/Purity-Story">Product Purity</a>
                </li>
                <li>
                <a href="/content/The-Antidote-Podcast-Series">Randy's Podcasts</a>
                </li>
                <li>
                <a href="/content/HealthWise-Rewards-Program">Customer Rewards</a>
                </li>
                <li>
                <a href="/faqs">Frequent Questions (FAQs)</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                <li>
                <a href="#">Health & Nutrition</a>
                <div class="dropdown_1column">
                <div class="col_1">
                <ul>
                <li>
                <a href="/content/omega-3-facts-sources">Omega-3 Basics</a>
                </li>
                <li>
                <a href="/content/Health-Benefits-of-Fish">Seafood Benefits</a>
                </li>
                <li>
                <a href="/content/Omega-3-6-Balance-Scores-How-to-Use-Them">Omega-3/6 Balance</a>
                </li>
                <li>
                <a href="/content/Healthy-Mom-Baby">Healthy Mom & Baby</a>
                </li>
                <li>
                <a href="/content/Purity-Story">Seafood Purity & Safety</a>
                </li>
                <li>
                <a href="/content/recommended-reading">Recommended Reading</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                <li>
                <a href="/products/wholesale" class="highlighted-sec-menu">Wholesale</a>
                <div class="dropdown_1column">
                <div class="col_1">
                <ul>
                <li>
                <a href="/products/canned-wild-seafood">Canned Wild Seafood</a>
                </li>
                <li>
                <a href="/products/dietary-supplements">Dietary Supplements</a>
                </li>
                <li>
                <a href="/products/organic-foods">Organic Foods</a>
                </li>
                <div class="sub-sec-menu-separator"></div>
                <li>
                <a href="/content/wholesale-promotions">Wholesale Promotions</a>
                </li>
                <li>
                <a href="/content/wholesale-faq">FAQ</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                <li class="last-child">
                <a href="/content/newsletter-sign-up">Newsletter</a>
                <div class="dropdown_1column">
                <div class="col_1">
                <ul>
                <li>
                <a href="/content/newsletter-sign-up">Sign Up</a>
                </li>
                <li>
                <a href="/articles">Archives</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                </ul>{{end}}
{{define "secondary_retail_menu"}}				<ul class="hide">
                <li>
                <a href="#">New</a>
                </li>
                <li>
                <a href="#">Signature Products</a>
                </li>
                <li>
                <a href="/products/specials-and-top-sellers">Special Offers</a>
                </li>
                <li>
                <a href="/content/in-the-kitchen">Recipes</a>
                <div class="dropdown_1column">
                <div class="col_1">
                <ul>
                <li>
                <a href="/content/in-the-kitchen">All Recipes</a>
                </li>
                <li>
                <a href="/recipe/Vital-Choice-Seafood-Cooking-Videos">Recipe Videos</a>
                </li>
                <li>
                <a href="/content/Seafood-Storage-Cooking-Tips">Seafood Cooking Tips</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                <li>
                <a href="/content/About-Vital-Choice">Why Vital Choice?</a>
                <div class="dropdown_1column">
                <div class="col_1">
                <ul>
                <li>
                <a href="/content/About-Vital-Choice">About Us</a>
                </li>
                <li>
                <a href="/content/our-mission">Our Mission</a>
                </li>
                <li>
                <a href="/content/what-are-people-saying-about-vital-choice">Testimonials</a>
                </li>
                <li>
                <a href="/content/Giving-Back-to-the-Community">Giving Back</a>
                </li>
                <li>
                <a href="/content/News-Room">News Room</a>
                </li>
                <li>
                <a href="/content/Vital-Green-Environmental-Stewardship-Program">Vital Green™</a>
                </li>
                <li>
                <a href="/content/Sustainability">Sustainability</a>
                </li>
                <li>
                <a href="/content/Purity-Story">Product Purity</a>
                </li>
                <li>
                <a href="/content/The-Antidote-Podcast-Series">Randy's Podcasts</a>
                </li>
                <li>
                <a href="/content/HealthWise-Rewards-Program">Customer Rewards</a>
                </li>
                <li>
                <a href="/faqs">Frequent Questions (FAQs)</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                <li>
                <a href="#">Health & Nutrition</a>
                <div class="dropdown_1column">
                <div class="col_1">
                <ul>
                <li>
                <a href="/content/omega-3-facts-sources">Omega-3 Basics</a>
                </li>
                <li>
                <a href="/content/Health-Benefits-of-Fish">Seafood Benefits</a>
                </li>
                <li>
                <a href="/content/Omega-3-6-Balance-Scores-How-to-Use-Them">Omega-3/6 Balance</a>
                </li>
                <li>
                <a href="/content/Healthy-Mom-Baby">Healthy Mom & Baby</a>
                </li>
                <li>
                <a href="/content/Purity-Story">Seafood Purity & Safety</a>
                </li>
                <li>
                <a href="/content/recommended-reading">Recommended Reading</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                <li>
                <a href="/content/request-catalog">Catalog</a>
                </li>
                <li class="last-child">
                <a href="/content/newsletter-sign-up">Newsletter</a>
                <div class="dropdown_1column">
                <div class="col_1">
                <ul>
                <li>
                <a href="/content/newsletter-sign-up">Sign Up</a>
                </li>
                <li>
                <a href="/articles">Archives</a>
                </li>
                </ul>
                </div>
                </div>
                </li>
                </ul>{{end}}
{{define "alert_below"}}{{end}}
{{define "assets_styles"}}<link rel="stylesheet" href="/main.css" />{{end}}
{{define "assets_scripts"}}<script src="/main.js"></script>{{end}}
{{define "custom_styles"}}/* CSS Comment Test */{{end}}
{{define "head_scripts"}}<script src="/head.js"></script>{{end}}
{{define "body_scripts"}}<script src="/body.js"></script>{{end}}
{{define "body_end_scripts"}}<script src="/bodyend.js"></script>{{end}}`

// composedLayoutSrc is the definition-only layout parse: the four overridable section
// defaults, the four nav fragment definitions (nested per workloads.md — menu → column →
// section → link, dispatching on the precomputed .TitleLinked/.HasDropdown booleans), and
// the "layout" definition holding the full literal chrome with {{block "body" .}} at the
// body-slot position and {{range .Nav.Menus}}/{{range .Nav.FooterColumns}} at the nav
// call sites. Parsing it renders nothing.
const composedLayoutSrc = `{{define "meta"}}<title>Title</title>{{end}}
{{define "socialmeta"}}<meta property="og:image" content="/files/catalog/img.jpg">
        <meta itemprop="image" content="/files/catalog/img.jpg"/>
        <link rel="image_src" href="/files/catalog/img.jpg"/>{{end}}
{{define "page_scripts"}}{{end}}
{{define "endpage_scripts"}}{{end}}
{{define "nav_link"}}<li class="nav-link"><a href="{{.Href}}">{{.Label}}</a></li>{{end}}
{{define "nav_section"}}<div class="nav-section">{{if .TitleLinked}}<span class="nav-title"><a href="{{.Href}}">{{.Title}}</a></span>{{else}}<span class="nav-title">{{.Title}}</span>{{end}}<ul>{{range .Links}}{{template "nav_link" .}}{{end}}</ul></div>{{end}}
{{define "nav_column"}}<div class="nav-column">{{range .Sections}}{{template "nav_section" .}}{{end}}</div>{{end}}
{{define "mega_menu"}}<div class="top-menu-wrapper"><ul class="top-menu">{{range .Tabs}}<li class="{{.Css}}"><a href="{{.Href}}" class="drop">{{.Label}}</a>{{if .HasDropdown}}<div class="{{.DropdownCss}}">{{range .Columns}}{{template "nav_column" .}}{{end}}</div>{{end}}</li>{{end}}</ul></div>{{end}}
{{define "layout"}}<!DOCTYPE html>
<!--[if lt IE 9]>
    <html class="no-js lt-ie9" lang="en">
<![endif]-->
<!--[if gte IE 9]>
    <html class="no-js" lang="en">
<![endif]-->
<!--[if !IE]><!-->
<html class="no-js" lang="en">
<!--<![endif]-->
<head>
    <meta charset="utf-8">
    <meta http-equiv="x-ua-compatible" content="ie=edge">
    {{template "meta"}}
    {{template "socialmeta"}}
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="msvalidate.01" content="4A9D524947EE62F01A1A9607511248C4" />
    <link href="//fonts.googleapis.com/css?family=Francois+One" rel="stylesheet" type="text/css">
    <link rel="apple-touch-icon" href="/assets/miscellaneous/apple-touch-icon.png">
    <link rel="icon" type="img/ico" href="/assets/miscellaneous/favicon.ico">
    {{template "assets_styles"}}
    <style>
        {{template "custom_styles"}}
    </style>
    {{template "head_scripts"}}
</head>
<body>
    {{template "body_scripts"}}
    <div>
        <div class="header-top main">
            <div class="header-top-right">
                <div class="actions-top-nav">
                    <a href="/index/profile">Your Account</a>
                    <a href="/Account/Register">Register</a>
                    <a class="last-child" href="/content/contact-customer-service">Contact Us</a>
                </div>
            </div>
        </div>
    </div>
    <div class="separator-top"></div>
<div class="alert-banner">
    {{template "alert_top"}}
</div>
    <div class="main">
        <header>
            <div class="header-main">
                <div class="logo-holder">
                    <a href="/">
                    </a>
                </div>
                <div class="header-right-sidebar">
                    <div class="search-area">
                        <form id="search-area-form" action="/help/search" method="get">
                            <input type="text" name="q" value="What can we help you find?" onclick="this.value = ( this.value == this.defaultValue ) ? '' : this.value;return true;" dir="ltr" autocomplete="off" spellcheck="false" style="outline: none;">
                            <input type="image" class="active" style="display: none;" src="/Assets/images/mag.jpg" />
                            <input type="image" class="not-active" src="/Assets/images/mag-off.png" />
                        </form>
                    </div>
                    <div class="header-actions-holder">
                        <div class="cart-work-area">
                            <a class="view-cart" href="#">
                                <div class="cart-icon"></div>
                                <div class="item-total">
                                    <span id="cart-lite-component"></span>
                                    <span>Item(s)</span>
                                </div>
                            </a>
                            <a class="checkout-button" href="#">
                                <button>CHECKOUT</button>
                            </a>
                        </div>
                    </div>
                    <div class="clear"></div>
                    <div class="header-top-right">
                        <div class="actions-top-nav">
                            <a class="wholesale-home" href="/products/wholesale">Wholesale Home</a>
                        </div>
                    </div>
                </div>
            </div>
            <nav class="hide top-nav secondary-nav">
                {{template "secondary_wholesale_menu"}}
                {{template "secondary_retail_menu"}}
            </nav>
            {{range .Nav.Menus}}{{template "mega_menu" .}}{{end}}
        </header>
        <div class="content">
            <div class="alert-banner">
                {{template "alert_below"}}
            </div>
            <div class="content-dynamic">
                {{block "body" .}}{{end}}
            </div>
            <div class="content-bottom">
            </div>
        </div>
        <footer>
            <div class="footer-main">
                <nav class="footer-nav">
                    {{range .Nav.FooterColumns}}{{template "nav_column" .}}{{end}}
                </nav>
                <div class="footer-market">
                    <div class="market-left">
                        <a href="/content/request-catalog">
                            <img alt="See Our Catalog" src="/Assets/images/seeourcatalog.jpg" />
                        </a>
                    </div>
                    <div class="market-right">
                        <div class="ssl-area">
                        </div>
                        <div id="socialiconshome-102413">
                            <img src="/Assets/images/socialicons5-home.jpg" usemap="#Social">
                        </div>
                        <div class="clear"></div>
                    </div>
                </div>
            </div>
        </footer>
    </div>
    {{template "assets_scripts"}}
    <script src="/app/modules/profile/gccheckform.js"></script>
    {{template "page_scripts"}}
    {{template "endpage_scripts"}}
    {{template "body_end_scripts"}}
</body>
</html>{{end}}`

// composedHomeSrc is the page parse: {{define "body"}} carries the slider markup (the
// verifier's removed-segment calibration pin — an empty body fails) and, being a later
// non-empty definition in the associated set, overrides the layout's empty block default;
// the root action then renders the layout.
const composedHomeSrc = `{{define "body"}}<div class="slider-wrapper theme-default">
            <div id="slider" class="nivoSlider">
            </div>
            <div class="home-content">
                <div>
                    <a href="/products/gluten-free">
                        <img src="/files/homepage/homebtmbanners/gluten-hp.jpg" width="984" border="0" />
                    </a>
                </div>
            </div>
        </div>{{end}}{{template "layout" .}}`

// ---- workload 2 — trivial-substitution (text/template) ---------------------------------------

// Dense one-line <article> card, ten {{.Member}} substitutions in pinned order, including
// the two attribute positions.
const trivialSubstitutionSrc = `<article><h1>{{.Title}}</h1><p class="sku">{{.Sku}}</p><p class="price">{{.Price}}</p><p class="brand">{{.Brand}}</p><p class="cat">{{.Category}}</p><p class="avail">{{.Availability}}</p><a class="link" href="{{.Url}}"><img src="{{.ImageUrl}}"></a><p class="sum">{{.Summary}}</p><p class="rating">{{.Rating}}</p></article>`

// ---- workload 3 — large-loop (text/template) -------------------------------------------------

// The display name row-{i} is composed by the template as the literal `row-` + the value
// substitution (E21 — the model carries only the int). text/template renders ints via fmt
// (%v), identical bytes to strconv.Itoa.
const largeLoopSrc = `{{range .Items}}<tr><td>row-{{.Value}}</td><td>{{.Value}}</td></tr>{{end}}`

// ---- workload 4 — mixed-page (text/template) -------------------------------------------------

// The pinned skeleton transcribed line-for-line (line breaks between sibling elements are
// N2/N3-erased; the <style> line and all text-bearing elements stay dense); the footer
// keeps its single literal spaces. The display SKU is composed as MX-{{.SkuNumber}} and
// the blurb sentence lives in the template around {{.Batch}} (E21).
const mixedPageSrc = `<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>{{.PageTitle}}</title>
<style>body{font:16px/1.5 system-ui;margin:0;color:#222}header{background:#1a2b3c;color:#fff;padding:12px 24px}nav a{color:#9cf;margin-right:12px;text-decoration:none}main{max-width:960px;margin:0 auto;padding:24px}.hero{background:#f4f6f8;padding:32px;border-radius:8px}.banner{background:#fff4d6;padding:8px 16px;border-radius:4px}.grid{display:flex;flex-wrap:wrap;gap:16px}.card{border:1px solid #ddd;border-radius:6px;padding:16px;width:280px}.card h3{margin:0 0 8px}.price{font-weight:700}.sale{color:#b00020;font-weight:700}footer{border-top:1px solid #ddd;margin-top:32px;padding:16px 24px;color:#666}</style>
</head>
<body>
<header>
<h1>{{.StoreName}}</h1>
<nav><a href="/">Home</a><a href="/catalog">Catalog</a><a href="/deals">Deals</a><a href="/about">About</a><a href="/support">Support</a><a href="/account">Account</a></nav>
</header>
<main>
{{if .ShowBanner}}<div class="banner">{{.BannerText}}</div>{{end}}
<section class="hero">
<h2>{{.HeroHeading}}</h2>
<p>{{.HeroTagline}}</p>
</section>
<section class="grid">
{{range .Products}}<article class="card"><h3>{{.Name}}</h3><p class="sku">MX-{{.SkuNumber}}</p><p class="price">{{.Price}}</p>{{if .OnSale}}<p class="sale">On sale</p>{{end}}<p class="blurb">A dependable workshop staple from batch {{.Batch}}, checked for daily use and backed by our lifetime guarantee.</p></article>{{end}}
</section>
{{if .ShowDebugPanel}}<pre class="debug">debug</pre>{{end}}
</main>
<footer>
<p>{{.FooterNote}}</p>
<p>{{.StoreName}} {{.Year}} {{.SupportEmail}}</p>
</footer>
</body>
</html>`

// ---- workload 5 — conditional-heavy (text/template) ------------------------------------------

// The pinned single-line <ul class="matrix"> body: four-way chain per row plus the two
// toggles. The note text is composed as the literal `note ` + {{.Seq}} (E21).
const conditionalHeavySrc = `<ul class="matrix">{{range .Rows}}<li>{{if .IsBronze}}<span class="t0">bronze</span>{{else if .IsSilver}}<span class="t1">silver</span>{{else if .IsGold}}<span class="t2">gold</span>{{else}}<span class="t3">platinum</span>{{end}}<em>{{.Name}}</em>{{if .HasNote}}<small>note {{.Seq}}</small>{{end}}{{if .IsActive}}<b>active</b>{{end}}</li>{{end}}</ul>`

// ---- workload 6 — fragment-heavy (text/template) ---------------------------------------------

// The E20 redesign: six {{define}}s (tile, badge, price, card — which nests badge + price
// against the row's .Promo, the one nesting level — media_row, stat), dispatched per row by
// the boolean {{if .IsTile}}/{{else if}} chain (workloads.md §Dispatch per family, Go
// stdlib row). Derived display text is composed by the template as
// literal-plus-substitution (E21): /img/{{.Name}}.jpg, Caption for {{.Name}},
// {{.Promo.Price}} rendered through the price partial as {{.Price}}.99.
const fragmentHeavySrc = `{{define "tile"}}<section class="tile"><h3>{{.Name}}</h3><p class="v">{{.Value}}</p><span class="badge">{{.Badge}}</span></section>{{end}}{{define "badge"}}<span class="promo-badge">{{.Label}}</span>{{end}}{{define "price"}}<p class="price">{{.Price}}.99</p>{{end}}{{define "card"}}<article class="card"><h3>{{.Name}}</h3>{{template "badge" .Promo}}{{template "price" .Promo}}<p class="v">{{.Value}}</p></article>{{end}}{{define "media_row"}}<div class="media-row"><img src="/img/{{.Name}}.jpg" alt="{{.Name}}" /><div class="media-body"><h4>{{.Name}}</h4><p>Caption for {{.Name}}</p></div></div>{{end}}{{define "stat"}}<div class="stat"><span class="stat-name">{{.Name}}</span><span class="stat-value">{{.Value}}</span><span class="stat-delta">{{.Delta}}</span></div>{{end}}<div class="panel">{{range .Items}}{{if .IsTile}}{{template "tile" .}}{{else if .IsCard}}{{template "card" .}}{{else if .IsMedia}}{{template "media_row" .}}{{else}}{{template "stat" .}}{{end}}{{end}}</div>`

// ---- workload 7 — fortunes-encoded (html/template) -------------------------------------------

// The pinned one-line skeleton; html/template's contextual escaper fires on the
// text-context substitutions ({{.Message}}), with spellings reconciled by N5 (D4).
const fortunesEncodedSrc = `<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr>{{range .Rows}}<tr><td>{{.Id}}</td><td>{{.Message}}</td></tr>{{end}}</table></body></html>`

// ---- workload 8 — encoded-loop (html/template) -----------------------------------------------

// {{.Tag}} sits in quoted-attribute context; the other two substitutions in text context.
const encodedLoopSrc = `<table>{{range .Items}}<tr><td data-tag="{{.Tag}}">{{.Name}}</td><td>{{.Comment}}</td></tr>{{end}}</table>`

// ---- parsed templates (once, at package init — the cached-template render path) --------------

var (
	composedTpl = func() *texttemplate.Template {
		t := texttemplate.New("composed-page")
		texttemplate.Must(t.Parse(composedChromeSrc))
		texttemplate.Must(t.Parse(composedLayoutSrc))
		return texttemplate.Must(t.Parse(composedHomeSrc))
	}()
	trivialSubstitutionTpl = texttemplate.Must(texttemplate.New("trivial-substitution").Parse(trivialSubstitutionSrc))
	largeLoopTpl           = texttemplate.Must(texttemplate.New("large-loop").Parse(largeLoopSrc))
	mixedPageTpl           = texttemplate.Must(texttemplate.New("mixed-page").Parse(mixedPageSrc))
	conditionalHeavyTpl    = texttemplate.Must(texttemplate.New("conditional-heavy").Parse(conditionalHeavySrc))
	fragmentHeavyTpl       = texttemplate.Must(texttemplate.New("fragment-heavy").Parse(fragmentHeavySrc))
	fortunesEncodedTpl     = htmltemplate.Must(htmltemplate.New("fortunes-encoded").Parse(fortunesEncodedSrc))
	encodedLoopTpl         = htmltemplate.Must(htmltemplate.New("encoded-loop").Parse(encodedLoopSrc))
)
