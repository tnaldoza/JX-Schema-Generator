using System.IO;
using System.Text.Json;

namespace JXSchemaGenerator.Emit;

public static class JsonEmitter
{
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true
	};

	public static void WriteToFile<T>(string path, T value)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
	}
}
