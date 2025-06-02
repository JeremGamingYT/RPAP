using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;

namespace REALIS.UrbanLife
{
    public class LocationDatabase
    {
        private Dictionary<LocationType, List<GameLocation>> locations;
        private Random random;
        
        public LocationDatabase()
        {
            random = new Random();
            locations = new Dictionary<LocationType, List<GameLocation>>();
            InitializeLocations();
        }
        
        private void InitializeLocations()
        {
            // Initialiser toutes les catégories
            foreach (LocationType locationType in Enum.GetValues(typeof(LocationType)))
            {
                locations[locationType] = new List<GameLocation>();
            }
            
            // Bureaux et buildings d'affaires
            AddBusinessLocations();
            
            // Restaurants et cafés
            AddFoodLocations();
            
            // Parcs et espaces verts
            AddRecreationLocations();
            
            // Magasins et centres commerciaux
            AddShoppingLocations();
            
            // Lieux touristiques
            AddTouristLocations();
            
            // Lieux de travail
            AddWorkLocations();
            
            // Écoles et universités
            AddEducationLocations();
            
            // Lieux résidentiels
            AddResidentialLocations();
            
            // Autres lieux
            AddMiscLocations();
        }
        
        private void AddBusinessLocations()
        {
            // Downtown Los Santos - Gratte-ciels d'affaires
            locations[LocationType.Office].AddRange(new[]
            {
                new GameLocation("Maze Bank Tower", new Vector3(-75.2f, -826.9f, 243.4f), LocationType.Office),
                new GameLocation("Arcadius Business Center", new Vector3(-141.3f, -620.9f, 168.8f), LocationType.Office),
                new GameLocation("FIB Building", new Vector3(136.0f, -749.2f, 258.1f), LocationType.Office),
                new GameLocation("Weazel Plaza", new Vector3(-904.3f, -378.7f, 113.1f), LocationType.Office),
                new GameLocation("Life Invader Building", new Vector3(-1047.9f, -233.0f, 44.0f), LocationType.Office),
                new GameLocation("Lombank West", new Vector3(-1581.2f, -558.4f, 108.5f), LocationType.Office),
                new GameLocation("Eclipse Towers", new Vector3(-773.4f, 342.0f, 211.4f), LocationType.Office),
                new GameLocation("Del Perro Heights", new Vector3(-1447.6f, -538.5f, 74.0f), LocationType.Office)
            });
        }
        
        private void AddFoodLocations()
        {
            // Restaurants
            locations[LocationType.Restaurant].AddRange(new[]
            {
                new GameLocation("Bahama Mamas West", new Vector3(-1388.9f, -588.4f, 30.3f), LocationType.Restaurant),
                new GameLocation("The Hen House", new Vector3(-312.8f, 6224.4f, 31.5f), LocationType.Restaurant),
                new GameLocation("Vanilla Unicorn", new Vector3(129.2f, -1299.0f, 29.2f), LocationType.Restaurant),
                new GameLocation("Tequi-la-la", new Vector3(-565.2f, 276.6f, 83.1f), LocationType.Restaurant),
                new GameLocation("Yellow Jack Inn", new Vector3(1986.6f, 3053.9f, 47.2f), LocationType.Restaurant)
            });
            
            // Cafés et fast-food
            locations[LocationType.Cafe].AddRange(new[]
            {
                new GameLocation("Bean Machine Coffee", new Vector3(-636.8f, 236.0f, 81.9f), LocationType.Cafe),
                new GameLocation("Bean Machine Vespucci", new Vector3(-1173.9f, -1572.1f, 4.7f), LocationType.Cafe),
                new GameLocation("Lucky Plucker", new Vector3(-1191.8f, -1395.1f, 4.9f), LocationType.Cafe),
                new GameLocation("Up-n-Atom Burger", new Vector3(87.8f, 294.7f, 110.2f), LocationType.Cafe)
            });
            
            locations[LocationType.FastFood].AddRange(new[]
            {
                new GameLocation("Burger Shot Downtown", new Vector3(-1196.0f, -885.5f, 13.8f), LocationType.FastFood),
                new GameLocation("Cluckin' Bell Paleto", new Vector3(-146.9f, 6161.5f, 31.2f), LocationType.FastFood),
                new GameLocation("Burger Shot Mirror Park", new Vector3(1245.9f, -345.8f, 69.1f), LocationType.FastFood)
            });
        }
        
        private void AddRecreationLocations()
        {
            // Parcs
            locations[LocationType.Park].AddRange(new[]
            {
                new GameLocation("Vinewood Hills Park", new Vector3(300.6f, 180.5f, 104.4f), LocationType.Park),
                new GameLocation("Legion Square", new Vector3(215.9f, -875.9f, 30.5f), LocationType.Park),
                new GameLocation("Pershing Square", new Vector3(229.6f, -992.3f, 29.4f), LocationType.Park),
                new GameLocation("Mirror Park", new Vector3(1142.5f, -450.9f, 66.8f), LocationType.Park),
                new GameLocation("Galileo Park", new Vector3(-1002.8f, -240.6f, 37.9f), LocationType.Park),
                new GameLocation("Glen Park", new Vector3(-448.5f, -305.3f, 34.9f), LocationType.Park)
            });
            
            // Plages
            locations[LocationType.Beach].AddRange(new[]
            {
                new GameLocation("Vespucci Beach", new Vector3(-1394.5f, -1067.6f, 4.6f), LocationType.Beach),
                new GameLocation("Del Perro Beach", new Vector3(-1616.9f, -1015.5f, 13.1f), LocationType.Beach),
                new GameLocation("Chumash Beach", new Vector3(-3426.9f, 967.9f, 8.3f), LocationType.Beach),
                new GameLocation("Palomino Highlands Beach", new Vector3(2584.7f, 4671.8f, 34.1f), LocationType.Beach)
            });
        }
        
        private void AddShoppingLocations()
        {
            // Centres commerciaux
            locations[LocationType.Mall].AddRange(new[]
            {
                new GameLocation("Rockford Plaza", new Vector3(-709.2f, -904.2f, 19.2f), LocationType.Mall),
                new GameLocation("Del Perro Mall", new Vector3(-1449.9f, -379.0f, 38.3f), LocationType.Mall),
                new GameLocation("Vinewood Plaza", new Vector3(618.9f, 2765.5f, 42.1f), LocationType.Mall)
            });
            
            // Magasins
            locations[LocationType.Shop].AddRange(new[]
            {
                new GameLocation("24/7 Downtown", new Vector3(25.7f, -1347.3f, 29.5f), LocationType.Shop),
                new GameLocation("24/7 Vinewood", new Vector3(373.5f, 325.6f, 103.6f), LocationType.Shop),
                new GameLocation("Rob's Liquor Vespucci", new Vector3(-1487.6f, -379.1f, 40.2f), LocationType.Shop),
                new GameLocation("LTD Gasoline Mirror Park", new Vector3(1163.4f, -323.8f, 69.2f), LocationType.Shop)
            });
            
            locations[LocationType.ConvenienceStore] = locations[LocationType.Shop].ToList();
        }
        
        private void AddTouristLocations()
        {
            // Points d'intérêt touristiques
            locations[LocationType.Landmark].AddRange(new[]
            {
                new GameLocation("Vinewood Sign", new Vector3(725.2f, 1204.5f, 353.5f), LocationType.Landmark),
                new GameLocation("Galileo Observatory", new Vector3(-438.8f, 1076.0f, 352.4f), LocationType.Landmark),
                new GameLocation("Los Santos International Airport", new Vector3(-1034.6f, -2733.6f, 20.2f), LocationType.Landmark),
                new GameLocation("Vinewood Bowl", new Vector3(686.2f, 577.9f, 130.5f), LocationType.Landmark),
                new GameLocation("Oriental Theater", new Vector3(300.7f, 200.8f, 104.4f), LocationType.Landmark),
                new GameLocation("Kortz Center", new Vector3(-2240.6f, 264.2f, 174.6f), LocationType.Landmark),
                new GameLocation("Los Santos Pier", new Vector3(-1850.2f, -1231.7f, 13.0f), LocationType.Landmark)
            });
        }
        
        private void AddWorkLocations()
        {
            // Chantiers de construction
            locations[LocationType.Construction].AddRange(new[]
            {
                new GameLocation("Mile High Club Construction", new Vector3(-141.3f, -620.9f, 168.8f), LocationType.Construction),
                new GameLocation("LSIA Construction", new Vector3(-1336.2f, -3044.5f, 13.9f), LocationType.Construction),
                new GameLocation("Downtown Construction", new Vector3(-595.6f, -930.9f, 23.9f), LocationType.Construction),
                new GameLocation("Paleto Construction", new Vector3(-378.7f, 6040.5f, 31.5f), LocationType.Construction)
            });
            
            // Bars
            locations[LocationType.Bar].AddRange(new[]
            {
                new GameLocation("Yellow Jack Inn Bar", new Vector3(1986.6f, 3053.9f, 47.2f), LocationType.Bar),
                new GameLocation("Tequi-la-la Bar", new Vector3(-565.2f, 276.6f, 83.1f), LocationType.Bar),
                new GameLocation("Bahama Mamas", new Vector3(-1388.9f, -588.4f, 30.3f), LocationType.Bar)
            });
        }
        
        private void AddEducationLocations()
        {
            // Écoles et universités
            locations[LocationType.School].AddRange(new[]
            {
                new GameLocation("University of San Andreas", new Vector3(-1600.4f, 205.1f, 59.2f), LocationType.School),
                new GameLocation("Los Santos High School", new Vector3(210.8f, -424.8f, 47.7f), LocationType.School),
                new GameLocation("Davis High School", new Vector3(56.3f, -1670.9f, 29.6f), LocationType.School)
            });
        }
        
        private void AddResidentialLocations()
        {
            // Quartiers résidentiels
            locations[LocationType.Residential].AddRange(new[]
            {
                new GameLocation("Vinewood Hills Mansion", new Vector3(-174.2f, 497.5f, 137.7f), LocationType.Residential),
                new GameLocation("Richman Mansion", new Vector3(-1673.9f, 205.1f, 59.2f), LocationType.Residential),
                new GameLocation("Mirror Park House", new Vector3(1259.4f, -606.3f, 69.6f), LocationType.Residential),
                new GameLocation("Grove Street House", new Vector3(-14.8f, -1441.2f, 31.1f), LocationType.Residential),
                new GameLocation("Del Perro Apartment", new Vector3(-1447.6f, -538.5f, 74.0f), LocationType.Residential)
            });
        }
        
        private void AddMiscLocations()
        {
            // Hôpitaux
            locations[LocationType.Hospital].AddRange(new[]
            {
                new GameLocation("Pillbox Hill Medical Center", new Vector3(307.0f, -1433.5f, 29.9f), LocationType.Hospital),
                new GameLocation("Central Los Santos Medical Center", new Vector3(1151.2f, -1529.6f, 35.4f), LocationType.Hospital),
                new GameLocation("Sandy Shores Medical Center", new Vector3(1839.6f, 3672.8f, 34.3f), LocationType.Hospital)
            });
            
            // Commissariats
            locations[LocationType.Police].AddRange(new[]
            {
                new GameLocation("Mission Row Police Station", new Vector3(428.0f, -984.4f, 30.7f), LocationType.Police),
                new GameLocation("Davis Police Station", new Vector3(361.9f, -1584.8f, 29.3f), LocationType.Police),
                new GameLocation("Vespucci Police Station", new Vector3(-1096.1f, -809.3f, 19.0f), LocationType.Police),
                new GameLocation("Sandy Shores Sheriff", new Vector3(1854.1f, 3689.6f, 34.3f), LocationType.Police)
            });
        }
        
        public GameLocation GetRandomLocation(LocationType locationType)
        {
            if (locations.ContainsKey(locationType) && locations[locationType].Count > 0)
            {
                var locationList = locations[locationType];
                return locationList[random.Next(locationList.Count)];
            }
            
            // Fallback : retourner une position par défaut
            return new GameLocation("Default Location", new Vector3(0, 0, 72), locationType);
        }
        
        public GameLocation GetNearestLocation(LocationType locationType, Vector3 position)
        {
            if (!locations.ContainsKey(locationType) || locations[locationType].Count == 0)
            {
                return GetRandomLocation(locationType);
            }
            
            var nearestLocation = locations[locationType]
                .OrderBy(loc => loc.Position.DistanceTo(position))
                .FirstOrDefault();
                
            return nearestLocation ?? GetRandomLocation(locationType);
        }
        
        public List<GameLocation> GetLocationsNear(Vector3 position, float radius, LocationType? filterType = null)
        {
            var result = new List<GameLocation>();
            
            var searchLocations = filterType.HasValue 
                ? locations.ContainsKey(filterType.Value) ? locations[filterType.Value] : new List<GameLocation>()
                : locations.SelectMany(kvp => kvp.Value);
            
            return searchLocations.Where(loc => loc.Position.DistanceTo(position) <= radius).ToList();
        }
        
        public void AddCustomLocation(GameLocation location)
        {
            if (!locations.ContainsKey(location.Type))
            {
                locations[location.Type] = new List<GameLocation>();
            }
            
            locations[location.Type].Add(location);
        }
        
        public int GetLocationCount(LocationType locationType)
        {
            return locations.ContainsKey(locationType) ? locations[locationType].Count : 0;
        }
    }
    
    public class GameLocation
    {
        public string Name { get; }
        public Vector3 Position { get; }
        public LocationType Type { get; }
        public string Description { get; set; }
        public bool IsAccessible { get; set; } = true;
        public DateTime LastVisited { get; set; }
        
        public GameLocation(string name, Vector3 position, LocationType type, string description = "")
        {
            Name = name;
            Position = position;
            Type = type;
            Description = description;
            LastVisited = DateTime.MinValue;
        }
        
        public void MarkAsVisited()
        {
            LastVisited = DateTime.Now;
        }
        
        public float DistanceTo(Vector3 position)
        {
            return Position.DistanceTo(position);
        }
        
        public override string ToString()
        {
            return $"{Name} ({Type}) - {Position}";
        }
    }
    
    public enum LocationType
    {
        Office,           // Bureaux
        Restaurant,       // Restaurants
        Cafe,             // Cafés
        FastFood,         // Fast-food
        Park,             // Parcs
        Beach,            // Plages
        Mall,             // Centres commerciaux
        Shop,             // Magasins
        ConvenienceStore, // Épiceries
        Landmark,         // Points d'intérêt
        Construction,     // Chantiers
        Bar,              // Bars
        School,           // Écoles
        Residential,      // Résidentiel
        Hospital,         // Hôpitaux
        Police,           // Commissariats
        Bank,             // Banques
        Gas,              // Stations-service
        Gym,              // Salles de sport
        Airport           // Aéroports
    }
} 