module heddle.dev/benchmarks/go

go 1.26

toolchain go1.26.5

require github.com/a-h/templ v0.3.1020

require (
	github.com/a-h/parse v0.0.0-20250122154542-74294addb73e // indirect
	github.com/aclements/go-moremath v0.0.0-20210112150236-f10218a38794 // indirect
	github.com/andybalholm/brotli v1.1.0 // indirect
	github.com/cenkalti/backoff/v4 v4.3.0 // indirect
	github.com/cli/browser v1.3.0 // indirect
	github.com/fatih/color v1.16.0 // indirect
	github.com/fsnotify/fsnotify v1.7.0 // indirect
	github.com/mattn/go-colorable v0.1.13 // indirect
	github.com/mattn/go-isatty v0.0.20 // indirect
	github.com/natefinch/atomic v1.0.1 // indirect
	golang.org/x/mod v0.26.0 // indirect
	golang.org/x/net v0.57.0 // indirect
	golang.org/x/perf v0.0.0-20260709024250-82a0b07e230d // indirect
	golang.org/x/sync v0.16.0 // indirect
	golang.org/x/sys v0.47.0 // indirect
	golang.org/x/tools v0.35.0 // indirect
)

tool (
	github.com/a-h/templ/cmd/templ
	golang.org/x/perf/cmd/benchstat
)
