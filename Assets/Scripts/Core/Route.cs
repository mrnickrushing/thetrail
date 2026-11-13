using System.Collections.Generic;

namespace TheTrail.Core
{
    public enum StopKind { Town, Fort, River, Landmark, Pass, Junction, Diggings }
    public enum Ground { Prairie, Plains, Sage, Hills, Mountains, Desert, Forest }

    /// <summary>The road from one stop to the next.</summary>
    public sealed class Leg
    {
        public string To, Label, Note;
        public int Miles;
        public Ground Ground;
        public float Grass;   // 0..1, how good the feed is in a good year before anyone else has passed
        public int Dry;       // miles without water at the start of the leg
        public Leg(string to, int miles, Ground ground, float grass, int dry = 0, string label = null, string note = null)
        { To = to; Miles = miles; Ground = ground; Grass = grass; Dry = dry; Label = label; Note = note; }
    }

    public sealed class Stop
    {
        public string Id, Name, Art, Blurb;
        public StopKind Kind;
        public float Prices;                 // 0 = nothing for sale
        public bool Water = true;            // kegs can be filled here
        public bool Fresh;                   // fresh food can be had (resets scurvy)
        public int Width, DepthLo, DepthHi;  // rivers: feet
        public float Swift;                  // 0..1
        public int Ferry, FerryWait;         // dollars a wagon (0 = none), days in line at most
        public int Haul;                     // 1 = lower the wagon down a slope here, 2 = haul it up
        public float Lat, Lon;
        public (string who, string say)[] Talk = new (string, string)[0];
        public Leg[] Next = new Leg[0];
        public bool End => Kind == StopKind.Diggings;
        public bool Store => Prices > 0;
    }

    /// <summary>The California Trail as the forty-niners travelled it, St. Joseph to the diggings.</summary>
    public static class Route
    {
        public const int Year = 1849;
        public const string Start = "stjoe";
        public static readonly Dictionary<string, Stop> All = new Dictionary<string, Stop>();
        public static readonly List<string> Order = new List<string>();

        public static Stop Get(string id) => All.TryGetValue(id ?? "", out var s) ? s : All[Start];
        public static bool Exists(string id) => id != null && All.ContainsKey(id);

        static void Add(Stop s) { All[s.Id] = s; Order.Add(s.Id); }

        static Route()
        {
            Add(new Stop
            {
                Id = "stjoe", Name = "St. Joseph, Missouri", Kind = StopKind.Town, Art = "town", Prices = 1f, Fresh = true,
                Lat = 39.77f, Lon = -94.85f,
                Blurb = "The jumping-off town on the Missouri. Every hotel, livery and outfitter is full of men bound for the gold fields, and the ferry runs from dawn to dark.",
                Next = new[] { new Leg("bigblue", 115, Ground.Prairie, 0.8f) },
                Talk = new[]
                {
                    ("An outfitter's clerk", "Flour, bacon, coffee, powder. Everybody wants the same four things and they all want them today."),
                    ("A returned trapper", "Don't load that wagon like a store. Half the trail west is littered with stoves and anvils men couldn't haul."),
                    ("A doctor from Ohio", "The cholera came up the river on the steamboats this spring. Boil your water and keep your camp clean."),
                },
            });
            Add(new Stop
            {
                Id = "bigblue", Name = "Big Blue River", Kind = StopKind.River, Art = "river",
                Width = 220, DepthLo = 2, DepthHi = 5, Swift = 0.3f, Lat = 39.84f, Lon = -96.65f,
                Blurb = "A clear river over sand and gravel. A spring with a little waterfall runs in just upstream, and the bank is cut deep by the wagons ahead.",
                Next = new[] { new Leg("kearny", 180, Ground.Prairie, 0.85f) },
                Talk = new[]
                {
                    ("A wagon captain from Illinois", "We sent a man across on horseback first. Where his horse found bottom, we drove the teams."),
                    ("A young argonaut", "Three weeks out and I have already written my mother four letters. Nobody going east to carry them."),
                    ("A Kansa trader", "The river rises fast after rain upstream. Cross in the morning, before the afternoon storms."),
                },
            });
            Add(new Stop
            {
                Id = "kearny", Name = "Fort Kearny", Kind = StopKind.Fort, Art = "fort", Prices = 1.3f,
                Lat = 40.64f, Lon = -99.0f,
                Blurb = "A new Army post of sod and adobe on the Platte River, a year old. The soldiers count the wagons: thousands have passed this spring.",
                Next = new[] { new Leg("ashhollow", 160, Ground.Plains, 0.7f) },
                Talk = new[]
                {
                    ("The post sutler", "Prices are what they are. Every sack of flour here came out by wagon, same as you."),
                    ("A soldier on the register", "Over four thousand wagons since May, and more every day. The grass along the river is eaten to the roots."),
                    ("A Pawnee man trading moccasins", "Your people come like the buffalo did. Too many to count, and the grass is gone behind them."),
                },
            });
            Add(new Stop
            {
                Id = "ashhollow", Name = "Ash Hollow", Kind = StopKind.Landmark, Art = "hollow", Haul = 1, Fresh = true,
                Lat = 41.29f, Lon = -102.12f,
                Blurb = "The road drops down Windlass Hill into a shady hollow of ash trees and wild roses with a cold spring. The slope is so steep the wagons go down on ropes.",
                Next = new[] { new Leg("courthouse", 45, Ground.Plains, 0.65f) },
                Talk = new[]
                {
                    ("A teamster with rope-burned hands", "Lock the wheels, run the rope round a stake, and let her down slow. Too fast and she runs away from you."),
                    ("A woman picking chokecherries", "Fresh fruit! The first in a month. Fill a bucket, it keeps the scurvy off."),
                    ("A preacher with a Bible", "Rest a day here if you can. The shade is a mercy and the water is sweet."),
                },
            });
            Add(new Stop
            {
                Id = "courthouse", Name = "Courthouse Rock", Kind = StopKind.Landmark, Art = "courthouse",
                Lat = 41.6f, Lon = -103.12f,
                Blurb = "A great clay bluff the emigrants named for a courthouse back home, with a smaller one, the Jail, beside it. A thin spire is in sight to the west.",
                Next = new[] { new Leg("laramie", 85, Ground.Plains, 0.6f) },
                Talk = new[]
                {
                    ("A surveyor's assistant", "It looks an hour's walk. It's five miles if it's one. Distances lie on the plains."),
                    ("A boy from Missouri", "We climbed it to carve our names. You can see wagons strung out for twenty miles both ways."),
                    ("An old freighter", "Fort Laramie's three days on. Buy there if you must, but you'll pay."),
                },
            });
            Add(new Stop
            {
                Id = "laramie", Name = "Fort Laramie", Kind = StopKind.Fort, Art = "fort", Prices = 1.6f,
                Lat = 42.2f, Lon = -104.55f,
                Blurb = "The old adobe trading post. The Army bought it this summer, and Lakota and Cheyenne families still come to trade beside the soldiers' tents.",
                Next = new[] { new Leg("platteferry", 125, Ground.Hills, 0.55f) },
                Talk = new[]
                {
                    ("A Lakota woman trading buffalo robes", "Good robes for the cold mountains. Flour and coffee are worth more to us than your coins."),
                    ("A blacksmith in the post", "Half the wagons that come in need a tire set. The dry air shrinks the wood off the iron."),
                    ("A company that turned back", "We've seen the elephant. Sold our outfit here for half what it cost and we're going home."),
                },
            });
            Add(new Stop
            {
                Id = "platteferry", Name = "Mormon Ferry, North Platte", Kind = StopKind.River, Art = "ferry",
                Width = 400, DepthLo = 5, DepthHi = 10, Swift = 0.6f, Ferry = 5, FerryWait = 4, Lat = 42.85f, Lon = -106.3f,
                Blurb = "Ferrymen from the Salt Lake settlements take wagons over the deep North Platte on rafts. The line of wagons waiting stretches back along the bank.",
                Next = new[] { new Leg("devilsgate", 55, Ground.Sage, 0.45f) },
                Talk = new[]
                {
                    ("A ferryman", "Five dollars a wagon and your stock swims. The line's moving, friend, but not fast."),
                    ("A man drying his goods", "We tried to float her ourselves on a raft of cottonwood logs. Lost a barrel of pork and nearly a mule."),
                    ("A gambler with a deck of cards", "Long wait for the ferry. Care to make it pass quicker?"),
                },
            });
            Add(new Stop
            {
                Id = "devilsgate", Name = "Devil's Gate", Kind = StopKind.Landmark, Art = "gate",
                Lat = 42.44f, Lon = -107.21f,
                Blurb = "The Sweetwater River cuts through a wall of granite in a cleft hundreds of feet deep. Behind you, the great dome of Independence Rock is painted with names.",
                Next = new[] { new Leg("southpass", 95, Ground.Sage, 0.45f) },
                Talk = new[]
                {
                    ("A man painting his name in axle grease", "Everybody's name is on that rock. Mine will be too, before we go."),
                    ("A woman nursing a sick husband", "The alkali water sickened half our oxen. Keep them off the white-crusted pools."),
                    ("A hunter with an antelope", "There is game in these hills if you go quietly. Come at them from downwind."),
                },
            });
            Add(new Stop
            {
                Id = "southpass", Name = "South Pass", Kind = StopKind.Junction, Art = "pass",
                Lat = 42.36f, Lon = -108.92f,
                Blurb = "A wide, windy saddle over the Continental Divide. Past Pacific Springs the water runs to the Pacific. Here the road divides.",
                Next = new[]
                {
                    new Leg("greenriver", 70, Ground.Sage, 0.35f, 45, "Take the Greenwood Cutoff",
                        "Shorter, but the first 45 miles have no water at all. Then Fort Hall."),
                    new Leg("saltlake", 250, Ground.Mountains, 0.55f, 0, "Go by Great Salt Lake City",
                        "Longer, but water all the way and fresh food at the Mormon settlement."),
                },
                Talk = new[]
                {
                    ("A guide for hire", "The cutoff saves you near a week. But fill every keg you own and drive at night."),
                    ("A Mormon teamster", "Go by the city. Our people will trade you fresh vegetables and good fat cattle for your tired ones."),
                    ("A man reading a guidebook", "The book says the Pass is so gentle you'd never know you crossed a mountain. The book is right."),
                },
            });
            Add(new Stop
            {
                Id = "greenriver", Name = "Green River", Kind = StopKind.River, Art = "river",
                Width = 360, DepthLo = 6, DepthHi = 11, Swift = 0.7f, Ferry = 8, FerryWait = 2, Lat = 41.95f, Lon = -110.05f,
                Blurb = "A cold, deep, fast river at the end of the dry cutoff. Mountain men run ferries on rafts and charge what the crowd will bear.",
                Next = new[] { new Leg("bearriver", 110, Ground.Mountains, 0.6f) },
                Talk = new[]
                {
                    ("A mountain man running a ferry", "Eight dollars. You can swim her yourself and see how that goes."),
                    ("A man with a broken wagon", "We lost three oxen on the dry stretch. They smelled this river a mile off and stampeded."),
                    ("A Shoshone man trading fish", "The river is too strong here for wagons. Use the rafts, or go up where it spreads wide."),
                },
            });
            Add(new Stop
            {
                Id = "bearriver", Name = "Bear River Valley", Kind = StopKind.Landmark, Art = "valley", Fresh = true,
                Lat = 42.3f, Lon = -111.1f,
                Blurb = "Green meadows along a winding river, with good grass at last. Bubbling soda springs a day ahead taste like lemonade.",
                Next = new[] { new Leg("hall", 95, Ground.Hills, 0.6f) },
                Talk = new[]
                {
                    ("A Shoshone family camped by the river", "We trade fish and berries for cloth and needles. Your animals can eat here; the grass is good this year."),
                    ("An argonaut with a fishing line", "Trout! Caught a dozen this morning. Best meal since Missouri."),
                    ("A man with a map", "Fort Hall next. They say the Company will sell you flour, at a price."),
                },
            });
            Add(new Stop
            {
                Id = "hall", Name = "Fort Hall", Kind = StopKind.Fort, Art = "fort", Prices = 2f,
                Lat = 43.02f, Lon = -112.43f,
                Blurb = "A whitewashed Hudson's Bay Company post on the Snake River plain. The California road turns south from here.",
                Next = new[] { new Leg("cityrocks", 165, Ground.Sage, 0.4f) },
                Talk = new[]
                {
                    ("The Company's clerk", "Flour's a dollar a pound and I can't spare much. Half your countrymen want it."),
                    ("A Scots trapper", "South to the Raft River, then the City of Rocks. After that it's the Humboldt, and nobody loves the Humboldt."),
                    ("An argonaut selling his books", "Lightening the load. Shakespeare, a Bible, a geology. Ten cents apiece."),
                },
            });
            Add(new Stop
            {
                Id = "saltlake", Name = "Great Salt Lake City", Kind = StopKind.Town, Art = "saltlake", Prices = 1.4f, Fresh = true,
                Lat = 40.76f, Lon = -111.89f,
                Blurb = "Two years old: adobe houses, irrigation ditches and green fields by a salt lake. The settlers trade fresh food and sound stock for the argonauts' goods.",
                Next = new[] { new Leg("cityrocks", 250, Ground.Sage, 0.45f, 0, null, "The Salt Lake Cutoff, north round the lake to the City of Rocks.") },
                Talk = new[]
                {
                    ("A settler with a vegetable cart", "Potatoes, onions, butter, eggs. You'll take some, you've the look of a man who needs greens."),
                    ("A trader buying worn oxen", "I'll give you one fat ox for two of your tired ones. Mine will get you to California."),
                    ("A blacksmith", "Wagons, harness, shoes. Everyone wants the work done at once and they pay in gold dust, mostly promised."),
                },
            });
            Add(new Stop
            {
                Id = "cityrocks", Name = "City of Rocks", Kind = StopKind.Landmark, Art = "rocks",
                Lat = 42.08f, Lon = -113.72f,
                Blurb = "A valley of granite towers and domes like the ruins of a city. The Salt Lake road joins here, and the climb to the Humboldt begins.",
                Next = new[] { new Leg("humboldtwells", 115, Ground.Hills, 0.5f) },
                Talk = new[]
                {
                    ("A man writing names in axle grease", "Every rock here is a register. I found my brother's name from June. He's two weeks ahead."),
                    ("A tired woman", "My children play in the rocks like it was a castle. I just want to see the end of it."),
                    ("A guide", "From here to the Sink, the grass gets worse every week. Don't wear the stock out now."),
                },
            });
            Add(new Stop
            {
                Id = "humboldtwells", Name = "Humboldt Wells", Kind = StopKind.Landmark, Art = "wells",
                Lat = 41.11f, Lon = -114.96f,
                Blurb = "Deep, clear springs at the head of the Humboldt River. From here the road follows the river west across the desert for weeks.",
                Next = new[] { new Leg("gravellyford", 115, Ground.Sage, 0.45f) },
                Talk = new[]
                {
                    ("An argonaut drawing water", "Best water we'll see for a month, they say. The river gets worse every mile it goes."),
                    ("A Western Shoshone man", "The river feeds no one well. Our people gather seeds and roots; there is little for your cattle."),
                    ("A man with a lame mule", "Rest them here a day. There's nothing better ahead till California."),
                },
            });
            Add(new Stop
            {
                Id = "gravellyford", Name = "Gravelly Ford", Kind = StopKind.River, Art = "humboldt",
                Width = 90, DepthLo = 1, DepthHi = 3, Swift = 0.2f, Lat = 40.62f, Lon = -116.36f,
                Blurb = "A broad crossing of the Humboldt over a gravel bed. The water is low and warm and already tastes of alkali.",
                Next = new[] { new Leg("lassen", 150, Ground.Sage, 0.35f) },
                Talk = new[]
                {
                    ("A man fishing", "Shallow as a puddle this time of year. Even the Humboldt can't drown you here."),
                    ("A sick argonaut", "The water gripes my belly something awful. Boil it, they say. With what wood?"),
                    ("A wagon captain", "Keep a night guard on the stock. Animals stray in this country and you won't find them."),
                },
            });
            Add(new Stop
            {
                Id = "lassen", Name = "Lassen Meadows", Kind = StopKind.Junction, Art = "meadow",
                Lat = 40.72f, Lon = -118.22f,
                Blurb = "Rough meadows by the river where a trail branches north. Men argue in the road over which way is shorter to the gold.",
                Next = new[]
                {
                    new Leg("sink", 55, Ground.Desert, 0.25f, 0, "Keep to the Humboldt",
                        "To the Sink, the Forty Mile Desert, the Carson River and the Sierra at Carson Pass."),
                    new Leg("blackrock", 60, Ground.Desert, 0.15f, 60, "Take Lassen's Cutoff",
                        "No Forty Mile Desert and no high pass, but 60 dry miles at once and a very long road round."),
                },
                Talk = new[]
                {
                    ("A man with a printed handbill", "Lassen's road! The shortest way to the northern mines. No desert worth the name."),
                    ("An old mountain man", "Short? It runs north for a hundred miles before it turns west. I'd not take it."),
                    ("A woman mending a wagon cover", "Half the companies are going north. The grass will be gone whichever way you choose."),
                },
            });
            Add(new Stop
            {
                Id = "sink", Name = "Humboldt Sink", Kind = StopKind.Landmark, Art = "sink",
                Lat = 40.0f, Lon = -118.6f,
                Blurb = "The Humboldt dies in a marsh of reeds and alkali. Ahead lies the Forty Mile Desert. Cut grass, fill every keg, and go at night.",
                Next = new[] { new Leg("ragtown", 40, Ground.Desert, 0.0f, 40, null, "The Forty Mile Desert: no water, no grass.") },
                Talk = new[]
                {
                    ("A man cutting grass with a scythe", "Cut it now and carry it. There's not a blade between here and the Carson."),
                    ("A relief party rider", "The desert is the worst of it. Travel at night, rest in the heat, and don't stop near the dead stock."),
                    ("A Paiute guide", "There is a hot spring halfway. The water is bad to drink until it cools. Most of your people do not wait."),
                },
            });
            Add(new Stop
            {
                Id = "ragtown", Name = "Carson River (Ragtown)", Kind = StopKind.Landmark, Art = "carson", Fresh = true, Prices = 2.2f,
                Lat = 39.48f, Lon = -118.88f,
                Blurb = "Cottonwoods and clean water after the desert. Companies wash their ragged clothes and hang them on every bush; traders sell at desert prices.",
                Next = new[] { new Leg("summit", 110, Ground.Mountains, 0.6f) },
                Talk = new[]
                {
                    ("A trader with a wagon of flour", "Two dollars a pound, and I'm the only one selling. You'll thank me in the mountains."),
                    ("A man laughing in the river", "We made it! We made the river! I'll never be thirsty again as long as I live."),
                    ("A Washoe man", "The snow comes early to the high pass. Do not wait long here."),
                },
            });
            Add(new Stop
            {
                Id = "summit", Name = "Carson Pass", Kind = StopKind.Pass, Art = "summit", Haul = 2,
                Lat = 38.69f, Lon = -119.99f,
                Blurb = "The Sierra Nevada. Above Red Lake the road climbs a granite pitch so steep the wagons are hauled up one at a time on chains and a windlass.",
                Next = new[] { new Leg("hangtown", 70, Ground.Forest, 0.7f) },
                Talk = new[]
                {
                    ("A man at the windlass", "Hitch on, take up the slack, and haul steady. Jerk the chain and she'll snap."),
                    ("A woman looking west", "Over this, they say, and it's all downhill to California."),
                    ("A relief party rider", "The Army has men on the passes with flour for those who've run out. Nobody should die this close."),
                },
            });
            Add(new Stop
            {
                Id = "hangtown", Name = "Hangtown, the Dry Diggings", Kind = StopKind.Diggings, Art = "diggings", Prices = 3f, Fresh = true,
                Lat = 38.73f, Lon = -120.8f,
                Blurb = "A gulch full of tents, brush huts and men with pans. Gold has come out of these ravines by the pound. Find a claim and go to work.",
                Talk = new[]
                {
                    ("A miner with a full poke", "Took two pounds out of that gulch in a month. Course, it's dug out now. Try further up."),
                    ("A storekeeper", "Eggs a dollar each. Flour a dollar a pound. You'll pay in dust, I've a scale right here."),
                    ("A man sitting in the mud", "Six months to get here and I've found eleven dollars. Don't let anyone tell you it's easy."),
                },
            });
            Add(new Stop
            {
                Id = "blackrock", Name = "Black Rock Springs", Kind = StopKind.Landmark, Art = "blackrock",
                Lat = 40.97f, Lon = -119.0f,
                Blurb = "Boiling springs at the foot of a black volcanic point, after sixty miles of desert. The water must cool before the stock can drink.",
                Next = new[] { new Leg("highrock", 35, Ground.Desert, 0.2f) },
                Talk = new[]
                {
                    ("A man burying an ox", "We drove all night and all day. Six oxen down. I don't know how we'll pull now."),
                    ("A Paiute man", "The cold spring is around the point. Your people always drink the hot one first."),
                    ("A guide", "High Rock Canyon next. Narrow, but there's water and some grass."),
                },
            });
            Add(new Stop
            {
                Id = "highrock", Name = "High Rock Canyon", Kind = StopKind.Landmark, Art = "canyon",
                Lat = 41.36f, Lon = -119.33f,
                Blurb = "A narrow canyon between high walls of rock, with water and a little grass. Names of the companies ahead are cut into the stone.",
                Next = new[] { new Leg("gooselake", 110, Ground.Hills, 0.35f) },
                Talk = new[]
                {
                    ("A man carving his name", "Thought I'd leave a mark in case we don't make it. Morbid, my wife says."),
                    ("A man counting his food", "Six weeks of flour for ten weeks of road. Somebody lied about this cutoff."),
                    ("A young woman", "The canyon walls frighten me at night. But the water is good."),
                },
            });
            Add(new Stop
            {
                Id = "gooselake", Name = "Goose Lake", Kind = StopKind.Landmark, Art = "lake",
                Lat = 41.95f, Lon = -120.4f,
                Blurb = "A long shallow lake in the high country. The road turns south and west at last, through pine forest toward the Sacramento Valley.",
                Next = new[] { new Leg("lassenrancho", 200, Ground.Forest, 0.4f) },
                Talk = new[]
                {
                    ("A wagon captain", "We've come north two hundred miles to go west. Lassen ought to be ashamed."),
                    ("A man trading horses", "The forest has feed, at least. Your animals look like they need it."),
                    ("A relief party rider", "Supply trains are coming up from the valley. Hold on a little longer."),
                },
            });
            Add(new Stop
            {
                Id = "lassenrancho", Name = "Lassen's Rancho", Kind = StopKind.Diggings, Art = "diggings", Prices = 3.6f, Fresh = true,
                Lat = 40.1f, Lon = -122.05f,
                Blurb = "Peter Lassen's ranch on Deer Creek, at the edge of the Sacramento Valley. The northern diggings on the Feather River are a few days' ride.",
                Talk = new[]
                {
                    ("A ranch hand", "Every company that comes off that road is half-starved. Food is dear here; it has to come up the river."),
                    ("A miner back from the Feather", "There's gold on the bars if you get there before the rains. After that the river takes them."),
                    ("A man selling his wagon", "I'll take an ounce of dust for it. Nobody needs a wagon in the diggings."),
                },
            });
        }

        /// <summary>The stops between start and finish along the longest way (for the map and the progress line).</summary>
        public const int TotalMiles = 1950;
    }
}
