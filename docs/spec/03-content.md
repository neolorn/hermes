# 03 — Content

How a message's text is written, in which languages, how values reach it, how it is kept
safe and bounded, and what is checked before the first send. Every value named
`Hermes:...` is configuration (`08` section 4).

---

**MSG-TPL-001** — Every channel of every kind SHALL have a template in each declared
language, written in Liquid and rendered by Fluid, made of the parts its channel needs.

**Values (M-002, M-005, M-026).**

| Channel | Parts |
|---|---|
| Email | `subject` and `text`, required; `html`, optional |
| SMS | `text`, required |
| Push | `title` and `body`, required; `url`, `tag`, `icon`, `badge`, optional |
| In-app | `title`, required; `body`, optional (`02` MSG-INAPP-002) |

A template part is found by its `TemplateKey` (`04` MSG-LIB-002): the kind, the channel,
the language and the part, through the template source (MSG-TPL-006). An in-app
template's parts are rendered in every declared language when the send is recorded,
every other channel's in the send's language alone (`02` MSG-INAPP-002). The library's
own pages (MSG-TPL-006) are keyed by page, language and part instead, and an automatic
reply's template (`09` MSG-CONV-003) by its rule's name as `Reply`, the language and the
part `text` (M-035).

*Source: M-002, M-005, M-026, M-035*

**Acceptance criteria**
1. A kind's email in Arabic renders its Arabic subject and text, and its HTML part where
   one exists.

---

**MSG-TPL-002** — A template SHALL take its values only from the send's `Values` and
from the values the library adds, every value a template uses SHALL be passed, and a
send that passes a value wrongly SHALL be a fault of the calling code.

**Values (M-002, M-005, M-026).** `Values` maps names to values. A name is 1 to 64
letters, digits and `_`, starting with a letter, and no two names of a send are equal
ignoring case (Fluid compares names ignoring case). A value is a string, a whole number
(64-bit), a decimal number, a boolean, an instant (`DateTimeOffset`), or a list of maps
whose values are of those scalar kinds, so that an order's lines can be listed; a list
holds no list. A map inside a list follows the same rule for its entry names, the
library's reserved names being reserved at the top level only. Rendering is strict
(Fluid's `StrictVariables`): a send passes every top-level name read by the templates it
renders (those of its channels, in its language, and its in-app channel's in every
declared language), in an output or a condition alike, save the values the library adds
below, Fluid's `forloop`, and their loop variables and assigned names (the startup walk
of MSG-TPL-004 collects them), and one it does not pass is a fault found before the
render, never an empty string; a member missing from data it passes (the first line of
an empty list, an index past a list's end) is refused with `messaging.send.renderfailed`
(MSG-TPL-007), since a host's data can cause it. A kind's in-app target values (`02`
MSG-INAPP-001) are top-level names of scalar values, and a send that does not pass one,
or passes a list under it, is a fault as for a template's value. The library adds, to
every template, these values, whose names a send SHALL NOT use:
- `unsubscribe`: the one-click address, in an optional category's email only (`02`
  MSG-EMAIL-006);
- `language`: the send's language (for an in-app part, the language it is rendered in),
  and `direction`: `ltr` or `rtl` (MSG-TPL-003).

And these filters, which replace Fluid's filters of the same names:
- `format_date: "<format>"`: an instant in the send's time zone, under a .NET date
  format string, in the send's language's culture;
- `format_number: "<format>"`: a number under a .NET numeric format string, in that
  culture.

Fluid's clock is the library's `TimeProvider` and its time zone the send's; an in-app
part rendered in a language other than the send's is formatted in that language's
culture. A host formats anything else (an amount with its currency, an address) before
passing it.

*Source: M-002, M-005, M-026*

**Acceptance criteria**
1. A send that leaves out a value its template reads, in a condition or a branch not
   taken included, is a fault and records nothing.
2. A send passing `orderId` and `OrderID`, or a value named `language`, is a fault.
3. `format_date` renders an instant in the send's time zone and language.
4. A template reading `lines.first.name`, sent with an empty `lines`, is refused with
   `messaging.send.renderfailed`.

---

**MSG-TPL-003** — The deployment SHALL declare the languages it sends in and its
default, and each SHALL resolve to a culture of its own.

**Values (M-005).** `Hermes:Languages` (BCP 47 tags, at least one) and
`Hermes:DefaultLanguage`, which is among them. Each language resolves to a .NET culture
other than the invariant one, resolved as a predefined culture only (so a mistyped tag
never passes as a made-up one), or startup fails with
`messaging.startup.configurationinvalid`, `details.key` `Languages`; a process running
in globalization-invariant mode therefore does not start. A language's direction is
`rtl` where its culture's text runs right to left and `ltr` otherwise. A send naming a
language that matches no declared one is rendered in the default (`01` MSG-SEND-003). An
HTML template writes `language` and `direction` on its root element, as the library's
pages do; a push payload carries both (`02` MSG-PUSH-002).

*Source: M-005*

**Acceptance criteria**
1. `direction` is `rtl` for `ar` and `ltr` for `en`.
2. A deployment declaring a language that resolves only to the invariant culture does
   not start.

---

**MSG-TPL-004** — Every template SHALL be loaded, parsed and checked when the
application starts, and any failure SHALL stop the start.

**Values (M-005).** The check walks each parsed template's syntax tree (Fluid's public
`AstVisitor`), since Fluid resolves filters only when it renders. Startup fails with
`messaging.startup.declarationinvalid`, `details.declaration` the kind (or the page),
`details.field` the channel, language and part, for:
- a template part missing for a declared language, where the part is required;
- a template that does not parse;
- a tag or filter outside the allowed sets (MSG-TPL-007);
- a range whose bounds are not both literal numbers, or whose span exceeds
  `Hermes:Templates:MaxSteps` (MSG-TPL-007);
- `for` loops nested deeper than `Hermes:Templates:MaxRecursion`, since with `include`
  and `render` refused the depth is fixed by the template's text (MSG-TPL-007);
- an allowed filter given fewer arguments than it requires, which Fluid would mostly
  read as nil at render: two for `replace` and `replace_first`; one for `append`,
  `prepend`, `remove`, `remove_first`, `slice`, `at_least`, `at_most`, `divided_by`,
  `minus`, `modulo`, `plus`, `times`, `map`, `where`, `default`, `format_date` and
  `format_number`; none for the others;
- a `format_date` or `format_number` whose format is not a literal string, or a standard
  numeric format whose precision is above 28, since such a format could build a string
  of any length before the render's limits are checked (MSG-TPL-007);
- a part or a page that uses a value the library adds only elsewhere: `unsubscribe`
  outside an optional category's email, or `category` or `action` in a page that is not
  given it (MSG-TPL-006), since strict rendering would then fail every send or page;
- an optional category's email whose `text` part, or `html` part where it has one, does
  not use `unsubscribe`, and an essential category's email that does (`02`
  MSG-EMAIL-006);
- where an optional category has an email kind, a page part missing for a declared
  language (MSG-TPL-006).

Where conversations are registered, every automatic reply rule's template (`09`
MSG-CONV-003) is checked in every declared language as a kind's is, and a missing one
fails the start with `messaging.startup.templatemissing`, naming the rule and the
language (M-035).

*Source: M-002, M-005, M-035*

**Acceptance criteria**
1. Each condition above stops the start and names the template.
2. A deployment adding a language without its templates does not start.
3. A `replace` given one argument, a `format_number` whose format is a value, and a
   `format_number: "F999999999"` each stop the start.

---

**MSG-TPL-005** — Every value written into an HTML part SHALL be HTML-encoded, and every
part SHALL be safe for where it is written.

**Values (M-005).** An HTML part renders under an HTML encoder that allows every Unicode
range (`HtmlEncoder.Create(UnicodeRanges.All)`), so it escapes markup and quotes and
leaves Arabic text as it is, and no filter that writes unencoded is allowed
(MSG-TPL-007). The text, SMS and push parts render without HTML encoding; the push
payload is serialised as JSON under `JavaScriptEncoder.Create(UnicodeRanges.All)`, which
escapes what JSON requires and the characters HTML treats specially, and leaves the
letters of every script as they are, so neither part grows against its channel's limit
by escaping letters. In an email's subject and in a display name, every control
character (U+0000 to U+001F and U+007F) is replaced by a space before the MIME header is
written, so no value can start a header of its own.

*Source: M-002, M-005*

**Acceptance criteria**
1. A value `<script>` in an HTML part arrives as `&lt;script&gt;`.
2. A subject value carrying a line feed and `Bcc: x@example.com` arrives as one subject
   line, with no `Bcc` header.
3. An Arabic word in an HTML part, and in a push payload, arrives as its own characters,
   not as numeric references or `\u` escapes.
4. In an HTML part, `{% capture c %}x{% endcapture %}{{ c | append: v }}` with `v` set
   to `<script>` arrives as `x&lt;script&gt;`.

---

**MSG-TPL-006** — A host SHALL be able to supply its templates through a template
source; the library's own reads them from a folder and from assemblies the host passes,
and ships the templates of its own pages.

**Values (M-005, M-016).** `ITemplateSource.ReadAsync(TemplateKey, CancellationToken)`
(`04` MSG-LIB-002) answers a part's text, or none. Where a host registers one, it is
asked first; for a key it answers none, the library's own is asked, in this order:
1. the file under `Hermes:Templates:Path`: `<kind>/<channel>.<language>.<part>.liquid`,
   for a page `pages/<page>.<language>.<part>.liquid`, or for an automatic reply
   `replies/<name>.<language>.text.liquid` (`09` MSG-CONV-003);
2. the embedded resources of each assembly the host passes in `AddHermes`
   (`declare.Templates(assembly)`), matched by a resource's logical name equal to that
   relative path;
3. for a page, the library's shipped template.

Templates are read at startup and kept; a changed template takes effect at the next
start. A folder that does not exist holds no template, so a host whose templates all
come from its source or its assemblies needs none (`08` section 4).

The library's pages, each with one part, `html`:

| Page | Shown | Values |
|---|---|---|
| `unsubscribe` | By `GET /unsubscribe/{token}`: asks the person to confirm, with one button that posts a form carrying a field named `page` to `action` | `category` (the category's label in the language), `action` (the address the form posts to), `language`, `direction` |
| `unsubscribed` | After the page's form is posted: confirms that the category's messages stop | `category`, `language`, `direction` |
| `unsubscribeinvalid` | By `GET /unsubscribe/{token}`, or a `POST` carrying `page`, for a token that does not verify: says the link no longer works | `language`, `direction` |

The library ships the three pages in `en` and `ar`. A page renders in the language its
token records, or the default where that language is no longer declared or the token
does not verify (`05` MSG-API-005). A category's label is the host's (`01`
MSG-SEND-002): the library holds no wording of its own besides the shipped pages, which
a host replaces by supplying its own.

*Source: M-005, M-016, M-035*

**Acceptance criteria**
1. A template file under the folder is used for its kind, channel, language and part.
2. An embedded resource whose logical name is `order.placed/email.ar.subject.liquid` is
   used for kind `order.placed`.
3. A host's own `unsubscribe` page in `en` replaces the shipped one.

---

**MSG-TPL-007** — Rendering SHALL be bounded, so that no template can run, or build a
value, without limit.

**Values (M-002, M-005).**
- **Tags:** `if`, `elsif`, `else`, `unless`, `case`, `when`, `for` (with `break` and
  `continue`), `assign`, `capture` and `comment`. `include` and `render` are not
  allowed, so a template reads nothing but its own text and its values; nor is the `raw`
  tag.
- **Filters:** `append`, `capitalize`, `downcase`, `upcase`, `lstrip`, `rstrip`,
  `strip`, `prepend`, `remove`, `remove_first`, `replace`, `replace_first`, `slice`,
  `strip_html`, `strip_newlines`, `truncate`, `truncatewords`, `url_encode`, `abs`,
  `at_least`, `at_most`, `ceil`, `divided_by`, `floor`, `minus`, `modulo`, `plus`,
  `round`, `times`, `first`, `last`, `join`, `map`, `reverse`, `size`, `sort`,
  `sort_natural`, `uniq`, `where`, `compact`, `default`, and the library's `format_date`
  and `format_number`. Every other filter is refused, Fluid's `raw`, `date`, `json`,
  `format_string`, `time_zone`, `newline_to_br`, hashing and encoding filters among
  them. Fluid's `StrictFilters` is set.
- **Limits:** Fluid's `MaxSteps` is `Hermes:Templates:MaxSteps` (default 100,000) and
  its `MaxRecursion` `Hermes:Templates:MaxRecursion` (default 20). Every buffer a render
  writes into stops the render once it holds more than the channel's limit (`02`
  MSG-EMAIL-003, ten segments of MSG-SMS-001, MSG-PUSH-002). Fluid's `capture` renders
  into a buffer of its own, so the library registers its own `capture` block, writing
  into such a bounded buffer, in place of Fluid's, and the startup check treats it as
  `capture`. A filter whose result would be a longer string than the limit, or an array
  of more than `MaxSteps` items, stops the render too: for `replace` and `join`, whose
  result can exceed the sum of their inputs, and `url_encode`, whose result can be nine
  times its input, the library computes the result's length from the arguments before
  calling Fluid's filter, and a `replace` whose search is empty answers its input
  unchanged; for the others it checks the result. A range is checked at startup
  (MSG-TPL-004).
- **Telling a limit from a fault:** a render runs on the library's own subclass of
  Fluid's `TemplateContext`. Fluid throws an `InvalidOperationException` for both of its
  limits; the depth of nesting is checked at startup (MSG-TPL-004), so at render only
  the steps can be passed: where an `InvalidOperationException` escapes a render and the
  subclass's step count is above `MaxSteps`, the render is refused; a `FluidException`
  at render can come only from strict variables (the startup walk leaves no unknown
  filter, and the check before the render no top-level name missing), so from a member
  missing from passed data, or from an assigned name, a loop variable or `forloop` read
  where it was not set, and is refused with `messaging.send.renderfailed` (MSG-TPL-002);
  an `ArithmeticException` from an allowed filter (a division by zero, an overflow), and
  an argument a filter cannot take, which the library's wrappers check before the call
  (a `slice` offset or length that is not a whole number, `round` digits outside 0 to
  28), are refused with `messaging.send.renderfailed`, since a host's data can cause
  them; any other exception is thrown on.
- **Captured text in HTML:** a `capture` in an HTML part keeps its markup, its values
  encoded as they are written, and its result is marked as already encoded. Where a
  string filter (`append`, `prepend`, `replace`, `replace_first`, `truncate`, `default`
  and the rest) combines an already-encoded value with any other, as its input or as an
  argument, the library's wrapper HTML-encodes the text of every value not already
  encoded, whatever its type (a list's included), and marks the result as already
  encoded, so a value never joins encoded text unencoded and a capture's markup is kept
  either way.

A render whose buffer, or a filter's result, would pass the channel's limit is refused
with `messaging.send.toolong`, and one past `MaxSteps` with
`messaging.send.renderlimit`, `details.channel` naming the channel in both (`01`
MSG-SEND-003); the send records nothing. Either can follow from a host's data, so
neither is a fault (`07` MSG-ENG-003).

*Source: M-002, M-005*

**Acceptance criteria**
1. A template looping over a 10-million-item list is refused with
   `messaging.send.renderlimit`, and the send records nothing.
2. A template doubling a value with `append` thirty times, or building one in a
   `capture` inside a loop, is refused with `messaging.send.toolong` before its string
   passes the channel's limit.
3. A `join` of a long list with a long separator is refused before the joined string is
   built.
4. A template using `include`, `raw` or the range `(1..2000000000)`, or nesting `for`
   loops deeper than `Hermes:Templates:MaxRecursion`, stops the start.
