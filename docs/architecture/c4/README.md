# C4 Model — GDB Platform

The [C4 model](https://c4model.com/) describes software architecture at four
zoom levels. This suite covers the top three (the fourth, code, is the source
itself + the [component reference](../request-flow.md)).

| Level | Diagram | Question it answers |
|---|---|---|
| 1 | [System Context](level-1-system-context.md) | Who uses GDB and what external systems does it touch? |
| 2 | [Container](level-2-container.md) | What are the deployable services and how do they talk? |
| 3 | [Component](level-3-component.md) | Inside a service, what are the parts? (uses `accounts_service`) |

Diagrams are written in **Mermaid** so they render directly on GitHub/most IDEs.


