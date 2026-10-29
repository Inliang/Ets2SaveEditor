namespace Ets2SaveEditor.Core.GameData
{
    /// <summary>
    /// A city in the game world.
    /// <para>
    /// 不可变：全局目录里的城市对象会被多个界面共享（<c>Cities()</c> 返回的
    /// 是同一批对象），一旦允许改属性就会污染全局目录。
    /// </para>
    /// </summary>
    public class City
    {
        /// <summary>Internal token used by the game (e.g. "london").</summary>
        public string Token { get; }

        /// <summary>Human-readable name.</summary>
        public string Name { get; }

        /// <summary>World X coordinate (meters).</summary>
        public double X { get; }

        /// <summary>World Y coordinate (meters).</summary>
        public double Y { get; }

        /// <summary>World Z coordinate (meters).</summary>
        public double Z { get; }

        public City(string token, string name, double x = 0, double y = 0, double z = 0)
        {
            Token = token ?? string.Empty;
            Name = name ?? string.Empty;
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>A cargo type.</summary>
    public class Cargo
    {
        /// <summary>Internal token used by the game (e.g. "oil").</summary>
        public string Token { get; set; }

        /// <summary>Human-readable name.</summary>
        public string Name { get; set; }

        public Cargo()
        {
            Token = string.Empty;
            Name = string.Empty;
        }

        public Cargo(string token, string name)
        {
            Token = token ?? string.Empty;
            Name = name ?? string.Empty;
        }
    }

    /// <summary>A company (cargo source/destination).</summary>
    public class Company
    {
        /// <summary>Internal token used by the game.</summary>
        public string Token { get; set; }

        /// <summary>Human-readable name.</summary>
        public string Name { get; set; }

        public Company()
        {
            Token = string.Empty;
            Name = string.Empty;
        }

        public Company(string token, string name)
        {
            Token = token ?? string.Empty;
            Name = name ?? string.Empty;
        }
    }
}
