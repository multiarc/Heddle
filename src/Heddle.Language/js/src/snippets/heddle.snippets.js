// Ace snippet definitions for the Heddle mode.
//
// NOTE: this module is a JS template literal, so Ace `${n:label}` tabstops must
// be escaped as `\${n:label}` — otherwise JS would interpolate `${...}` and the
// tabstop markers would be lost (or, with a `:`, raise a parse error).
module.exports = `# list
snippet list
	@list(\${1}) {{

	}}
# if
snippet if
	@if(\${1}) {{

	}}
# ifnot
snippet ifnot
	@ifnot(\${1}) {{

	}}
# elif
snippet elif
	@elif(\${1}) {{

	}}
# else
snippet else
	@else {{

	}}
# for
snippet for
	@for(\${1:count}) {{

	}}
# param
snippet param
	@param(\${1})
# prop
snippet prop
	<\${1:name}(\${2:prop}: \${3:Type} = \${4:default})> {{

	}}
# slot
snippet slot
	<\${1:name}(out:: \${2:Type})> {{
		@out(\${3:this})
	}}
# region
snippet region
	<:\${1:name}> {{
		\${2}
	}}
# regiontype
snippet regiontype
	<:\${1:name} :: \${2:Type}> {{
		\${3}
	}}

`;