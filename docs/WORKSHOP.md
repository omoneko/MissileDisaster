# Steam Workshop description (paste into the uploader)

Below is a ready-to-paste description using Steam BBCode. A 16:9 preview image is required by
the in-game Content Manager — create one separately (e.g. a screenshot of a nuclear strike).

---

[h1]Missile Disaster[/h1]
Launch missiles at any spot — and defend against them. Five warhead types, adjustable yield,
air/ground burst, realistic blast falloff, fire, and persistent radioactive fallout.

[b]Base game only — no DLC required.[/b] With the Natural Disasters DLC, the vanilla meteor
effect is used for explosions; otherwise a built-in fireball is used.

[h2]Features[/h2]
[list]
[*][b]5 warheads[/b]: Conventional, Cluster, White Phosphorus, Thermobaric, Nuclear
[*][b]Adjustable yield[/b]: nuclear presets (Little Boy … Tsar Bomba) or custom kt; conventional in kg TNT (radius ∝ yield^1/3)
[*][b]Air / ground burst[/b]: ground makes a crater and destroys roads/pipes/metro; air spares them but blasts wider
[*][b]Distance falloff[/b]: total destruction at ground zero, tapering outward
[*][b]Radioactive fallout[/b] from nuclear ground bursts — persists, expires after 50 in-game years
[*][b]Missile defense[/b]: PAC-3 / THAAD / Aegis interceptors with realistic kill probabilities, plus a Radar boost
[*][b]Explosions & sound[/b]: scaled meteor blast, nuclear mushroom cloud, 3D launch/impact/intercept SFX
[/list]

[h2]Required companion assets[/h2]
This mod detects buildings [b]by name[/b]. Subscribe/create building assets whose names contain:
[list]
[*][b]PAC3[/b] — terminal-tier interceptor
[*][b]THAAD[/b] — mid-tier interceptor
[*][b]Aegis[/b] — high-tier interceptor
[*][b]Radar[/b] — boosts intercept chance
[*][b]Decontamination[/b] (e.g. "Decontamination facility") — removes fallout (~5%/in-game month)
[/list]

[h2]How to use[/h2]
[olist]
[*]Open the Missile Launch Control panel (top-left) or press [b]F9[/b]
[*]Pick a warhead, set the yield, choose air or ground burst
[*]Click [b]Start Targeting[/b], then click the map
[/olist]

Also cleans reactor fallout from the companion [b]NuclearMeltdown[/b] mod. Water treatment plants
do not decontaminate.

[h2]Source code[/h2]
This mod is open source under the MIT licence. Bug reports and pull requests are welcome:
[url=https://github.com/omoneko/MissileDisaster]github.com/omoneko/MissileDisaster[/url]
