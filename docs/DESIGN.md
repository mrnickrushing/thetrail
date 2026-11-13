# Argonauts '49: design

An original Gold Rush survival game for iPhone and iPad (landscape). It replaces "The OG Trail", which
followed The Oregon Trail (1985) too closely to publish; that version is kept unchanged on the branch
`og-original` and is never submitted. Nothing here may copy another game's names, terms, screens,
scoring or minigames. Real history (places, dates, peoples, period songs) is fine.

## Premise

Spring 1849. Gold has been found in California. You raise a company of argonauts in St. Joseph,
Missouri, take a wagon about 2,000 miles over the California Trail, and work a claim in the diggings
until the winter rains. Your result is the company's take: gold dust, cash left, and everyone you
brought through.

## What makes it its own game

| System | How it works |
|---|---|
| **The company** | Pick 4 partners from a hiring board of 8 strangers. Each has a trade (blacksmith, physician, hunter, guide, cook, teamster, miner, greenhorn) with a real effect, a temper (steady, cheerful, grumbler) and money they put in the pot. Experts bring skills, greenhorns bring cash. |
| **Outfitting** | Stepper store with weights: the wagon has a load limit set by the team. Oxen (slow, graze anything) or mules (fast, need grass). Water kegs, repair kits, medicine, powder, and mining tools (pan, pick, rocker). |
| **The road** | Orders in plain numbers: hours on the road (8 to 14) and pounds of food per person per day (1 to 3). Health and team strength are percentages; the wagon has a condition. A running journal records the days. |
| **Grass and water** | 1849 brought record traffic: grass thins as the season goes on, so late companies starve their teams. Dry stretches (Greenwood Cutoff, Forty Mile Desert, Black Rock Desert) use water by the head. |
| **Morale** | Hard days, deaths and short food lower it; rest, fresh meat and good news raise it. A company with low morale breaks up: partners leave and take their share of the stores. |
| **Choices** | 26 decision events (shortcut maps for sale, abandoned goods, cholera camps, quarrels, gamblers, stampedes, traders, letters home) with outcomes that depend on the company's trades. |
| **Crossings** | Scout three places to cross (depth, current, bottom). Then drive the team across: the current pushes the wagon off the shallow bar and you hold to whip the team back. Ferries cost money and days in line. |
| **Stalking game** | Creep up on grazing game when its head is down; freeze when it looks up; shoot when in range. Meat is fresh food (it holds off scurvy). |
| **Ropes** | Lower the wagon down Windlass Hill and haul it up the Sierra summit on a chain: keep the strain under the breaking point while snags and snow come on. |
| **Two ways in** | At Lassen Meadows: the Carson route (Forty Mile Desert, then the Sierra) to Hangtown, or Lassen's cutoff (no desert crossing, but long and grassless) to the northern diggings. At South Pass: the Greenwood Cutoff or Great Salt Lake City. |
| **The diggings** | Arrival date decides how good the claims still are. Choose a claim (river bar, dry gulch, hillside), then work week by week: pan (a swirl game whose quality sets the week's yield), rock with a rocker, rest, prospect, or buy at gold-camp prices. River bars flood when the rains come. |
| **Result** | Gold sold at $16 an ounce, plus cash, plus $500 for every partner brought through. The Assay Ledger keeps the best ten companies. |

## Look and sound

Full-colour pixel art on a 480x270 screen (a 54-colour palette in `Palette.cs`), scenes drawn in code,
proportional lettering cut from the public-domain font8x8 glyphs, no CRT effect. Music and effects
are a plucked-string synth (Karplus-Strong) playing public-domain tunes of the day: Stephen Foster's
*Oh! Susanna* (1848; the Forty-Niners sang it as "Oh, California").

## Route (real places, 1849)

St. Joseph, Missouri -> Big Blue River -> Fort Kearny -> Ash Hollow (Windlass Hill: lower the wagon)
-> Courthouse Rock -> Fort Laramie -> North Platte (Mormon ferry) -> Devil's Gate -> South Pass
-> { Greenwood Cutoff (dry) -> Green River ferry -> Bear River -> Fort Hall | Great Salt Lake City ->
Salt Lake Cutoff } -> City of Rocks -> Humboldt Wells -> Lassen Meadows ->
{ Humboldt Sink -> Forty Mile Desert -> Carson River -> Carson Pass (haul) -> Hangtown |
Black Rock Desert -> High Rock Canyon -> Goose Lake -> Lassen's Rancho }.
