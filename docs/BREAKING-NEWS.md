# How the program decides that news is breaking (0.20.0)

Everything runs on this PC (`Core/BreakingEngine.cs`, `Core/BreakingFeedback.cs`, `Core/BreakingWhy.cs`). No text leaves the computer and no cloud service is asked.

## Steps
1. **Stories.** Headlines are grouped into stories by word overlap (words cut to five letters, numbers, character pairs for Japanese / Chinese / Korean). The same story from several sites is one story; the publisher is the root host (`news.example.com` = `example.com`). The provider keeps the publishers that its near-duplicate removal folded away (`NewsItem.StoryReports`), so they still count.
2. **Score 0-100** = confirmation (2 / 3 / 4+ independent publishers within 30 minutes: 22 / 34 / 42) + speed (publishers in the last 10 minutes: 6 / 10 / 14) + freshness (<= 3 / 10 / 30 / 90 min: 16 / 12 / 7 / 3) + source (agency or official 10, major 6, other 3, minus what the user rejected) + disaster data (same kind and place, or the magnitude written in the headline; strong 45, medium 38, M5 24) + urgent word 10 / interest word 8 / both 14.
3. **Negative filters**: opinion, analysis, interview, lists, past events / anniversaries, schedules and announcements, clickbait (-22 to -30 and a ceiling under the Sensitive threshold), sports and showbiz hyperbole (-20 / -35 unless a disaster matches), a question (-15). Undated or older-than-limit stories are never breaking.
4. **Threshold** by sensitivity: Strict 72, Normal 58, Sensitive 42. Only the best source of a story is announced.
5. **Why**: the reasons are kept as codes and shown as a sentence in the user's language (`why.*` keys).

## What the user can teach it (local file `breaking-feedback.json`)
"This is not breaking news" (the story, and -4 per rejection up to -20 for its source), "Mute this source", "Mute this topic" (the three longest words of the headline). "Forget my choices" clears the file.

## Tests
`tests/OpsisNewsCrawler.Core.Tests/BreakingCorpus.cs` holds 65 labelled situations in 14 languages; `BreakingEngineTests.cs` asserts precision >= 92 % and recall >= 85 % (Normal), that Strict raises no false alarm, that a keyword without confirmation is never breaking, a time-based burst, the feedback and the cost of 1000 headlines. The numbers are written to `breaking-metrics.md` next to the test assembly and printed in the CI job summary.

## Limits
Word overlap is not understanding: two different events with the same words in the same minutes can merge; a real event reported by one small site is not announced (unless disaster data confirms it). The weights were tuned on the corpus, so the corpus must grow with real false alarms and misses.
