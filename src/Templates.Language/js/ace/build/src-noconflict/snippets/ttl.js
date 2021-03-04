ace.define("ace/snippets/ttl",[], function(require, exports, module) {
"use strict";

exports.snippetText = "# list\n\
snippet list\n\
	@list(${1}) {{\n\
\n\
	}}\n\
# if\n\
snippet if\n\
	@if(${1}) {{\n\
\n\
	}}\n\
# ifnot\n\
snippet ifnot\n\
	@else(${1}) {{\n\
\n\
	}}";
exports.scope = "ttl";

});                (function() {
                    ace.require(["ace/snippets/ttl"], function(m) {
                        if (typeof module == "object" && typeof exports == "object" && module) {
                            module.exports = m;
                        }
                    });
                })();
            