# Steam Workshop description (as published)

The maintained copy of the Workshop item's description. Uploaded via the `cs-workshop-upload`
skill (SteamCMD), not the in-game publisher.

Two rules, both learned the hard way:

- **Steam caps it at 8000 bytes.** Over that, `workshop_build_item` fails with `Invalid Parameter`
  and does not say which field was at fault. Nothing uploads, so the item is left untouched —
  trim and retry.
- **No release-notes block here.** It used to carry a "New in this update" section that gained a
  block every release, which is how it hit the cap. The changenote is the release history; this
  is a description.

---
[h1]Missile Disaster[/h1]Launch missiles at any spot — and defend against them. [b]Base game only, no DLC required.[/b][h2]Features[/h2][list][*][b]5 warheads[/b]: Conventional, Cluster, White Phosphorus, Thermobaric, Nuclear[*][b]Adjustable yield[/b]: nuclear presets (Little Boy … Tsar Bomba) or custom kt; conventional in kg TNT[*][b]Air / ground burst[/b]: ground craters and cuts roads, pipes and metro; air spares them and reaches wider[*][b]Mushroom cloud[/b] as wide as the destruction radius, always rising from the ground[*][b]Shock wave[/b] racing out across the ground, with a rolling wall of dust behind it[*][b]Base surge[/b]: a wide, low collar of dirt rolling out from the foot of the column[*][b]Blast debris[/b]: solid lit rubble swept outward as one expanding ring[*][b]Traffic and people are thrown[/b] from ground zero — cars, buses, parked cars, pedestrians[*][b]Everything drifts downwind[/b] on the game's own wind[*][b]True incendiary[/b]: White Phosphorus makes no crater and almost no blast — a heavier charge only spreads the fires[*][b]Trees catch fire[/b] inside the thermal ring. [i]Needs the Natural Disasters DLC; without it nothing happens there and the rest of the mod is unaffected.[/i][*][b]Radioactive fallout[/b] from nuclear ground bursts — persists 50 in-game years, or clean it with a Decontamination facility[*][b]Missile defense[/b]: PAC-3 / THAAD / Aegis interceptors with realistic kill probabilities, plus a Radar boost[/list][h2]Random strikes (disaster mode) — OFF by default[/h2][b]Missiles never fall on their own unless you switch this on[/b] in [b]Options → Mods → Missile Disaster[/b]. Once on, they hit your city automatically and destroy buildings, exactly like a natural disaster — so if your city is being flattened and you did not launch anything, that box is ticked. The first strike in each city also chirps to say so.[list][*][b]Frequency[/b] scales with the game's natural-disaster frequency (adjustable ×0.25–3.0)[*][b]Attack pattern[/b]: Single, [b]MIRV[/b] (3–6 warheads at once), or Random[*][b]Priority targets[/b] configurable by keyword — by default nuclear plants and interceptor sites, then airports, stations and harbours, then landmarks[/list][h2]Required companion assets[/h2]The mod finds buildings [b]by name[/b]. Subscribe or create assets whose names contain:[list][*][b]PAC3[/b] / [b]THAAD[/b] / [b]Aegis[/b] — interceptors, low / mid / high altitude[*][b]Radar[/b] — boosts intercept chance[*][b]Decontamination[/b] — removes fallout (~5% per in-game month)[/list]A matching asset set:
https://steamcommunity.com/sharedfiles/filedetails/?id=3765194357
[h2]How to use[/h2][olist][*]Open the [b]Disasters[/b] info-view panel and click the [b]missile icon[/b] next to the vanilla disaster buttons[*]Pick a warhead, set the yield, choose air or ground burst[*]Click [b]Start Targeting[/b], then click the map[/olist]Or press the launch key ([b]F9[/b] by default, rebindable) to re-arm targeting instantly.
[h2]Language[/h2]English and Japanese, picked by the game's own language setting. Other languages are welcome: copy Locales/en.txt to your language code, translate the values, and open a pull request or post the file in the comments. Missing lines fall back to English.
[h1]Source code[/h1]MIT licence. Bug reports and pull requests welcome:
[url=https://github.com/omoneko/MissileDisaster]github.com/omoneko/MissileDisaster[/url]
[h1]Support[/h1]https://ko-fi.com/omoneko

