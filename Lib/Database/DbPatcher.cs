using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACE.Server;

namespace ACE.Mods.Legend.Lib.Database
{
    internal class DbPatcher
    {
        private static void ExecuteScript(MySqlConnector.MySqlCommand scriptCommand)
        {
            if (scriptCommand.Connection?.State != System.Data.ConnectionState.Open)
            {
                scriptCommand.Connection?.Open();
            }
            scriptCommand.ExecuteNonQuery();
            ModManager.Log(".");
        }

        private static void CleanupConnection(MySqlConnector.MySqlConnection connection)
        {
            if (connection.State != System.Data.ConnectionState.Closed)
            {
                try
                {
                    connection.Close();
                }
                catch
                {
                }
            }
        }

        public static void PatchDatabase(string dbType, string host, uint port, string username, string password, string authDB, string shardDB, string worldDB)
        {
            var separator = Path.DirectorySeparatorChar;
            var updatesPath = $"DatabaseSetupScripts{separator}Updates{separator}{dbType}";
            var updatesFile = $"{updatesPath}{Path.DirectorySeparatorChar}applied_updates.txt";
            var customUpdatesPath = $"{Mod.ModPath}{separator}Lib{separator}Database{separator}Updates{separator}Shard";

            if (!Directory.Exists(updatesPath))
            {
                // File not found in Environment.CurrentDirectory
                // Lets try the ExecutingAssembly Location
                var executingAssemblyLocation = Assembly.GetExecutingAssembly().Location;

                var directoryName = Path.GetFullPath(Path.GetDirectoryName(executingAssemblyLocation));

                updatesPath = Path.Combine(directoryName, $"DatabaseSetupScripts{Path.DirectorySeparatorChar}Updates{Path.DirectorySeparatorChar}{dbType}");

                if (!Directory.Exists(updatesPath))
                {
                    ModManager.Log($" error!", ModManager.LogLevel.Error);
                    ModManager.Log($" Unable to locate updates directory", ModManager.LogLevel.Error);
                }
                else
                {
                    updatesFile = $"{updatesPath}{Path.DirectorySeparatorChar}applied_updates.txt";
                }

            }

            ModManager.Log(customUpdatesPath, ModManager.LogLevel.Warn);
            if (!Directory.Exists(customUpdatesPath))
            {
                ModManager.Log($" error!", ModManager.LogLevel.Error);
                ModManager.Log($" Unable to locate custom updates directory", ModManager.LogLevel.Error);
                return;
            }

            var appliedUpdates = Array.Empty<string>();

            var containerUpdatesFile = $"/ace/Config/{dbType}_applied_updates.txt";
            if (Program.IsRunningInContainer && File.Exists(containerUpdatesFile))
                File.Copy(containerUpdatesFile, updatesFile, true);

            if (File.Exists(updatesFile))
                appliedUpdates = File.ReadAllLines(updatesFile);

            ModManager.Log($"Searching for {dbType} update SQL scripts .... ");
            foreach (var file in new DirectoryInfo(customUpdatesPath).GetFiles("*.sql").OrderBy(f => f.Name))
            {
                if (appliedUpdates.Contains(file.Name))
                    continue;

                ModManager.Log($"Found {file.Name} .... ");
                var sqlDBFile = File.ReadAllText(file.FullName);
                var database = "";
                switch (dbType)
                {
                    case "Authentication":
                        database = authDB;
                        break;
                    case "Shard":
                        database = shardDB;
                        break;
                    case "World":
                        database = worldDB;
                        break;
                }
                var sqlConnect = new MySqlConnector.MySqlConnection($"server={host};port={port};user={username};password={password};database={database};DefaultCommandTimeout=120;SslMode=None;AllowPublicKeyRetrieval=true");
                sqlDBFile = sqlDBFile.Replace("ace_auth", authDB);
                sqlDBFile = sqlDBFile.Replace("ace_shard", shardDB);
                sqlDBFile = sqlDBFile.Replace("ace_world", worldDB);
                var script = new MySqlConnector.MySqlCommand(sqlDBFile, sqlConnect);

                Console.Write($"Importing into {database} database on SQL server at {host}:{port} .... ");
                try
                {
                    ExecuteScript(script);
                    //ModManager.Log($" {count} database records affected ....");
                    Console.WriteLine(" complete!");
                }
                catch (MySqlConnector.MySqlException ex)
                {
                    ModManager.Log($" error!", ModManager.LogLevel.Error);
                    ModManager.Log($" Unable to apply patch due to following exception: {ex}", ModManager.LogLevel.Error);
                }
                File.AppendAllText(updatesFile, file.Name + Environment.NewLine);
                CleanupConnection(sqlConnect);
            }

            if (Program.IsRunningInContainer && File.Exists(updatesFile))
                File.Copy(updatesFile, containerUpdatesFile, true);

            ModManager.Log($"{dbType} update SQL scripts import complete!");
        }
    }
}
