"use strict";

var TokenIterator = require("../token_iterator").TokenIterator;

var defaultExtensions = [
    "date",
    "time",
    "html",
    "guid",
    "int",
    "list",
    "model",
    "money",
    "out",
    "param",
    "partial",
    "string",
    "if",
    "ifnot",
    "elif",
    "elseif",
    "else",
    "profile",
    "raw",
    "import",
    "using",
    "for"
];

// Native-expression built-in functions (FunctionRegistry.Default).
//
// MAINTENANCE COUPLING: this list mirrors the default whitelist seeded by the
// C# engine in src/Heddle/Runtime/Expressions/BuiltInFunctions.cs
// (CreateEntries) and documented on FunctionRegistry.Default. Adding or
// removing a built-in in that C# source requires updating this list so the Ace
// completer keeps offering the correct set. Names are the distinct registered
// names (overloads collapsed).
var nativeFunctions = [
    "upper",
    "lower",
    "trim",
    "len",
    "contains",
    "startswith",
    "endswith",
    "replace",
    "substr",
    "format",
    "str",
    "abs",
    "min",
    "max",
    "round",
    "floor",
    "ceil",
    "range"
];

function is(token, type) {
    return token.type == type;
}

// Phase 7 (post-2.0) — public region declarations in the current document:
// `<:name>` / `<:name :: Type>` (whitespace allowed around the visibility `:`
// and the `::` type annotation, per lexer mode DEF where WS is hidden).
// The optional type capture covers plain / dotted / array type names; a
// generic type (`List<Article>`) is not matched by this scan — an accepted
// editor-layer approximation (the LSP, which owns semantic analysis, handles
// those).
var regionDeclarationPattern = /<\s*:(?!:)\s*([a-zA-Zа-яА-Я_][a-zA-Zа-яА-Я0-9_]*)\s*(?:::\s*([a-zA-Zа-яА-Я_][a-zA-Zа-яА-Я0-9_.]*(?:\[\])*)\s*)?>/g;

// `<:name>` text occurring inside a `@* … *@` comment (terminated or running
// to EOF) or inside a single-line `"…"` / `'…'` string literal is not a
// declaration and must not be offered. Same editor-layer approximation as
// above: spans are blanked lexically (comments first, then strings, matching
// the highlight rules' quoted-string patterns) rather than parsed. Each span
// is replaced by a sentinel that cannot occur in a declaration, so blanking
// never splices the surrounding text into a false `<:name>` match.
var nonDeclarationSpanPattern = /@\*[\s\S]*?(?:\*@|$)|"(?:\\.|[^"\\\n])*"|'(?:\\.|[^'\\\n])*'/g;

function stripNonDeclarationSpans(text) {
    return text.replace(nonDeclarationSpanPattern, "~");
}

/**
 * Scans document text for public region declarations.
 * @returns {{name: string, typeName: string|null}[]} unique regions in
 *   declaration order (first declaration of a name wins, mirroring HED5020's
 *   "the duplicate is never stored" parse behavior).
 */
function findPublicRegions(text) {
    var regions = [];
    var seen = {};
    var match;
    text = stripNonDeclarationSpans(text);
    regionDeclarationPattern.lastIndex = 0;
    while ((match = regionDeclarationPattern.exec(text)) !== null) {
        if (seen[match[1]])
            continue;
        seen[match[1]] = true;
        regions.push({ name: match[1], typeName: match[2] || null });
    }
    return regions;
}

var HeddleCompletions = function() {

};

(function() {

    this.getCompletions = function(state, session, pos, prefix) {
        var token = session.getTokenAt(pos.row, pos.column);

        if (!token)
            return [];

        if (is(token, "out-start"))
            return this.getExtensionCompletions(state, session, pos, prefix);

        if (is(token, "def_override"))
            return this.getExtensionOverrideCompletions(state, session, pos, prefix);

        // Phase 7 (post-2.0) — region-override position: the caret sits on (or
        // just after) the `<` that opens a definition name, or on a partial name
        // inside it. Parity with the LSP's RegionOverride completion (WI5): the
        // document's PUBLIC region names are offered with the `name:name`
        // override insert. The LSP additionally narrows the set to the *callee's*
        // regions via semantic analysis; the token-driven Ace completer offers
        // every public region declared in the document instead.
        if (is(token, "heddle-def.punctuation.operator.paren.lparen")
            || token.type.indexOf("heddle-def-name.identifier") === 0)
            return this.getRegionOverrideCompletions(state, session, pos, prefix);

        return [];
    };

    this.getExtensionCompletions = function (state, session, pos, prefix) {
        var extensions = defaultExtensions.map(function (name) {
            return {
                caption: name,
                snippet: name + '($0)',
                meta: "variable",
                score: Number.MAX_VALUE
            };
        });
        var functions = nativeFunctions.map(function (name) {
            return {
                caption: name,
                snippet: name + '($0)',
                meta: "function",
                score: Number.MAX_VALUE
            };
        });
        return extensions.concat(functions);
    };
    this.getExtensionOverrideCompletions = function (state, session, pos, prefix) {
        return defaultExtensions.map(function (name) {
            return {
                caption: name,
                snippet: name,
                meta: "variable",
                score: Number.MAX_VALUE
            };
        });
    };

    // Phase 7 (post-2.0) — public region names for the `<name:name>` call-body
    // override. Mirrors the LSP CompletionProvider RegionOverride branch: the
    // insert text is `name:name`, and the meta is "region" or
    // "region :: Type" for a typed region (the untyped/object case shows the
    // bare "region" label).
    this.getRegionOverrideCompletions = function (state, session, pos, prefix) {
        if (!session || typeof session.getValue !== "function")
            return [];
        return findPublicRegions(session.getValue()).map(function (region) {
            return {
                caption: region.name,
                snippet: region.name + ":" + region.name,
                meta: !region.typeName || region.typeName === "object"
                    ? "region"
                    : "region :: " + region.typeName,
                score: Number.MAX_VALUE
            };
        });
    };

}).call(HeddleCompletions.prototype);

exports.HeddleCompletions = HeddleCompletions;
