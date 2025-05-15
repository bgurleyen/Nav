using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

[System.Serializable]
public class DDL_data
{
    public List<double> altitude = new List<double>();
    public List<double> speed = new List<double>();
    public List<double> flap = new List<double>();
    public List<double> speedBrake = new List<double>();
    public List<double> landingGear = new List<double>();
    public List<double> verticalMode = new List<double>();
    public List<double> fuelFlow = new List<double>();
    public double remainingFuel;
    public List<string> time = new List<string>();
}

[System.Serializable]
public class Progress_Data
{
    public List<double> progress = new List<double>();
}

public interface ISerializer
{
    string Serialize<T>(T data);
    T Deserialize<T>(string data);
}

public class JsonSerializer : ISerializer
{
    public string Serialize<T>(T data) => JsonUtility.ToJson(data);
    public T Deserialize<T>(string data) => JsonUtility.FromJson<T>(data);
}

public interface IStorage
{
    Task SaveAsync(string data, string path);
    Task<string> LoadAsync(string path);
}

/*public class FirebaseCloudStorage : IStorage
{
    private FirebaseStorage storage;
    private string bucket;

    public FirebaseCloudStorage(string bucketUrl)
    {
        storage = FirebaseStorage.GetInstance(bucketUrl);
        bucket = bucketUrl;
    }

    public async Task SaveAsync(string data, string path)
    {
        StorageReference reference = storage.GetReference(path);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(data);

        await reference.PutBytesAsync(bytes).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"Failed to save to Firebase: {task.Exception}");
                throw task.Exception;
            }
            Debug.Log($"Saved to Firebase at {path}");
        });
    }

    public async Task<string> LoadAsync(string path)
    {
        StorageReference reference = storage.GetReference(path);
        const long maxSize = 10 * 1024 * 1024; // 10MB max

        try
        {
            byte[] bytes = await reference.GetBytesAsync(maxSize);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to load from Firebase: {ex}");
            return null;
        }
    }
}*/

public class LocalFileStorage : IStorage
{
    private string basePath;

    public LocalFileStorage()
    {
        basePath = Application.persistentDataPath;
    }

    public async Task SaveAsync(string data, string path)
    {
        string fullPath = Path.Combine(basePath, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        await File.WriteAllTextAsync(fullPath, data);
        Debug.Log($"Saved locally at {fullPath}");
    }

    public async Task<string> LoadAsync(string path)
    {
        string fullPath = Path.Combine(basePath, path);
        if (File.Exists(fullPath))
        {
            return await File.ReadAllTextAsync(fullPath);
        }
        Debug.LogWarning($"No local file found at {fullPath}");
        return null;
    }
}

public class SaveLoadManager
{
    private readonly ISerializer serializer;
    private readonly IStorage storage;

    public SaveLoadManager(ISerializer serializer, IStorage storage)
    {
        this.serializer = serializer;
        this.storage = storage;
    }

    public async Task SaveAsync<T>(T data, string path)
    {
        string serializedData = serializer.Serialize(data);
        await storage.SaveAsync(serializedData, path);
    }

    public async Task<T> LoadAsync<T>(string path)
    {
        string data = await storage.LoadAsync(path);
        return data != null ? serializer.Deserialize<T>(data) : default;
    }
}