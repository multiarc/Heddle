define(function (require, exports, module) {
    // Generated from d:\Work\Templater\src\Templates.Language\TtlParser.g4 by ANTLR 4.5.1
    // jshint ignore: start
    var antlr4 = require('antlr4/index');

    // This class defines a complete listener for a parse tree produced by TtlParser.
    function TtlParserListener() {
        antlr4.tree.ParseTreeListener.call(this);
        return this;
    }

    TtlParserListener.prototype = Object.create(antlr4.tree.ParseTreeListener.prototype);
    TtlParserListener.prototype.constructor = TtlParserListener;

    // Enter a parse tree produced by TtlParser#ttl.
    TtlParserListener.prototype.enterTtl = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#ttl.
    TtlParserListener.prototype.exitTtl = function (ctx) {
    };


    // Enter a parse tree produced by TtlParser#comment.
    TtlParserListener.prototype.enterComment = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#comment.
    TtlParserListener.prototype.exitComment = function (ctx) {
    };


    // Enter a parse tree produced by TtlParser#raw.
    TtlParserListener.prototype.enterRaw = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#raw.
    TtlParserListener.prototype.exitRaw = function (ctx) {
    };


    // Enter a parse tree produced by TtlParser#definition.
    TtlParserListener.prototype.enterDefinition = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#definition.
    TtlParserListener.prototype.exitDefinition = function (ctx) {
    };


    // Enter a parse tree produced by TtlParser#def.
    TtlParserListener.prototype.enterDef = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#def.
    TtlParserListener.prototype.exitDef = function (ctx) {
    };


    // Enter a parse tree produced by TtlParser#inherited_def.
    TtlParserListener.prototype.enterInherited_def = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#inherited_def.
    TtlParserListener.prototype.exitInherited_def = function (ctx) {
    };


    // Enter a parse tree produced by TtlParser#simple_def.
    TtlParserListener.prototype.enterSimple_def = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#simple_def.
    TtlParserListener.prototype.exitSimple_def = function (ctx) {
    };


    // Enter a parse tree produced by TtlParser#default_chain.
    TtlParserListener.prototype.enterDefault_chain = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#default_chain.
    TtlParserListener.prototype.exitDefault_chain = function (ctx) {
    };


    // Enter a parse tree produced by TtlParser#outblock.
    TtlParserListener.prototype.enterOutblock = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#outblock.
    TtlParserListener.prototype.exitOutblock = function (ctx) {
    };


    // Enter a parse tree produced by TtlParser#chain.
    TtlParserListener.prototype.enterChain = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#chain.
    TtlParserListener.prototype.exitChain = function (ctx) {
    };


    // Enter a parse tree produced by TtlParser#call.
    TtlParserListener.prototype.enterCall = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#call.
    TtlParserListener.prototype.exitCall = function (ctx) {
    };


    // Enter a parse tree produced by TtlParser#named_call.
    TtlParserListener.prototype.enterNamed_call = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#named_call.
    TtlParserListener.prototype.exitNamed_call = function (ctx) {
    };


    // Enter a parse tree produced by TtlParser#unnamed_call.
    TtlParserListener.prototype.enterUnnamed_call = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#unnamed_call.
    TtlParserListener.prototype.exitUnnamed_call = function (ctx) {
    };


    // Enter a parse tree produced by TtlParser#csharp_expression.
    TtlParserListener.prototype.enterCsharp_expression = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#csharp_expression.
    TtlParserListener.prototype.exitCsharp_expression = function (ctx) {
    };


    // Enter a parse tree produced by TtlParser#subtemplate.
    TtlParserListener.prototype.enterSubtemplate = function (ctx) {
    };

    // Exit a parse tree produced by TtlParser#subtemplate.
    TtlParserListener.prototype.exitSubtemplate = function (ctx) {
    };



    exports.TtlParserListener = TtlParserListener;
});