#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using SQLite; // Import SQLite-net namespace
using System.IO;

public class SQLiteNetEditorExample
{
    private static string dbPath = Path.Combine(Application.dataPath, "Database/mydatabase.sqlite");

    [MenuItem("Tools/SQLite/Initialize Database")]
    public static void InitializeDatabase()
    {
        // Ensure the directory exists
        var directory = Path.GetDirectoryName(dbPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Initialize the database
        using (var connection = new SQLiteConnection(dbPath))
        {
            // Create a table if it doesn't exist
            connection.CreateTable<MyTable>();
            Debug.Log("Database initialized and table created!");
        }
    }

    [MenuItem("Tools/SQLite/Insert Sample Data")]
    public static void InsertSampleData()
    {
        using (var connection = new SQLiteConnection(dbPath))
        {
            // Insert data into the table
            var newData = new MyTable { Name = "Sample Name" };
            connection.Insert(newData);

            Debug.Log($"Inserted data: {newData.Name}");
        }
    }

    [MenuItem("Tools/SQLite/Read Data")]
    public static void ReadData()
    {
        using (var connection = new SQLiteConnection(dbPath))
        {
            // Read all data from the table
            var dataList = connection.Table<MyTable>().ToList();

            foreach (var data in dataList)
            {
                Debug.Log($"ID: {data.Id}, Name: {data.Name}");
            }
        }
    }

    // Define a class to represent the table schema
    [System.Serializable]
    public class MyTable
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
#endif
