# Critical Engagement With Feedback

**Project:** Back To The Forge — GADS 7331 POE Part 2  
**Playtesters:** Farrell, Mia  
**Related document:** `feedback-summary.md`

---

## What did we expect?

We expected attendees to comment on **placeholders lacking sprites**, since unfinished assets remain visible throughout the build. We also expected **controls and lack of direction to confuse players** — our prototype assumes JRPG familiarity we had not taught in-game. The **flee mechanic** carries a 50% success chance, so we expected playtesters to fail often and report frustration.

We assumed **art style and premise** would draw the most attention, as both felt distinct from other showings. We expected players **not to find the key quest item** and braced for **more bugs than were reported**, given risks around additive combat, LLM calls, and scene transitions. Broadly, we anticipated critique of production gaps, not praise of incomplete polish.

## What surprised us?

We **did not expect positive art reactions** — both players described the style as liked or cute, despite placeholders we thought would dominate impressions. We also **did not expect UI praise**; Farrell found the interface legible even without glasses.

**Random encounters felt more frequent than intended**, suggesting tuning or map layout amplified triggers beyond our internal model. We **did not expect players to ignore mercenaries** — not because the system failed, but because we never explained hiring in-game. Conversely, we were **surprised players engaged with mining**, a loop we considered secondary. The **in-game UI on the main menu** was an unexpected bug; other feared issues did not surface. Feedback clustered on clarity and pacing rather than widespread breakage.

## What did we choose not to implement?

We declined removing **random encounters**. Our goal emulates old-school JRPG overworld tension: frustrating frequency is intentional. Without overworld risk, players could gather resources without consequence, undermining the risk–reward of travel. We also kept **flee at 50%** — unreliable escape is deliberate tension, not a bug. Placeholder replacement was deferred where scope demanded clarity fixes first.

## Evaluation of feasibility

Actionable changes were **feasible in Unity with Ollama local inference**. Encounter rate and flee chance are **value tweaks**; clearer control and objective indicators fit our existing UI Toolkit setup without re-engineering core systems.

**Performance shaped earlier choices**: a previous LLM model consumed too many resources, so we switched to a more performant one — limiting how heavily we could rely on runtime generation. Removing encounters was technically easy but **design-infeasible**. Expanding LLM dialogue to solve every clarity gap was unrealistic within inference budgets; structured onboarding was the practical response.

## Our final judgement

Refinements we accepted: wayfinding cues, clearer objectives, combat target indication, encounter tuning, and in-game mercenary explanation. Following lecturer feedback that **LLM integration lacked gameplay relevance**, we added **companion dialogue** where LLM responses can grant a **new battle skill** or apply a **negative passive debuff** — tying generation to combat outcomes.

We declined removing encounters and guaranteeing flee, both conflicting with intentional JRPG pacing.

This experience showed that in **AI-driven development**, novelty draws attention but players judge navigation, affordance, and legibility first. AI accelerated iteration — we could prototype companion consequences and tune values quickly — yet critique confirmed that **LLM flavour cannot replace in-game teaching**. The strongest signal was not that our premise was different, but that players needed to understand it without us explaining aloud.
