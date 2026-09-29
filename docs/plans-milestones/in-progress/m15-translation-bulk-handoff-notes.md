# M15 translation bulk — handoff notes

> Scratch tier — one `## U#` section per unit, **appended, never
> rewritten**. A unit's Exit check writes its section at the end. The
> authoritative design is
> `docs/design/m15-translation-bulk-design.md` (U00); the register is
> `docs/plans-milestones/plan-m15-translation-bulk.md`; the unit plans
> are `m15-u00.md` … `m15-u06.md` in this folder.
>
> **Test-running note (the house quirk):** build, then run each
> assembly in-process through xunit.v3's own runner —
> `dotnet build Kumunita.slnx -c Debug` / `dotnet exec
> tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
> / `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.
> Web.Tests.dll`. Core starts `postgres:18` via Testcontainers (~20 s).

---

_(no units recorded yet — U00 seeds the first section on its Exit)_
