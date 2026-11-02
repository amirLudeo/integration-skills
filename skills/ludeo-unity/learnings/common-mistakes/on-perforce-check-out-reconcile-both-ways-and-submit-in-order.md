---
category: common-mistakes
tier: generalizable
sourceGame: SurvivalSample
phase: "1,2,3,4,5,6,7,8"
question: "Is the game's source in Perforce (files read-only until checked out), with the integration split across several pending changelists on a depot the studio shares? Check files out instead of clearing the read-only flag, reconcile edits as well as adds, and list what a changelist depends on before submitting it in dependency order."
sanitized: true
---

# On Perforce: check out, reconcile both ways, and submit in dependency order

Perforce only knows what it was told, and a submit sends only the changelist selected. Clearing a
read-only bit or submitting the newest phase first leaves the shared line broken.

## An edit Perforce was never told about is invisible to it

Clearing the read-only flag lets you write the file, but it is now modified without being opened.
`p4 sync` reporting "up to date" says nothing about it, and a later sync can silently overwrite it. On
one integration: 31 new source files were never added; an accessor block in a game file was lost to a
sync and came back as 11 compile errors; the recovery reconcile was **add-only**, so edited assembly
references and hooks were missed again; five days later a scan still found 18 diverged files.

1. **Check files out before editing** (`p4 edit`), or ask the integrator to. If the server is
   unreachable and you must clear the bit, list every file touched and reconcile when it is back.
2. **Reconcile both ways** over every touched folder - `p4 reconcile -n -a` for adds **and**
   `p4 reconcile -n -e` for edits - into named changelists.
3. **Close every Editor before revert, shelve or workspace-switch work.** Its Perforce integration
   opens files itself, adds new files to the default changelist, and reopens files as fast as you
   revert them.
4. **Shelve work in progress** so a copy lives on the server. Shelving is not submitting.

## List what a change depends on before you submit it

The phase 5 changelist was submitted alone to the studio's shared development line. It depended on the
still-pending SDK package and phases 0-3, unadded files and unopened project-file edits; the line did
not compile for the whole studio until four more submits, and 605 files were later backed out, about
five hours of recovery.

1. List what the change depends on that is not in it: the SDK package, earlier phases, new files and
   edited project files such as asmdefs (the two reconciles above).
2. Check older pending work (`p4 changes -s pending`, `p4 opened`) and submit in dependency order: SDK
   first, then phases in order. Check who else has the files open.
3. Show the integrator the changelist number, full file list, target branch and dependencies before
   anything is submitted.
4. Prefer a private branch for integration work from day one; merge down before copying up.
5. After a submit, prove the depot is whole: a reconcile that finds nothing, ideally a compile against
   a clean sync.
