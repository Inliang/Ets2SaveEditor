using System.Collections.Generic;

namespace Ets2SaveEditor.Core.GameData
{
    /// <summary>
    /// Built-in ETS2 game data. NOTE: this is a small starter set and must be
    /// expanded with DLC / version-specific cities, cargoes and companies as the
    /// project grows.
    /// </summary>
    public class BuiltInGameData : IGameDataProvider
    {
        private static readonly List<City> CitiesList = new List<City>
        {
            new City("london", "London"),
            new City("manchester", "Manchester"),
            new City("glasgow", "Glasgow"),
            new City("dublin", "Dublin"),
            new City("paris", "Paris"),
            new City("lyon", "Lyon"),
            new City("marseille", "Marseille"),
            new City("amsterdam", "Amsterdam"),
            new City("berlin", "Berlin"),
            new City("hamburg", "Hamburg"),
            new City("munich", "Munich"),
            new City("frankfurt", "Frankfurt"),
            new City("prague", "Prague"),
            new City("vienna", "Vienna"),
            new City("bratislava", "Bratislava"),
            new City("budapest", "Budapest"),
            new City("warsaw", "Warsaw"),
            new City("milan", "Milan"),
            new City("rome", "Rome"),
            new City("zurich", "Zurich"),
            new City("barcelona", "Barcelona"),
            new City("madrid", "Madrid"),
            new City("lisbon", "Lisbon"),
            new City("stockholm", "Stockholm"),
            new City("oslo", "Oslo"),
            new City("copenhagen", "Copenhagen"),
            new City("helsinki", "Helsinki"),
            new City("athens", "Athens")
        };

        private static readonly List<Cargo> CargoList = new List<Cargo>
        {
            new Cargo("oil", "Oil"),
            new Cargo("milk", "Milk"),
            new Cargo("furniture", "Furniture"),
            new Cargo("machinery", "Machinery"),
            new Cargo("food", "Food"),
            new Cargo("steel", "Steel"),
            new Cargo("wood", "Wood"),
            new Cargo("cars", "Cars"),
            new Cargo("chemicals", "Chemicals"),
            new Cargo("clothes", "Clothes")
        };

        private static readonly List<Company> CompanyList = new List<Company>
        {
            new Company("scania", "Scania"),
            new Company("volvo", "Volvo"),
            new Company("man", "MAN"),
            new Company("mercedes", "Mercedes-Benz"),
            new Company("iveco", "Iveco")
        };

        public List<City> Cities() => new List<City>(CitiesList);

        public List<Cargo> Cargoes() => new List<Cargo>(CargoList);

        public List<Company> Companies() => new List<Company>(CompanyList);
    }
}
