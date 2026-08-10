package heddle.benchmarks.jvm.model;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import heddle.benchmarks.jvm.gate.Corpus;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Locale;

/**
 * All pinned workload models.
 * Static final instances materialized once (the {@code LoopContent.Shared} discipline);
 * JavaBean-style POJOs because Thymeleaf's OGNL resolves {@code ${p.name}} through getters
 * and JTE templates call the same getters explicitly. All string assembly is
 * {@code String.format(Locale.ROOT, ...)} or plain ASCII/int concatenation - no locale,
 * time, or randomness anywhere. Pinned data must match the golden corpus byte-for-byte.
 *
 * One rule governs every shape here: the model tier carries DATA only - derived
 * display strings ({@code row-<i>}, {@code MX-<sku>}, {@code note <i>}, blurb sentences,
 * media captions, display prices) are composed by the TEMPLATES as
 * literal-plus-substitution, never pre-formatted model-side. Zero-padded identity names
 * ({@code unit-%03d}, {@code item-%02d}, {@code Product %02d}) and the encoded-suite
 * payloads stay model-side by design (row identity / untrusted input).
 *
 * This source file is saved UTF-8 without BOM (rows 4/8 carry em dash U+2014; row 12 and
 * the encoded-loop comments carry Japanese text).
 */
public final class Models {

    private Models() {
    }

    // ---- workload 2: trivial-substitution ----------------------------------------------

    public static final class SubstitutionModel {
        private final String title;
        private final String sku;
        private final int price;
        private final String brand;
        private final String category;
        private final String availability;
        private final String url;
        private final String imageUrl;
        private final String summary;
        private final String rating;

        public SubstitutionModel(String title, String sku, int price, String brand,
                                 String category, String availability, String url,
                                 String imageUrl, String summary, String rating) {
            this.title = title;
            this.sku = sku;
            this.price = price;
            this.brand = brand;
            this.category = category;
            this.availability = availability;
            this.url = url;
            this.imageUrl = imageUrl;
            this.summary = summary;
            this.rating = rating;
        }

        public String getTitle() { return title; }
        public String getSku() { return sku; }
        public int getPrice() { return price; }
        public String getBrand() { return brand; }
        public String getCategory() { return category; }
        public String getAvailability() { return availability; }
        public String getUrl() { return url; }
        public String getImageUrl() { return imageUrl; }
        public String getSummary() { return summary; }
        public String getRating() { return rating; }
    }

    public static final SubstitutionModel SUBSTITUTION = new SubstitutionModel(
            "Heddle Handbook", "HB-2001", 4200, "Heddle Press", "Reference", "In stock",
            "/catalog/handbook", "/img/handbook.png",
            "A concise field guide to the engine.", "4.8");

    // ---- workload 3: large-loop --------------------------------------------------------

    /**
     * The row carries ONLY the ordinal - the display name {@code row-<i>} is composed
     * by the templates as {@code row-} + the value substitution.
     */
    public static final class LoopRow {
        private final int value;

        public LoopRow(int value) {
            this.value = value;
        }

        public int getValue() { return value; }
    }

    public static final List<LoopRow> LOOP_ROWS = buildLoopRows();

    private static List<LoopRow> buildLoopRows() {
        List<LoopRow> rows = new ArrayList<>(5000);
        for (int i = 0; i <= 4999; i++) {
            rows.add(new LoopRow(i));
        }
        return Collections.unmodifiableList(rows);
    }

    // ---- workload 4: mixed-page --------------------------------------------------------

    public static final class MixedProduct {
        private final String name;
        private final int skuNumber;
        private final int price;
        private final boolean onSale;
        private final int batch;

        public MixedProduct(String name, int skuNumber, int price, boolean onSale, int batch) {
            this.name = name;
            this.skuNumber = skuNumber;
            this.price = price;
            this.onSale = onSale;
            this.batch = batch;
        }

        public String getName() { return name; }
        /** Numeric SKU - the templates compose the display SKU {@code MX-<skuNumber>}. */
        public int getSkuNumber() { return skuNumber; }
        public int getPrice() { return price; }
        public boolean isOnSale() { return onSale; }
        /** Batch ordinal - the templates compose the blurb sentence around it. */
        public int getBatch() { return batch; }
    }

    public static final class MixedModel {
        private final String pageTitle;
        private final String storeName;
        private final String heroHeading;
        private final String heroTagline;
        private final boolean showBanner;
        private final String bannerText;
        private final boolean showDebugPanel;
        private final String footerNote;
        private final int year;
        private final String supportEmail;
        private final List<MixedProduct> products;

        public MixedModel(String pageTitle, String storeName, String heroHeading,
                          String heroTagline, boolean showBanner, String bannerText,
                          boolean showDebugPanel, String footerNote, int year,
                          String supportEmail, List<MixedProduct> products) {
            this.pageTitle = pageTitle;
            this.storeName = storeName;
            this.heroHeading = heroHeading;
            this.heroTagline = heroTagline;
            this.showBanner = showBanner;
            this.bannerText = bannerText;
            this.showDebugPanel = showDebugPanel;
            this.footerNote = footerNote;
            this.year = year;
            this.supportEmail = supportEmail;
            this.products = products;
        }

        public String getPageTitle() { return pageTitle; }
        public String getStoreName() { return storeName; }
        public String getHeroHeading() { return heroHeading; }
        public String getHeroTagline() { return heroTagline; }
        public boolean isShowBanner() { return showBanner; }
        public String getBannerText() { return bannerText; }
        public boolean isShowDebugPanel() { return showDebugPanel; }
        public String getFooterNote() { return footerNote; }
        public int getYear() { return year; }
        public String getSupportEmail() { return supportEmail; }
        public List<MixedProduct> getProducts() { return products; }
    }

    public static final MixedModel MIXED = buildMixed();

    private static MixedModel buildMixed() {
        List<MixedProduct> products = new ArrayList<>(36);
        for (int i = 1; i <= 36; i++) {
            products.add(new MixedProduct(
                    String.format(Locale.ROOT, "Product %02d", i),
                    1000 + i,
                    950 + i * 7,
                    i % 3 == 0,
                    i));
        }
        return new MixedModel(
                "Mercantile - Catalog", "Mercantile", "Autumn hardware sale",
                "Hand-picked tools, fair prices, shipped tomorrow.", true,
                "Free shipping on orders over 60.", false,
                "Prices include VAT where applicable.", 2026,
                "support at mercantile.example",
                Collections.unmodifiableList(products));
    }

    // ---- workload 5: conditional-heavy -------------------------------------------------

    public static final class ConditionalRow {
        private final String name;
        private final int seq;
        private final boolean bronze;
        private final boolean silver;
        private final boolean gold;
        private final boolean hasNote;
        private final boolean active;

        public ConditionalRow(String name, int seq, boolean bronze, boolean silver,
                              boolean gold, boolean hasNote, boolean active) {
            this.name = name;
            this.seq = seq;
            this.bronze = bronze;
            this.silver = silver;
            this.gold = gold;
            this.hasNote = hasNote;
            this.active = active;
        }

        public String getName() { return name; }
        /** Row ordinal - the templates compose the note text {@code note <seq>}. */
        public int getSeq() { return seq; }
        public boolean isBronze() { return bronze; }
        public boolean isSilver() { return silver; }
        public boolean isGold() { return gold; }
        public boolean isHasNote() { return hasNote; }
        public boolean isActive() { return active; }
    }

    public static final List<ConditionalRow> CONDITIONAL_ROWS = buildConditionalRows();

    private static List<ConditionalRow> buildConditionalRows() {
        List<ConditionalRow> rows = new ArrayList<>(200);
        for (int i = 0; i <= 199; i++) {
            rows.add(new ConditionalRow(
                    String.format(Locale.ROOT, "unit-%03d", i),
                    i,
                    i % 4 == 0, i % 4 == 1, i % 4 == 2,
                    i % 2 == 0, i % 5 != 0));
        }
        return Collections.unmodifiableList(rows);
    }

    // ---- workload 6: fragment-heavy ----------------------------------------------------

    /**
     * 48 rows of four dispatched fragment kinds (12 each) with one level of
     * nesting (the card fragment renders badge + price from {@link FragmentPromo}). The
     * model carries DATA only - the media caption ({@code Caption for } + name), image src
     * ({@code /img/} + name + {@code .jpg}) and display price (price + {@code .99}) are
     * composed by the templates.
     */
    public static final class FragmentRow {
        private final String kind;
        private final boolean tile;
        private final boolean card;
        private final boolean media;
        private final boolean stat;
        private final String name;
        private final int value;
        private final String badge;
        private final int delta;
        private final FragmentPromo promo;

        public FragmentRow(String kind, boolean tile, boolean card, boolean media,
                           boolean stat, String name, int value, String badge, int delta,
                           FragmentPromo promo) {
            this.kind = kind;
            this.tile = tile;
            this.card = card;
            this.media = media;
            this.stat = stat;
            this.name = name;
            this.value = value;
            this.badge = badge;
            this.delta = delta;
            this.promo = promo;
        }

        /** Informational; engines dispatch on the booleans below, never on this string. */
        public String getKind() { return kind; }
        public boolean isTile() { return tile; }
        public boolean isCard() { return card; }
        public boolean isMedia() { return media; }
        public boolean isStat() { return stat; }
        public String getName() { return name; }
        public int getValue() { return value; }
        public String getBadge() { return badge; }
        /** Stat rows render it. */
        public int getDelta() { return delta; }
        /** The nesting level: the card fragment renders badge + price from it. Present on
         * every row so no engine needs a null guard. */
        public FragmentPromo getPromo() { return promo; }
    }

    public static final class FragmentPromo {
        private final String label;
        private final int price;

        public FragmentPromo(String label, int price) {
            this.label = label;
            this.price = price;
        }

        public String getLabel() { return label; }
        /** Whole-currency units only; the templates compose the display price
         * ({@code <price>.99}) - formatting is rendering work, not model work. */
        public int getPrice() { return price; }
    }

    public static final List<FragmentRow> FRAGMENT_ROWS = buildFragmentRows();

    private static List<FragmentRow> buildFragmentRows() {
        String[] kinds = {"tile", "card", "media", "stat"};
        String[] badges = {"new", "hot", "sale", "std"};
        List<FragmentRow> rows = new ArrayList<>(48);
        for (int i = 0; i <= 47; i++) {
            String kind = kinds[i % 4];
            String badge = badges[i % 4];
            rows.add(new FragmentRow(
                    kind,
                    "tile".equals(kind), "card".equals(kind),
                    "media".equals(kind), "stat".equals(kind),
                    String.format(Locale.ROOT, "item-%02d", i),
                    i * 11,
                    badge,
                    i % 7 - 3,
                    new FragmentPromo(badge, 9 + i)));
        }
        return Collections.unmodifiableList(rows);
    }

    // ---- workload 7: fortunes-encoded --------------------------------------------------

    public static final class FortuneRow {
        private final int id;
        private final String message;

        public FortuneRow(int id, String message) {
            this.id = id;
            this.message = message;
        }

        public int getId() { return id; }
        public String getMessage() { return message; }
    }

    /**
     * The 12 pinned fortune rows, byte-for-byte across every ecosystem: row 1 writes
     * {@code 4.33e67} (no {@code +}); rows 4/8 carry em dash U+2014; row 11 is the exact
     * TechEmpower XSS payload; row 12 the Japanese string.
     */
    public static final List<FortuneRow> FORTUNE_ROWS = List.of(
            new FortuneRow(1, "A bad random number generator: 1, 1, 1, 1, 1, 4.33e67, 1, 1, 1"),
            new FortuneRow(2, "A computer program does what you tell it to do, not what you want it to do."),
            new FortuneRow(3, "A computer scientist is someone who fixes things that aren't broken."),
            new FortuneRow(4, "A list is only as strong as its weakest link. — Donald Knuth"),
            new FortuneRow(5, "After enough decimal places, nobody gives a damn."),
            new FortuneRow(6, "Any program that runs right is obsolete."),
            new FortuneRow(7, "Computers make very fast, very accurate mistakes."),
            new FortuneRow(8, "Emacs is a nice operating system, but I prefer UNIX. — Tom Christiansen"),
            new FortuneRow(9, "Feature: A bug with seniority."),
            new FortuneRow(10, "fortune: No such file or directory"),
            new FortuneRow(11, "<script>alert(\"This should not be displayed in a browser alert box.\");</script>"),
            new FortuneRow(12, "フレームワークのベンチマーク"));

    // ---- workload 8: encoded-loop ------------------------------------------------------

    public static final class EncodedLoopRow {
        private final String tag;
        private final String name;
        private final String comment;

        public EncodedLoopRow(String tag, String name, String comment) {
            this.tag = tag;
            this.name = name;
            this.comment = comment;
        }

        public String getTag() { return tag; }
        public String getName() { return name; }
        public String getComment() { return comment; }
    }

    public static final List<EncodedLoopRow> ENCODED_ITEMS = buildEncodedItems();

    private static List<EncodedLoopRow> buildEncodedItems() {
        List<EncodedLoopRow> rows = new ArrayList<>(5000);
        for (int i = 0; i <= 4999; i++) {
            rows.add(new EncodedLoopRow(
                    "tag-" + i + "&'" + (i % 7) + "'",
                    "item <" + i + "> & \"co\"",
                    "'q' & <angle> \"d\" こんにちは " + i));
        }
        return Collections.unmodifiableList(rows);
    }

    // ---- workload 1: composed-page -----------------------------------------------------

    /**
     * Composed-page model: pure structured navigation and NOTHING else - every fragment
     * of literal page text lives in the templates. {@code ComposedModel} stays exactly
     * {@code { nav }}, mirroring the model shape every other ecosystem pins.
     */
    public static final class ComposedModel {
        private final NavModel nav;

        public ComposedModel(NavModel nav) {
            this.nav = nav;
        }

        public NavModel getNav() { return nav; }
    }

    /** Two mega menus (wholesale, retail) and four footer columns. */
    public static final class NavModel {
        private final List<MegaMenu> menus;
        private final List<NavColumn> footerColumns;

        public NavModel(List<MegaMenu> menus, List<NavColumn> footerColumns) {
            this.menus = menus;
            this.footerColumns = footerColumns;
        }

        public List<MegaMenu> getMenus() { return menus; }
        public List<NavColumn> getFooterColumns() { return footerColumns; }
    }

    public static final class MegaMenu {
        private final List<MenuTab> tabs;

        public MegaMenu(List<MenuTab> tabs) {
            this.tabs = tabs;
        }

        public List<MenuTab> getTabs() { return tabs; }
    }

    public static final class MenuTab {
        private final String label;
        private final String href;
        private final String css;
        private final boolean hasDropdown;
        private final String dropdownCss;
        private final List<NavColumn> columns;

        public MenuTab(String label, String href, String css, boolean hasDropdown,
                       String dropdownCss, List<NavColumn> columns) {
            this.label = label;
            this.href = href;
            this.css = css;
            this.hasDropdown = hasDropdown;
            this.dropdownCss = dropdownCss;
            this.columns = columns;
        }

        public String getLabel() { return label; }
        public String getHref() { return href; }
        public String getCss() { return css; }
        /** Precomputed boolean - no engine evaluates a collection test. */
        public boolean isHasDropdown() { return hasDropdown; }
        public String getDropdownCss() { return dropdownCss; }
        public List<NavColumn> getColumns() { return columns; }
    }

    public static final class NavColumn {
        private final List<NavSection> sections;

        public NavColumn(List<NavSection> sections) {
            this.sections = sections;
        }

        public List<NavSection> getSections() { return sections; }
    }

    public static final class NavSection {
        private final String title;
        private final String href;
        private final boolean titleLinked;
        private final List<NavLink> links;

        public NavSection(String title, String href, boolean titleLinked, List<NavLink> links) {
            this.title = title;
            this.href = href;
            this.titleLinked = titleLinked;
            this.links = links;
        }

        public String getTitle() { return title; }
        public String getHref() { return href; }
        /** Precomputed: the title renders as a link exactly when the fixture linked it. */
        public boolean isTitleLinked() { return titleLinked; }
        public List<NavLink> getLinks() { return links; }
    }

    public static final class NavLink {
        private final String label;
        private final String href;

        public NavLink(String label, String href) {
            this.label = label;
            this.href = href;
        }

        public String getLabel() { return label; }
        public String getHref() { return href; }
    }

    /**
     * Loaded once at class initialization (lazy holder = static init on first touch) from
     * {@code GoldenCorpus/fixtures/composed-page/nav.json} - the single source of truth
     * every non-.NET ecosystem loads from - resolved through the SAME corpus-dir
     * resolution the gate uses ({@link Corpus#resolveRoot()}, honoring
     * {@code -Dheddle.corpus}). Parsed with the already-pinned Jackson dependency (the
     * manifest reader's mapper); a load or schema failure fails fast.
     */
    public static ComposedModel composed() {
        return ComposedHolder.MODEL;
    }

    private static final class ComposedHolder {
        static final ComposedModel MODEL = loadComposed();
    }

    private static ComposedModel loadComposed() {
        Path path = Corpus.resolveRoot()
                .resolve("fixtures").resolve("composed-page").resolve("nav.json");
        JsonNode root;
        try {
            root = new ObjectMapper().readTree(Files.readAllBytes(path));
        } catch (IOException e) {
            throw new Corpus.CorpusException(
                    "Cannot read composed-page nav fixture " + path
                            + " (set -Dheddle.corpus=...)", e);
        }
        List<MegaMenu> menus = new ArrayList<>();
        for (JsonNode menu : array(root, "menus")) {
            List<MenuTab> tabs = new ArrayList<>();
            for (JsonNode tab : array(menu, "tabs")) {
                tabs.add(new MenuTab(
                        text(tab, "label"),
                        text(tab, "href"),
                        text(tab, "css"),
                        bool(tab, "has_dropdown"),
                        text(tab, "dropdown_css"),
                        columns(array(tab, "columns"))));
            }
            menus.add(new MegaMenu(Collections.unmodifiableList(tabs)));
        }
        NavModel nav = new NavModel(
                Collections.unmodifiableList(menus),
                columns(array(root, "footer_columns")));
        assertRule4(nav);
        return new ComposedModel(nav);
    }

    private static List<NavColumn> columns(JsonNode columnsNode) {
        List<NavColumn> columns = new ArrayList<>();
        for (JsonNode column : columnsNode) {
            List<NavSection> sections = new ArrayList<>();
            for (JsonNode section : array(column, "sections")) {
                List<NavLink> links = new ArrayList<>();
                for (JsonNode link : array(section, "links")) {
                    links.add(new NavLink(text(link, "label"), text(link, "href")));
                }
                sections.add(new NavSection(
                        text(section, "title"),
                        text(section, "href"),
                        bool(section, "title_linked"),
                        Collections.unmodifiableList(links)));
            }
            columns.add(new NavColumn(Collections.unmodifiableList(sections)));
        }
        return Collections.unmodifiableList(columns);
    }

    private static JsonNode field(JsonNode node, String name) {
        JsonNode value = node.get(name);
        if (value == null || value.isNull()) {
            throw new IllegalStateException("nav.json: missing field '" + name + "'");
        }
        return value;
    }

    private static JsonNode array(JsonNode node, String name) {
        JsonNode value = field(node, name);
        if (!value.isArray()) {
            throw new IllegalStateException("nav.json: field '" + name + "' is not an array");
        }
        return value;
    }

    private static String text(JsonNode node, String name) {
        JsonNode value = field(node, name);
        if (!value.isTextual()) {
            throw new IllegalStateException("nav.json: field '" + name + "' is not a string");
        }
        return value.asText();
    }

    private static boolean bool(JsonNode node, String name) {
        JsonNode value = field(node, name);
        if (!value.isBoolean()) {
            throw new IllegalStateException("nav.json: field '" + name + "' is not a boolean");
        }
        return value.asBoolean();
    }

    /**
     * The nav-data character rule, asserted the way {@code NavData}'s static constructor
     * asserts it: every nav text value is printable ASCII with none of {@code & < > " '},
     * so raw and would-be-escaped renderings coincide and no default-escaping engine can
     * double-escape.
     */
    private static void assertRule4(NavModel nav) {
        for (MegaMenu menu : nav.getMenus()) {
            for (MenuTab tab : menu.getTabs()) {
                rule4(tab.getLabel());
                rule4(tab.getHref());
                rule4(tab.getCss());
                rule4(tab.getDropdownCss());
                rule4Columns(tab.getColumns());
            }
        }
        rule4Columns(nav.getFooterColumns());
    }

    private static void rule4Columns(List<NavColumn> columns) {
        for (NavColumn column : columns) {
            for (NavSection section : column.getSections()) {
                rule4(section.getTitle());
                rule4(section.getHref());
                for (NavLink link : section.getLinks()) {
                    rule4(link.getLabel());
                    rule4(link.getHref());
                }
            }
        }
    }

    private static void rule4(String value) {
        for (int i = 0; i < value.length(); i++) {
            char c = value.charAt(i);
            if (c < 0x20 || c > 0x7E || c == '&' || c == '<' || c == '>' || c == '"' || c == '\'') {
                throw new IllegalStateException("nav.json: value violates the nav-data"
                        + " character rule (printable ASCII, none of &<>\"'): \"" + value + "\"");
            }
        }
    }
}
