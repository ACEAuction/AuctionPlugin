using System.Collections.ObjectModel;
using System.Globalization;

/// https://github.com/Mag-nus/Mag-Plugins/blob/master/Shared/Spells/SpellTools.cs

namespace ACE.Mods.Auction.Lib.Common.Spells;

public static class SpellTools
{
    private static List<string> SpellTableHeader = new List<string>();
    static readonly Collection<string[]> SpellTable = new Collection<string[]>();

    private static string SpellCsvFile = $"{Mod.ModPath}\\Lib\\Common\\Spells\\Spells.csv";

    static readonly Dictionary<int, Spell> SpellsById = new Dictionary<int, Spell>();

    public static void Init()
    {
        string csvFilePath = Path.Combine(SpellCsvFile);

        ModManager.Log("Reading Spell Table....");

        if (File.Exists(csvFilePath))
        {
            using (FileStream fileStream = new FileStream(csvFilePath, FileMode.Open, FileAccess.Read))
            using (StreamReader reader = new StreamReader(fileStream))
            {
                SpellTableHeader = new List<string>(reader.ReadLine().Split(','));
                while (!reader.EndOfStream)
                {
                    var spell = reader.ReadLine().Split(',');
                    SpellTable.Add(spell);
                }
            }
        }
        else
        {
            ModManager.Log($"File not found: {csvFilePath}", ModManager.LogLevel.Error);
        }
    }

    /// <summary>
    /// Will return null if no spell was found.
    /// </summary>
    public static Spell GetSpell(int id)
    {
        if (SpellsById.TryGetValue(id, out var spell))
            return spell;

        int idIndex = SpellTableHeader.IndexOf("Id");

        foreach (string[] line in SpellTable)
        {
            if (line[idIndex] == id.ToString(CultureInfo.InvariantCulture))
                return GetSpell(line);
        }

        //throw new ArgumentException("Spell of id: " + id + " not found in Spells.csv");
        return null;
    }

    /// <summary>
    /// Will return null if no spell was found.
    /// </summary>
    public static Spell GetSpell(string name)
    {
        foreach (var kvp in SpellsById)
        {
            if (string.Equals(kvp.Value.Name, name, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        int nameIndex = SpellTableHeader.IndexOf("Name");

        foreach (string[] line in SpellTable)
        {
            if (string.Equals(line[nameIndex], name, StringComparison.OrdinalIgnoreCase))
                return GetSpell(line);
        }

        //throw new ArgumentException("Spell of name: " + name + " not found in Spells.csv");
        return null;
    }

    private static Spell GetSpell(string[] splitLine)
    {
        int idIndex = SpellTableHeader.IndexOf("Id");
        int nameIndex = SpellTableHeader.IndexOf("Name");
        int difficultyIndex = SpellTableHeader.IndexOf("Difficulty");
        int durationIndex = SpellTableHeader.IndexOf("Duration");
        int familyIndex = SpellTableHeader.IndexOf("Family");

        int id;
        int.TryParse(splitLine[idIndex], out id);

        string name = splitLine[nameIndex];

        int difficulty;
        int.TryParse(splitLine[difficultyIndex], out difficulty);

        int duration;
        int.TryParse(splitLine[durationIndex], out duration);

        int family;
        int.TryParse(splitLine[familyIndex], out family);

        var spell = new Spell(id, name, difficulty, duration, family);

        if (!SpellsById.ContainsKey(spell.Id))
            SpellsById.Add(spell.Id, spell);

        return spell;
    }

    public static bool IsAKnownSpell(int id)
    {
        if (SpellsById.ContainsKey(id))
            return true;

        int idIndex = SpellTableHeader.IndexOf("Id");

        foreach (string[] line in SpellTable)
        {
            if (line[idIndex] == id.ToString(CultureInfo.InvariantCulture))
                return true;
        }

        return false;
    }

    public static bool IsAKnownSpell(string name)
    {
        foreach (var kvp in SpellsById)
        {
            if (string.Equals(kvp.Value.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        int nameIndex = SpellTableHeader.IndexOf("Name");

        foreach (string[] line in SpellTable)
        {
            if (line[nameIndex] == name)
                return true;
        }

        return false;
    }
}
