@param heddle.benchmarks.jvm.model.Models.ComposedModel m
@param gg.jte.Content bodySlot
<!DOCTYPE html>
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
    <title>Title</title>
    <meta property="og:image" content="/files/catalog/img.jpg">
    <meta itemprop="image" content="/files/catalog/img.jpg"/>
    <link rel="image_src" href="/files/catalog/img.jpg"/>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="msvalidate.01" content="4A9D524947EE62F01A1A9607511248C4" />
    <link href="//fonts.googleapis.com/css?family=Francois+One" rel="stylesheet" type="text/css">
    <link rel="apple-touch-icon" href="/assets/miscellaneous/apple-touch-icon.png">
    <link rel="icon" type="img/ico" href="/assets/miscellaneous/favicon.ico">
    @template.controlled.assetsstyles()
    <style>
        @template.controlled.customstyles()
    </style>
    @template.controlled.headscripts()
</head>
<body>
    @template.controlled.bodyscripts()
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
    @template.controlled.alerttop()
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
                @template.controlled.secondarywholesalemenu()
                @template.controlled.secondaryretailmenu()
            </nav>
            @for(var menu : m.getNav().getMenus())@template.controlled.megamenu(menu)@endfor
        </header>
        <div class="content">
            <div class="alert-banner">
                @template.controlled.alertbelow()
            </div>
            <div class="content-dynamic">
                ${bodySlot}
            </div>
            <div class="content-bottom">
            </div>
        </div>
        <footer>
            <div class="footer-main">
                <nav class="footer-nav">
                    @for(var col : m.getNav().getFooterColumns())@template.controlled.navcolumn(col)@endfor
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
    @template.controlled.assetsscripts()
    <script src="/app/modules/profile/gccheckform.js"></script>
    
    
    @template.controlled.bodyendscripts()
</body>
</html>
