package heddle.benchmarks.jvm.model;

import java.io.IOException;
import java.io.InputStream;
import java.io.UncheckedIOException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

/**
 * All pinned workload models (Phase 3 spec, construct-mapping.md &sect;Java models).
 * Static final instances materialized once (the {@code LoopContent.Shared} discipline);
 * JavaBean-style POJOs because Thymeleaf's OGNL resolves {@code ${p.name}} through getters
 * and JTE templates call the same getters explicitly. All string assembly is
 * {@code String.format(Locale.ROOT, ...)} or plain ASCII/int concatenation - no locale,
 * time, or randomness anywhere. Pinned data must match Phase 1 byte-for-byte
 * (docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/workloads.md).
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

    public static final class LoopRow {
        private final String name;
        private final int value;

        public LoopRow(String name, int value) {
            this.name = name;
            this.value = value;
        }

        public String getName() { return name; }
        public int getValue() { return value; }
    }

    public static final List<LoopRow> LOOP_ROWS = buildLoopRows();

    private static List<LoopRow> buildLoopRows() {
        List<LoopRow> rows = new ArrayList<>(5000);
        for (int i = 0; i <= 4999; i++) {
            rows.add(new LoopRow("row-" + i, i));
        }
        return Collections.unmodifiableList(rows);
    }

    // ---- workload 4: mixed-page --------------------------------------------------------

    public static final class MixedProduct {
        private final String name;
        private final String sku;
        private final int price;
        private final boolean onSale;
        private final String blurb;

        public MixedProduct(String name, String sku, int price, boolean onSale, String blurb) {
            this.name = name;
            this.sku = sku;
            this.price = price;
            this.onSale = onSale;
            this.blurb = blurb;
        }

        public String getName() { return name; }
        public String getSku() { return sku; }
        public int getPrice() { return price; }
        public boolean isOnSale() { return onSale; }
        public String getBlurb() { return blurb; }
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
                    "MX-" + (1000 + i),
                    950 + i * 7,
                    i % 3 == 0,
                    "A dependable workshop staple from batch " + i
                            + ", checked for daily use and backed by our lifetime guarantee."));
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
        private final String note;
        private final boolean bronze;
        private final boolean silver;
        private final boolean gold;
        private final boolean hasNote;
        private final boolean active;

        public ConditionalRow(String name, String note, boolean bronze, boolean silver,
                              boolean gold, boolean hasNote, boolean active) {
            this.name = name;
            this.note = note;
            this.bronze = bronze;
            this.silver = silver;
            this.gold = gold;
            this.hasNote = hasNote;
            this.active = active;
        }

        public String getName() { return name; }
        public String getNote() { return note; }
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
                    "note " + i,
                    i % 4 == 0, i % 4 == 1, i % 4 == 2,
                    i % 2 == 0, i % 5 != 0));
        }
        return Collections.unmodifiableList(rows);
    }

    // ---- workload 6: fragment-heavy ----------------------------------------------------

    public static final class FragmentRow {
        private final String name;
        private final int value;
        private final String badge;

        public FragmentRow(String name, int value, String badge) {
            this.name = name;
            this.value = value;
            this.badge = badge;
        }

        public String getName() { return name; }
        public int getValue() { return value; }
        public String getBadge() { return badge; }
    }

    public static final List<FragmentRow> FRAGMENT_ROWS = buildFragmentRows();

    private static List<FragmentRow> buildFragmentRows() {
        String[] badges = {"new", "hot", "sale", "std"};
        List<FragmentRow> rows = new ArrayList<>(48);
        for (int i = 0; i <= 47; i++) {
            rows.add(new FragmentRow(
                    String.format(Locale.ROOT, "tile-%02d", i), i * 11, badges[i % 4]));
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
     * The 12 pinned rows of Phase 1 workloads.md workload 7, byte-for-byte: row 1 writes
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
     * Composed-page model, loaded once (lazily) from the fragment resource files under
     * {@code composed-page/} on the classpath (construct-mapping.md &sect;Composed-page
     * fragment resources). The resources are a WI2 deliverable; until they exist,
     * {@link #composed()} throws with a clear missing-resource message so WI1's
     * template-independent verbs (probe, calibrate) are unaffected.
     */
    public static final class ComposedModel {
        private final Map<String, String> sections;
        private final Map<String, String> comps;
        private final Map<String, String> areas;
        private final List<String> areaNames;

        public ComposedModel(Map<String, String> sections, Map<String, String> comps,
                             Map<String, String> areas, List<String> areaNames) {
            this.sections = sections;
            this.comps = comps;
            this.areas = areas;
            this.areaNames = areaNames;
        }

        public Map<String, String> getSections() { return sections; }
        public Map<String, String> getComps() { return comps; }
        public Map<String, String> getAreas() { return areas; }
        public List<String> getAreaNames() { return areaNames; }
    }

    private static volatile ComposedModel composed;

    public static ComposedModel composed() {
        ComposedModel model = composed;
        if (model == null) {
            synchronized (Models.class) {
                model = composed;
                if (model == null) {
                    composed = model = loadComposed();
                }
            }
        }
        return model;
    }

    private static ComposedModel loadComposed() {
        List<String> areaNames = new ArrayList<>();
        for (String line : resource("composed-page/area-order.txt").split("\n", -1)) {
            String name = line.endsWith("\r") ? line.substring(0, line.length() - 1) : line;
            if (!name.isEmpty()) {
                areaNames.add(name);
            }
        }
        Map<String, String> sections = new LinkedHashMap<>();
        Map<String, String> comps = new LinkedHashMap<>();
        Map<String, String> areas = new LinkedHashMap<>();
        // Key sets mirror TwinContent.Sections()/Components()/Areas exactly; the concrete
        // file set is transcribed by WI2. Each fragment file is one UTF-8 (no BOM) resource
        // named section.<key>.txt / comp.<key>.txt / area.<index>.<slug>.txt; a manifest-free
        // convention: WI2 ships an index file listing them.
        for (String entry : resource("composed-page/fragments.txt").split("\n", -1)) {
            String line = entry.endsWith("\r") ? entry.substring(0, entry.length() - 1) : entry;
            if (line.isEmpty()) {
                continue;
            }
            // Format per line: <kind>\t<key>\t<file>
            String[] parts = line.split("\t", 3);
            if (parts.length != 3) {
                throw new IllegalStateException(
                        "composed-page/fragments.txt: malformed line \"" + line + "\"");
            }
            String content = resource("composed-page/" + parts[2]);
            switch (parts[0]) {
                case "section" -> sections.put(parts[1], content);
                case "comp" -> comps.put(parts[1], content);
                case "area" -> areas.put(parts[1], content);
                default -> throw new IllegalStateException(
                        "composed-page/fragments.txt: unknown kind \"" + parts[0] + "\"");
            }
        }
        return new ComposedModel(
                Collections.unmodifiableMap(sections),
                Collections.unmodifiableMap(comps),
                Collections.unmodifiableMap(areas),
                Collections.unmodifiableList(areaNames));
    }

    private static String resource(String name) {
        try (InputStream in = Models.class.getClassLoader().getResourceAsStream(name)) {
            if (in == null) {
                throw new IllegalStateException("Missing classpath resource '" + name
                        + "' (composed-page fragment resources are a WI2 deliverable)");
            }
            return new String(in.readAllBytes(), StandardCharsets.UTF_8);
        } catch (IOException e) {
            throw new UncheckedIOException("Failed reading classpath resource '" + name + "'", e);
        }
    }
}
