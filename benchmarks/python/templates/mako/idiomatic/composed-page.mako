## Idiomatic Mako composed-page: native template inheritance -- the
## page inherits the full-chrome layout with <%inherit>, and its body (the slider
## markup, the same in both tracks) splices at the layout's live ${self.body()} slot
##.
## Doc: https://docs.makotemplates.org/en/latest/inheritance.html
<%inherit file="shared/layout.mako"/>
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
