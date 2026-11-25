# Review Guide

Use this checklist when reviewing a change to Depot. It follows
[VentureOS's](https://github.com/tonytins/VentureOS/blob/main/REVIEW.md), with checks for a designer that draws
through Hudini. The rules it checks against are in [AGENTS.md](AGENTS.md).

## Before reviewing

- Read the change description and the relevant code, tests, and docs.
- Identify what the change is meant to do for someone designing an interface.
- Run `dotnet build Depot.slnx`, both .NET test suites, and, when `depot-render` changes, `swift test` in it. Run
  `tests/Depot.Tests` with a display, such as Xvfb, so the test that draws through `depot-render` isn't skipped.
- When the change affects what Depot shows or how it responds, run it and look. Report checks that couldn't be run.

## Review checklist

- Does the behavior match the stated intent, including empty documents, documents Hudini refuses, `.uis` files, and
  sizes at or past a limit?
- **Work:** Can the change lose someone's work? Does saving still write the text exactly, and leave the file as it
  was when it fails? Is every edit a change to the text that Undo takes back? Are unsaved changes still asked about?
- **Faithfulness:** Is the canvas still drawn by Hudini, and does the selection still come from the frame map?
- **Subset:** Can a designer edit make a document Hudini refuses? Does `HudiniSubset` still match the spec's Hudini
  section?
- **Failures:** Is every new failure reported, in the status bar or on standard error, and does Depot carry on? Can
  `depot-render` hang Depot, or a late answer be shown for the wrong document?
- **Protocol:** Do older requests and frame maps still work? Is every new field documented in
  [docs/depot-render.md](docs/depot-render.md)? Are requests validated before anything is read or drawn?
- **Threads:** Does code that touches Avalonia run on its thread, tests included?
- **Dependencies:** Are new packages or version bumps called out, approved, and kept in a change of their own?
- **Tests:** Do focused tests cover the change, and would a new test fail without it?
- **Docs:** Are the README, design note, `depot-render` reference, look notes, and AGENTS.md updated where affected,
  and do they describe only what works?

## Writing review comments

- Report concrete correctness, data-loss, compatibility, or missing-test concerns; don't block on style.
- Make each comment specific and actionable: the smallest relevant location, the impact, and a useful next step.
- Prioritize findings by impact:
  - **Blocking**: loses or damages someone's document, crashes Depot in normal use, or stops it building.
  - **High**: breaks designing, saving, or drawing, lets the designer make a document Hudini refuses, or breaks
    the `depot-render` protocol for existing callers.
  - **Medium**: a limited but meaningful behavior or reliability issue, or docs describing something that isn't
    true.
  - **Low**: a small edge case or non-blocking improvement.
- If no actionable issues remain, say so and note any important checks that weren't run.
