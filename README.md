
> This repository and project is a work in progress
 
<!-- Title and Badge section -->
<p align="center">
  <img src="docs/Screenshots/Scoria_TempIcon.png" alt="Project Icon" width="100" />
</p>

<h1 align="center">Scoria — A Minimalist Markdown Editor</h1>

<p align="center">
  ✨ Clean, minimal but powerful UI. ⚡ Fast, performant 🧪 Powered by Markdig, Avalonia, .NET.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue" />
  <img src="https://img.shields.io/badge/license-TBD-lightgrey" />
  <img src="https://img.shields.io/badge/markdown-Markdig-success" />
</p>

---

## 📸 Preview

<!-- insert screenshots or a demo GIF here -->
![screenshot](docs/Screenshots/Scoria_0.1.png)

---

##  Introduction

<p>
Scoria is an obsidian-inspired markdown text editor with a much more specific use case. 

I use/used obsidian for years as my daily driver for note-taking(zettelkasten), journaling, daily notes (TODO), life 
organization, habit tracking and much more. What I fundamentally love about obsidian is its openness, and the 
data-oriented approach with its simple markdown files. 

If you're not familiar with the "file over app" philosophy, I recommend checking out this excellent breakdown by CEO of 
obsidian: https://stephango.com/file-over-app

However, after years of use, the software is also fundamentally flawed, utilizing a bloated electron tech stack. 

Scoria's goal is to take the best parts of my own personal usage of Obsidian and crank the performance 10x. Its purpose 
is that of a performant daily driver that you can use to structure aspects of your life, projects or learnings in a 
simple, but deep quick and efficient manner.

I'm not designing Scoria to be a competitor, it is free and will always be.
</p>

---

## ✨ Current Features

- 📝 Live Markdown preview and editor.
- 🧠 Supports advanced Markdown syntax via [Markdig](https://github.com/xoofx/markdig)
- 🖼️ Toggle between edit and preview modes with `Ctrl+E`
- 📂 Open a folder and explore `.md` files via a responsive TreeView
- 💾 Save markdown files after editing.
- ✅ Rendered Checkboxes that update md
- Rendered & Editable file properties (Currently supporting tags, aliases & date)
- Bi-Directional note linking. 
  - Using "[[]]" syntax you can link notes together and receive a preview of it on mouse over.
  - Left click to change to the clicked note.
---

## 🚧 Features for MVP

> A brief breakdown of completed and planned features to reach MVP.

| Feature                                                                                                    |
|------------------------------------------------------------------------------------------------------------|
| ~~Initial Avalonia UI scaffold, editor + preview mode toggle~~                                             |
| ~~TreeView loads `.md` files from folders and supports nested folders~~                                    |
| ~~Savable editable markdown text~~                                                                         |
| Robust pane system including tabs: Draggable, pivot-able, panels.                                          |
| ~~Feature rich markdown rendering: Checkboxes, yaml header file properties, bi-directional note linking.~~ |
| Settings panel: UI properties and application settings, save settings on startup, project properties.      |
| Command Palette: Execute searchable commands.                                                              |
| Search: Search throughout application or folder.                                                           |
| Templates: Ability to create notes based off note templates.                                               |
| Daily Notes: A note that is generated daily when opening the editor.                                       |

---

## 🔮 Planned Future Features

> Features coming after MVP

| Feature                                                                                                                  |
|--------------------------------------------------------------------------------------------------------------------------|
|                                                                                                                          |
| Data analytics: Tools to enable note data manipulation, editing, graph and statistical analysis.                         |
| Projects: A pipeline the user can create. Enables easy manipulation of common tasks surrounding a single, large project. |
| ZettelKasten support: Provide ease of settings up, editing, viewing and navigating a user created ZettelKasten.          | 
| (More features to come here!)                                                                                            |


## 🔧 Tech Stack

- [Avalonia UI](https://avaloniaui.net/) – Cross-platform WPF-style UI framework
- [Markdig](https://github.com/xoofx/markdig) – Fast and extensible Markdown parser
- [Markdown.Avalonia](https://github.com/whistyun/Markdown.Avalonia) – Renders Markdown to Avalonia visuals
- [.NET 9]

---

## 📂 Folder Structure

```bash
Scoria/                     # project root
├─ docs/                    # screenshots, design notes, diagrams
├─ Scoria/                  # ⬅ all source files live here
│  ├─ Controls/             # reusable Avalonia user-controls (TagBadge, …)
│  ├─ Models/               # simple POCOs (FileItem, NoteMetadata, …)
│  ├─ Rendering/            # MarkdownRenderer + helpers
│  ├─ Services/             # I/O, toast notifications, link index, YAML parser
│  ├─ ViewModels/           # MVVM glue (MainWindowViewModel)
│  ├─ Views/                # XAML views (MainWindow, App.axaml, …)
└─ README.md
