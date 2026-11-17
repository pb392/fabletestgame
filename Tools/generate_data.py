#!/usr/bin/env python3
"""Generates Assets/Resources/Data/species.json and moves.json.

The roster below is the single source of truth for creatures. Edit a line,
run `python3 Tools/generate_data.py` from the repository root, and the JSON
the game loads at startup is rebuilt. Stats, learnsets and trait pools are
derived deterministically from each line, so re-running never reshuffles
existing creatures.

Line format:
    Base:T > Evo1:TT > Evo2:TT >> Hidden:TT | archetype | origin
Type letters:
    E Ember  T Tide   G Grove  A Gale    S Stone     F Frost
    V Storm  P Spirit D Shade  R Radiance B Feral    M Arcane
Origins: Y yokai, F fairy tale, M mythology, D divine
Underscores in names become spaces.
"""
import json
import os
import random
import zlib

TYPES = "ETGASFVPDRBM"
TYPE_NAMES = ["Ember", "Tide", "Grove", "Gale", "Stone", "Frost",
              "Storm", "Spirit", "Shade", "Radiance", "Feral", "Arcane"]

THREE_STAGE = """
Kitling:E > Kitsune:EP > Kyubi:EM >> Tenko:ER | caster | Y
Kappling:T > Kappa:TG > Suiko:TB | bruiser | Y
Tanukit:B > Tanuki:BM > Bakedanuki:BD | balanced | Y
Kotengu:A > Karasu_Tengu:AD > Daitengu:AM >> Sojobo:AR | speed | Y
Onikko:B > Oni:BS > Shuten:BE | bruiser | Y
Tatsuling:T > Mizuchi:TV > Ryuo:TA | caster | Y
Wyrmlet:E > Drake:ES > Fafnir:ED >> Nidhogg:DG | bruiser | M
Frostling:F > Yuki-Warashi:FP > Yuki-Onna:FP | caster | Y
Wolfkin:B > Direwolf:BF > Wargrim:BD >> Fenrir:BF | speed | M
Cinderchick:E > Firebird:EA > Phoenix:ER >> Simurgh:RA | caster | M
Foalicorn:R > Unicorn:RB > Alicorn:RA | support | F
Pebblin:S > Golem:SM > Colossus:SV | tank | M
Sproutling:G > Treant:GS > Eldertree:GR | tank | F
Kodamako:G > Kodama:GP > Kukunochi:GP | support | Y
Wisp:P > Hitodama:PE > Onibi:PD | glass | Y
Wailette:P > Banshee:PD > Bean-Nighe:PT | caster | M
Imp:D > Fiend:DE > Archfiend:DM | glass | M
Pixie:M > Sprite:MA > Titania:MR | support | F
Gnomeling:S > Gnome:SG > Gnome_King:SM | balanced | F
Gob:B > Hobgoblin:BD > Goblin_King:BS | balanced | F
Trollkin:S > Troll:SB > Jotunn:SF | tank | M
Merrow:T > Siren:TM > Melusine:TA | caster | M
Sealkit:T > Selkie:TF > Roanmor:TF | balanced | M
Sparkwing:V > Stormhawk:VA > Thunderbird:VA | speed | M
Raikit:V > Raiju:VB > Nue:VD | speed | Y
Bakuling:M > Baku:MP > Yumekui:MD | support | Y
Bakekit:D > Bakeneko:DE > Nekomata:DP | speed | Y
Inuko:B > Inugami:BP > Okuri-Inu:BD | bruiser | Y
Shishiko:S > Komainu:SR > Foo_Lion:SE | tank | Y
Kumoling:D > Jorogumo:DG > Tsuchigumo:DS | bruiser | Y
Hebiko:G > Uwabami:GT > Orochi:GD >> Yamata:TD | bruiser | Y
Gryphlet:A > Hippogriff:AB > Gryphon:AR | bruiser | M
Salamandrite:E > Salamander:ES > Ifrit:EA | caster | M
Dewdrop:T > Undine:TM > Nereid:TR | support | M
Zephyrling:A > Sylph:AM > Djinn:AV | caster | M
Smithling:S > Dvergr:SE > Brokkr:SE | bruiser | M
Honekko:D > Gaikotsu:DP > Gashadokuro:DS | tank | Y
Batling:D > Vampyr:DA > Strigoi:DM | speed | M
Familiar:M > Grimalkin:MD > Cait_Sith:MB | speed | F
Tadprince:T > Frog_Prince:TG > Toad_King:TS | balanced | F
Twigpuppet:G > Marionette:GM > Puppetmaster:GD | glass | F
Beanling:G > Stalkclimber:GA > Cloud_Giant:AS | bruiser | F
Strawpig:B > Stickboar:BG > Brickboar:BS | tank | F
Cygnet:T > Swan_Maiden:TR > Valkyrie:AR | balanced | F
Mirrorling:M > Magic_Mirror:MR > Ungaikyo:MD | caster | F
Kasako:A > Karakasa:AT > Amefurashi:TV | balanced | Y
Chochin:E > Chochin-Obake:EP > Burabura:ED | glass | Y
Snowcub:F > Yeti:FB > Wendigo:FD | bruiser | M
Rimefinch:F > Frostcrane:FA > Hyorin:FR | speed | Y
Jackalope:B > Wolpertinger:BA > Almiraj:BR | speed | M
Pupberus:D > Orthrus:DE > Cerberus:DE | bruiser | M
Hydrakin:T > Hydra:TD > Lernaean:TG | tank | M
Calfling:B > Minotaur:BS > Asterion:BM | bruiser | M
Wingfoal:A > Pegasus:AR > Sleipnir:AV | speed | M
Riddlekit:M > Sphinx:MS > Androsphinx:MR | caster | M
Kirinko:V > Kirin:VR > Qilin:RE | support | M
Kameling:T > Minogame:TG > Horai_Turtle:TS | tank | Y
Tsuruko:A > Tsuru:AR > Senkaku:AP | support | Y
Sporeling:G > Mycelf:GM > Myconid_King:GD | support | F
Strawling:G > Kuebiko:GP > Harvest_Wraith:GD | caster | Y
Pumpkit:G > Jack-o-Lantern:GE > Stingy_Jack:ED | glass | F
Grotesque:S > Gargoyle:SA > Cathedral_Warden:SR | tank | M
Sparkeel:V > Ikuchi:VT > Abaia:VT | balanced | Y
Namazuko:S > Namazu:ST > Onamazu:SV | tank | Y
Mochibun:R > Moon_Rabbit:RM > Jade_Hare:RF | support | M
Lampling:M > Genie:MA > Marid:MT | caster | M
Voltkit:V > Thunder_Tanuki:VB > Raitaiko:VS | bruiser | Y
"""

TWO_STAGE = """
Zashiki:P > Zashiki-Warashi:PR | support | Y
Blankface:D > Noppera-Bo:DM | glass | Y
Longneck:P > Rokurokubi:PD | caster | Y
Itachi:A > Kamaitachi:AB | speed | Y
Lickling:T > Akaname:TD | balanced | Y
Zori:B > Bakezori:BP | speed | Y
Biwako:M > Biwa-Bokuboku:MP | support | Y
Clothling:A > Ittan-Momen:AP | speed | Y
Kabeko:S > Nurikabe:SP | tank | Y
Wheelspark:E > Wanyudo:ED | bruiser | Y
Emberpaw:E > Kasha:EB | speed | Y
Echokit:A > Yamabiko:AS | balanced | Y
Amabiko:T > Amabie:TR | support | Y
Seashade:T > Umibozu:TD | tank | Y
Finling:T > Isonade:TB | bruiser | Y
Basanchick:E > Basan:EA | balanced | Y
Tsuchinoko:SB > Nozuchi:SG | tank | Y
Cloverkin:G > Leprechaun:GM | speed | F
Brownie:G > Boggart:GD | balanced | F
Kelpling:T > Kelpie:TD | bruiser | M
Pookit:D > Pooka:DB | speed | M
Dullakin:D > Dullahan:DV | bruiser | M
Capling:D > Redcap:DB | bruiser | F
Nixling:T > Nixie:TM | caster | M
Acornymph:G > Dryad:GR | support | M
Faunlet:G > Satyr:GB | balanced | M
Harpling:A > Harpy:AD | speed | M
Oneye:S > Cyclops:SV | bruiser | M
Centaurling:B > Centaur:BA | balanced | M
Chimerling:BE > Chimera:ED | bruiser | M
Basilet:S > Basilisk:SD | caster | M
Cocklet:A > Cockatrice:AS | balanced | M
Mantikit:B > Manticore:BD | bruiser | M
Gorgonet:S > Gorgon:SM | caster | M
Lamiette:M > Lamia:MB | balanced | M
Krakling:T > Kraken:TV | tank | M
Rocling:A > Roc:AS | bruiser | M
Draugling:F > Draugr:FD | tank | M
Huldrekit:G > Huldra:GM | caster | M
Nisse:F > Tomte:FG | support | F
Squirrelkin:B > Ratatoskr:BV | speed | M
Gosling:R > Golden_Goose:RA | support | F
Bootkit:B > Puss-in-Boots:BM | speed | F
Brayling:B > Bremen_Band:BV | balanced | F
Spinnet:M > Goldspinner:MR | caster | F
Shardling:F > Glacielle:FM | glass | F
Foamling:T > Seafoam_Spirit:TP | support | F
Tinling:S > Steadfast:SE | tank | F
Nutkin:G > Nutcracker:GS | bruiser | F
Mousekin:B > Mouse_King:BD | balanced | F
Kidgruff:B > Great_Gruff:BS | bruiser | F
Candykin:M > Sugar_Hag:ME | caster | F
Matchlit:E > Emberdream:EP | glass | F
Hutling:B > Chicken-Leg_Hut:BM | tank | F
Hearthkin:E > Domovoi:EP | support | M
Leshling:G > Leshy:GB | tank | M
Vodling:T > Vodyanoy:TG | balanced | M
Rusalkit:T > Rusalka:TP | caster | M
Anansling:M > Anansi:MG | speed | M
Coyotl:B > Trickster_Coyote:BM | speed | M
Hopling:D > Jiangshi:DP | bruiser | M
Nianling:B > Nian:BE | bruiser | M
Pixiukit:R > Pixiu:RS | tank | M
Zekit:M > Bai_Ze:MR | support | M
Nagaling:T > Naga:TR | caster | M
Icicling:F > Tsurara-Onna:FT | caster | Y
"""

SINGLE = """
Raitaro:V | bruiser | Y
Sparkfae:VM | glass | F
Leigong_Hawk:VA | speed | M
Elmo_Fire:VP | caster | M
Stormcrow:VD | speed | M
Tesso:BS | bruiser | Y
Azukiarai:TS | balanced | Y
Betobeto:D | speed | Y
Mokumokuren:PG | caster | Y
Hitotsume-Kozo:P | balanced | Y
Tofu-Kozo:GT | support | Y
Abura-Akago:EP | glass | Y
Enenra:EA | caster | Y
Sazae-Oni:TS | tank | Y
Ushi-Oni:BT | bruiser | Y
Hihi:B | bruiser | Y
Satori:MB | caster | Y
Yukinko:F | support | Y
Jubokko:GD | tank | Y
Ningyo:TM | support | Y
Shachihoko:TE | bruiser | Y
Itsumade:AD | caster | Y
Aosagibi:AE | glass | Y
Bunbuku:SE | tank | Y
Grindylow:TD | bruiser | M
Bogle:DG | balanced | M
Barghest:DV | speed | M
Cu_Sith:GB | bruiser | M
Nuckelavee:DT | bruiser | M
Wyvern:AD | bruiser | M
Lindworm:SG | tank | M
Jack_Frost:FA | speed | F
Krampus:FD | bruiser | M
Yule_Cat:FB | speed | M
Nightmare:DM | caster | M
Changeling:MD | balanced | F
Homunculus:MS | balanced | M
Mandrake:GM | caster | M
Alraune:GR | support | M
Peryton:AG | speed | M
Catoblepas:BS | tank | M
Bennu:RE | caster | M
Sunscarab:RS | tank | M
Girtablilu:SD | bruiser | M
Lamassu:RB | tank | M
Anzu:VS | bruiser | M
Coatl:AR | support | M
Ahuizotl:TB | speed | M
Chupacabra:BD | speed | M
Wampus:BM | speed | M
Piasa:AS | bruiser | M
Bunyip:TG | tank | M
Taniwha:TS | tank | M
Menehune:SG | support | M
Tikbalang:BA | speed | M
Aswang:DA | glass | M
Frost_Wight:FP | caster | M
Snow_Lion:FR | bruiser | M
"""

DIVINE = """
Amaterra:RE | caster | D
Tsukuyoru:DM | caster | D
Susaraga:VT | bruiser | D
Yggdra:GP | support | D
Jormund:TS | tank | D
Surtyr:ES | bruiser | D
Skadhira:FA | speed | D
Anubeth:PD | balanced | D
Byakkorin:BV | speed | D
Morgaine:MR | caster | D
"""

HIDDEN_ITEMS = {
    "Tenko": "celestial_pearl",
    "Sojobo": "kings_feather_fan",
    "Nidhogg": "gnawed_root_rune",
    "Fenrir": "gleipnir_thread",
    "Simurgh": "simurgh_plume",
    "Yamata": "eightfold_sake",
}

ARCH = {  # hp, atk, def, mag, res, spd
    "tank": (65, 45, 65, 35, 55, 35),
    "bruiser": (60, 70, 50, 30, 40, 50),
    "speed": (45, 60, 40, 45, 40, 70),
    "caster": (45, 30, 40, 75, 55, 55),
    "support": (55, 35, 50, 55, 65, 40),
    "balanced": (50, 50, 50, 50, 50, 50),
    "glass": (35, 55, 30, 75, 35, 70),
}
ARCH_AP = {"tank": 6, "bruiser": 8, "speed": 8, "caster": 6,
           "support": 6, "balanced": 7, "glass": 7}
ARCH_TRAITS = {
    "tank": ["hardy", "stalwart", "warded", "thornhide", "unshaken"],
    "bruiser": ["fierce", "vigor", "hardy", "intimidate", "thornhide"],
    "speed": ["swift", "keen", "firststrike", "vigor", "fierce"],
    "caster": ["gifted", "warded", "attuned", "scholar", "keen"],
    "support": ["regrowth", "warded", "purity", "hardy", "scholar"],
    "balanced": ["hardy", "attuned", "swift", "purity", "fortune"],
    "glass": ["keen", "gifted", "swift", "firststrike", "attuned"],
}
RARE_TRAITS = ["mythic", "lastlight", "wellspring", "regrowth"]

# Biomes: 0 Meadow 1 Forest 2 Wetland 3 Coast 4 Mountain 5 Snowfield
#         6 Volcanic 7 Ruins 8 Highlands 9 Sanctum
TYPE_BIOMES = {"E": [6], "T": [3, 2], "G": [1, 0], "A": [8], "S": [4],
               "F": [5], "V": [8, 4], "P": [7], "D": [7, 1], "R": [0, 8],
               "B": [0, 1], "M": [7, 1]}

# name lists per type: P1 P2 P3 M1 M2 M3(all foes) M4 S
MOVE_NAMES = {
    "E": ["Cinder Claw", "Blazing Rush", "Inferno Fang", "Ember Bolt", "Fox-Fire", "Wildfire", "Sunflare", "Stoke the Hearth"],
    "T": ["Fin Slap", "Riptide Lunge", "Maelstrom Crash", "Water Bolt", "Undertow", "Great Wave", "Abyssal Torrent", "Healing Rain"],
    "G": ["Vine Lash", "Thorn Tackle", "Ironwood Slam", "Leaf Dart", "Life Sap", "Bramble Burst", "Elder Bloom", "Verdant Mend"],
    "A": ["Wing Cut", "Sickle Wind", "Skyfall Dive", "Breeze Shot", "Slicing Gale", "Cyclone", "Heaven's Breath", "Fair Winds"],
    "S": ["Rock Toss", "Boulder Bash", "Mountain Breaker", "Pebble Shot", "Stone Spire", "Earthshake", "Meteor Fall", "Bulwark"],
    "F": ["Ice Nip", "Icicle Lance", "Glacier Crush", "Frost Breath", "Hoarfrost Grip", "Whiteout", "Fimbulwinter", "Rime Veil"],
    "V": ["Spark Jab", "Volt Fang", "Lightning Drum", "Jolt", "Thunderclap", "Chain Lightning", "Heaven's Wrath", "Static Field"],
    "P": ["Phantom Touch", "Soul Rend", "Reaper's Arc", "Spirit Flame", "Soul Siphon", "Wailing Chorus", "Otherworld Gate", "Soul Ward"],
    "D": ["Shadow Stab", "Night Fang", "Eclipse Slash", "Gloom Bolt", "Curse Mark", "Umbral Tide", "Void Maw", "Evil Eye"],
    "R": ["Glint Strike", "Halo Edge", "Dawnbreaker", "Light Ray", "Sun Lance", "Radiant Burst", "Judgment", "Purify"],
    "B": ["Bite", "Savage Claw", "Primal Rampage", "Wild Cry", "Spirit Fang", "Stampede", "Beast King's Roar", "Dread Howl"],
    "M": ["Rune Tap", "Spell Blade", "Arcane Cleave", "Magic Dart", "Mystic Seal", "Star Shower", "Grand Arcanum", "Enchant"],
}
P2_EFFECT = {"E": ("Burn", 30, 0), "T": ("DefDown", 30, 1), "G": ("Poison", 30, 0), "A": ("SpdDown", 30, 1),
             "S": ("Stun", 20, 0), "F": ("SpdDown", 30, 1), "V": ("Stun", 25, 0), "P": ("ResDown", 30, 1),
             "D": ("Poison", 30, 0), "R": ("AtkDown", 30, 1), "B": ("DefDown", 30, 1), "M": ("MagDown", 30, 1)}
M2_EFFECT = {"E": ("Burn", 30, 0), "T": ("SpdDown", 30, 1), "G": ("Drain", 100, 50), "A": ("DefDown", 30, 1),
             "S": ("DefDown", 30, 1), "F": ("Stun", 20, 0), "V": ("Stun", 30, 0), "P": ("Drain", 100, 50),
             "D": ("ResDown", 30, 1), "R": ("MagDown", 30, 1), "B": ("AtkDown", 30, 1), "M": ("ResDown", 30, 1)}
# S moves: (category target cost costIsMP effect chance power)
# targets: 0 one foe, 1 all foes, 2 self, 3 one ally, 4 all allies
S_MOVES = {
    "E": (4, 8, True, "AtkUp", 100, 1), "T": (4, 12, True, "Heal", 100, 25),
    "G": (3, 8, True, "Heal", 100, 45), "A": (4, 8, True, "SpdUp", 100, 1),
    "S": (4, 8, True, "DefUp", 100, 1), "F": (1, 8, True, "SpdDown", 100, 1),
    "V": (0, 10, True, "Stun", 70, 0), "P": (4, 8, True, "ResUp", 100, 1),
    "D": (0, 6, True, "ResDown", 100, 2), "R": (3, 10, True, "Cleanse", 100, 30),
    "B": (1, 3, False, "AtkDown", 100, 1), "M": (4, 8, True, "MagUp", 100, 1),
}

# Class-exclusive skills: id, name, type, category(0 phys,1 magic,2 status), power, cost, costIsMP, target, effect, chance, effectPower
CLASS_MOVES = [
    ("class_immolate", "Immolate", "E", 1, 80, 12, True, 0, "Burn", 100, 0),
    ("class_solar_crown", "Solar Crown", "E", 1, 85, 22, True, 1, "None", 0, 0),
    ("class_soothing_current", "Soothing Current", "T", 2, 0, 16, True, 4, "Heal", 100, 35),
    ("class_drowning_deep", "Drowning Deep", "T", 1, 85, 22, True, 1, "SpdDown", 50, 1),
    ("class_entangle", "Entangle", "G", 1, 40, 10, True, 0, "Stun", 60, 0),
    ("class_world_root", "World Root", "G", 2, 0, 24, True, 4, "Heal", 100, 50),
    ("class_twin_gale", "Twin Gale Slash", "A", 0, 85, 5, False, 0, "None", 0, 0),
    ("class_eye_of_storm", "Eye of the Storm", "A", 0, 80, 8, False, 1, "None", 0, 0),
    ("class_shield_wall", "Shield Wall", "S", 2, 0, 4, False, 4, "DefUp", 100, 2),
    ("class_titans_hammer", "Titan's Hammer", "S", 0, 120, 8, False, 0, "Stun", 30, 0),
    ("class_hoarfrost_edge", "Hoarfrost Edge", "F", 0, 80, 5, False, 0, "SpdDown", 60, 1),
    ("class_ghost_winter", "Ghost Winter", "F", 1, 85, 22, True, 1, "Stun", 20, 0),
    ("class_thousand_palms", "Thousand Palms", "V", 0, 85, 5, False, 0, "None", 0, 0),
    ("class_raijin_drumroll", "Raijin Drumroll", "V", 0, 80, 8, False, 1, "Stun", 25, 0),
    ("class_shikigami_bind", "Shikigami Bind", "P", 1, 60, 12, True, 0, "Stun", 50, 0),
    ("class_spirit_parade", "Hundred Spirit Parade", "P", 1, 90, 24, True, 1, "None", 0, 0),
    ("class_assassinate", "Assassinate", "D", 0, 90, 5, False, 0, "Crit", 100, 0),
    ("class_clone_flurry", "Shadow Clone Flurry", "D", 0, 80, 8, False, 1, "None", 0, 0),
    ("class_lay_on_hands", "Lay on Hands", "R", 2, 0, 12, True, 3, "Heal", 100, 60),
    ("class_divine_verdict", "Divine Verdict", "R", 1, 125, 22, True, 0, "None", 0, 0),
    ("class_bloodrush", "Bloodrush", "B", 2, 0, 3, False, 2, "AtkUp", 100, 2),
    ("class_ragnarok_fang", "Ragnarok Fang", "B", 0, 130, 9, False, 0, "None", 0, 0),
    ("class_polymorph_curse", "Polymorph Curse", "M", 2, 0, 10, True, 0, "AtkDown", 100, 2),
    ("class_tales_end", "Fairy Tale's End", "M", 1, 95, 26, True, 1, "None", 0, 0),
]

EFFECT_TEXT = {
    "None": "", "Burn": "may burn", "Poison": "may poison", "Stun": "may stun",
    "Drain": "drains HP", "Heal": "restores HP", "Cleanse": "heals and cures ailments",
    "Crit": "always strikes true", "AtkUp": "raises Attack", "DefUp": "raises Defense",
    "MagUp": "raises Magic", "ResUp": "raises Resistance", "SpdUp": "raises Speed",
    "AtkDown": "lowers Attack", "DefDown": "lowers Defense", "MagDown": "lowers Magic",
    "ResDown": "lowers Resistance", "SpdDown": "lowers Speed",
}


def tid(letter):
    return TYPES.index(letter)


def move_id(letter, slot):
    return TYPE_NAMES[tid(letter)].lower() + "_" + slot


def build_moves():
    moves = []

    def add(mid, name, t, cat, power, cost, mp, target, eff, chance, epow, acc=95):
        bits = []
        if cat != 2:
            bits.append(("Technique" if cat == 0 else "Spell") + ", power %d" % power)
        if target == 1:
            bits.append("hits all foes")
        elif target == 4:
            bits.append("affects all allies")
        if EFFECT_TEXT[eff]:
            bits.append(EFFECT_TEXT[eff])
        moves.append({"id": mid, "name": name, "type": t, "category": cat, "power": power,
                      "accuracy": acc, "cost": cost, "costIsMP": mp, "target": target,
                      "effect": eff, "effectChance": chance, "effectPower": epow,
                      "desc": "; ".join(bits).capitalize() + "."})

    add("strike", "Strike", -1, 0, 30, 0, False, 0, "None", 0, 0, 100)
    add("focus", "Focus", -1, 2, 0, 0, False, 2, "MagUp", 100, 1, 100)
    for L in TYPES:
        n = MOVE_NAMES[L]
        t = tid(L)
        add(move_id(L, "p1"), n[0], t, 0, 40, 2, False, 0, "None", 0, 0, 100)
        e = P2_EFFECT[L]
        add(move_id(L, "p2"), n[1], t, 0, 65, 4, False, 0, e[0], e[1], e[2])
        add(move_id(L, "p3"), n[2], t, 0, 95, 6, False, 0, "None", 0, 0, 90)
        add(move_id(L, "m1"), n[3], t, 1, 45, 4, True, 0, "None", 0, 0, 100)
        e = M2_EFFECT[L]
        add(move_id(L, "m2"), n[4], t, 1, 70, 8, True, 0, e[0], e[1], e[2])
        add(move_id(L, "m3"), n[5], t, 1, 60, 14, True, 1, "None", 0, 0)
        add(move_id(L, "m4"), n[6], t, 1, 110, 18, True, 0, "None", 0, 0, 90)
        s = S_MOVES[L]
        add(move_id(L, "s"), n[7], t, 2, 0, s[1], s[2], s[0], s[3], s[4], s[5], 100)
    for c in CLASS_MOVES:
        add(c[0], c[1], tid(c[2]), c[3], c[4], c[5], c[6], c[7], c[8], c[9], c[10])
    return moves


def learnset(types, physical, stage):
    a, b = types[0], (types[1] if len(types) > 1 else None)
    A = ["p1", "p2", "p3"] if physical else ["m1", "m2", "m4"]
    B = ["m1", "m2", "m4"] if physical else ["p1", "p2", "p3"]
    plan = [(1, "strike"), (1, move_id(a, A[0])), (6, move_id(a, B[0])), (10, move_id(a, "s"))]
    if b:
        plan += [(14, move_id(b, A[0])), (18, move_id(a, A[1])), (23, move_id(b, "s")),
                 (28, move_id(b, A[1])), (33, move_id(a, A[2])), (38, move_id(a, "m3")),
                 (44, move_id(b, A[2])), (50, move_id(b, "m3")), (56, move_id(a, B[2]))]
    else:
        plan += [(14, move_id(a, A[1])), (20, move_id(a, B[1])), (28, move_id(a, "m3")),
                 (36, move_id(a, A[2])), (46, move_id(a, B[2]))]
    return plan


def parse(block):
    out = []
    for line in block.strip().splitlines():
        line = line.strip()
        if not line:
            continue
        chain, arch, origin = [p.strip() for p in line.split("|")]
        hidden = None
        if ">>" in chain:
            chain, hidden = [p.strip() for p in chain.split(">>")]
        forms = [f.strip() for f in chain.split(">")]
        if hidden:
            forms.append(hidden)
        parsed = []
        for f in forms:
            name, t = f.split(":")
            parsed.append((name.replace("_", " "), t))
        out.append((parsed, arch, origin, hidden is not None))
    return out


def main():
    species = []
    families = (parse(THREE_STAGE) + parse(TWO_STAGE) + parse(SINGLE) + parse(DIVINE))
    names_seen = set()
    for fam_index, (forms, arch, origin, has_hidden) in enumerate(families):
        normal_len = len(forms) - (1 if has_hidden else 0)
        divine = origin == "D"
        ids = [len(species) + i for i in range(len(forms))]
        for i, (name, t) in enumerate(forms):
            assert name not in names_seen, "duplicate name " + name
            names_seen.add(name)
            assert all(c in TYPES for c in t) and 1 <= len(t) <= 2, name
            if i > 0:
                assert len(t) == 2, "evolutions need two types: " + name
            rng = random.Random(zlib.crc32(name.encode()))
            is_hidden = has_hidden and i == len(forms) - 1
            if divine:
                mult = 1.95
            elif is_hidden:
                mult = 1.85
            elif normal_len == 1:
                mult = 1.4
            elif normal_len == 2:
                mult = (1.05, 1.55)[i]
            else:
                mult = (1.0, 1.3, 1.62)[i]
            base = [max(10, int(round(v * mult * rng.uniform(0.9, 1.1)))) for v in ARCH[arch]]
            hp, atk, df, mag, res, spd = base
            physical = atk >= mag
            if is_hidden:
                evo_to, evo_level, evo_item = -1, 0, ""
            elif i + 1 < len(forms):
                nxt_hidden = has_hidden and i + 1 == len(forms) - 1
                evo_to = ids[i + 1]
                if nxt_hidden:
                    evo_level, evo_item = 50, HIDDEN_ITEMS[forms[i + 1][0]]
                elif normal_len == 2:
                    evo_level, evo_item = rng.randint(20, 28), ""
                else:
                    evo_level, evo_item = (rng.randint(14, 18), rng.randint(32, 38))[i], ""
            else:
                evo_to, evo_level, evo_item = -1, 0, ""
            ls = learnset(t, physical, i)
            pool = ARCH_TRAITS[arch][:]
            random.Random(zlib.crc32(forms[0][0].encode())).shuffle(pool)
            commons = pool[:2]
            rare = [r for r in RARE_TRAITS if r not in commons]
            rare = rare[zlib.crc32(forms[0][0].encode()) % len(rare)]
            biomes = []
            if divine:
                biomes = [9]
            else:
                for bm in TYPE_BIOMES[t[0]] + (TYPE_BIOMES[t[1]][:1] if len(t) > 1 else []):
                    if bm not in biomes:
                        biomes.append(bm)
            if divine:
                rarity, cap = 3, 0.03
            elif is_hidden:
                rarity, cap = 3, 0.04
            elif normal_len == 1:
                rarity, cap = 1, 0.30
            else:
                rarity, cap = (0, 1, 2)[i], (0.45, 0.25, 0.12)[i]
            if has_hidden and i == 0:
                rarity = 1
            total = sum(base)
            species.append({
                "id": ids[i], "name": name, "t1": tid(t[0]), "t2": tid(t[1]) if len(t) > 1 else -1,
                "stage": i, "family": fam_index, "arch": arch,
                "hp": hp, "atk": atk, "def": df, "mag": mag, "res": res, "spd": spd,
                "mp": int(20 + mag * 0.6), "ap": ARCH_AP[arch] + (1 if i >= 2 or divine else 0),
                "evolvesTo": evo_to, "evolveLevel": evo_level, "evolveItem": evo_item,
                "learnLevels": [l for l, _ in ls], "learnMoves": [m for _, m in ls],
                "traits": commons + [rare], "rarity": rarity, "biomes": biomes,
                "captureRate": cap, "origin": {"Y": "Yokai", "F": "Fairy Tale", "M": "Myth", "D": "Divine"}[origin],
                "expYield": int(total / 6), "hiddenForm": is_hidden,
            })

    moves = build_moves()
    move_ids = {m["id"] for m in moves}
    for s in species:
        for m in s["learnMoves"]:
            assert m in move_ids, m

    here = os.path.dirname(os.path.abspath(__file__))
    out = os.path.join(here, "..", "Assets", "Resources", "Data")
    os.makedirs(out, exist_ok=True)
    with open(os.path.join(out, "species.json"), "w") as f:
        json.dump({"species": species}, f, indent=0)
    with open(os.path.join(out, "moves.json"), "w") as f:
        json.dump({"moves": moves}, f, indent=0)

    base = [s for s in species if s["stage"] == 0]
    once = [s for s in base if s["evolvesTo"] >= 0]
    twice = [s for s in species if s["stage"] == 1 and s["evolvesTo"] >= 0]
    hidden = [s for s in species if s["hiddenForm"]]
    divine = [s for s in species if s["origin"] == "Divine"]
    print("total species      :", len(species))
    print("base creatures     :", len(base))
    print("  evolve once      :", len(once))
    print("  evolve twice     :", len(twice))
    print("  hidden evolutions:", len(hidden))
    print("  divine           :", len(divine))
    print("moves              :", len(moves))
    for b in range(10):
        n = len([s for s in base if b in s["biomes"]])
        print("  biome %d base species: %d" % (b, n))
    for t in range(12):
        n = len([s for s in species if s["t1"] == t or s["t2"] == t])
        print("  %-9s %d" % (TYPE_NAMES[t], n))


if __name__ == "__main__":
    main()
