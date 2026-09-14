using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tagger.db
{
    public class DatabaseConfig
    {
        public static string GetConnectionString()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolderPath = Path.Combine(appDataPath, "Tagger");

            Directory.CreateDirectory(appFolderPath);

            string dbFilePath = Path.Combine(appFolderPath, "tagger.db");
            return $"Data Source={dbFilePath}";
        }
    }
}
