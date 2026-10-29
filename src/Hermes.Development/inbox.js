"use strict";

// The development inbox's page (02 MSG-DEV-004). It reads only the group's own routes and
// writes every value of a message as text, never as markup (MSG-DEV-005).
(function () {
  const every = 1000;
  const reloadEvery = 10;
  const most = 1000;
  const root = document.body.dataset.root || "";

  const list = document.getElementById("list");
  const empty = document.getElementById("empty");
  const detail = document.getElementById("detail");
  const status = document.getElementById("status");
  const filters = {
    to: document.getElementById("filter-to"),
    person: document.getElementById("filter-person"),
    channel: document.getElementById("filter-channel"),
    app: document.getElementById("filter-app"),
  };

  const channels = { email: "Email", sms: "SMS", push: "Push" };
  const encodings = { gsm7: "GSM-7", ucs2: "UCS-2" };

  let messages = [];
  let newest = null;
  let selected = null;
  let ticks = 0;

  // An element with its attributes and children; a child string, and the "text" attribute, are
  // written as text nodes.
  function element(name, attributes, ...children) {
    const node = document.createElement(name);
    for (const [key, value] of Object.entries(attributes || {})) {
      if (value === null || value === undefined || value === false) {
        continue;
      }

      if (key === "class") {
        node.className = value;
      } else if (key === "text") {
        node.textContent = value;
      } else {
        node.setAttribute(key, value === true ? "" : String(value));
      }
    }

    for (const child of children) {
      if (child !== null && child !== undefined) {
        node.append(child);
      }
    }

    return node;
  }

  function say(text) {
    status.textContent = text;
  }

  function when(at) {
    const date = new Date(at);
    return Number.isNaN(date.getTime()) ? at : date.toLocaleString();
  }

  function delivery(message) {
    const parts = [message.kind, message.category, message.language].filter((part) => part);
    return message.kind ? parts.join(" · ") : "Sent directly";
  }

  function recipient(message) {
    return message.to || (message.channel === "push" ? "A browser" : "");
  }

  function matches(message) {
    const to = filters.to.value.trim().toLowerCase();
    const person = filters.person.value.trim();
    return (!to || (message.to || "").toLowerCase().includes(to))
      && (!person || (message.person || "").includes(person))
      && (!filters.channel.value || message.channel === filters.channel.value)
      && (!filters.app.value || message.app === filters.app.value);
  }

  function showApps() {
    const chosen = filters.app.value;
    const apps = [...new Set(messages.map((message) => message.app))].sort();
    filters.app.replaceChildren(
      element("option", { value: "", text: "Every application" }),
      ...apps.map((app) => element("option", { value: app, text: app, selected: app === chosen })));
  }

  function showList() {
    const shown = messages.filter(matches);
    list.replaceChildren(...shown.map((message) => element("li", null,
      element("button", { type: "button", class: "entry", "data-id": message.id, "aria-current": message.id === selected ? "true" : null },
        element("span", { class: "row" },
          element("span", { class: "badge " + message.channel, text: channels[message.channel] || message.channel }),
          element("span", { class: "to", dir: "auto", text: recipient(message) }),
          element("time", { datetime: message.at, text: when(message.at) })),
        element("span", { class: "summary", dir: "auto", text: message.summary }),
        element("span", { class: "meta", text: message.app + " · " + delivery(message) })))));
    empty.hidden = shown.length > 0;
    empty.textContent = messages.length === 0
      ? "No message yet. Each message the application sends appears here as it is captured."
      : "No message matches the filters.";
  }

  function hold(added) {
    const known = new Set(messages.map((message) => message.id));
    const fresh = added.filter((message) => !known.has(message.id));
    if (fresh.length === 0) {
      return;
    }

    messages = [...fresh, ...messages]
      .sort((one, other) => (one.at < other.at ? 1 : one.at > other.at ? -1 : 0))
      .slice(0, most);
    newest = messages[0].at;
    showApps();
    showList();
  }

  async function reload() {
    const answer = await fetch(root + "/messages?limit=" + most, { cache: "no-store" });
    if (!answer.ok) {
      throw new Error(String(answer.status));
    }

    const body = await answer.json();
    messages = [];
    newest = null;
    hold(body.messages);
    showApps();
    showList();
    if (selected && !messages.some((message) => message.id === selected)) {
      selected = null;
      detail.replaceChildren(element("p", { class: "empty", text: "Choose a message to read it." }));
    }
  }

  async function poll() {
    ticks += 1;
    try {
      if (newest === null || ticks % reloadEvery === 0) {
        await reload();
      } else {
        const answer = await fetch(root + "/messages?after=" + encodeURIComponent(newest) + "&limit=" + most, { cache: "no-store" });
        if (!answer.ok) {
          throw new Error(String(answer.status));
        }

        hold((await answer.json()).messages);
      }

      if (status.dataset.failed) {
        delete status.dataset.failed;
        say("");
      }
    } catch {
      status.dataset.failed = "true";
      say("The application is not answering; the page keeps trying.");
    } finally {
      window.setTimeout(poll, every);
    }
  }

  function facts(rows) {
    return element("dl", { class: "facts" }, ...rows
      .filter(([, value]) => value !== null && value !== undefined && value !== "")
      .flatMap(([name, value]) => [element("dt", { text: name }), element("dd", { dir: "auto", text: String(value) })]));
  }

  function address(mailbox) {
    return mailbox ? (mailbox.displayName ? mailbox.displayName + " <" + mailbox.address + ">" : mailbox.address) : null;
  }

  // Tabs, each a button over its panel, moved between with the arrow keys.
  function tabs(name, pages) {
    const bar = element("div", { class: "tabs", role: "tablist", "aria-label": name });
    const panels = pages.map(([, content], index) => element("div", { class: "panel", role: "tabpanel", id: "panel-" + index, hidden: index !== 0 }, content));
    const buttons = pages.map(([title], index) => element("button", {
      type: "button", role: "tab", id: "tab-" + index, "aria-controls": "panel-" + index,
      "aria-selected": index === 0 ? "true" : "false", tabindex: index === 0 ? null : "-1", text: title,
    }));
    function choose(chosen) {
      buttons.forEach((button, index) => {
        button.setAttribute("aria-selected", index === chosen ? "true" : "false");
        button.tabIndex = index === chosen ? 0 : -1;
        panels[index].hidden = index !== chosen;
      });
      buttons[chosen].focus();
    }

    buttons.forEach((button, index) => {
      button.addEventListener("click", () => choose(index));
      button.addEventListener("keydown", (event) => {
        if (event.key === "ArrowRight" || event.key === "ArrowLeft") {
          event.preventDefault();
          choose((index + (event.key === "ArrowRight" ? 1 : buttons.length - 1)) % buttons.length);
        }
      });
      panels[index].setAttribute("aria-labelledby", button.id);
    });
    bar.append(...buttons);
    return element("div", null, bar, ...panels);
  }

  function email(message) {
    const mail = message.email;
    const view = mail.html !== null
      ? element("iframe", {
        class: "mail",
        title: "The email as it arrives",
        sandbox: "allow-popups allow-popups-to-escape-sandbox",
        referrerpolicy: "no-referrer",
        src: root + "/messages/" + encodeURIComponent(message.id) + "/html",
      })
      : element("pre", { class: "text", dir: "auto", text: mail.text });
    const headers = element("table", { class: "headers" },
      element("thead", null, element("tr", null, element("th", { scope: "col", text: "Header" }), element("th", { scope: "col", text: "Value" }))),
      element("tbody", null, ...mail.headers.map((header) => element("tr", null, element("td", { text: header.name }), element("td", { dir: "auto", text: header.value })))));
    return element("section", { class: "email", "aria-label": "Email" },
      facts([["From", address(mail.from)], ["Reply to", address(mail.replyTo)], ["Subject", mail.subject], ["Unsubscribe", mail.unsubscribe]]),
      element("p", null, element("a", {
        class: "button", href: root + "/messages/" + encodeURIComponent(message.id) + "/eml", download: message.id + ".eml", text: "Download the .eml",
      })),
      tabs("The email", [["Message", view], ["Plain text", element("pre", { class: "text", dir: "auto", text: mail.text })], ["Headers", headers]]));
  }

  function sms(message) {
    const text = message.sms;
    const parts = text.segments === 1 ? "1 part" : text.segments + " parts";
    return element("section", { class: "phone", "aria-label": "SMS" },
      element("p", { class: "sender", dir: "auto", text: text.sender }),
      element("p", { class: "bubble", dir: "auto", text: text.text }),
      element("p", { class: "meta", text: (encodings[text.encoding] || text.encoding) + " · " + parts + " · to " + message.to }));
  }

  function picture(source) {
    return typeof source === "string" && /^(https?:|data:image\/|\/(?!\/))/i.test(source)
      ? element("img", { class: "icon", src: source, alt: "", referrerpolicy: "no-referrer" })
      : null;
  }

  function push(message) {
    const sent = message.push;
    const shown = sent.notification;
    const direction = ["ltr", "rtl", "auto"].includes(shown.dir) ? shown.dir : "auto";
    return element("section", { class: "push", "aria-label": "Push notification" },
      element("div", { class: "notification", lang: shown.lang || null, dir: direction },
        picture(shown.icon),
        element("div", null,
          element("p", { class: "title", text: shown.title }),
          element("p", { class: "body", text: shown.body }),
          shown.url ? element("p", { class: "opens", text: "Opens " + shown.url }) : null)),
      facts([["Service", sent.service], ["Urgency", sent.urgency], ["Kept for", sent.ttl + " seconds"], ["Topic", sent.topic], ["Tag", shown.tag], ["Badge", shown.badge]]));
  }

  function links(message) {
    if (message.links.length === 0) {
      return null;
    }

    return element("section", { class: "links", "aria-label": "Links" },
      element("h3", { text: "Links" }),
      element("ul", null, ...message.links.map((link) => element("li", null,
        element("code", { class: "link", text: link }),
        element("a", { class: "button", href: link, target: "_blank", rel: "noopener noreferrer", text: "Open", "aria-label": "Open " + link }),
        element("button", { type: "button", class: "copy", "data-link": link, text: "Copy", "aria-label": "Copy " + link })))));
  }

  async function show(id) {
    selected = id;
    showList();
    const answer = await fetch(root + "/messages/" + encodeURIComponent(id), { cache: "no-store" });
    if (selected !== id) {
      return;
    }

    if (!answer.ok) {
      detail.replaceChildren(element("p", { class: "empty", text: "The inbox no longer holds this message." }));
      return;
    }

    const message = await answer.json();
    const body = message.channel === "email" ? email(message) : message.channel === "sms" ? sms(message) : push(message);
    detail.replaceChildren(
      element("h2", { dir: "auto", text: message.summary }),
      facts([["Application", message.app], ["Captured", when(message.at)], ["Delivery", delivery(message)], ["Person", message.person], ["Recipient", recipient(message)]]),
      body,
      links(message));
    detail.focus({ preventScroll: true });
  }

  list.addEventListener("click", (event) => {
    const entry = event.target.closest("button.entry");
    if (entry) {
      show(entry.dataset.id).catch(() => say("The message could not be read."));
    }
  });

  detail.addEventListener("click", (event) => {
    const copy = event.target.closest("button.copy");
    if (copy) {
      navigator.clipboard.writeText(copy.dataset.link)
        .then(() => say("The link is copied."), () => say("The link could not be copied."));
    }
  });

  document.getElementById("filters").addEventListener("submit", (event) => event.preventDefault());
  for (const filter of Object.values(filters)) {
    filter.addEventListener("input", showList);
  }

  document.getElementById("clear").addEventListener("click", async () => {
    if (!window.confirm("Delete every captured message?")) {
      return;
    }

    const answer = await fetch(root + "/messages", { method: "DELETE", headers: { "X-Hermes-Inbox": "clear" } });
    if (answer.ok) {
      messages = [];
      newest = null;
      selected = null;
      showApps();
      showList();
      detail.replaceChildren(element("p", { class: "empty", text: "Choose a message to read it." }));
      say("The inbox is empty.");
    } else {
      say("The inbox could not be cleared.");
    }
  });

  poll();
})();
