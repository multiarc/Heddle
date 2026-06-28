import { defineConfig } from 'vitepress'
import { withMermaid } from 'vitepress-plugin-mermaid'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
// Bundled grammars the Heddle grammar embeds. `@shikijs/langs/html` also carries its
// own javascript + css dependencies.
import csharpLangs from '@shikijs/langs/csharp'
import htmlLangs from '@shikijs/langs/html'

// Reuse the project's existing TextMate grammar as the single source of truth so
// every ```heddle block is highlighted at build time by VitePress's built-in Shiki.
// The grammar embeds `source.cs` (csharp) and `text.html.basic` (html); listing
// those under `embeddedLangs` makes Shiki load the bundled grammars so the grammar's
// `include`s resolve. If a build ever errors with "missing grammar for scope
// source.cs / text.html.basic", that list is the place to fix it.
const heddleGrammar = JSON.parse(
  readFileSync(
    fileURLToPath(new URL('../../syntaxes/heddle.tmLanguage.json', import.meta.url)),
    'utf-8'
  )
)

const REPO_BLOB = 'https://github.com/multiarc/Heddle/blob/main/'

export default withMermaid(
  defineConfig({
    title: 'Heddle',
    description: 'A compiled, strongly-typed text template engine for .NET.',
    // Project Pages are served from https://multiarc.github.io/Heddle/.
    base: '/Heddle/',
    lastUpdated: true,

    // Serve docs/README.md as the site home (/) without renaming the file, so the
    // existing README stays the single source and links to it still resolve.
    rewrites: { 'README.md': 'index.md' },

    // assessment.md stays in the repo for contributors but is not published.
    // develop.txt is not Markdown.
    srcExclude: ['assessment.md', 'develop.txt'],

    // Heddle templates use `{{ }}` pervasively, which Vue would otherwise parse as
    // mustache interpolation in prose/inline code. The docs use no intentional Vue
    // interpolation, so disable it by setting delimiters that never appear in text.
    vue: {
      template: {
        compilerOptions: {
          delimiters: ['__VP_NO_INTERP_OPEN__', '__VP_NO_INTERP_CLOSE__']
        }
      }
    },

    markdown: {
      languages: [
        ...(csharpLangs as any[]),
        ...(htmlLangs as any[]),
        {
          ...heddleGrammar,
          name: 'heddle',
          embeddedLangs: ['csharp', 'html']
        } as any
      ],
      // Rewrite repo-relative source links (../src/..., ../syntaxes/..., ../build.ps1,
      // ../develop.txt, ...) to GitHub blob URLs opened in a new tab. Same-folder .md
      // links and absolute URLs are untouched and resolved natively by VitePress.
      config(md) {
        const defaultRender =
          md.renderer.rules.link_open ||
          ((tokens, idx, options, _env, self) => self.renderToken(tokens, idx, options))
        md.renderer.rules.link_open = (tokens, idx, options, env, self) => {
          const href = tokens[idx].attrGet('href')
          if (href && href.startsWith('../')) {
            tokens[idx].attrSet('href', REPO_BLOB + href.replace(/^(\.\.\/)+/, ''))
            tokens[idx].attrSet('target', '_blank')
            tokens[idx].attrSet('rel', 'noreferrer')
          }
          return defaultRender(tokens, idx, options, env, self)
        }
      }
    },

    themeConfig: {
      nav: [
        { text: 'Getting Started', link: '/getting-started' },
        { text: 'Language', link: '/language-reference' },
        { text: 'C# API', link: '/csharp-api' },
        { text: 'GitHub', link: 'https://github.com/multiarc/Heddle' }
      ],
      sidebar: [
        {
          text: 'Overview',
          items: [
            { text: 'Introduction', link: '/' },
            { text: 'Getting Started', link: '/getting-started' }
          ]
        },
        {
          text: 'Authoring Templates',
          items: [
            { text: 'Language Reference', link: '/language-reference' },
            { text: 'Built-in Extensions', link: '/built-in-extensions' },
            { text: 'Patterns & Recipes', link: '/patterns' }
          ]
        },
        {
          text: 'Using from C#',
          items: [
            { text: 'C# API Reference', link: '/csharp-api' },
            { text: 'MVC Integration', link: '/mvc-integration' }
          ]
        },
        {
          text: 'Extending & Internals',
          items: [
            { text: 'Writing Custom Extensions', link: '/custom-extensions' },
            { text: 'Architecture', link: '/architecture' },
            { text: 'Syntax Highlighting', link: '/syntax-highlighting' },
            { text: 'Building & Testing', link: '/building' }
          ]
        }
      ],
      search: { provider: 'local' },
      editLink: {
        pattern: 'https://github.com/multiarc/Heddle/edit/main/docs/:path',
        text: 'Edit this page on GitHub'
      },
      socialLinks: [{ icon: 'github', link: 'https://github.com/multiarc/Heddle' }]
    }
  })
)
