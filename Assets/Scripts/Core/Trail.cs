using System.Collections.Generic;

namespace TheTrail.Core
{
    public enum Kind { Town, River, Fort, Landmark, Pass, End }
    public enum Terrain { Plains, Hills, Mountains, Desert, Forest }

    public sealed class Route
    {
        public string To; public int Miles; public string Label; public bool Raft; public int Toll;
        public Route(string to, int miles, string label = null, bool raft = false, int toll = 0)
        { To = to; Miles = miles; Label = label; Raft = raft; Toll = toll; }
    }

    public sealed class Landmark
    {
        public string Id, Name, Art, Blurb;
        public Kind Kind;
        public Terrain Ahead;           // ground between here and the next stop
        public Route[] Next = new Route[0];
        public float Prices;            // 0 = no store
        public int RiverWidth, DepthLo, DepthHi, Ferry; // feet, feet, feet, dollars (0 = no ferry)
        public float Swift;             // 0..1 strength of the current
        public float Lat, Lon;           // where it really is, for the map
        public (string who, string say)[] Talk = new (string, string)[0];
        public bool HasStore => Prices > 0;
    }

    /// <summary>The 1848 emigrant road from Independence, Missouri to Oregon City.</summary>
    public static class Trail
    {
        public const int Year = 1848;
        public static readonly Dictionary<string, Landmark> All = new Dictionary<string, Landmark>();
        public const string Start = "independence", Finish = "oregoncity";

        public static Landmark Get(string id) => All[id];

        static Landmark Add(Landmark l) { All[l.Id] = l; return l; }

        static Trail()
        {
            Add(new Landmark
            {
                Id = "independence", Name = "Independence, Missouri", Kind = Kind.Town, Art = "town", Ahead = Terrain.Plains,
                Prices = 1f, Lat = 39.09f, Lon = -94.42f,
                Next = new[] { new Route("kansas", 102) },
                Blurb = "The jumping-off place. Every spring the town fills with wagons, oxen and families bound for Oregon.",
                Talk = new[]
                {
                    ("Ezra Pell, wheelwright", "Buy your oxen here, not out yonder. Mules are quick, but an ox will eat prairie grass and pull till it drops."),
                    ("Mrs. Hattie Coombs", "My sister went out in '46. Her letters say take more flour than you think, and twice the coffee."),
                    ("A tired teamster", "Leave too early and there's no grass. Leave too late and the snow catches you in the Blue Mountains. Spring, friend. Spring."),
                }
            });
            Add(new Landmark
            {
                Id = "kansas", Name = "Kansas River Crossing", Kind = Kind.River, Art = "river", Ahead = Terrain.Plains,
                RiverWidth = 620, DepthLo = 3, DepthHi = 6, Ferry = 5, Swift = 0.35f, Lat = 39.05f, Lon = -95.68f,
                Next = new[] { new Route("bigblue", 83) },
                Blurb = "A wide brown river with a ferry run by a local family. Spring rains can swell it fast.",
                Talk = new[]
                {
                    ("Jean Lavalle, ferryman", "Five dollars and I take your wagon over dry. Ford her yourself and you may take her over wet."),
                    ("A Kansa man trading corn", "The river is high from the rains upstream. In two days it will be lower. Patience costs nothing."),
                    ("Amos Reed, emigrant", "We lost a barrel of flour to the current last week. Tie everything down twice."),
                }
            });
            Add(new Landmark
            {
                Id = "bigblue", Name = "Big Blue River Crossing", Kind = Kind.River, Art = "river", Ahead = Terrain.Plains,
                RiverWidth = 220, DepthLo = 2, DepthHi = 4, Ferry = 0, Swift = 0.25f, Lat = 39.84f, Lon = -96.65f,
                Next = new[] { new Route("kearny", 119) },
                Blurb = "Clear water over a sandy bottom. There is no ferry here; you must cross on your own.",
                Talk = new[]
                {
                    ("Sarah Whitcomb", "There is a lovely spring just upstream with a waterfall. We rested two days and the children played in it."),
                    ("Old Tom Garrity, guide", "The Blue is gentle most days. If it's above the hubs, wait. If it's below, go."),
                    ("A young bride", "Mother cried when we left Ohio. I have not cried yet. I keep a list of every flower I see instead."),
                }
            });
            Add(new Landmark
            {
                Id = "kearny", Name = "Fort Kearny", Kind = Kind.Fort, Art = "fort", Ahead = Terrain.Plains,
                Prices = 1.25f, Lat = 40.64f, Lon = -99.0f,
                Next = new[] { new Route("ashhollow", 176) },
                Blurb = "A new Army post on the Platte River, built this very year to guard the road west.",
                Talk = new[]
                {
                    ("Lt. Henry Dorsey", "The Platte is a mile wide and an inch deep. Don't drink it straight; let it settle in a bucket first."),
                    ("A soldier's wife", "Buffalo chips burn well enough once you stop minding what they are. There's no wood for a hundred miles."),
                    ("Caleb Moss, emigrant", "Cholera took two wagons ahead of us. Boil your water, keep your camp clean, and pray."),
                }
            });
            Add(new Landmark
            {
                Id = "ashhollow", Name = "Ash Hollow", Kind = Kind.Landmark, Art = "hollow", Ahead = Terrain.Plains,
                Lat = 41.29f, Lon = -102.12f,
                Next = new[] { new Route("chimney", 74) },
                Blurb = "Wagons are let down the steep slope of Windlass Hill by rope into a shady glen with sweet spring water.",
                Talk = new[]
                {
                    ("Nathaniel Bray", "We locked the wheels and lowered her on ropes, all twelve of us hauling. My hands are raw but the wagon's whole."),
                    ("Mrs. Lucy Ames", "Ash trees, wild roses, a cold spring. After the Platte it is like a church."),
                    ("A traveling preacher", "Rest here a day. The road ahead is long and dry, and the soul needs shade as much as the body."),
                }
            });
            Add(new Landmark
            {
                Id = "chimney", Name = "Chimney Rock", Kind = Kind.Landmark, Art = "chimney", Ahead = Terrain.Plains,
                Lat = 41.7f, Lon = -103.35f,
                Next = new[] { new Route("scotts", 42) },
                Blurb = "A clay spire rising hundreds of feet above the North Platte. Travelers can see it for days before they reach it.",
                Talk = new[]
                {
                    ("Abigail Hart, diarist", "We saw it three days ago and thought it a few hours off. Distances lie out here."),
                    ("Will Tanner, 12 years old", "Pa says folks carve their names up the side. I aim to carve mine higher than anybody."),
                    ("An Army scout", "From here the country climbs. Mind your oxen; the grass gets thin."),
                }
            });
            Add(new Landmark
            {
                Id = "scotts", Name = "Scotts Bluff", Kind = Kind.Landmark, Art = "bluff", Ahead = Terrain.Plains,
                Lat = 41.83f, Lon = -103.7f,
                Next = new[] { new Route("laramie", 44) },
                Blurb = "Great bluffs of sandstone and clay stand like a ruined city beside the river.",
                Talk = new[]
                {
                    ("Mrs. Prudence Kline", "My husband says it looks like a castle. I say it looks like a long way from home."),
                    ("A fur trader heading east", "Fort Laramie's two days on. They'll charge you dear for flour, but they have it."),
                    ("Josiah Crane", "Our wagon tongue cracked yesterday. Carry spares, friend. Out here a stick of hickory is worth a dollar."),
                }
            });
            Add(new Landmark
            {
                Id = "laramie", Name = "Fort Laramie", Kind = Kind.Fort, Art = "fort", Ahead = Terrain.Hills,
                Prices = 1.5f, Lat = 42.2f, Lon = -104.55f,
                Next = new[] { new Route("indrock", 190) },
                Blurb = "An adobe trading post where Lakota and Cheyenne families, trappers and emigrants all meet to trade.",
                Talk = new[]
                {
                    ("A fur company clerk", "Sugar is dear and coffee dearer. Everything here came by wagon from St. Louis, same as you."),
                    ("Rebecca Long, widow", "My husband is buried at the Blue. I'm going on with the children. Turning back would be harder."),
                    ("A Lakota trader", "Your oxen are thin. Rest them two days by the river and they will thank you in the mountains."),
                }
            });
            Add(new Landmark
            {
                Id = "indrock", Name = "Independence Rock", Kind = Kind.Landmark, Art = "rock", Ahead = Terrain.Hills,
                Lat = 42.49f, Lon = -107.13f,
                Next = new[] { new Route("southpass", 102) },
                Blurb = "A great granite dome covered with emigrants' names. Reach it by the Fourth of July and you will beat the snow.",
                Talk = new[]
                {
                    ("Thaddeus Grant", "We painted our names with axle grease and gunpowder. Some fool will paint over them next year."),
                    ("Mrs. Eliza Marsh", "The water around here is alkali. It sickens the oxen. Keep them off the white-crusted pools."),
                    ("A boy with a fiddle", "Fourth of July we danced on top of the rock. Never been up so high in my life."),
                }
            });
            Add(new Landmark
            {
                Id = "southpass", Name = "South Pass", Kind = Kind.Pass, Art = "pass", Ahead = Terrain.Mountains,
                Lat = 42.36f, Lon = -108.92f,
                Next = new[]
                {
                    new Route("bridger", 125, "Head to Fort Bridger"),
                    new Route("greenriver", 57, "Take the Sublette Cutoff"),
                },
                Blurb = "A broad, gentle saddle over the Continental Divide. From here, the rivers run to the Pacific.",
                Talk = new[]
                {
                    ("Old Tom Garrity, guide", "The cutoff saves you near a week, but it's fifty miles without water. Fill every keg you own."),
                    ("A Mormon teamster", "Fort Bridger has a blacksmith and fresh stock. Longer road, softer on the animals."),
                    ("Hannah Fisk", "I expected mountains like walls. Instead it is a wide meadow, and the wind never stops."),
                }
            });
            Add(new Landmark
            {
                Id = "greenriver", Name = "Green River Crossing", Kind = Kind.River, Art = "river", Ahead = Terrain.Mountains,
                RiverWidth = 380, DepthLo = 6, DepthHi = 11, Ferry = 10, Swift = 0.55f, Lat = 41.95f, Lon = -110.05f,
                Next = new[] { new Route("soda", 144) },
                Blurb = "A swift, deep, cold river at the end of the dry cutoff. Some Mormon men run a ferry for those who can pay.",
                Talk = new[]
                {
                    ("Brother Isaac Pratt, ferryman", "Ten dollars. I know it's dear. Ask the folks who tried to ford her what it cost them."),
                    ("Martha Grey", "We nearly lost the oxen on the dry stretch. They smelled this water a mile off and ran."),
                    ("A trapper", "Green River's deep and fast as a mill race. Don't ford it. Ever."),
                }
            });
            Add(new Landmark
            {
                Id = "bridger", Name = "Fort Bridger", Kind = Kind.Fort, Art = "fort", Ahead = Terrain.Mountains,
                Prices = 1.75f, Lat = 41.32f, Lon = -110.39f,
                Next = new[] { new Route("soda", 162) },
                Blurb = "A small log trading post kept by mountain men, with a forge and fresh oxen to trade.",
                Talk = new[]
                {
                    ("A mountain man", "Been out here twenty years. The trail's busier every season. Soon there'll be no beaver nor peace either one."),
                    ("A blacksmith", "I can set a tire or shoe an ox, but you'll pay mountain prices for mountain iron."),
                    ("Clara Benson", "Some families turned south here for California. We're Oregon folk, through and through."),
                }
            });
            Add(new Landmark
            {
                Id = "soda", Name = "Soda Springs", Kind = Kind.Landmark, Art = "springs", Ahead = Terrain.Mountains,
                Lat = 42.65f, Lon = -111.6f,
                Next = new[] { new Route("hall", 57) },
                Blurb = "Bubbling springs of naturally fizzy water. One spring roars and sputters like a steam engine.",
                Talk = new[]
                {
                    ("Mr. Elias Webb", "Mix the soda water with a little sugar and it's as good as lemonade back home."),
                    ("A sick child's mother", "They say the waters cure. My boy drank and he's no better, but no worse either."),
                    ("A Shoshone guide", "Fort Hall is close. The Snake River country after it is hard. Carry water and grass if you can."),
                }
            });
            Add(new Landmark
            {
                Id = "hall", Name = "Fort Hall", Kind = Kind.Fort, Art = "fort", Ahead = Terrain.Desert,
                Prices = 2f, Lat = 43.02f, Lon = -112.43f,
                Next = new[] { new Route("snake", 182) },
                Blurb = "A Hudson's Bay Company post of whitewashed adobe on the Snake River plain.",
                Talk = new[]
                {
                    ("The Company factor", "The Company sells what it has. Flour comes dear this far out; buy what you need and no more."),
                    ("Noah Ellis", "From here it's sagebrush and lava rock. The oxen will look at you like you've lost your mind."),
                    ("A Scots clerk", "Mind the Snake. She's wide and treacherous at the islands. Many a wagon's gone to the bottom."),
                }
            });
            Add(new Landmark
            {
                Id = "snake", Name = "Snake River Crossing", Kind = Kind.River, Art = "river", Ahead = Terrain.Desert,
                RiverWidth = 1000, DepthLo = 3, DepthHi = 6, Ferry = 0, Swift = 0.5f, Lat = 42.95f, Lon = -115.31f,
                Next = new[] { new Route("boise", 113) },
                Blurb = "Three Island Crossing. Wagons hop from island to island across a wide, powerful river.",
                Talk = new[]
                {
                    ("A Shoshone fisherman", "The salmon are running. The river is strong here. Follow the islands and do not hurry."),
                    ("Jacob Hollis", "Some families skip the crossing and keep to the south bank. It's dry and long, but it's not drowning."),
                    ("Mrs. Annie Price", "We chained six wagons together and crossed like a train. It held."),
                }
            });
            Add(new Landmark
            {
                Id = "boise", Name = "Fort Boise", Kind = Kind.Fort, Art = "fort", Ahead = Terrain.Mountains,
                Prices = 2.25f, Lat = 43.82f, Lon = -117.02f,
                Next = new[] { new Route("bluemts", 160) },
                Blurb = "A small Hudson's Bay Company post where the Boise River meets the Snake.",
                Talk = new[]
                {
                    ("A French-Canadian trapper", "The Blue Mountains are next. Thick timber, steep grades. Your axle had better be sound."),
                    ("Ruth Calloway", "Salmon! The traders here have salmon. I never thought I'd be so glad to see a fish."),
                    ("A hired hand", "Lot of folks run short of food about now. Keep a little back for the last push."),
                }
            });
            Add(new Landmark
            {
                Id = "bluemts", Name = "Blue Mountains", Kind = Kind.Landmark, Art = "mountains", Ahead = Terrain.Forest,
                Lat = 45.45f, Lon = -118.3f,
                Next = new[] { new Route("dalles", 125) },
                Blurb = "Dark forests of pine and fir. The first real timber since Missouri, and the steepest grades yet.",
                Talk = new[]
                {
                    ("Levi Sutter", "We cut our way through the deadfall a mile a day in places. Tough going, but the trees are beautiful."),
                    ("Mrs. Jane Kirby", "It snowed on us last night and it's not yet October. Hurry, if you're behind."),
                    ("A Cayuse horse trader", "Good horses for good blankets. Your oxen are tired. Tired animals fall on the hills."),
                }
            });
            Add(new Landmark
            {
                Id = "dalles", Name = "The Dalles", Kind = Kind.Town, Art = "dalles", Ahead = Terrain.Forest,
                Prices = 2.5f, Lat = 45.6f, Lon = -121.18f,
                Next = new[]
                {
                    new Route("oregoncity", 110, "Float down the Columbia River", raft: true),
                    new Route("barlow", 30, "Take the Barlow Road ($5 toll)", toll: 5),
                },
                Blurb = "A Methodist mission above the rapids of the Columbia. The wagon road ends here. Float the river or cross the mountains.",
                Talk = new[]
                {
                    ("A mission helper", "The Columbia is fast and full of rocks below here. Rafts go down every week. Not all arrive."),
                    ("The toll road agent", "Five dollars a wagon and the road's yours. It's rough, but it's dry, mostly."),
                    ("Ellen Shaw", "We made it this far. Whatever comes, we made it this far."),
                }
            });
            Add(new Landmark
            {
                Id = "barlow", Name = "Barlow Road Toll Gate", Kind = Kind.Pass, Art = "pass", Ahead = Terrain.Mountains,
                Lat = 45.28f, Lon = -121.69f,
                Next = new[] { new Route("oregoncity", 80) },
                Blurb = "A rough wagon road around the shoulder of Mount Hood. Laurel Hill ahead is so steep wagons are let down by rope.",
                Talk = new[]
                {
                    ("A toll keeper", "Snow up top already some years. Keep moving and keep your animals fed."),
                    ("Mrs. Rose Daley", "We had to lower the wagon down Laurel Hill tied to a tree. I didn't breathe the whole way."),
                    ("A returning settler", "Oregon City's a few days on. Good land, good water. You'll see."),
                }
            });
            Add(new Landmark
            {
                Id = "oregoncity", Name = "Oregon City", Kind = Kind.End, Art = "valley", Ahead = Terrain.Forest,
                Lat = 45.36f, Lon = -122.61f,
                Blurb = "The end of the trail, at the falls of the Willamette River. Green fields wait in the valley beyond.",
            });
        }

        /// <summary>Miles along the road through the given stops, in order.</summary>
        public static int MilesFromStart(IList<string> visited)
        {
            int m = 0;
            for (int i = 0; i + 1 < visited.Count; i++)
                foreach (var r in Get(visited[i]).Next)
                    if (r.To == visited[i + 1]) { m += r.Miles; break; }
            return m;
        }
    }
}
